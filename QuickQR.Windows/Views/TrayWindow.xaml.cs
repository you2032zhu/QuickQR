using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using QuickQR.Infrastructure;
using QuickQR.ViewModels;

namespace QuickQR.Views;

public partial class TrayWindow : Window
{
    private readonly Action _showMain;
    private readonly Action _showSettings;
    private readonly Action _exit;
    public DateTime LastDeactivatedUtc { get; private set; }
    public TrayWindow(QRCodeViewModel viewModel, Action showMain, Action showSettings, Action exit)
    {
        InitializeComponent(); DataContext = viewModel; _showMain = showMain; _showSettings = showSettings; _exit = exit;
        InputBindings.Add(new KeyBinding(new RelayCommand(() => InputBox.Focus()), Key.L, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(Hide), Key.Escape, ModifierKeys.None));
        Deactivated += (_, _) =>
        {
            if (InteractionState.ModalDepth > 0) return;
            LastDeactivatedUtc = DateTime.UtcNow;
            Hide();
        };
        InteractionState.ModalEnded += OnModalEnded;
    }
    private void OnModalEnded() => Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { if (IsVisible && !IsActive && InteractionState.ModalDepth == 0) Hide(); }));
    protected override void OnClosed(EventArgs e) { InteractionState.ModalEnded -= OnModalEnded; base.OnClosed(e); }
    public void FocusInput() => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => { if (IsVisible) { Activate(); InputBox.Focus(); } }));
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!((App)Application.Current).IsExiting) { e.Cancel = true; Hide(); }
        base.OnClosing(e);
    }
    private void Settings_Click(object sender, RoutedEventArgs e) { Hide(); _showSettings(); }
    private void OpenMain_Click(object sender, RoutedEventArgs e) { Hide(); _showMain(); }
    private void Exit_Click(object sender, RoutedEventArgs e) => _exit();
}
