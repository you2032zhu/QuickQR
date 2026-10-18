using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using QuickQR.ViewModels;

namespace QuickQR.Views;

public partial class MainWindow : Window
{
    private readonly Action _showSettings;
    public MainWindow(QRCodeViewModel viewModel, Action showSettings)
    {
        InitializeComponent(); DataContext = viewModel; _showSettings = showSettings;
        InputBindings.Add(new KeyBinding(new Infrastructure.RelayCommand(() => InputBox.Focus()), Key.L, ModifierKeys.Control));
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!((App)Application.Current).IsExiting) { e.Cancel = true; Hide(); }
        base.OnClosing(e);
    }
    private void Settings_Click(object sender, RoutedEventArgs e) => _showSettings();
}
