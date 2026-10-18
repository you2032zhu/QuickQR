using System;
using Microsoft.Win32;

namespace QuickQR.Services;

public sealed class StartupService(string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run", string valueName = "QuickQR", string? executablePath = null)
{
    private string Command => "\"" + (executablePath ?? Environment.ProcessPath ?? throw new InvalidOperationException("无法确定应用程序路径。")) + "\" --login";
    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(runKey);
        return string.Equals(key?.GetValue(valueName) as string, Command, StringComparison.OrdinalIgnoreCase);
    }
    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(runKey, true) ?? throw new UnauthorizedAccessException("无法访问当前用户的启动配置。");
        if (enabled) key.SetValue(valueName, Command, RegistryValueKind.String);
        else key.DeleteValue(valueName, false);
        if (IsEnabled() != enabled) throw new InvalidOperationException("启动设置未能写入，请检查当前用户权限。");
    }
}
