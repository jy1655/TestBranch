namespace OcrLite.Utils;

internal static class TextSimilarity
{
    public static double Similarity(string a, string b)
    {
        a ??= string.Empty;
        b ??= string.Empty;

        if (a.Length == 0 && b.Length == 0)
        {
            return 1.0;
        }

        int distance = LevenshteinDistance(a, b);
        int maxLength = Math.Max(a.Length, b.Length);
        if (maxLength == 0)
        {
            return 1.0;
        }

        return 1.0 - (distance / (double)maxLength);
    }

    private static int LevenshteinDistance(string source, string target)
    {
        int n = source.Length;
        int m = target.Length;

        if (n == 0)
        {
            return m;
        }

        if (m == 0)
        {
            return n;
        }

        var d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++)
        {
            d[i, 0] = i;
        }

        for (int j = 0; j <= m; j++)
        {
            d[0, j] = j;
        }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = source[i - 1] == target[j - 1] ? 0 : 1;

                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost
                );
            }
        }

        return d[n, m];
    }
}
