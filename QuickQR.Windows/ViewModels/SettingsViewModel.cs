using System;
using QuickQR.Infrastructure;
using QuickQR.Models;
using QuickQR.Services;

namespace QuickQR.ViewModels;

public sealed record Choice<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

public sealed class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly SettingsService _store;
    private readonly ThemeService _theme;
    private readonly StartupService _startup;
    private string _message = "";
    public Choice<ThemeMode>[] Themes { get; } = [new(ThemeMode.System, "跟随 Windows"), new(ThemeMode.Light, "浅色"), new(ThemeMode.Dark, "深色")];
    public Choice<StartupMode>[] StartupModes { get; } = [new(StartupMode.Standard, "标准模式 · 显示主窗口"), new(StartupMode.TrayFirst, "托盘优先 · 后台驻留")];
    public string Message { get => _message; private set => Set(ref _message, value); }
    public ThemeMode Theme
    {
        get => _store.Settings.Theme;
        set { if (!Enum.IsDefined(value) || value == Theme) return; _store.Settings.Theme = value; _theme.Apply(value); Raise(); _store.ScheduleSave(); }
    }
    public StartupMode StartupMode
    {
        get => _store.Settings.StartupMode;
        set { if (!Enum.IsDefined(value) || value == StartupMode) return; _store.Settings.StartupMode = value; Raise(); _store.ScheduleSave(); }
    }
    public bool AutoPaste
    {
        get => _store.Settings.AutoPaste;
        set { if (value == AutoPaste) return; _store.Settings.AutoPaste = value; Raise(); _store.ScheduleSave(); }
    }
    public bool StartAtLogin
    {
        get => _store.Settings.StartAtLogin;
        set
        {
            if (value == StartAtLogin) return;
            try { _startup.SetEnabled(value); _store.Settings.StartAtLogin = _startup.IsEnabled(); Message = "启动设置已更新"; }
            catch (Exception ex)
            {
                Message = "无法修改启动项：" + ex.Message;
                try { _store.Settings.StartAtLogin = _startup.IsEnabled(); } catch (Exception) { }
            }
            Raise(); _store.ScheduleSave();
        }
    }
    public SettingsViewModel(SettingsService store, ThemeService theme, StartupService startup)
    { _store = store; _theme = theme; _startup = startup; _store.SaveFailed += OnSaveFailed; Message = store.LoadWarning ?? ""; RefreshStartup(); }
    private void OnSaveFailed(string message) => Message = message;
    public void Dispose() => _store.SaveFailed -= OnSaveFailed;
    public void RefreshStartup()
    {
        try { _store.Settings.StartAtLogin = _startup.IsEnabled(); Raise(nameof(StartAtLogin)); _store.ScheduleSave(); }
        catch (Exception ex) { Message = "无法读取启动设置：" + ex.Message; }
    }
}
