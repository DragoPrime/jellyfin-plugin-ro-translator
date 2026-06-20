using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RoTranslator.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RoTranslator.Tasks
{
    public class UnlockMetadataTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly TranslationLockService _lockService;
        private readonly ILogger<UnlockMetadataTask> _logger;

        public UnlockMetadataTask(
            ILibraryManager libraryManager,
            TranslationLockService lockService,
            ILogger<UnlockMetadataTask> logger)
        {
            _libraryManager = libraryManager;
            _lockService = lockService;
            _logger = logger;
        }

        public string Name => "Resetează lock-urile de traducere";
        public string Key => "RoTranslatorUnlockMetadata";
        public string Description => "Elimina toate lock-urile aplicate de plugin, permitand TMDB sa suprascrie metadatele la urmatorul refresh.";
        public string Category => "Romanian Metadata Translator";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("[RoTranslator] Incep resetarea lock-urilor...");

            var allLocks = _lockService.GetAllLocks();
            var lockedIds = allLocks.Keys.ToList();
            int total = lockedIds.Count;

            if (total == 0)
            {
                _logger.LogInformation("[RoTranslator] Nu exista lock-uri de resetat.");
                return;
            }

            int processed = 0;

            foreach (var itemId in lockedIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var item = _libraryManager.GetItemById(itemId);
                    if (item != null && item.LockedFields != null && item.LockedFields.Length > 0)
                    {
                        var lockedByUs = allLocks[itemId];
                        var fieldsToRemove = new HashSet<MetadataField>();

                        if (lockedByUs.Contains("Overview")) fieldsToRemove.Add(MetadataField.Overview);
                        if (lockedByUs.Contains("Name")) fieldsToRemove.Add(MetadataField.Name);
                        if (lockedByUs.Contains("Genres")) fieldsToRemove.Add(MetadataField.Genres);
                        if (lockedByUs.Contains("Tags")) fieldsToRemove.Add(MetadataField.Tags);

                        item.LockedFields = item.LockedFields
                            .Where(f => !fieldsToRemove.Contains(f))
                            .ToArray();

                        await _libraryManager.UpdateItemAsync(
                            item, item.GetParent(), ItemUpdateType.MetadataEdit, cancellationToken
                        ).ConfigureAwait(false);
                    }

                    _lockService.ClearLocks(itemId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RoTranslator] Eroare la deblocarea itemului {Id}", itemId);
                }

                processed++;
                progress?.Report((double)processed / total * 100);
            }

            _lockService.Save();

            _logger.LogInformation("[RoTranslator] Resetare finalizata. {Count} iteme deblocate.", total);
        }
    }
}
