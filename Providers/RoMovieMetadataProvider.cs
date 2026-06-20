using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RoTranslator.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RoTranslator.Providers
{
    /// <summary>
    /// Provider pentru filme - ruleaza dupa TMDB si aplica traducerea + lock.
    /// </summary>
    public class RoMovieMetadataProvider : IRemoteMetadataProvider<Movie, MovieInfo>
    {
        private readonly DeepLTranslationService _translator;
        private readonly TranslationLockService _lockService;
        private readonly ILogger<RoMovieMetadataProvider> _logger;

        public string Name => "RO Translator (DeepL)";

        public RoMovieMetadataProvider(
            DeepLTranslationService translator,
            TranslationLockService lockService,
            ILogger<RoMovieMetadataProvider> logger)
        {
            _translator = translator;
            _lockService = lockService;
            _logger = logger;
        }

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            // Acest provider nu aduce date noi - returneaza un rezultat gol
            // Traducerea se face in TranslateMetadataTask dupa ce TMDB si-a facut treaba
            return await Task.FromResult(new MetadataResult<Movie> { HasMetadata = false });
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
            => Task.FromResult(Enumerable.Empty<RemoteSearchResult>());

        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage());
    }
}
