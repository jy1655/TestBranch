using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace OcrLite.Ocr;

internal sealed class WindowsOcrService
{
    private OcrEngine? _engine;
    private string _activeLanguage = "auto";

    public string EngineName => "WindowsOCR";
    public string ActiveLanguage => _activeLanguage;

    public void Initialize(string languageTag)
    {
        languageTag = (languageTag ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(languageTag) || languageTag.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            _engine = OcrEngine.TryCreateFromUserProfileLanguages();
            _activeLanguage = "auto";
            return;
        }

        try
        {
            var language = new Language(languageTag);
            if (OcrEngine.IsLanguageSupported(language))
            {
                _engine = OcrEngine.TryCreateFromLanguage(language);
                _activeLanguage = languageTag;
            }
            else
            {
                _engine = OcrEngine.TryCreateFromUserProfileLanguages();
                _activeLanguage = "auto";
            }
        }
        catch
        {
            _engine = OcrEngine.TryCreateFromUserProfileLanguages();
            _activeLanguage = "auto";
        }
    }

    public async Task<string> RecognizeAsync(Bitmap bitmap, CancellationToken cancellationToken)
    {
        if (_engine is null)
        {
            throw new InvalidOperationException("OCR engine is not initialized.");
        }

        using var softwareBitmap = ToSoftwareBitmap(bitmap);
        var result = await _engine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken).ConfigureAwait(false);

        if (result is null || result.Lines is null || result.Lines.Count == 0)
        {
            return string.Empty;
        }

        var lines = result.Lines
            .Select(line => line.Text?.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line));

        return string.Join(Environment.NewLine, lines);
    }

    private static SoftwareBitmap ToSoftwareBitmap(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            int bytes = Math.Abs(data.Stride) * bitmap.Height;
            byte[] pixelBytes = new byte[bytes];
            Marshal.Copy(data.Scan0, pixelBytes, 0, bytes);

            return SoftwareBitmap.CreateCopyFromBuffer(
                pixelBytes.AsBuffer(),
                BitmapPixelFormat.Bgra8,
                bitmap.Width,
                bitmap.Height,
                BitmapAlphaMode.Ignore,
                (uint)Math.Abs(data.Stride));
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
