using System.Drawing;
using System.Text.Encodings.Web;
using System.Text.Json;
using OcrLite.Utils;

namespace OcrLite.Logging;

internal sealed class TranscriptLogger : IDisposable
{
    private readonly object _lock = new();
    private readonly string _sourceLang;
    private readonly string _targetLang;
    private readonly string _engineName;
    private readonly string _windowTitle;
    private readonly IntPtr _windowHandle;
    private readonly Rectangle _roi;
    private readonly bool _sourceOnly;
    private readonly double _sameWindowSimilarity;
    private readonly TimeSpan _windowMergeGap;

    private readonly StreamWriter _txtWriter;
    private readonly StreamWriter _jsonlWriter;

    private int _entryId;
    private int _dialogueWindowId;
    private string _lastText = string.Empty;
    private DateTimeOffset _lastEventAt = DateTimeOffset.MinValue;

    public string SessionDirectory { get; }

    public TranscriptLogger(
        string rootLogDirectory,
        string sourceLang,
        string targetLang,
        string engineName,
        string windowTitle,
        IntPtr windowHandle,
        Rectangle roi,
        bool sourceOnly = true,
        double sameWindowSimilarity = 0.72,
        TimeSpan? windowMergeGap = null)
    {
        _sourceLang = sourceLang;
        _targetLang = targetLang;
        _engineName = engineName;
        _windowTitle = windowTitle;
        _windowHandle = windowHandle;
        _roi = roi;
        _sourceOnly = sourceOnly;
        _sameWindowSimilarity = sameWindowSimilarity;
        _windowMergeGap = windowMergeGap ?? TimeSpan.FromSeconds(1.6);

        string session = $"session_{DateTime.Now:yyyyMMdd_HHmmss}";
        SessionDirectory = Path.Combine(rootLogDirectory, session);
        Directory.CreateDirectory(SessionDirectory);

        _txtWriter = new StreamWriter(Path.Combine(SessionDirectory, "transcript.txt"), append: true);
        _jsonlWriter = new StreamWriter(Path.Combine(SessionDirectory, "transcript.jsonl"), append: true);

        WriteHeader();
    }

    public void Log(string sourceText, string translatedText)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return;
        }

        translatedText ??= string.Empty;

        lock (_lock)
        {
            DateTimeOffset now = DateTimeOffset.Now;
            bool isNewDialogueWindow = IsNewDialogueWindow(sourceText, now);
            if (isNewDialogueWindow)
            {
                _dialogueWindowId++;
            }

            _entryId++;

            var entry = new
            {
                entry_id = _entryId,
                dialogue_window_id = _dialogueWindowId,
                timestamp = now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                source_lang = _sourceLang,
                target_lang = _targetLang,
                source_text = sourceText,
                translated_text = translatedText,
                engine = _engineName,
                attached_window = new
                {
                    handle = $"0x{_windowHandle.ToInt64():X}",
                    title = _windowTitle
                },
                roi = new
                {
                    x = _roi.X,
                    y = _roi.Y,
                    width = _roi.Width,
                    height = _roi.Height
                }
            };

            string json = JsonSerializer.Serialize(entry, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            });

            _jsonlWriter.WriteLine(json);
            _jsonlWriter.Flush();

            if (isNewDialogueWindow)
            {
                _txtWriter.WriteLine();
                _txtWriter.WriteLine(new string('-', 72));
                _txtWriter.WriteLine($"[DIALOGUE_WINDOW {_dialogueWindowId:0000}]");
            }

            _txtWriter.WriteLine($"[ENTRY {_entryId:00000}] {now:yyyy-MM-dd HH:mm:ss}");
            _txtWriter.WriteLine($"SRC({_sourceLang}): {sourceText}");
            if (!_sourceOnly)
            {
                _txtWriter.WriteLine($"TRN({_targetLang}): {translatedText}");
            }
            _txtWriter.WriteLine();
            _txtWriter.Flush();

            _lastText = sourceText;
            _lastEventAt = now;
        }
    }

    private void WriteHeader()
    {
        _txtWriter.WriteLine("OCR-Lite Transcript");
        _txtWriter.WriteLine($"Started At: {DateTime.Now:yyyy-MM-ddTHH:mm:ss}");
        _txtWriter.WriteLine($"Source Lang: {_sourceLang}");
        _txtWriter.WriteLine($"Target Lang: {_targetLang}");
        _txtWriter.WriteLine($"OCR Engine: {_engineName}");
        _txtWriter.WriteLine($"Attached Window: {_windowTitle}");
        _txtWriter.WriteLine($"Window Handle: 0x{_windowHandle.ToInt64():X}");
        _txtWriter.WriteLine($"ROI: {_roi.X},{_roi.Y},{_roi.Width},{_roi.Height}");
        _txtWriter.WriteLine(new string('=', 72));
        _txtWriter.WriteLine();
        _txtWriter.Flush();
    }

    private bool IsNewDialogueWindow(string currentText, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(_lastText))
        {
            return true;
        }

        if (now - _lastEventAt > _windowMergeGap)
        {
            return true;
        }

        if (currentText.Contains(_lastText, StringComparison.Ordinal) ||
            _lastText.Contains(currentText, StringComparison.Ordinal))
        {
            return false;
        }

        double similarity = TextSimilarity.Similarity(_lastText, currentText);
        return similarity < _sameWindowSimilarity;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _txtWriter.Dispose();
            _jsonlWriter.Dispose();
        }
    }
}
