using System.Text;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QuickQR.Services;
using QuickQR.ViewModels;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace QuickQR.Tests;

internal static class Program
{
    private static int _passed;
    private static readonly QRCodeService Qr = new();
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "QuickQR-Tests-" + Guid.NewGuid().ToString("N"));
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Test(string name, Action test) { test(); _passed++; Console.WriteLine("PASS " + name); }

    [STAThread]
    public static int Main(string[] args)
    {
        Directory.CreateDirectory(Root);
        var app = new QuickQR.App(false);
        app.InitializeComponent();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        try
        {
            if (args.Length == 2 && args[0] == "--export-sample")
            {
                ImageExportService.SavePng(Qr.Generate("https://example.com/QuickQR\n你好，世界！Hello, Windows 👋", "M", "#000000", "#FFFFFF"), 1024, args[1]);
                return 0;
            }
            if (args.Length == 2 && args[0] == "--preview")
            {
                RunAsync(() => PreviewAsync(args[1]));
                return 0;
            }
            foreach (var text in new[] { "Hello, QuickQR", "你好，世界！中文二维码", "https://example.com/路径?q=你好&emoji=😀", "😀🐼🚀 e\u0301 Ω Ж العربية", "第一行\n第二行\r\n第三行\t!@#$%^&*()", "  " })
                foreach (var level in new[] { "L", "M", "Q", "H" })
                    Test("UTF-8 roundtrip / " + level + " / " + text.Length, () => Assert(Decode(QRCodeService.Render(Qr.Generate(text, level, "#000000", "#FFFFFF"), 512)) == text, "Decoded payload differs"));
            Test("Numeric compact capacity above 2953 bytes", () =>
            {
                var text = new string('7', 4000);
                Assert(Decode(QRCodeService.Render(Qr.Generate(text, "L", "#000000", "#FFFFFF"), 1024)) == text, "Numeric roundtrip failed");
            });
            Test("Oversize content rejected", () => Expect<WriterException>(() => Qr.Generate(new string('文', 8000), "M", "#000000", "#FFFFFF")));
            Test("Correction-dependent capacity rejected", () => Expect<WriterException>(() => Qr.Generate(new string('a', 1400), "H", "#000000", "#FFFFFF")));
            Test("Unpaired surrogate rejected", () => Expect<EncoderFallbackException>(() => Qr.Generate("\uD800", "M", "#000000", "#FFFFFF")));
            Test("Empty rejected", () => Expect<ArgumentException>(() => Qr.Generate("", "M", "#000000", "#FFFFFF")));
            Test("Unsafe colors rejected", () =>
            {
                Assert(!QRCodeService.IsSafeColorPair("#AAAAAA", "#FFFFFF"), "Low contrast accepted");
                Assert(!QRCodeService.IsSafeColorPair("#FFFFFF", "#000000"), "Inverse QR accepted");
                Assert(!QRCodeService.IsSafeColorPair("invalid", "#FFFFFF"), "Invalid color accepted");
                Assert(!QRCodeService.IsSafeColorPair(null, "#FFFFFF"), "Null accepted");
                Assert(QRCodeService.IsSafeColorPair("#173C72", "#FFF9E8"), "Safe color rejected");
            });
            foreach (var size in new[] { 512, 1024, 2048 })
                Test("PNG geometry / integer pixels / " + size, () => CheckPng(size));
            Test("Settings defaults, roundtrip, no payload", () =>
            {
                var path = Path.Combine(Root, "settings.json");
                using (var settings = new SettingsService(path))
                {
                    Assert(settings.Settings.ExportSize == 1024 && settings.Settings.ErrorCorrection == "M", "Defaults wrong");
                    settings.Settings.Theme = QuickQR.Models.ThemeMode.Dark;
                    settings.Settings.AutoPaste = true;
                    settings.Save();
                }
                using var loaded = new SettingsService(path);
                Assert(loaded.Settings.Theme == QuickQR.Models.ThemeMode.Dark && loaded.Settings.AutoPaste, "Roundtrip failed");
                using var json = JsonDocument.Parse(File.ReadAllText(path));
                Assert(json.RootElement.EnumerateObject().Count() == 8, "Unexpected persisted fields");
                Assert(!File.ReadAllText(path).Contains("Text"), "Payload must not persist");
            });
            foreach (var invalid in new[] { "{broken", "{\"ExportSize\":17}", "{\"Foreground\":null}", "{\"Theme\":\"Bogus\"}", "{\"Background\":\"#000000\"}" })
                Test("Damaged settings recovery", () =>
                {
                    var path = Path.Combine(Root, "bad.json"); File.WriteAllText(path, invalid);
                    using var settings = new SettingsService(path);
                    Assert(settings.LoadWarning is not null && settings.Settings.IsValid(), "Recovery failed");
                });
            Test("Settings write failures reported", () =>
            {
                var blocker = Path.Combine(Root, "blocked"); File.WriteAllText(blocker, "file");
                using var settings = new SettingsService(Path.Combine(blocker, "settings.json"));
                var failed = false; settings.SaveFailed += _ => failed = true; settings.Save();
                Assert(failed, "Save error not surfaced");
            });
            Test("Panel position / all taskbar edges / negative monitor / mixed DPI", CheckPosition);
            Test("MVVM debounce / latest wins / invalidation / shared view binding", () => RunAsync(CheckViewModelAsync));
            Test("Current-user startup enable / disable / quoted path", CheckStartup);
            if (args.Contains("--windows")) Test("Real windows / shared VM / tray toggle / focus / close-to-tray / theme", () => RunAsync(CheckWindowsAsync));
            if (args.Contains("--clipboard")) Test("Native STA image clipboard and text paste", () => RunAsync(CheckClipboardAsync));
            Console.WriteLine($"{_passed} tests passed. Test files: {Root}");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex); return 1; }
        finally { app.ExitApplication(); }
    }

    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
    private static string Decode(BitmapSource bitmap)
    {
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        var source = new RGBLuminanceSource(pixels, converted.PixelWidth, converted.PixelHeight, RGBLuminanceSource.BitmapFormat.BGRA32);
        return new QRCodeReader().decode(new BinaryBitmap(new HybridBinarizer(source)), new Dictionary<DecodeHintType, object> { [DecodeHintType.TRY_HARDER] = true })?.Text ?? throw new InvalidOperationException("QR decode failed");
    }
    private static void CheckPng(int size)
    {
        var text = "QuickQR 导出测试 😀 " + size;
        var doc = Qr.Generate(text, "M", "#173C72", "#FFF9E8");
        var path = Path.Combine(Root, size + ".png"); ImageExportService.SavePng(doc, size, path);
        using var stream = File.OpenRead(path);
        var bitmap = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        Assert(bitmap.PixelWidth == size && bitmap.PixelHeight == size, "Dimensions wrong");
        Assert(Decode(bitmap) == text, "PNG roundtrip failed");
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[size * size * 4]; converted.CopyPixels(pixels, size * 4, 0);
        var n = doc.Modules.Width; var scale = size / n; var offset = (size - n * scale) / 2;
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var mx = (x - offset) / scale; var my = (y - offset) / scale;
                var dark = x >= offset && y >= offset && x < offset + n * scale && y < offset + n * scale && doc.Modules[mx, my];
                var c = dark ? doc.Foreground : doc.Background; var i = (y * size + x) * 4;
                Assert(pixels[i] == c.B && pixels[i+1] == c.G && pixels[i+2] == c.R && pixels[i+3] == 255, "Interpolated or misplaced pixel");
            }
        for (var y = 0; y < n; y++) for (var x = 0; x < n; x++)
            if (x < 4 || y < 4 || x >= n - 4 || y >= n - 4) Assert(!doc.Modules[x, y], "Quiet zone missing");
        // Verify replacement of an existing destination is supported.
        stream.Dispose();
        ImageExportService.SavePng(doc, size, path);
    }
    private static void CheckPosition()
    {
        foreach (var work in new[] { new PixelRect(0, 0, 1920, 1040), new PixelRect(-2560, -300, 0, 1100), new PixelRect(1920, 0, 3840, 2160) })
            foreach (var edge in Enum.GetValues<TaskbarEdge>())
                foreach (var scale in new[] { 1d, 1.25, 1.5, 2d })
                {
                    var w = (int)(368 * scale); var h = Math.Min((int)(610 * scale), work.Height - 20);
                    var (x, y) = TrayPositioner.Calculate(work, edge, work.Right - 10, work.Bottom - 5, w, h, 10);
                    Assert(x >= work.Left && y >= work.Top && x+w <= work.Right && y+h <= work.Bottom, "Panel outside work area");
                }
        var b = new PixelRect(0, 0, 1920, 1080);
        Assert(TrayPositioner.DetectEdge(b, new(0,0,1920,1040), 1900,1060) == TaskbarEdge.Bottom, "Bottom edge");
        Assert(TrayPositioner.DetectEdge(b, new(0,40,1920,1080), 1900,20) == TaskbarEdge.Top, "Top edge");
        Assert(TrayPositioner.DetectEdge(b, new(40,0,1920,1080), 20,1060) == TaskbarEdge.Left, "Left edge");
        Assert(TrayPositioner.DetectEdge(b, new(0,0,1880,1080), 1900,1060) == TaskbarEdge.Right, "Right edge");
        Assert(TrayPositioner.DetectEdge(b, b, 1900,1079) == TaskbarEdge.Bottom, "Auto-hide edge");
    }
    private static async Task CheckViewModelAsync()
    {
        using var settings = new SettingsService(Path.Combine(Root, "vm.json"));
        using var vm = new QRCodeViewModel(Qr, settings, new(), new());
        var input1 = new System.Windows.Controls.TextBox { DataContext = vm };
        var input2 = new System.Windows.Controls.TextBox { DataContext = vm };
        input1.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding("Text") { Mode = System.Windows.Data.BindingMode.TwoWay, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
        input2.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding("Text") { Mode = System.Windows.Data.BindingMode.TwoWay, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
        input1.Text = "从主窗口输入 😀";
        await Task.Delay(30);
        Assert(input2.Text == input1.Text, "View binding not synchronized");
        for (var i = 0; i < 100; i++) vm.Text = "快速输入 " + i;
        await WaitForAsync(() => vm.HasCode);
        Assert(Decode(QRCodeService.Render(vm.Document!, 512)) == "快速输入 99", "Stale generation won");
        input2.Text = "托盘更新";
        await WaitForAsync(() => vm.HasCode);
        Assert(input1.Text == "托盘更新", "Reverse binding not synchronized");
        vm.Correction = "H"; Assert(!vm.HasCode && !vm.CopyCommand.CanExecute(null), "Stale code copy enabled");
        await WaitForAsync(() => vm.HasCode);
        vm.Text = new string('x', 10000);
        await WaitForAsync(() => vm.Status.Contains("内容过长"));
        Assert(!vm.HasCode && !vm.SaveCommand.CanExecute(null), "Error retains code");
        vm.ClearCommand.Execute(null);
        Assert(vm.Text == "" && vm.Status == "等待输入" && !vm.HasCode, "Clear failed");
        Assert(input1.Text == "" && input2.Text == "", "Clear not synchronized");
    }
    private static async Task CheckClipboardAsync()
    {
        var clipboard = new ClipboardService();
        var doc = Qr.Generate("QuickQR clipboard test 中文 😀", "M", "#000000", "#FFFFFF");
        await clipboard.CopyImageAsync(QRCodeService.Render(doc, 512));
        Assert(Clipboard.ContainsImage(), "Clipboard is not a real image");
        Assert(Decode(Clipboard.GetImage() ?? throw new InvalidOperationException("No image")) == "QuickQR clipboard test 中文 😀", "Clipboard image unreadable");
        Clipboard.SetText("QuickQR paste test 中文 😀");
        using var settings = new SettingsService(Path.Combine(Root, "paste.json"));
        using var vm = new QRCodeViewModel(Qr, settings, clipboard, new());
        await vm.PasteAsync(); await WaitForAsync(() => vm.HasCode);
        Assert(vm.Text == "QuickQR paste test 中文 😀", "Paste failed");
        await clipboard.CopyImageAsync(QRCodeService.Render(doc, 512));
        await vm.PasteAsync();
        Assert(vm.Notice.Contains("没有文本"), "No-text feedback missing");
    }
    private static void CheckStartup()
    {
        var keyPath = @"Software\QuickQR\Tests-" + Guid.NewGuid().ToString("N");
        try
        {
            var service = new StartupService(keyPath, "QuickQRTest", @"C:\Test Folder\QuickQR.exe");
            Assert(!service.IsEnabled(), "Unexpected startup state");
            service.SetEnabled(true); Assert(service.IsEnabled(), "Enable failed");
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath))
                Assert(key?.GetValue("QuickQRTest") as string == "\"C:\\Test Folder\\QuickQR.exe\" --login", "Path quoting wrong");
            service.SetEnabled(false); Assert(!service.IsEnabled(), "Disable failed");
        }
        finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(keyPath, false); }
    }
    private static async Task CheckWindowsAsync()
    {
        using var store = new SettingsService(Path.Combine(Root, "windows.json"));
        using var vm = new QRCodeViewModel(Qr, store, new(), new());
        var main = new QuickQR.Views.MainWindow(vm, () => { });
        var panel = new QuickQR.Views.TrayWindow(vm, main.Show, () => { }, () => { });
        using var theme = new ThemeService();
        using var tray = new TrayService(panel, store, vm, main.Show, () => { }, () => { });
        theme.Apply(QuickQR.Models.ThemeMode.Dark);
        main.Show(); await Task.Delay(100);
        Assert(main.Background is SolidColorBrush brush && brush.Color == Color.FromRgb(21,24,31), "Window dark theme failed");
        vm.Text = "共享窗口测试 😀"; await WaitForAsync(() => vm.HasCode);
        Assert(ReferenceEquals(main.DataContext, panel.DataContext), "Independent view models");
        Assert(vm.Foreground == "#000000" && vm.Background == "#FFFFFF", "Theme changed QR colors");
        tray.ShowPanel(false); await Task.Delay(150);
        Assert(panel.IsVisible && !panel.ShowInTaskbar, "Tray panel not visible or has taskbar button");
        tray.ShowPanel(true); Assert(!panel.IsVisible, "Toggle close failed");
        await Task.Delay(350); tray.ShowPanel(false); await Task.Delay(150);
        main.Activate(); await Task.Delay(150);
        Assert(!panel.IsVisible, "Deactivation should hide panel");
        tray.ShowPanel(false); await Task.Delay(150);
        InteractionState.ModalDepth++;
        try { main.Activate(); await Task.Delay(150); Assert(panel.IsVisible, "Modal operation incorrectly hides panel"); }
        finally { InteractionState.ModalDepth--; panel.Hide(); }
        main.Close(); Assert(!main.IsVisible, "Main close should hide window");
        tray.ShowPanel(false); await Task.Delay(150);
        Assert(panel.IsVisible, "Panel cannot reopen after main closes");
        panel.Hide();
        Assert(Application.Current.Windows.OfType<QuickQR.Views.TrayWindow>().Count() == 1, "Duplicate tray windows");
        theme.Apply(QuickQR.Models.ThemeMode.System);
    }
    private static async Task WaitForAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!predicate()) { if (DateTime.UtcNow > deadline) throw new TimeoutException(); await Task.Delay(25); }
    }
    // An isolated demo host for real desktop screenshots. It does not load user content/settings.
    private static async Task PreviewAsync(string mode)
    {
        if (mode is not ("light" or "dark" or "tray")) throw new ArgumentException("Use light, dark or tray.");
        using var store = new SettingsService(Path.Combine(Root, "preview-settings.json"));
        using var vm = new QRCodeViewModel(Qr, store, new(), new());
        using var theme = new ThemeService();
        theme.Apply(mode == "dark" ? QuickQR.Models.ThemeMode.Dark : QuickQR.Models.ThemeMode.Light);
        vm.Text = "https://example.com/QuickQR\n你好，世界！Hello, Windows 👋";
        await WaitForAsync(() => vm.HasCode);
        Window window;
        if (mode == "tray")
        {
            // Keep this isolated demo visible while the capture tool changes foreground windows.
            InteractionState.ModalDepth++;
            var panel = new QuickQR.Views.TrayWindow(vm, () => { }, () => { }, () => ((QuickQR.App)Application.Current).ExitApplication());
            TrayPositioner.ShowNearCursor(panel);
            panel.FocusInput();
            window = panel;
        }
        else
        {
            window = new QuickQR.Views.MainWindow(vm, () => { });
            theme.StyleWindow(window);
            window.Show(); window.Activate();
        }
        Console.WriteLine("PREVIEW_READY " + mode);
        await Task.Delay(TimeSpan.FromMinutes(3));
    }
    private static void RunAsync(Func<Task> test)
    {
        var task = test(); var frame = new DispatcherFrame();
        var dispatcher = Dispatcher.CurrentDispatcher;
        task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
        if (!task.IsCompleted) Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }
}
