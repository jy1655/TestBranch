using System.Drawing;

namespace OcrLite.Models;

public sealed record WindowInfo(IntPtr Handle, string Title, Rectangle Bounds)
{
    public string DisplayName => $"{Title} ({Bounds.Width}x{Bounds.Height})";

    public override string ToString()
    {
        return DisplayName;
    }
}
