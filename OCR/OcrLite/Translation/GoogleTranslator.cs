using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OcrLite.Translation;

internal sealed class GoogleTranslator : ITextTranslator
{
    private static readonly HttpClient Http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    private readonly TranslationSettings _settings;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string _cachedOAuthAccessToken = string.Empty;
    private DateTimeOffset _cachedOAuthAccessTokenExpiresAt = DateTimeOffset.MinValue;

    public GoogleTranslator(TranslationSettings settings)
    {
        _settings = settings;
    }

    public string Name => "Google";

    public async Task<TranslateResult> TranslateAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return TranslateResult.Success(string.Empty);
        }

        bool hasManualOAuth = !string.IsNullOrWhiteSpace(_settings.GoogleAccessToken) &&
                              !string.IsNullOrWhiteSpace(_settings.GoogleProjectId);
        bool hasRefreshOAuth = !string.IsNullOrWhiteSpace(_settings.GoogleClientId) &&
                               !string.IsNullOrWhiteSpace(_settings.GoogleClientSecret) &&
                               !string.IsNullOrWhiteSpace(_settings.GoogleRefreshToken) &&
                               !string.IsNullOrWhiteSpace(_settings.GoogleProjectId);

        if (hasRefreshOAuth)
        {
            var accessTokenResult = await GetAccessTokenByRefreshTokenAsync(cancellationToken).ConfigureAwait(false);
            if (accessTokenResult.IsError || string.IsNullOrWhiteSpace(accessTokenResult.Text))
            {
                return TranslateResult.Error(accessTokenResult.ErrorMessage, text);
            }

            return await TranslateByOAuthAsync(text, accessTokenResult.Text, "OAuth(refresh-token)", cancellationToken).ConfigureAwait(false);
        }

        if (hasManualOAuth)
        {
            return await TranslateByOAuthAsync(text, _settings.GoogleAccessToken.Trim(), "OAuth(access-token)", cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(_settings.GoogleApiKey))
        {
            return TranslateResult.Error("Google credentials are missing.", text);
        }

        return await TranslateByApiKeyAsync(text, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TranslateResult> TranslateByApiKeyAsync(string text, CancellationToken cancellationToken)
    {
        string apiKey = _settings.GoogleApiKey.Trim();
        string endpoint = $"https://translation.googleapis.com/language/translate/v2?key={Uri.EscapeDataString(apiKey)}";

        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["q"] = text,
            ["source"] = _settings.SourceLanguage,
            ["target"] = _settings.TargetLanguage,
            ["format"] = "text",
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
                return TranslateResult.Error($"Google(apiKey) error: {(int)response.StatusCode} {response.ReasonPhrase}", text);
            }

            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("data", out var dataNode) ||
                !dataNode.TryGetProperty("translations", out var translations) ||
                translations.GetArrayLength() == 0)
            {
                return TranslateResult.Error("Google(apiKey) response parse failed.", text);
            }

            string translated = translations[0].GetProperty("translatedText").GetString() ?? string.Empty;
            translated = WebUtility.HtmlDecode(translated);
            return TranslateResult.Success(translated);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return TranslateResult.Error($"Google(apiKey) request failed: {ex.Message}", text);
        }
    }

    private async Task<TranslateResult> TranslateByOAuthAsync(
        string text,
        string accessToken,
        string modeLabel,
        CancellationToken cancellationToken)
    {
        string projectId = _settings.GoogleProjectId.Trim();
        string endpoint = $"https://translation.googleapis.com/v3/projects/{Uri.EscapeDataString(projectId)}/locations/global:translateText";

        var payload = new
        {
            contents = new[] { text },
            sourceLanguageCode = _settings.SourceLanguage,
            targetLanguageCode = _settings.TargetLanguage,
        };

        string json = JsonSerializer.Serialize(payload);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return TranslateResult.Error($"Google({modeLabel}) error: {(int)response.StatusCode} {response.ReasonPhrase}", text);
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("translations", out var translations) ||
                translations.GetArrayLength() == 0)
            {
                return TranslateResult.Error($"Google({modeLabel}) response parse failed.", text);
            }

            string translated = translations[0].GetProperty("translatedText").GetString() ?? string.Empty;
            translated = WebUtility.HtmlDecode(translated);
            return TranslateResult.Success(translated);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return TranslateResult.Error($"Google({modeLabel}) request failed: {ex.Message}", text);
        }
    }

    private async Task<TranslateResult> GetAccessTokenByRefreshTokenAsync(CancellationToken cancellationToken)
    {
        // Reuse access token until near expiry to avoid token endpoint calls on every OCR cycle.
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(_cachedOAuthAccessToken) &&
            now < _cachedOAuthAccessTokenExpiresAt.AddSeconds(-60))
        {
            return TranslateResult.Success(_cachedOAuthAccessToken);
        }

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(_cachedOAuthAccessToken) &&
                now < _cachedOAuthAccessTokenExpiresAt.AddSeconds(-60))
            {
                return TranslateResult.Success(_cachedOAuthAccessToken);
            }

            using var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _settings.GoogleClientId.Trim(),
                ["client_secret"] = _settings.GoogleClientSecret.Trim(),
                ["refresh_token"] = _settings.GoogleRefreshToken.Trim(),
                ["grant_type"] = "refresh_token"
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
            {
                Content = body
            };

            using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return TranslateResult.Error($"Google OAuth token error: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("access_token", out var tokenNode))
            {
                return TranslateResult.Error("Google OAuth token parse failed: access_token is missing.");
            }

            string accessToken = tokenNode.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return TranslateResult.Error("Google OAuth token parse failed: access_token is empty.");
            }

            int expiresIn = 3600;
            if (doc.RootElement.TryGetProperty("expires_in", out var expiresInNode) &&
                expiresInNode.ValueKind == JsonValueKind.Number &&
                expiresInNode.TryGetInt32(out int expiresParsed) &&
                expiresParsed > 0)
            {
                expiresIn = expiresParsed;
            }

            _cachedOAuthAccessToken = accessToken;
            _cachedOAuthAccessTokenExpiresAt = now.AddSeconds(expiresIn);
            return TranslateResult.Success(accessToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return TranslateResult.Error($"Google OAuth token request failed: {ex.Message}");
        }
        finally
        {
            _tokenLock.Release();
        }
    }
}
