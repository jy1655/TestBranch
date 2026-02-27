namespace OcrLite.Translation;

internal interface ITextTranslator
{
    string Name { get; }

    Task<TranslateResult> TranslateAsync(string text, CancellationToken cancellationToken);
}
