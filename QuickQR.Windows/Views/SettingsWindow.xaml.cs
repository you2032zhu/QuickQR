using System.ComponentModel;
using System.Windows;
using QuickQR.ViewModels;

namespace QuickQR.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel) { InitializeComponent(); DataContext = viewModel; }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!((App)Application.Current).IsExiting) { e.Cancel = true; Hide(); }
        base.OnClosing(e);
    }
}
