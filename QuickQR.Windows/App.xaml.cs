using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using QuickQR.Models;
using QuickQR.Services;
using QuickQR.ViewModels;
using QuickQR.Views;

namespace QuickQR;

public partial class App : Application
{
    private readonly bool _initializeServices;
    public App() : this(true) { }
    // Allows the integration host to load the real WPF resources without a second app instance.
    public App(bool initializeServices) => _initializeServices = initializeServices;
    private Mutex? _mutex;
    private bool _ownsMutex;
    private EventWaitHandle? _activation;
    private RegisteredWaitHandle? _activationWait;
    private SettingsService? _settings;
    private ThemeService? _theme;
    private QRCodeViewModel? _qr;
    private SettingsViewModel? _settingsVm;
    private MainWindow? _main;
    private TrayWindow? _panel;
    private SettingsWindow? _settingsWindow;
    private TrayService? _tray;
    public bool IsExiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!_initializeServices) return;
        base.OnStartup(e);
        _mutex = new Mutex(true, @"Local\QuickQR.Windows.1.Instance", out _ownsMutex);
        _activation = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\QuickQR.Windows.1.Activate");
        if (!_ownsMutex) { _activation.Set(); Shutdown(); return; }
        DispatcherUnhandledException += OnUnhandledException;
        try
        {
            _settings = new SettingsService();
            _theme = new ThemeService();
            _theme.Apply(_settings.Settings.Theme);
            _qr = new QRCodeViewModel(new(), _settings, new(), new());
            _settings.SaveFailed += _qr.Notify;
            _settingsVm = new SettingsViewModel(_settings, _theme, new());
            _main = new MainWindow(_qr, ShowSettings);
            _theme.StyleWindow(_main);
            MainWindow = _main;
            _panel = new TrayWindow(_qr, ShowMain, ShowSettings, ExitApplication);
            _tray = new TrayService(_panel, _settings, _qr, ShowMain, ShowSettings, ExitApplication);
            _activationWait = ThreadPool.RegisterWaitForSingleObject(_activation, (_, _) =>
            { if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(ShowMain); }, null, Timeout.Infinite, false);
            if (_settings.Settings.StartupMode == StartupMode.Standard) ShowMain();
            if (_settings.LoadWarning is { } warning)
            {
                _qr.Notify(warning);
                _tray.ShowPanel(false);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("QuickQR 无法启动：" + ex.Message, "QuickQR", MessageBoxButton.OK, MessageBoxImage.Error);
            ExitApplication();
        }
    }
    public void ShowMain()
    {
        if (IsExiting || _main is null) return;
        _panel?.Hide(); _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }
    public void ShowSettings()
    {
        if (IsExiting || _settingsVm is null) return;
        _panel?.Hide(); _settingsVm.RefreshStartup();
        if (_settingsWindow is null) { _settingsWindow = new SettingsWindow(_settingsVm); _theme?.StyleWindow(_settingsWindow); }
        _settingsWindow.Show();
        if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
    }
    public void ExitApplication()
    {
        if (IsExiting) return;
        IsExiting = true;
        _tray?.Dispose();
        foreach (Window window in Windows.Cast<Window>().ToArray()) window.Close();
        Shutdown();
    }
    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Report UI failures without persisting content or clipboard data to logs.
        e.Handled = true;
        if (_qr is not null) _qr.Notify("操作失败：" + e.Exception.Message);
        else { MessageBox.Show("操作失败：" + e.Exception.Message, "QuickQR"); ExitApplication(); }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        IsExiting = true;
        _activationWait?.Unregister(null);
        _tray?.Dispose(); _qr?.Dispose(); _theme?.Dispose(); _settingsVm?.Dispose();
        if (_settings is not null) { if (_qr is not null) _settings.SaveFailed -= _qr.Notify; _settings.Dispose(); }
        _activation?.Dispose();
        if (_ownsMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        DispatcherUnhandledException -= OnUnhandledException;
        base.OnExit(e);
    }
}
