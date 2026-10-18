using System;
using System.Windows;
using System.Windows.Media;
using QuickQR.Services;

namespace QuickQR.Views;

// Draw at the current monitor's physical pixel resolution, including at fractional DPI scales.
public sealed class QRPreview : FrameworkElement
{
    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(nameof(Document), typeof(QRDocument), typeof(QRPreview), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public QRDocument? Document { get => (QRDocument?)GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }
    public QRPreview() { SnapsToDevicePixels = true; RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Brushes.White, null, new Rect(RenderSize));
        if (Document is not { } document) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        var size = (int)Math.Floor(Math.Min(ActualWidth * dpi.DpiScaleX, ActualHeight * dpi.DpiScaleY));
        if (size < document.Modules.Width) return;
        var bitmap = QRCodeService.Render(document, Math.Min(4096, size));
        var origin = PointToScreen(new Point(0, 0));
        var x = (Math.Round(origin.X + (ActualWidth * dpi.DpiScaleX - size) / 2) - origin.X) / dpi.DpiScaleX;
        var y = (Math.Round(origin.Y + (ActualHeight * dpi.DpiScaleY - size) / 2) - origin.Y) / dpi.DpiScaleY;
        dc.DrawImage(bitmap, new Rect(x, y, size / dpi.DpiScaleX, size / dpi.DpiScaleY));
    }
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) { base.OnDpiChanged(oldDpi, newDpi); InvalidateVisual(); }
    protected override void OnRenderSizeChanged(SizeChangedInfo info) { base.OnRenderSizeChanged(info); InvalidateVisual(); }
}
