using System.Drawing;
using System.Drawing.Imaging;
using OcrLite.Interop;

namespace OcrLite.Capture;

internal sealed class WindowCaptureService
{
    public bool TryCapture(IntPtr hWnd, out Bitmap? frame, out Rectangle bounds, out string error)
    {
        frame = null;
        bounds = Rectangle.Empty;
        error = string.Empty;

        if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd))
        {
            error = "Attached window is not valid.";
            return false;
        }

        if (NativeMethods.IsIconic(hWnd))
        {
            error = "Attached window is minimized.";
            return false;
        }

        if (!NativeMethods.GetWindowRect(hWnd, out var rectRaw))
        {
            error = "Failed to get attached window bounds.";
            return false;
        }

        bounds = rectRaw.ToRectangle();
        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            error = "Attached window bounds are too small.";
            return false;
        }

        try
        {
            var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }

            frame = bitmap;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Capture failed: {ex.Message}";
            return false;
        }
    }
}
