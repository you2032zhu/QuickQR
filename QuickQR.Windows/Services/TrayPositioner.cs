using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Forms;

namespace QuickQR.Services;

public enum TaskbarEdge { Bottom, Top, Left, Right }
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

public static class TrayPositioner
{
    public static TaskbarEdge DetectEdge(PixelRect bounds, PixelRect work, int x, int y)
    {
        if (work.Bottom < bounds.Bottom) return TaskbarEdge.Bottom;
        if (work.Top > bounds.Top) return TaskbarEdge.Top;
        if (work.Left > bounds.Left) return TaskbarEdge.Left;
        if (work.Right < bounds.Right) return TaskbarEdge.Right;
        // Auto-hidden taskbars do not shrink the working area. Use the triggering cursor edge.
        var distances = new[] { bounds.Bottom - y, y - bounds.Top, x - bounds.Left, bounds.Right - x };
        var best = 0;
        for (var i = 1; i < distances.Length; i++) if (distances[i] < distances[best]) best = i;
        return (TaskbarEdge)best;
    }
    public static (int X, int Y) Calculate(PixelRect work, TaskbarEdge edge, int anchorX, int anchorY, int width, int height, int gap)
    {
        var x = anchorX - width / 2;
        var y = anchorY - height / 2;
        switch (edge)
        {
            case TaskbarEdge.Bottom: y = work.Bottom - height - gap; break;
            case TaskbarEdge.Top: y = work.Top + gap; break;
            case TaskbarEdge.Left: x = work.Left + gap; break;
            case TaskbarEdge.Right: x = work.Right - width - gap; break;
        }
        return (Math.Clamp(x, work.Left + gap, Math.Max(work.Left + gap, work.Right - width - gap)),
                Math.Clamp(y, work.Top + gap, Math.Max(work.Top + gap, work.Bottom - height - gap)));
    }
    public static void ShowNearCursor(Window window)
    {
        var point = Cursor.Position;
        var screen = Screen.FromPoint(point);
        var bounds = new PixelRect(screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Right, screen.Bounds.Bottom);
        var work = new PixelRect(screen.WorkingArea.Left, screen.WorkingArea.Top, screen.WorkingArea.Right, screen.WorkingArea.Bottom);
        var scale = 1d;
        var monitor = MonitorFromPoint(new POINT(point.X, point.Y), 2);
        if (GetDpiForMonitor(monitor, 0, out var dx, out _) == 0) scale = dx / 96d;
        var gap = (int)Math.Ceiling(10 * scale);
        window.MaxHeight = (work.Height - 2 * gap) / scale;
        window.MaxWidth = (work.Width - 2 * gap) / scale;
        // Move the hidden HWND to the target monitor before measuring, so WPF adopts its DPI.
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        SetWindowPos(hwnd, IntPtr.Zero, work.Left + gap, work.Top + gap, 0, 0, 0x15);
        window.Show();
        window.UpdateLayout();
        var width = (int)Math.Ceiling(window.ActualWidth * scale);
        var height = (int)Math.Ceiling(window.ActualHeight * scale);
        var position = Calculate(work, DetectEdge(bounds, work, point.X, point.Y), point.X, point.Y, width, height, gap);
        SetWindowPos(hwnd, IntPtr.Zero, position.X, position.Y, width, height, 0x14);
    }
    [StructLayout(LayoutKind.Sequential)] private readonly struct POINT(int x, int y) { public readonly int X = x; public readonly int Y = y; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
