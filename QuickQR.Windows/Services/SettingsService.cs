using System;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using QuickQR.Models;

namespace QuickQR.Services;

public sealed class SettingsService : IDisposable
{
    private readonly string _path;
    private readonly DispatcherTimer _timer;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public AppSettings Settings { get; private set; } = new();
    public string? LoadWarning { get; private set; }
    public event Action<string>? SaveFailed;

    public SettingsService(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickQR", "settings.json");
        _timer = new() { Interval = TimeSpan.FromMilliseconds(350) };
        _timer.Tick += OnTick;
        try
        {
            if (File.Exists(_path))
            {
                Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions) ?? throw new JsonException();
                if (!Settings.IsValid()) throw new JsonException("设置值无效。");
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Settings = new();
            LoadWarning = "设置文件损坏或无法读取，已使用默认设置。";
            try { if (File.Exists(_path)) File.Copy(_path, _path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); }
            catch (Exception backupError) when (backupError is IOException or UnauthorizedAccessException) { }
        }
    }
    public void ScheduleSave() { _timer.Stop(); _timer.Start(); }
    private void OnTick(object? sender, EventArgs e) { _timer.Stop(); Save(); }
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(Settings, JsonOptions));
            File.Move(_path + ".tmp", _path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { SaveFailed?.Invoke("设置保存失败：" + ex.Message); }
    }
    public void Dispose() { _timer.Stop(); _timer.Tick -= OnTick; Save(); }
}
