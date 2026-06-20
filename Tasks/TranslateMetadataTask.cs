using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
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
            return Array.Empty<TaskTriggerInfo>();
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
                    BaseItemKind.Movie,
                    BaseItemKind.Series,
                    BaseItemKind.Season,
                    BaseItemKind.Episode
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
                    bool changed = await ProcessItemAsync(item, config, cancellationToken).ConfigureAwait(false);
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
                progress?.Report((double)processed / total * 100);
            }

            _lockService.Save();

            _logger.LogInformation(
                "[RoTranslator] Finalizat. Traduse: {T}, Sarite: {S}, Erori: {E} din {Total}.",
                translated, skipped, errors, total);
        }

        private async Task<bool> ProcessItemAsync(BaseItem item, Configuration.PluginConfiguration config, CancellationToken cancellationToken)
        {
            bool changed = false;

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
                            // Tagline nu are camp dedicat in MetadataField in 10.11, folosim Overview lock separat prin lockService
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

        private static MetadataField[] AddLockedField(MetadataField[]? existing, MetadataField field)
        {
            if (existing == null) return new[] { field };
            if (Array.IndexOf(existing, field) >= 0) return existing;
            var list = new List<MetadataField>(existing) { field };
            return list.ToArray();
        }
    }
}
