using System;
using System.Text.Json.Serialization;

namespace QuickQR.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ThemeMode>))]
public enum ThemeMode { System, Light, Dark }
[JsonConverter(typeof(JsonStringEnumConverter<StartupMode>))]
public enum StartupMode { Standard, TrayFirst }

public sealed class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public StartupMode StartupMode { get; set; } = StartupMode.Standard;
    public bool StartAtLogin { get; set; }
    public string ErrorCorrection { get; set; } = "M";
    public string Foreground { get; set; } = "#000000";
    public string Background { get; set; } = "#FFFFFF";
    public int ExportSize { get; set; } = 1024;
    public bool AutoPaste { get; set; }

    public bool IsValid() => Enum.IsDefined(Theme) && Enum.IsDefined(StartupMode)
        && ErrorCorrection is "L" or "M" or "Q" or "H"
        && ExportSize is 512 or 1024 or 2048
        && Services.QRCodeService.IsSafeColorPair(Foreground, Background);
}
