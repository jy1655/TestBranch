using OcrLite.Utils;

namespace OcrLite.Logging;

internal sealed class TextDeduplicator
{
    private readonly double _similarityThreshold;
    private readonly TimeSpan _minInterval;

    private string _lastText = string.Empty;
    private DateTimeOffset _lastEmitAt = DateTimeOffset.MinValue;

    public TextDeduplicator(double similarityThreshold = 0.93, TimeSpan? minInterval = null)
    {
        _similarityThreshold = similarityThreshold;
        _minInterval = minInterval ?? TimeSpan.FromMilliseconds(150);
    }

    public bool ShouldEmit(string text, DateTimeOffset now)
    {
        text = Normalize(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (_lastEmitAt != DateTimeOffset.MinValue && now - _lastEmitAt < _minInterval)
        {
            return false;
        }

        if (string.IsNullOrEmpty(_lastText))
        {
            Remember(text, now);
            return true;
        }

        double similarity = TextSimilarity.Similarity(_lastText, text);
        if (similarity >= _similarityThreshold)
        {
            return false;
        }

        Remember(text, now);
        return true;
    }

    private static string Normalize(string text)
    {
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private void Remember(string text, DateTimeOffset now)
    {
        _lastText = text;
        _lastEmitAt = now;
    }
}
