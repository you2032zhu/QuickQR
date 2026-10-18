using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace QuickQR.Services;

public sealed class ImageExportService
{
    public bool IsDialogOpen { get; private set; }
    public async Task<bool> SaveAsync(QRDocument document, int size)
    {
        if (IsDialogOpen) return false;
        IsDialogOpen = true;
        try
        {
            var dialog = new SaveFileDialog { FileName = "QRCode.png", DefaultExt = ".png", Filter = "PNG 图片 (*.png)|*.png", AddExtension = true, OverwritePrompt = true, Title = "保存二维码" };
            Window? owner = null;
            foreach (Window window in Application.Current.Windows) if (window.IsActive) { owner = window; break; }
            var result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
            if (result != true) return false;
            await Task.Run(() => SavePng(document, size, dialog.FileName));
            return true;
        }
        finally { IsDialogOpen = false; }
    }
    public static void SavePng(QRDocument document, int size, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(QRCodeService.Render(document, size)));
        // Encode fully before touching the destination. A failed write does not leave a half PNG.
        using var buffer = new MemoryStream();
        encoder.Save(buffer);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp, buffer.ToArray()); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
