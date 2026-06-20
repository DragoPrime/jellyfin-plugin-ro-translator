using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RoTranslator.Services
{
    /// <summary>
    /// Tine evidenta itemelor deja traduse pentru a preveni suprascrierea la refresh.
    /// Persista datele pe disc in fisierul translated_items.json.
    /// </summary>
    public class TranslationLockService
    {
        private readonly IApplicationPaths _appPaths;
        private readonly ILogger<TranslationLockService> _logger;
        private readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

        // itemId -> set de campuri traduse (ex: "Overview", "Tagline", "Name", "Genres", "Tags")
        private Dictionary<Guid, HashSet<string>> _translatedFields = new();
        private bool _loaded = false;

        public TranslationLockService(IApplicationPaths appPaths, ILogger<TranslationLockService> logger)
        {
            _appPaths = appPaths;
            _logger = logger;
        }

        private string FilePath => Path.Combine(_appPaths.DataPath, "ro_translator_locks.json");

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _fileLock.Wait();
            try
            {
                if (_loaded) return;
                Load();
                _loaded = true;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    _translatedFields = new Dictionary<Guid, HashSet<string>>();
                    return;
                }

                var json = File.ReadAllText(FilePath);
                var raw = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
                _translatedFields = new Dictionary<Guid, HashSet<string>>();

                if (raw != null)
                {
                    foreach (var kv in raw)
                    {
                        if (Guid.TryParse(kv.Key, out var guid))
                            _translatedFields[guid] = new HashSet<string>(kv.Value);
                    }
                }

                _logger.LogInformation("RoTranslator: {Count} iteme cu campuri blocate incarcate.", _translatedFields.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RoTranslator: Eroare la incarcarea lock-urilor.");
                _translatedFields = new Dictionary<Guid, HashSet<string>>();
            }
        }

        public void Save()
        {
            _fileLock.Wait();
            try
            {
                var raw = new Dictionary<string, List<string>>();
                foreach (var kv in _translatedFields)
                    raw[kv.Key.ToString()] = new List<string>(kv.Value);

                var json = JsonSerializer.Serialize(raw, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RoTranslator: Eroare la salvarea lock-urilor.");
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// Marcheaza un camp al unui item ca tradus si blocat.
        /// </summary>
        public void MarkTranslated(Guid itemId, string field)
        {
            EnsureLoaded();
            if (!_translatedFields.TryGetValue(itemId, out var fields))
            {
                fields = new HashSet<string>();
                _translatedFields[itemId] = fields;
            }
            fields.Add(field);
        }

        /// <summary>
        /// Verifica daca un camp al unui item este blocat (deja tradus).
        /// </summary>
        public bool IsLocked(Guid itemId, string field)
        {
            EnsureLoaded();
            return _translatedFields.TryGetValue(itemId, out var fields) && fields.Contains(field);
        }

        /// <summary>
        /// Elimina lock-urile unui item (pentru a permite re-traducerea).
        /// </summary>
        public void ClearLocks(Guid itemId)
        {
            EnsureLoaded();
            _translatedFields.Remove(itemId);
        }

        /// <summary>
        /// Returneaza toti itemii cu campuri blocate (pentru UI/debug).
        /// </summary>
        public IReadOnlyDictionary<Guid, HashSet<string>> GetAllLocks()
        {
            EnsureLoaded();
            return _translatedFields;
        }
    }
}
