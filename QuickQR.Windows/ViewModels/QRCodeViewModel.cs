using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using QuickQR.Infrastructure;
using QuickQR.Services;
using ZXing;

namespace QuickQR.ViewModels;

public sealed class QRCodeViewModel : ObservableObject, IDisposable
{
    private readonly QRCodeService _qr;
    private readonly SettingsService _settings;
    private readonly ClipboardService _clipboard;
    private readonly ImageExportService _export;
    private readonly DispatcherTimer _noticeTimer;
    private CancellationTokenSource? _generation;
    private string _text = "";
    private string _status = "等待输入";
    private string _notice = "";
    private QRDocument? _document;
    private bool _disposed;
    public string Text { get => _text; set { if (Set(ref _text, value ?? "")) { Raise(nameof(CharacterCount)); ScheduleGeneration(); } } }
    public string CharacterCount => $"{Text.Length:N0} 个字符";
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string Notice { get => _notice; private set => Set(ref _notice, value); }
    public QRDocument? Document { get => _document; private set { Set(ref _document, value); Raise(nameof(HasCode)); CopyCommand.Refresh(); SaveCommand.Refresh(); } }
    public bool HasCode => Document is not null;
    public string[] CorrectionLevels { get; } = ["L", "M", "Q", "H"];
    public int[] ExportSizes { get; } = [512, 1024, 2048];
    public string Correction
    {
        get => _settings.Settings.ErrorCorrection;
        set { if (value is not ("L" or "M" or "Q" or "H") || value == Correction) return; _settings.Settings.ErrorCorrection = value; Raise(); SettingsChanged(); }
    }
    public int ExportSize
    {
        get => _settings.Settings.ExportSize;
        set { if (value is not (512 or 1024 or 2048) || value == ExportSize) return; _settings.Settings.ExportSize = value; Raise(); _settings.ScheduleSave(); }
    }
    public string Foreground => _settings.Settings.Foreground;
    public string Background => _settings.Settings.Background;
    public Brush ForegroundBrush => new SolidColorBrush(QRCodeService.ParseColor(Foreground));
    public Brush BackgroundBrush => new SolidColorBrush(QRCodeService.ParseColor(Background));
    public RelayCommand ClearCommand { get; }
    public RelayCommand ForegroundCommand { get; }
    public RelayCommand BackgroundCommand { get; }
    public AsyncCommand CopyCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand PasteCommand { get; }

    public QRCodeViewModel(QRCodeService qr, SettingsService settings, ClipboardService clipboard, ImageExportService export)
    {
        _qr = qr; _settings = settings; _clipboard = clipboard; _export = export;
        _noticeTimer = new() { Interval = TimeSpan.FromSeconds(5) };
        _noticeTimer.Tick += OnNoticeTick;
        ClearCommand = new(() => Text = "");
        ForegroundCommand = new(() => PickColor(true));
        BackgroundCommand = new(() => PickColor(false));
        CopyCommand = new(CopyAsync, ex => Notify("复制失败，剪贴板可能正被占用：" + ex.Message), () => HasCode);
        SaveCommand = new(SaveAsync, ex => Notify("保存失败：" + ex.Message), () => HasCode);
        PasteCommand = new(PasteAsync, ex => Notify("无法读取剪贴板：" + ex.Message));
    }
    private void SettingsChanged() { _settings.ScheduleSave(); ScheduleGeneration(); }
    private void PickColor(bool foreground)
    {
        // The modal native color dialog is also protected from tray deactivation.
        InteractionState.ModalDepth++;
        try
        {
            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.ColorTranslator.FromHtml(foreground ? Foreground : Background) };
            var owner = new DialogOwner();
            if (dialog.ShowDialog(owner) != System.Windows.Forms.DialogResult.OK) return;
            var color = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            if (!QRCodeService.IsSafeColorPair(foreground ? color : Foreground, foreground ? Background : color))
            { Notify("颜色对比度不足。请选择深色前景和浅色背景（至少 4.5:1）。"); return; }
            if (foreground) { _settings.Settings.Foreground = color; Raise(nameof(Foreground)); Raise(nameof(ForegroundBrush)); }
            else { _settings.Settings.Background = color; Raise(nameof(Background)); Raise(nameof(BackgroundBrush)); }
            SettingsChanged();
        }
        catch (Exception ex) { Notify("无法选择颜色：" + ex.Message); }
        finally { InteractionState.ModalDepth--; }
    }
    private async void ScheduleGeneration()
    {
        _generation?.Cancel();
        _generation?.Dispose();
        var cancellation = _generation = new();
        var token = cancellation.Token;
        Document = null; // Never allow copying a previous QR while a new input is pending.
        if (Text.Length == 0) { Status = "等待输入"; return; }
        Status = "正在生成…";
        var text = Text; var correction = Correction; var fg = Foreground; var bg = Background;
        try
        {
            await Task.Delay(140, token);
            var document = await Task.Run(() => _qr.Generate(text, correction, fg, bg), token);
            if (token.IsCancellationRequested || _disposed) return;
            Document = document;
            Status = "二维码已生成";
        }
        catch (OperationCanceledException) { }
        catch (WriterException) { if (!token.IsCancellationRequested) Status = "内容过长，请缩短文本或降低纠错等级"; }
        catch (EncoderFallbackException) { if (!token.IsCancellationRequested) Status = "生成失败：文本包含无效的 Unicode 字符"; }
        catch (Exception ex) { if (!token.IsCancellationRequested) { Status = "生成失败"; Notify("生成失败：" + ex.Message); } }
    }
    private async Task CopyAsync()
    {
        if (Document is not { } document) return;
        var size = ExportSize;
        var bitmap = await Task.Run(() => QRCodeService.Render(document, size));
        await _clipboard.CopyImageAsync(bitmap);
        Notify("已复制二维码图片");
    }
    private async Task SaveAsync()
    {
        if (Document is not { } document) return;
        InteractionState.ModalDepth++;
        try { if (await _export.SaveAsync(document, ExportSize)) Notify("PNG 图片已保存"); }
        finally { InteractionState.ModalDepth--; }
    }
    public async Task PasteAsync()
    {
        var text = await _clipboard.GetTextAsync();
        if (string.IsNullOrEmpty(text)) { Notify("剪贴板中没有文本"); return; }
        Text = text;
    }
    public void Notify(string message) { Notice = message; _noticeTimer.Stop(); _noticeTimer.Start(); }
    private void OnNoticeTick(object? sender, EventArgs e) { _noticeTimer.Stop(); Notice = ""; }
    public void Dispose()
    {
        _disposed = true; _generation?.Cancel(); _generation?.Dispose();
        _noticeTimer.Stop(); _noticeTimer.Tick -= OnNoticeTick;
    }
    private sealed class DialogOwner : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle
        {
            get
            {
                foreach (Window window in Application.Current.Windows)
                    if (window.IsActive) return new System.Windows.Interop.WindowInteropHelper(window).Handle;
                return IntPtr.Zero;
            }
        }
    }
}

public static class InteractionState
{
    private static int _modalDepth;
    public static event Action? ModalEnded;
    public static int ModalDepth
    {
        get => _modalDepth;
        set
        {
            var previous = _modalDepth;
            _modalDepth = Math.Max(0, value);
            if (previous > 0 && _modalDepth == 0) ModalEnded?.Invoke();
        }
    }
}
