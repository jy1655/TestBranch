namespace OcrLite.Translation;

internal sealed record TranslateResult(string Text, bool IsError, string ErrorMessage)
{
    public static TranslateResult Success(string text)
    {
        return new TranslateResult(text, false, string.Empty);
    }

    public static TranslateResult Error(string message, string fallbackText = "")
    {
        return new TranslateResult(fallbackText, true, message);
    }
}
