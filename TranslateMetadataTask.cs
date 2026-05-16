using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RoTranslator.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RoTranslator.Tasks
{
    /// <summary>
    /// Task principal: traduce metadatele si le blocheaza impotriva refresh-ului TMDB.
    /// Ruleaza dupa orice refresh de biblioteca sau manual.
    /// </summary>
    public class TranslateMetadataTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly DeepLTranslationService _translationService;
        private readonly TranslationLockService _lockService;
        private readonly ILogger<TranslateMetadataTask> _logger;

        public TranslateMetadataTask(
            ILibraryManager libraryManager,
            DeepLTranslationService translationService,
            TranslationLockService lockService,
            ILogger<TranslateMetadataTask> logger)
        {
            _libraryManager = libraryManager;
            _translationService = translationService;
            _lockService = lockService;
            _logger = logger;
        }

        public string Name => "Traduce metadate în română";
        public string Key => "RoTranslatorTranslateMetadata";
        public string Description => "Traduce și blochează metadatele bibliotecii în română folosind DeepL. Câmpurile blocate nu vor fi suprascrise la refresh-ul TMDB.";
        public string Category => "Romanian Metadata Translator";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerDaily,
                    TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
                }
            };
        }

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var config = Plugin.Instance!.Configuration;

            if (string.IsNullOrWhiteSpace(config.DeepLApiKey))
            {
                _logger.LogWarning("[RoTranslator] DeepL API key lipseste. Configureaza plugin-ul.");
                return;
            }

            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[]
                {
                    typeof(Movie).FullName!,
                    typeof(Series).FullName!,
                    typeof(Season).FullName!,
                    typeof(Episode).FullName!
                },
                IsVirtualItem = false,
                Recursive = true
            };

            var items = _libraryManager.GetItemList(query);
            int total = items.Count;

            if (total == 0)
            {
                _logger.LogInformation("[RoTranslator] Niciun item gasit in biblioteca.");
                return;
            }

            _logger.LogInformation("[RoTranslator] Incep procesarea pentru {Total} iteme.", total);

            int processed = 0, translated = 0, skipped = 0, errors = 0;

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    bool changed = await ProcessItemAsync(item, config, cancellationToken);
                    if (changed) translated++;
                    else skipped++;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    errors++;
                    _logger.LogError(ex, "[RoTranslator] Eroare la: {Name}", item.Name);
                }

                processed++;
                progress.Report((double)processed / total * 100);
            }

            // Salveaza lock-urile pe disc dupa procesare completa
            _lockService.Save();

            _logger.LogInformation(
                "[RoTranslator] Finalizat. Traduse: {T}, Sarite: {S}, Erori: {E} din {Total}.",
                translated, skipped, errors, total);
        }

        private async Task<bool> ProcessItemAsync(BaseItem item, Configuration.PluginConfiguration config, CancellationToken cancellationToken)
        {
            bool changed = false;

            // --- Colecteaza textele de tradus prin DeepL (batch API) ---
            var batch = new List<(string Field, string Text)>();

            if (config.TranslateOverview && !string.IsNullOrWhiteSpace(item.Overview))
            {
                if (!_lockService.IsLocked(item.Id, "Overview") || config.OverwriteExisting)
                    batch.Add(("Overview", item.Overview));
            }

            if (config.TranslateTagline && item is Movie movie && !string.IsNullOrWhiteSpace(movie.Tagline))
            {
                if (!_lockService.IsLocked(item.Id, "Tagline") || config.OverwriteExisting)
                    batch.Add(("Tagline", movie.Tagline));
            }

            if (config.TranslateTitle && !string.IsNullOrWhiteSpace(item.Name))
            {
                if (!_lockService.IsLocked(item.Id, "Name") || config.OverwriteExisting)
                    batch.Add(("Name", item.Name));
            }

            // --- Traducere batch prin DeepL ---
            if (batch.Count > 0)
            {
                var texts = batch.Select(b => b.Text).ToList();
                var translations = await _translationService.TranslateBatchAsync(
                    texts, config.DeepLApiKey, config.UseProApi, config.RequestsPerMinute, cancellationToken
                ).ConfigureAwait(false);

                for (int i = 0; i < batch.Count && i < translations.Count; i++)
                {
                    var field = batch[i].Field;
                    var translatedText = translations[i];
                    if (string.IsNullOrWhiteSpace(translatedText)) continue;

                    switch (field)
                    {
                        case "Overview":
                            item.Overview = translatedText;
                            _lockService.MarkTranslated(item.Id, "Overview");
                            item.LockedFields = AddLockedField(item.LockedFields, MetadataField.Overview);
                            changed = true;
                            break;

                        case "Tagline":
                            ((Movie)item).Tagline = translatedText;
                            _lockService.MarkTranslated(item.Id, "Tagline");
                            item.LockedFields = AddLockedField(item.LockedFields, MetadataField.TagLines);
                            changed = true;
                            break;

                        case "Name":
                            if (string.IsNullOrWhiteSpace(item.OriginalTitle))
                                item.OriginalTitle = item.Name;
                            item.Name = translatedText;
                            _lockService.MarkTranslated(item.Id, "Name");
                            item.LockedFields = AddLockedField(item.LockedFields, MetadataField.Name);
                            changed = true;
                            break;
                    }
                }
            }

            // --- Genuri (dictionar static, fara apel API) ---
            if (config.TranslateGenres && item.Genres != null && item.Genres.Length > 0)
            {
                if (!_lockService.IsLocked(item.Id, "Genres") || config.OverwriteExisting)
                {
                    var roGenres = item.Genres.Select(GenreTagDictionary.TranslateGenre).ToArray();
                    if (!roGenres.SequenceEqual(item.Genres))
                    {
                        item.Genres = roGenres;
                        _lockService.MarkTranslated(item.Id, "Genres");
                        item.LockedFields = AddLockedField(item.LockedFields, MetadataField.Genres);
                        changed = true;
                    }
                }
            }

            // --- Etichete/Tags (dictionar static, fara apel API) ---
            if (config.TranslateTags && item.Tags != null && item.Tags.Length > 0)
            {
                if (!_lockService.IsLocked(item.Id, "Tags") || config.OverwriteExisting)
                {
                    var roTags = item.Tags.Select(GenreTagDictionary.TranslateTag).ToArray();
                    if (!roTags.SequenceEqual(item.Tags))
                    {
                        item.Tags = roTags;
                        _lockService.MarkTranslated(item.Id, "Tags");
                        item.LockedFields = AddLockedField(item.LockedFields, MetadataField.Tags);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                await _libraryManager.UpdateItemAsync(
                    item, item.GetParent(), ItemUpdateType.MetadataEdit, cancellationToken
                ).ConfigureAwait(false);

                _logger.LogDebug("[RoTranslator] Tradus si blocat: {Name}", item.Name);
            }

            return changed;
        }

        /// <summary>
        /// Adauga un MetadataField la array-ul LockedFields fara duplicate.
        /// Campurile blocate NU vor fi suprascrise de TMDB la urmatorul refresh.
        /// </summary>
        private static MetadataField[] AddLockedField(MetadataField[]? existing, MetadataField field)
        {
            if (existing == null) return new[] { field };
            if (Array.IndexOf(existing, field) >= 0) return existing;
            var list = new List<MetadataField>(existing) { field };
            return list.ToArray();
        }
    }
}
