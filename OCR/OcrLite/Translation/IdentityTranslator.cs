namespace OcrLite.Translation;

internal sealed class IdentityTranslator : ITextTranslator
{
    public string Name => "None";

    public Task<TranslateResult> TranslateAsync(string text, CancellationToken cancellationToken)
    {
        return Task.FromResult(TranslateResult.Success(text));
    }
}
