using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;
using FormatException = System.FormatException;

namespace QuickQR.Services;

// No text is retained in the document. The matrix already contains a four-module quiet zone.
public sealed class QRDocument(BitMatrix modules, Color foreground, Color background)
{
    public BitMatrix Modules { get; } = modules;
    public Color Foreground { get; } = foreground;
    public Color Background { get; } = background;
}

public sealed class QRCodeService
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public QRDocument Generate(string text, string correction, string foreground, string background)
    {
        if (string.IsNullOrEmpty(text)) throw new ArgumentException("请输入文本。");
        // The most compact possible QR (numeric, version 40-L) stores 7089 digits.
        // Let ZXing decide the actual capacity for each mode and correction level.
        if (StrictUtf8.GetByteCount(text) > 7089) throw new WriterException("内容超过二维码容量。");
        if (!IsSafeColorPair(foreground, background)) throw new ArgumentException("请选择深色前景和浅色背景，对比度至少为 4.5:1。");
        var level = correction switch { "L" => ErrorCorrectionLevel.L, "Q" => ErrorCorrectionLevel.Q, "H" => ErrorCorrectionLevel.H, _ => ErrorCorrectionLevel.M };
        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.CHARACTER_SET] = "UTF-8",
            [EncodeHintType.ERROR_CORRECTION] = level,
            [EncodeHintType.MARGIN] = 4,
            [EncodeHintType.DISABLE_ECI] = false
        };
        var matrix = new QRCodeWriter().encode(text, BarcodeFormat.QR_CODE, 0, 0, hints);
        return new(matrix, ParseColor(foreground), ParseColor(background));
    }

    public static Color ParseColor(string value)
    {
        if (value.Length != 7 || value[0] != '#') throw new FormatException("颜色必须为 #RRGGBB。");
        return (Color)ColorConverter.ConvertFromString(value);
    }

    public static bool IsSafeColorPair(string? foreground, string? background)
    {
        try
        {
            if (foreground is null || background is null) return false;
            var f = Luminance(ParseColor(foreground));
            var b = Luminance(ParseColor(background));
            return b > f && (b + .05) / (f + .05) >= 4.5;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or NotSupportedException) { return false; }
    }
    private static double Luminance(Color c)
    {
        static double Linear(byte v) { var d = v / 255d; return d <= .04045 ? d / 12.92 : Math.Pow((d + .055) / 1.055, 2.4); }
        return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
    }

    // Draw from modules at target resolution, never resize a preview or interpolate.
    public static BitmapSource Render(QRDocument document, int size)
    {
        var n = document.Modules.Width;
        if (size < n || size > 4096) throw new ArgumentOutOfRangeException(nameof(size));
        var scale = size / n;
        var offset = (size - n * scale) / 2;
        var pixels = new byte[checked(size * size * 4)];
        var bg = document.Background;
        for (var i = 0; i < pixels.Length; i += 4) { pixels[i] = bg.B; pixels[i + 1] = bg.G; pixels[i + 2] = bg.R; pixels[i + 3] = 255; }
        var fg = document.Foreground;
        for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
                if (document.Modules[x, y])
                    for (var dy = 0; dy < scale; dy++)
                        for (var dx = 0; dx < scale; dx++)
                        {
                            var i = ((offset + y * scale + dy) * size + offset + x * scale + dx) * 4;
                            pixels[i] = fg.B; pixels[i + 1] = fg.G; pixels[i + 2] = fg.R;
                        }
        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
        bitmap.Freeze();
        return bitmap;
    }
}
