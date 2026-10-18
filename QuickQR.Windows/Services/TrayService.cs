using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using QuickQR.ViewModels;
using QuickQR.Views;

namespace QuickQR.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Icon _ownedIcon;
    private readonly ContextMenuStrip _menu;
    private readonly TrayWindow _panel;
    private readonly SettingsService _settings;
    private readonly QRCodeViewModel _viewModel;
    private readonly Dispatcher _dispatcher;
    private bool _disposed;

    public TrayService(TrayWindow panel, SettingsService settings, QRCodeViewModel viewModel, Action showMain, Action showSettings, Action exit)
    {
        _panel = panel; _settings = settings; _viewModel = viewModel; _dispatcher = System.Windows.Application.Current.Dispatcher;
        using var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/QuickQR;component/Resources/app.ico"))!.Stream;
        // A real 64px frame remains sharp on 100–400% notification areas. Explorer downsamples it.
        _ownedIcon = new Icon(stream, new System.Drawing.Size(64, 64));
        _menu = new ContextMenuStrip { ShowImageMargin = false };
        _menu.Items.Add("打开 QuickQR", null, (_, _) => Dispatch(showMain));
        _menu.Items.Add("显示二维码面板", null, (_, _) => Dispatch(() => ShowPanel(false)));
        _menu.Items.Add("设置", null, (_, _) => Dispatch(showSettings));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("退出 QuickQR", null, (_, _) => Dispatch(exit));
        _icon = new NotifyIcon { Text = "QuickQR · 即时生成二维码", Icon = _ownedIcon, ContextMenuStrip = _menu, Visible = true };
        _icon.MouseClick += OnClick;
        // NotifyIcon's native window handles TaskbarCreated, automatically restoring the icon
        // after Explorer restarts. Keep this one instance alive for the entire app lifetime.
    }
    private void Dispatch(Action action) { if (!_disposed) _dispatcher.BeginInvoke(action); }
    private void OnClick(object? sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Dispatch(() => ShowPanel(true)); }
    public async void ShowPanel(bool toggle)
    {
        if (_disposed || InteractionState.ModalDepth > 0) return;
        // Clicking the notification icon can deactivate the panel before MouseClick arrives.
        if (toggle && (_panel.IsVisible || DateTime.UtcNow - _panel.LastDeactivatedUtc < TimeSpan.FromMilliseconds(300))) { _panel.Hide(); return; }
        try
        {
            TrayPositioner.ShowNearCursor(_panel);
            _panel.FocusInput();
            if (_settings.Settings.AutoPaste) await _viewModel.PasteAsync();
        }
        catch (Exception ex) { _viewModel.Notify("无法打开快捷面板：" + ex.Message); }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _icon.Visible = false; _icon.MouseClick -= OnClick; _icon.Dispose(); _menu.Dispose(); _ownedIcon.Dispose();
    }
}
