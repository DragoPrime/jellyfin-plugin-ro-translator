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

        // Rate limiting
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private int _requestCount = 0;
        private DateTime _windowStart = DateTime.UtcNow;

        public DeepLTranslationService(IHttpClientFactory httpClientFactory, ILogger<DeepLTranslationService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        private string GetBaseUrl(bool usePro)
            => usePro
                ? "https://api.deepl.com/v2"
                : "https://api-free.deepl.com/v2";

        /// <summary>
        /// Traduce un text din engleza in romana.
        /// </summary>
        public async Task<string?> TranslateAsync(string text, string apiKey, bool usePro, int maxReqPerMin, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(apiKey))
                return null;

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await EnforceRateLimitAsync(maxReqPerMin, cancellationToken).ConfigureAwait(false);

                var baseUrl = GetBaseUrl(usePro);
                var client = _httpClientFactory.CreateClient("DeepL");

                var requestBody = new
                {
                    text = new[] { text },
                    source_lang = "EN",
                    target_lang = "RO"
                };

                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/translate");
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
                    _logger.LogError("DeepL API error {StatusCode}: {Error}", response.StatusCode, error);
                    return null;
                }

                var result = await response.Content.ReadFromJsonAsync<DeepLResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
                return result?.Translations?[0]?.Text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eroare la traducerea textului cu DeepL");
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Traduce mai multe texte dintr-o singura cerere API (mai eficient).
        /// </summary>
        public async Task<List<string?>> TranslateBatchAsync(List<string> texts, string apiKey, bool usePro, int maxReqPerMin, CancellationToken cancellationToken)
        {
            var results = new List<string?>();

            if (texts.Count == 0 || string.IsNullOrWhiteSpace(apiKey))
                return results;

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await EnforceRateLimitAsync(maxReqPerMin, cancellationToken).ConfigureAwait(false);

                var baseUrl = GetBaseUrl(usePro);
                var client = _httpClientFactory.CreateClient("DeepL");

                var requestBody = new
                {
                    text = texts.ToArray(),
                    source_lang = "EN",
                    target_lang = "RO"
                };

                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/translate");
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
                    _logger.LogError("DeepL API batch error {StatusCode}: {Error}", response.StatusCode, error);
                    return texts.ConvertAll(_ => (string?)null);
                }

                var result = await response.Content.ReadFromJsonAsync<DeepLResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);

                if (result?.Translations != null)
                {
                    foreach (var t in result.Translations)
                        results.Add(t.Text);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eroare la traducerea batch cu DeepL");
                return texts.ConvertAll(_ => (string?)null);
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
                var waitMs = (int)(60000 - (now - _windowStart).TotalMilliseconds) + 100;
                if (waitMs > 0)
                {
                    _logger.LogInformation("Rate limit atins. Astept {WaitMs}ms...", waitMs);
                    await Task.Delay(waitMs, cancellationToken).ConfigureAwait(false);
                    _windowStart = DateTime.UtcNow;
                    _requestCount = 0;
                }
            }

            _requestCount++;
        }

        // --- JSON models ---

        private class DeepLResponse
        {
            [JsonPropertyName("translations")]
            public List<DeepLTranslation>? Translations { get; set; }
        }

        private class DeepLTranslation
        {
            [JsonPropertyName("detected_source_language")]
            public string? DetectedSourceLanguage { get; set; }

            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }
    }
}
