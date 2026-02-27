using System.Text;
using System.Linq;
using OcrLite.Interop;
using OcrLite.Models;

namespace OcrLite.Capture;

internal sealed class WindowDiscoveryService
{
    public IReadOnlyList<WindowInfo> EnumerateWindows()
    {
        var result = new List<WindowInfo>();
        var shellWindow = NativeMethods.GetShellWindow();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (hWnd == IntPtr.Zero || hWnd == shellWindow)
            {
                return true;
            }

            if (!NativeMethods.IsWindowVisible(hWnd) || NativeMethods.IsIconic(hWnd))
            {
                return true;
            }

            int textLength = NativeMethods.GetWindowTextLength(hWnd);
            if (textLength <= 0)
            {
                return true;
            }

            var builder = new StringBuilder(textLength + 1);
            _ = NativeMethods.GetWindowText(hWnd, builder, builder.Capacity);
            var title = builder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return true;
            }

            if (!NativeMethods.GetWindowRect(hWnd, out var rectRaw))
            {
                return true;
            }

            var rect = rectRaw.ToRectangle();
            if (rect.Width < 200 || rect.Height < 120)
            {
                return true;
            }

            result.Add(new WindowInfo(hWnd, title, rect));
            return true;
        }, IntPtr.Zero);

        return result
            .OrderByDescending(w => w.Bounds.Width * w.Bounds.Height)
            .ThenBy(w => w.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
