using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using OcrLite.Capture;
using OcrLite.Logging;
using OcrLite.Models;
using OcrLite.Ocr;
using OcrLite.Translation;

namespace OcrLite;

public sealed class MainForm : Form
{
    private readonly WindowDiscoveryService _windowDiscovery = new();
    private readonly WindowCaptureService _windowCapture = new();
    private readonly WindowsOcrService _ocrService = new();

    private readonly System.Windows.Forms.Timer _previewTimer = new();

    private readonly object _frameLock = new();

    private ComboBox _cmbWindows = null!;
    private Button _btnRefresh = null!;
    private Button _btnAttach = null!;
    private Button _btnStart = null!;
    private Button _btnStop = null!;
    private Button _btnClearRoi = null!;
    private TextBox _txtLanguage = null!;
    private TextBox _txtTargetLanguage = null!;
    private TextBox _txtLogDir = null!;
    private ComboBox _cmbTranslator = null!;
    private TextBox _txtDeepLApiKey = null!;
    private TextBox _txtGoogleApiKey = null!;
    private TextBox _txtGoogleAccessToken = null!;
    private TextBox _txtGoogleProjectId = null!;
    private TextBox _txtGoogleClientId = null!;
    private TextBox _txtGoogleClientSecret = null!;
    private TextBox _txtGoogleRefreshToken = null!;
    private TextBox _txtPapagoClientId = null!;
    private TextBox _txtPapagoClientSecret = null!;
    private NumericUpDown _numInterval = null!;
    private Label _lblStatus = null!;
    private Label _lblRoi = null!;
    private PictureBox _picturePreview = null!;
    private TextBox _txtCurrentOcr = null!;
    private CheckBox _chkSourceOnly = null!;

    private IntPtr _attachedHandle = IntPtr.Zero;
    private string _attachedTitle = string.Empty;

    private Bitmap? _latestFrame;
    private Rectangle _roi = Rectangle.Empty;

    private bool _isDraggingRoi;
    private Point _dragStart;
    private Rectangle _dragRectPreview = Rectangle.Empty;

    private CancellationTokenSource? _ocrLoopCts;
    private Task? _ocrLoopTask;
    private TextDeduplicator _deduplicator = new();
    private TranscriptLogger? _transcriptLogger;
    private ITextTranslator _translator = new IdentityTranslator();
    private TranslatorType _currentTranslatorType = TranslatorType.None;

    public MainForm()
    {
        Text = "OCR-Lite (MORT-based)";
        Width = 1400;
        Height = 920;
        StartPosition = FormStartPosition.CenterScreen;

        InitializeLayout();
        WireEvents();
        InitializeTranslatorSelection();
        RefreshWindowList();

        _previewTimer.Interval = 120;
        _previewTimer.Tick += PreviewTimer_Tick;
        _previewTimer.Start();

        SetStatus("Ready. Select OBS window and click Attach.");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _previewTimer.Stop();

        _ocrLoopCts?.Cancel();
        try
        {
            _ocrLoopTask?.Wait(1000);
        }
        catch
        {
            // ignore
        }

        _transcriptLogger?.Dispose();
        _transcriptLogger = null;

        lock (_frameLock)
        {
            _latestFrame?.Dispose();
            _latestFrame = null;
        }

        var oldImage = _picturePreview.Image;
        _picturePreview.Image = null;
        oldImage?.Dispose();

        base.OnFormClosing(e);
    }

    private void InitializeLayout()
    {
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 305,
            Padding = new Padding(10)
        };

        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 980
        };

        _cmbWindows = new ComboBox
        {
            Left = 12,
            Top = 10,
            Width = 560,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        _btnRefresh = new Button
        {
            Left = 580,
            Top = 10,
            Width = 85,
            Height = 26,
            Text = "Refresh"
        };

        _btnAttach = new Button
        {
            Left = 670,
            Top = 10,
            Width = 85,
            Height = 26,
            Text = "Attach"
        };

        var lblLanguage = new Label
        {
            Left = 12,
            Top = 48,
            Width = 84,
            Text = "Source Lang"
        };

        _txtLanguage = new TextBox
        {
            Left = 98,
            Top = 44,
            Width = 70,
            Text = "ja"
        };

        var lblTargetLanguage = new Label
        {
            Left = 178,
            Top = 48,
            Width = 84,
            Text = "Target Lang"
        };

        _txtTargetLanguage = new TextBox
        {
            Left = 262,
            Top = 44,
            Width = 70,
            Text = "ko"
        };

        var lblInterval = new Label
        {
            Left = 340,
            Top = 48,
            Width = 110,
            Text = "Interval(ms)"
        };

        _numInterval = new NumericUpDown
        {
            Left = 425,
            Top = 44,
            Width = 88,
            Minimum = 100,
            Maximum = 5000,
            Value = 350,
            Increment = 50
        };

        var lblLogDir = new Label
        {
            Left = 520,
            Top = 48,
            Width = 60,
            Text = "Log Dir"
        };

        _txtLogDir = new TextBox
        {
            Left = 580,
            Top = 44,
            Width = 350,
            Text = "logs"
        };

        var lblTranslator = new Label
        {
            Left = 12,
            Top = 84,
            Width = 84,
            Text = "Translator"
        };

        _cmbTranslator = new ComboBox
        {
            Left = 98,
            Top = 80,
            Width = 140,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        var lblDeepL = new Label
        {
            Left = 248,
            Top = 84,
            Width = 80,
            Text = "DeepL Key"
        };

        _txtDeepLApiKey = new TextBox
        {
            Left = 325,
            Top = 80,
            Width = 605,
            UseSystemPasswordChar = true
        };

        var lblGoogleApiKey = new Label
        {
            Left = 12,
            Top = 118,
            Width = 84,
            Text = "Google Key"
        };

        _txtGoogleApiKey = new TextBox
        {
            Left = 98,
            Top = 114,
            Width = 250,
            UseSystemPasswordChar = true
        };

        var lblGoogleToken = new Label
        {
            Left = 360,
            Top = 118,
            Width = 90,
            Text = "OAuth Token"
        };

        _txtGoogleAccessToken = new TextBox
        {
            Left = 450,
            Top = 114,
            Width = 320,
            UseSystemPasswordChar = true
        };

        var lblGoogleProject = new Label
        {
            Left = 780,
            Top = 118,
            Width = 72,
            Text = "Project ID"
        };

        _txtGoogleProjectId = new TextBox
        {
            Left = 850,
            Top = 114,
            Width = 220
        };

        var lblGoogleClientId = new Label
        {
            Left = 12,
            Top = 186,
            Width = 84,
            Text = "Google CID"
        };

        _txtGoogleClientId = new TextBox
        {
            Left = 98,
            Top = 182,
            Width = 250
        };

        var lblGoogleClientSecret = new Label
        {
            Left = 360,
            Top = 186,
            Width = 90,
            Text = "Google Secret"
        };

        _txtGoogleClientSecret = new TextBox
        {
            Left = 450,
            Top = 182,
            Width = 320,
            UseSystemPasswordChar = true
        };

        var lblGoogleRefreshToken = new Label
        {
            Left = 780,
            Top = 186,
            Width = 72,
            Text = "Refresh Tok"
        };

        _txtGoogleRefreshToken = new TextBox
        {
            Left = 850,
            Top = 182,
            Width = 220,
            UseSystemPasswordChar = true
        };

        var lblPapagoId = new Label
        {
            Left = 12,
            Top = 152,
            Width = 84,
            Text = "Papago ID"
        };

        _txtPapagoClientId = new TextBox
        {
            Left = 98,
            Top = 148,
            Width = 250
        };

        var lblPapagoSecret = new Label
        {
            Left = 360,
            Top = 152,
            Width = 90,
            Text = "Papago Secret"
        };

        _txtPapagoClientSecret = new TextBox
        {
            Left = 450,
            Top = 148,
            Width = 320,
            UseSystemPasswordChar = true
        };

        _chkSourceOnly = new CheckBox
        {
            Left = 780,
            Top = 150,
            Width = 180,
            Text = "Log source text only",
            Checked = true
        };

        _btnStart = new Button
        {
            Left = 12,
            Top = 216,
            Width = 110,
            Height = 30,
            Text = "Start OCR"
        };

        _btnStop = new Button
        {
            Left = 128,
            Top = 216,
            Width = 90,
            Height = 30,
            Text = "Stop",
            Enabled = false
        };

        _btnClearRoi = new Button
        {
            Left = 224,
            Top = 216,
            Width = 100,
            Height = 30,
            Text = "Reset ROI"
        };

        _lblStatus = new Label
        {
            Left = 340,
            Top = 220,
            Width = 980,
            Height = 26,
            Text = "Status",
            AutoEllipsis = true
        };

        _lblRoi = new Label
        {
            Left = 12,
            Top = 254,
            Width = 1308,
            Height = 26,
            Text = "ROI: not selected",
            AutoEllipsis = true
        };

        _picturePreview = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            SizeMode = PictureBoxSizeMode.StretchImage
        };

        _txtCurrentOcr = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 10.5f),
        };

        topPanel.Controls.AddRange(
            new Control[]
            {
                _cmbWindows,
                _btnRefresh,
                _btnAttach,
                lblLanguage,
                _txtLanguage,
                lblTargetLanguage,
                _txtTargetLanguage,
                lblInterval,
                _numInterval,
                lblLogDir,
                _txtLogDir,
                lblTranslator,
                _cmbTranslator,
                lblDeepL,
                _txtDeepLApiKey,
                lblGoogleApiKey,
                _txtGoogleApiKey,
                lblGoogleToken,
                _txtGoogleAccessToken,
                lblGoogleProject,
                _txtGoogleProjectId,
                lblGoogleClientId,
                _txtGoogleClientId,
                lblGoogleClientSecret,
                _txtGoogleClientSecret,
                lblGoogleRefreshToken,
                _txtGoogleRefreshToken,
                lblPapagoId,
                _txtPapagoClientId,
                lblPapagoSecret,
                _txtPapagoClientSecret,
                _chkSourceOnly,
                _btnStart,
                _btnStop,
                _btnClearRoi,
                _lblStatus,
                _lblRoi
            });

        mainSplit.Panel1.Controls.Add(_picturePreview);
        mainSplit.Panel2.Controls.Add(_txtCurrentOcr);

        Controls.Add(mainSplit);
        Controls.Add(topPanel);
    }

    private void WireEvents()
    {
        _btnRefresh.Click += (_, _) => RefreshWindowList();
        _btnAttach.Click += (_, _) => AttachSelectedWindow();
        _btnStart.Click += (_, _) => StartOcrLoop();
        _btnStop.Click += async (_, _) => await StopOcrLoopAsync();
        _btnClearRoi.Click += (_, _) => ResetRoi();
        _cmbTranslator.SelectedIndexChanged += (_, _) => UpdateTranslatorInputVisibility();

        _picturePreview.Paint += PicturePreview_Paint;
        _picturePreview.MouseDown += PicturePreview_MouseDown;
        _picturePreview.MouseMove += PicturePreview_MouseMove;
        _picturePreview.MouseUp += PicturePreview_MouseUp;
    }

    private void InitializeTranslatorSelection()
    {
        _cmbTranslator.Items.Clear();
        _cmbTranslator.Items.Add(TranslatorType.None);
        _cmbTranslator.Items.Add(TranslatorType.DeepL);
        _cmbTranslator.Items.Add(TranslatorType.Google);
        _cmbTranslator.Items.Add(TranslatorType.Papago);
        _cmbTranslator.SelectedItem = TranslatorType.None;
        UpdateTranslatorInputVisibility();
    }

    private void RefreshWindowList()
    {
        var list = _windowDiscovery.EnumerateWindows();

        _cmbWindows.BeginUpdate();
        _cmbWindows.Items.Clear();

        foreach (var window in list)
        {
            _cmbWindows.Items.Add(window);
        }

        _cmbWindows.EndUpdate();

        // OBS 창을 우선 선택
        for (int i = 0; i < _cmbWindows.Items.Count; i++)
        {
            if (_cmbWindows.Items[i] is WindowInfo info &&
                info.Title.Contains("OBS", StringComparison.OrdinalIgnoreCase))
            {
                _cmbWindows.SelectedIndex = i;
                SetStatus("Window list refreshed. OBS window preselected.");
                return;
            }
        }

        if (_cmbWindows.Items.Count > 0)
        {
            _cmbWindows.SelectedIndex = 0;
        }

        SetStatus($"Window list refreshed. {list.Count} windows available.");
    }

    private void AttachSelectedWindow()
    {
        if (_cmbWindows.SelectedItem is not WindowInfo selected)
        {
            SetStatus("No window selected.");
            return;
        }

        _attachedHandle = selected.Handle;
        _attachedTitle = selected.Title;
        _roi = Rectangle.Empty;
        _txtCurrentOcr.Clear();

        SetStatus($"Attached: {_attachedTitle} (0x{_attachedHandle.ToInt64():X})");
    }

    private void PreviewTimer_Tick(object? sender, EventArgs e)
    {
        if (_attachedHandle == IntPtr.Zero)
        {
            return;
        }

        if (!_windowCapture.TryCapture(_attachedHandle, out var captured, out _, out var error))
        {
            SetStatus(error);
            return;
        }

        if (captured is null)
        {
            SetStatus("Capture returned empty frame.");
            return;
        }

        lock (_frameLock)
        {
            _latestFrame?.Dispose();
            _latestFrame = captured;

            if (_roi.Width <= 0 || _roi.Height <= 0)
            {
                _roi = BuildDefaultDialogueRoi(captured.Width, captured.Height);
                UpdateRoiLabel();
            }
        }

        var previewClone = (Bitmap)captured.Clone();
        var oldImage = _picturePreview.Image;
        _picturePreview.Image = previewClone;
        oldImage?.Dispose();

        _picturePreview.Invalidate();
    }

    private void StartOcrLoop()
    {
        if (_attachedHandle == IntPtr.Zero)
        {
            SetStatus("Attach a window first.");
            return;
        }

        if (_ocrLoopCts is not null)
        {
            SetStatus("OCR loop is already running.");
            return;
        }

        var translationSettings = BuildTranslationSettings();
        string language = translationSettings.SourceLanguage;
        _ocrService.Initialize(language);

        if (!TranslatorFactory.TryCreate(translationSettings, out var translator, out var translatorError))
        {
            SetStatus(translatorError);
            return;
        }

        _translator = translator;
        _currentTranslatorType = translationSettings.Type;

        var roi = GetCurrentRoi();
        if (roi.Width <= 0 || roi.Height <= 0)
        {
            SetStatus("ROI is not set. Drag the dialogue area on preview.");
            return;
        }

        string logDir = ResolveLogDirectory(_txtLogDir.Text.Trim());

        _transcriptLogger?.Dispose();
        _transcriptLogger = new TranscriptLogger(
            rootLogDirectory: logDir,
            sourceLang: language,
            targetLang: translationSettings.TargetLanguage,
            engineName: $"{_ocrService.EngineName}+{_translator.Name}",
            windowTitle: _attachedTitle,
            windowHandle: _attachedHandle,
            roi: roi,
            sourceOnly: _chkSourceOnly.Checked);

        _deduplicator = new TextDeduplicator();
        _ocrLoopCts = new CancellationTokenSource();
        _ocrLoopTask = Task.Run(() => OcrLoopAsync(_ocrLoopCts.Token));

        _btnStart.Enabled = false;
        _btnStop.Enabled = true;

        SetStatus($"OCR started ({_translator.Name}). Session: {_transcriptLogger.SessionDirectory}");
    }

    private async Task StopOcrLoopAsync()
    {
        if (_ocrLoopCts is null)
        {
            return;
        }

        _ocrLoopCts.Cancel();
        try
        {
            if (_ocrLoopTask is not null)
            {
                await _ocrLoopTask;
            }
        }
        catch (OperationCanceledException)
        {
            // normal cancellation path
        }
        catch (Exception ex)
        {
            SetStatus($"OCR loop error: {ex.Message}");
        }

        _ocrLoopCts.Dispose();
        _ocrLoopCts = null;
        _ocrLoopTask = null;

        _transcriptLogger?.Dispose();
        _transcriptLogger = null;
        _translator = new IdentityTranslator();
        _currentTranslatorType = TranslatorType.None;

        _btnStart.Enabled = true;
        _btnStop.Enabled = false;

        SetStatus("OCR stopped.");
    }

    private async Task OcrLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Bitmap? frame = CloneLatestFrame();
            if (frame is null)
            {
                await Task.Delay(120, token);
                continue;
            }

            try
            {
                var roi = GetCurrentRoi();
                if (roi.Width <= 0 || roi.Height <= 0)
                {
                    await Task.Delay(GetCurrentIntervalMs(), token);
                    continue;
                }

                var clamped = ClampToBounds(roi, new Rectangle(0, 0, frame.Width, frame.Height));
                using var cropped = frame.Clone(clamped, PixelFormat.Format32bppArgb);

                string text = await _ocrService.RecognizeAsync(cropped, token).ConfigureAwait(false);
                text = NormalizeText(text);

                var now = DateTimeOffset.Now;
                if (_deduplicator.ShouldEmit(text, now))
                {
                    var translateResult = await _translator.TranslateAsync(text, token).ConfigureAwait(false);
                    string translatedText = translateResult.Text;
                    if (string.IsNullOrWhiteSpace(translatedText))
                    {
                        translatedText = text;
                    }

                    _transcriptLogger?.Log(text, translatedText);
                    BeginInvoke((Action)(() =>
                    {
                        _txtCurrentOcr.Text = FormatOutputText(text, translatedText);
                        if (translateResult.IsError)
                        {
                            SetStatus(translateResult.ErrorMessage);
                        }
                    }));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                BeginInvoke((Action)(() => SetStatus($"OCR error: {ex.Message}")));
            }
            finally
            {
                frame.Dispose();
            }

            await Task.Delay(GetCurrentIntervalMs(), token);
        }
    }

    private void ResetRoi()
    {
        lock (_frameLock)
        {
            if (_latestFrame is null)
            {
                _roi = Rectangle.Empty;
            }
            else
            {
                _roi = BuildDefaultDialogueRoi(_latestFrame.Width, _latestFrame.Height);
            }
        }

        UpdateRoiLabel();
        _picturePreview.Invalidate();
    }

    private void PicturePreview_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_picturePreview.Image is null)
        {
            return;
        }

        _isDraggingRoi = true;
        _dragStart = e.Location;
        _dragRectPreview = Rectangle.Empty;
    }

    private void PicturePreview_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isDraggingRoi)
        {
            return;
        }

        _dragRectPreview = NormalizeRect(_dragStart, e.Location);
        _picturePreview.Invalidate();
    }

    private void PicturePreview_MouseUp(object? sender, MouseEventArgs e)
    {
        if (!_isDraggingRoi)
        {
            return;
        }

        _isDraggingRoi = false;

        var selected = NormalizeRect(_dragStart, e.Location);
        _dragRectPreview = Rectangle.Empty;

        if (selected.Width < 10 || selected.Height < 10)
        {
            _picturePreview.Invalidate();
            return;
        }

        var frameRect = PreviewRectToFrameRect(selected);
        if (frameRect.Width <= 0 || frameRect.Height <= 0)
        {
            _picturePreview.Invalidate();
            return;
        }

        lock (_frameLock)
        {
            _roi = frameRect;
        }

        UpdateRoiLabel();
        _picturePreview.Invalidate();
    }

    private void PicturePreview_Paint(object? sender, PaintEventArgs e)
    {
        if (_picturePreview.Image is not null)
        {
            var roi = GetCurrentRoi();
            if (roi.Width > 0 && roi.Height > 0)
            {
                var previewRoi = FrameRectToPreviewRect(roi);
                if (previewRoi.Width > 0 && previewRoi.Height > 0)
                {
                    using var pen = new Pen(Color.Lime, 2f);
                    e.Graphics.DrawRectangle(pen, previewRoi);
                }
            }
        }

        if (_isDraggingRoi && _dragRectPreview.Width > 0 && _dragRectPreview.Height > 0)
        {
            using var pen = new Pen(Color.Yellow, 2f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            e.Graphics.DrawRectangle(pen, _dragRectPreview);
        }
    }

    private Bitmap? CloneLatestFrame()
    {
        lock (_frameLock)
        {
            if (_latestFrame is null)
            {
                return null;
            }

            return (Bitmap)_latestFrame.Clone();
        }
    }

    private Rectangle GetCurrentRoi()
    {
        lock (_frameLock)
        {
            return _roi;
        }
    }

    private int GetCurrentIntervalMs()
    {
        if (InvokeRequired)
        {
            var value = Invoke(new Func<int>(() => (int)_numInterval.Value));
            return value is int i ? i : 350;
        }

        return (int)_numInterval.Value;
    }

    private void UpdateRoiLabel()
    {
        var roi = GetCurrentRoi();
        if (roi.Width <= 0 || roi.Height <= 0)
        {
            _lblRoi.Text = "ROI: not selected";
            return;
        }

        _lblRoi.Text = $"ROI: {roi.X}, {roi.Y}, {roi.Width}, {roi.Height}";
    }

    private void SetStatus(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke((Action)(() => _lblStatus.Text = message));
            return;
        }

        _lblStatus.Text = message;
    }

    private TranslatorType GetSelectedTranslatorType()
    {
        if (_cmbTranslator.SelectedItem is TranslatorType translatorType)
        {
            return translatorType;
        }

        return TranslatorType.None;
    }

    private TranslationSettings BuildTranslationSettings()
    {
        string sourceLang = string.IsNullOrWhiteSpace(_txtLanguage.Text) ? "ja" : _txtLanguage.Text.Trim();
        string targetLang = string.IsNullOrWhiteSpace(_txtTargetLanguage.Text) ? "ko" : _txtTargetLanguage.Text.Trim();

        return new TranslationSettings
        {
            Type = GetSelectedTranslatorType(),
            SourceLanguage = sourceLang,
            TargetLanguage = targetLang,
            DeepLApiKey = _txtDeepLApiKey.Text.Trim(),
            GoogleApiKey = _txtGoogleApiKey.Text.Trim(),
            GoogleAccessToken = _txtGoogleAccessToken.Text.Trim(),
            GoogleProjectId = _txtGoogleProjectId.Text.Trim(),
            GoogleClientId = _txtGoogleClientId.Text.Trim(),
            GoogleClientSecret = _txtGoogleClientSecret.Text.Trim(),
            GoogleRefreshToken = _txtGoogleRefreshToken.Text.Trim(),
            PapagoClientId = _txtPapagoClientId.Text.Trim(),
            PapagoClientSecret = _txtPapagoClientSecret.Text.Trim(),
        };
    }

    private void UpdateTranslatorInputVisibility()
    {
        var type = GetSelectedTranslatorType();
        _txtDeepLApiKey.Enabled = type == TranslatorType.DeepL;

        bool googleEnabled = type == TranslatorType.Google;
        _txtGoogleApiKey.Enabled = googleEnabled;
        _txtGoogleAccessToken.Enabled = googleEnabled;
        _txtGoogleProjectId.Enabled = googleEnabled;
        _txtGoogleClientId.Enabled = googleEnabled;
        _txtGoogleClientSecret.Enabled = googleEnabled;
        _txtGoogleRefreshToken.Enabled = googleEnabled;

        bool papagoEnabled = type == TranslatorType.Papago;
        _txtPapagoClientId.Enabled = papagoEnabled;
        _txtPapagoClientSecret.Enabled = papagoEnabled;
    }

    private string FormatOutputText(string sourceText, string translatedText)
    {
        if (_currentTranslatorType == TranslatorType.None)
        {
            return sourceText;
        }

        return $"[SRC]{Environment.NewLine}{sourceText}{Environment.NewLine}{Environment.NewLine}[TRN]{Environment.NewLine}{translatedText}";
    }

    private static Rectangle NormalizeRect(Point p1, Point p2)
    {
        int x = Math.Min(p1.X, p2.X);
        int y = Math.Min(p1.Y, p2.Y);
        int w = Math.Abs(p1.X - p2.X);
        int h = Math.Abs(p1.Y - p2.Y);
        return new Rectangle(x, y, w, h);
    }

    private Rectangle PreviewRectToFrameRect(Rectangle previewRect)
    {
        lock (_frameLock)
        {
            if (_latestFrame is null)
            {
                return Rectangle.Empty;
            }

            int previewW = Math.Max(1, _picturePreview.ClientSize.Width);
            int previewH = Math.Max(1, _picturePreview.ClientSize.Height);

            float scaleX = _latestFrame.Width / (float)previewW;
            float scaleY = _latestFrame.Height / (float)previewH;

            int x = (int)Math.Round(previewRect.X * scaleX);
            int y = (int)Math.Round(previewRect.Y * scaleY);
            int w = (int)Math.Round(previewRect.Width * scaleX);
            int h = (int)Math.Round(previewRect.Height * scaleY);

            return ClampToBounds(new Rectangle(x, y, w, h), new Rectangle(0, 0, _latestFrame.Width, _latestFrame.Height));
        }
    }

    private Rectangle FrameRectToPreviewRect(Rectangle frameRect)
    {
        lock (_frameLock)
        {
            if (_latestFrame is null)
            {
                return Rectangle.Empty;
            }

            int previewW = Math.Max(1, _picturePreview.ClientSize.Width);
            int previewH = Math.Max(1, _picturePreview.ClientSize.Height);

            float scaleX = previewW / (float)_latestFrame.Width;
            float scaleY = previewH / (float)_latestFrame.Height;

            int x = (int)Math.Round(frameRect.X * scaleX);
            int y = (int)Math.Round(frameRect.Y * scaleY);
            int w = (int)Math.Round(frameRect.Width * scaleX);
            int h = (int)Math.Round(frameRect.Height * scaleY);

            return ClampToBounds(new Rectangle(x, y, w, h), new Rectangle(0, 0, previewW, previewH));
        }
    }

    private static Rectangle BuildDefaultDialogueRoi(int frameWidth, int frameHeight)
    {
        int h = (int)Math.Round(frameHeight * 0.28);
        return new Rectangle(0, frameHeight - h, frameWidth, h);
    }

    private static Rectangle ClampToBounds(Rectangle rect, Rectangle bounds)
    {
        int x = Math.Max(bounds.Left, Math.Min(rect.X, bounds.Right - 1));
        int y = Math.Max(bounds.Top, Math.Min(rect.Y, bounds.Bottom - 1));
        int w = Math.Max(1, Math.Min(rect.Width, bounds.Right - x));
        int h = Math.Max(1, Math.Min(rect.Height, bounds.Bottom - y));
        return new Rectangle(x, y, w, h);
    }

    private static string ResolveLogDirectory(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            text = "logs";
        }

        if (!Path.IsPathRooted(text))
        {
            text = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, text));
        }

        Directory.CreateDirectory(text);
        return text;
    }

    private static string NormalizeText(string text)
    {
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal)
                   .Replace("\r", "\n", StringComparison.Ordinal);

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Trim())
                        .Where(line => line.Length > 0);

        return string.Join(Environment.NewLine, lines);
    }
}
