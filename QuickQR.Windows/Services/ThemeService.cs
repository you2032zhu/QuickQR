using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using QuickQR.Models;
using ThemeMode = QuickQR.Models.ThemeMode;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace QuickQR.Services;

public sealed class ThemeService : IDisposable
{
    private ThemeMode _mode;
    public ThemeService() => SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
    private void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (Application.Current is { } app && !app.Dispatcher.HasShutdownStarted)
            app.Dispatcher.BeginInvoke(() => Apply(_mode));
    }
    public void Apply(ThemeMode mode)
    {
        _mode = mode;
        var dark = mode == ThemeMode.Dark;
        if (mode == ThemeMode.System)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                dark = key?.GetValue("AppsUseLightTheme") is int value && value == 0;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException) { }
        }
        void Put(string name, string value) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); brush.Freeze(); Application.Current.Resources[name] = brush; }
        Put("WindowBrush", dark ? "#15181F" : "#F4F6FA");
        Put("SurfaceBrush", dark ? "#20242E" : "#FFFFFF");
        Put("InputBrush", dark ? "#181C24" : "#FAFBFD");
        Put("TextBrush", dark ? "#F2F4FA" : "#1C2333");
        Put("MutedBrush", dark ? "#A8B1C4" : "#626D82");
        Put("BorderBrush", dark ? "#363D4D" : "#E0E5EE");
        Put("HoverBrush", dark ? "#30394C" : "#EAF0FF");
        Put("AccentBrush", dark ? "#91ACFF" : "#365EDC");
        Put("AccentFillBrush", dark ? "#466BDC" : "#365EDC");
        foreach (Window window in Application.Current.Windows) ApplyTitleBar(window, dark);
    }
    public void StyleWindow(Window window)
    {
        window.SourceInitialized += (_, _) => Apply(_mode);
    }
    private static void ApplyTitleBar(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var value = dark ? 1 : 0;
        DwmSetWindowAttribute(handle, 20, ref value, sizeof(int));
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
}
