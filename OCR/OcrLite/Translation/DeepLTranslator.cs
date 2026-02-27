using System.Net.Http;
using System.Text.Json;

namespace OcrLite.Translation;

internal sealed class DeepLTranslator : ITextTranslator
{
    private static readonly HttpClient Http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    private readonly TranslationSettings _settings;

    public DeepLTranslator(TranslationSettings settings)
    {
        _settings = settings;
    }

    public string Name => "DeepL";

    public async Task<TranslateResult> TranslateAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return TranslateResult.Success(string.Empty);
        }

        string key = _settings.DeepLApiKey.Trim();
        string endpoint = key.EndsWith(":fx", StringComparison.OrdinalIgnoreCase)
            ? "https://api-free.deepl.com/v2/translate"
            : "https://api.deepl.com/v2/translate";

        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["auth_key"] = key,
            ["text"] = text,
            ["source_lang"] = _settings.SourceLanguage.ToUpperInvariant(),
            ["target_lang"] = _settings.TargetLanguage.ToUpperInvariant(),
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = body
        };

        try
        {
            using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return TranslateResult.Error($"DeepL error: {(int)response.StatusCode} {response.ReasonPhrase}", text);
            }

            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("translations", out var translations) || translations.GetArrayLength() == 0)
            {
                return TranslateResult.Error("DeepL response has no translations.", text);
            }

            string translated = translations[0].GetProperty("text").GetString() ?? string.Empty;
            return TranslateResult.Success(translated);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return TranslateResult.Error($"DeepL request failed: {ex.Message}", text);
        }
    }
}
