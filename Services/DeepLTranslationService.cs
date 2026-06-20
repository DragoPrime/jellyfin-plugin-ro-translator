using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RoTranslator.Services
{
    public class DeepLTranslationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DeepLTranslationService> _logger;

        // Semafor global cu 1 slot - garanteaza executie strict secventiala
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        // Rate limiting
        private int _requestCount = 0;
        private DateTime _windowStart = DateTime.UtcNow;

        // Flag global: daca cota e depasita, nu mai facem nicio cerere pana la restart
        private bool _quotaExceeded = false;

        public DeepLTranslationService(IHttpClientFactory httpClientFactory, ILogger<DeepLTranslationService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        private static string GetBaseUrl(bool usePro)
            => usePro
                ? "https://api.deepl.com/v2"
                : "https://api-free.deepl.com/v2";

        public async Task<List<string?>> TranslateBatchAsync(
            List<string> texts,
            string apiKey,
            bool usePro,
            int maxReqPerMin,
            CancellationToken cancellationToken)
        {
            var emptyResult = new List<string?>();
            for (int i = 0; i < texts.Count; i++) emptyResult.Add(null);

            if (texts.Count == 0 || string.IsNullOrWhiteSpace(apiKey))
                return emptyResult;

            // Daca cota e depasita, nu mai incercam nimic
            if (_quotaExceeded)
            {
                _logger.LogWarning("[RoTranslator] Cota DeepL depasita. Opresc toate cererile pana la resetarea lunara.");
                return emptyResult;
            }

            // Executie strict secventiala - un singur thread la un moment dat
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Verifica din nou dupa ce am obtinut semaforul
                if (_quotaExceeded)
                    return emptyResult;

                await EnforceRateLimitAsync(maxReqPerMin, cancellationToken).ConfigureAwait(false);

                var baseUrl = GetBaseUrl(usePro);
                var client = _httpClientFactory.CreateClient("DeepL");

                var requestBody = new
                {
                    text = texts.ToArray(),
                    source_lang = "EN",
                    target_lang = "RO"
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/translate");
                request.Headers.Add("Authorization", $"DeepL-Auth-Key {apiKey}");
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                    // 456 = cota depasita - opreste imediat tot
                    if ((int)response.StatusCode == 456)
                    {
                        _quotaExceeded = true;
                        _logger.LogError(
                            "[RoTranslator] Cota lunara DeepL DEPASITA (456). " +
                            "Plugin-ul se opreste automat. " +
                            "Cota se reseteaza la inceputul lunii viitoare. " +
                            "Itemele traduse pana acum sunt blocate si in siguranta.");
                        return emptyResult;
                    }

                    // 429 = prea multe cereri - rate limit
                    if ((int)response.StatusCode == 429)
                    {
                        _logger.LogWarning("[RoTranslator] Rate limit DeepL (429). Astept 60 secunde...");
                        await Task.Delay(60000, cancellationToken).ConfigureAwait(false);
                        return emptyResult;
                    }

                    _logger.LogError("[RoTranslator] DeepL API eroare {StatusCode}: {Error}", (int)response.StatusCode, error);
                    return emptyResult;
                }

                var result = await response.Content.ReadFromJsonAsync<DeepLResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
                var results = new List<string?>();

                if (result?.Translations != null)
                {
                    foreach (var t in result.Translations)
                        results.Add(t.Text);
                }

                return results;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RoTranslator] Eroare neasteptata la cererea DeepL.");
                return emptyResult;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task EnforceRateLimitAsync(int maxReqPerMin, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            if ((now - _windowStart).TotalSeconds >= 60)
            {
                _windowStart = now;
                _requestCount = 0;
            }

            if (_requestCount >= maxReqPerMin)
            {
                var waitMs = (int)(60000 - (now - _windowStart).TotalMilliseconds) + 500;
                if (waitMs > 0)
                {
                    _logger.LogInformation("[RoTranslator] Rate limit local atins ({Max}/min). Astept {Wait}ms...", maxReqPerMin, waitMs);
                    await Task.Delay(waitMs, cancellationToken).ConfigureAwait(false);
                    _windowStart = DateTime.UtcNow;
                    _requestCount = 0;
                }
            }

            _requestCount++;
        }

        // JSON models
        private sealed class DeepLResponse
        {
            [JsonPropertyName("translations")]
            public List<DeepLTranslation>? Translations { get; set; }
        }

        private sealed class DeepLTranslation
        {
            [JsonPropertyName("detected_source_language")]
            public string? DetectedSourceLanguage { get; set; }

            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }
    }
}
