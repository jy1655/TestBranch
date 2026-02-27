namespace OcrLite.Translation;

internal sealed class TranslationSettings
{
    public TranslatorType Type { get; set; } = TranslatorType.None;

    public string SourceLanguage { get; set; } = "ja";
    public string TargetLanguage { get; set; } = "ko";

    public string DeepLApiKey { get; set; } = string.Empty;

    public string GoogleApiKey { get; set; } = string.Empty;
    public string GoogleAccessToken { get; set; } = string.Empty;
    public string GoogleProjectId { get; set; } = string.Empty;
    public string GoogleClientId { get; set; } = string.Empty;
    public string GoogleClientSecret { get; set; } = string.Empty;
    public string GoogleRefreshToken { get; set; } = string.Empty;

    public string PapagoClientId { get; set; } = string.Empty;
    public string PapagoClientSecret { get; set; } = string.Empty;
}
