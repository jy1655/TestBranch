using System.Net.Http;
using System.Text.Json;

namespace OcrLite.Translation;

internal sealed class PapagoTranslator : ITextTranslator
{
    private static readonly HttpClient Http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    private readonly TranslationSettings _settings;

    public PapagoTranslator(TranslationSettings settings)
    {
        _settings = settings;
    }

    public string Name => "Papago";

    public async Task<TranslateResult> TranslateAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return TranslateResult.Success(string.Empty);
        }

        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["source"] = _settings.SourceLanguage,
            ["target"] = _settings.TargetLanguage,
            ["text"] = text,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://openapi.naver.com/v1/papago/n2mt")
        {
            Content = body
        };

        request.Headers.Add("X-Naver-Client-Id", _settings.PapagoClientId.Trim());
        request.Headers.Add("X-Naver-Client-Secret", _settings.PapagoClientSecret.Trim());

        try
        {
            using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return TranslateResult.Error($"Papago error: {(int)response.StatusCode} {response.ReasonPhrase}", text);
            }

            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("message", out var messageNode) ||
                !messageNode.TryGetProperty("result", out var resultNode) ||
                !resultNode.TryGetProperty("translatedText", out var translatedNode))
            {
                return TranslateResult.Error("Papago response parse failed.", text);
            }

            string translated = translatedNode.GetString() ?? string.Empty;
            return TranslateResult.Success(translated);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return TranslateResult.Error($"Papago request failed: {ex.Message}", text);
        }
    }
}
