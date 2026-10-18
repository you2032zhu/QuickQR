using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace QuickQR.Services;

public sealed class ClipboardService
{
    // Await preserves the WPF dispatcher / STA context between retries.
    private static async Task<T> RetryAsync<T>(Func<T> operation)
    {
        Application.Current.Dispatcher.VerifyAccess();
        for (var attempt = 0; ; attempt++)
        {
            try { return operation(); }
            catch (ExternalException) when (attempt < 5) { await Task.Delay(60 * (attempt + 1)); }
        }
    }
    public Task CopyImageAsync(BitmapSource bitmap) => RetryAsync(() =>
    {
        // WPF provides CF_DIB for Office and messaging clients. PNG is an additional lossless format.
        var data = new DataObject();
        data.SetImage(bitmap);
        using var stream = new System.IO.MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
        data.SetData("PNG", new System.IO.MemoryStream(stream.ToArray()));
        Clipboard.SetDataObject(data, true);
        return true;
    });
    public Task<string?> GetTextAsync() => RetryAsync(() => Clipboard.ContainsText() ? Clipboard.GetText(TextDataFormat.UnicodeText) : null);
}
