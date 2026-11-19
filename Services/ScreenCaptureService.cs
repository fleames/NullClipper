using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Clipper.Native;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using DrawingImaging = System.Drawing.Imaging;
using Forms = System.Windows.Forms;

namespace Clipper.Services;

public static class ScreenCaptureService
{
    // The overlay draws a frozen screenshot stretched across the monitor, so a
    // 1280px cap looks visibly soft on a 1080p display (1.5x upscale) — and the
    // freeze is the one thing a user judges the capture by. 2560 keeps 1080p and
    // 1440p pixel-exact and only softens 4K. Coordinates are mapped against the
    // full-resolution frame either way, so capture output is unaffected.
    public const int OverlayPreviewMaxEdge = 2560;
    public const int ToastPreviewMaxEdge = 240;

    public static Drawing.Bitmap Capture(Forms.Screen screen) => CaptureRect(screen.Bounds);

    public static Drawing.Bitmap CaptureRect(Drawing.Rectangle virtualRect)
    {
        var width = Math.Max(1, virtualRect.Width);
        var height = Math.Max(1, virtualRect.Height);
        var bitmap = new Drawing.Bitmap(width, height, DrawingImaging.PixelFormat.Format24bppRgb);
        using var graphics = Drawing.Graphics.FromImage(bitmap);
        try
        {
            graphics.CopyFromScreen(virtualRect.X, virtualRect.Y, 0, 0, new Drawing.Size(width, height), Drawing.CopyPixelOperation.SourceCopy);
        }
        catch
        {
            graphics.Clear(Drawing.Color.DimGray);
        }

        return bitmap;
    }

    public static Drawing.Bitmap Crop(Drawing.Bitmap source, Int32Rect pixels)
    {
        var width = Math.Max(1, pixels.Width);
        var height = Math.Max(1, pixels.Height);
        var x = Math.Clamp(pixels.X, 0, source.Width - 1);
        var y = Math.Clamp(pixels.Y, 0, source.Height - 1);
        width = Math.Min(width, source.Width - x);
        height = Math.Min(height, source.Height - y);

        var cropped = new Drawing.Bitmap(width, height, DrawingImaging.PixelFormat.Format24bppRgb);
        using var graphics = Drawing.Graphics.FromImage(cropped);
        graphics.DrawImage(
            source,
            new Drawing.Rectangle(0, 0, width, height),
            new Drawing.Rectangle(x, y, width, height),
            Drawing.GraphicsUnit.Pixel);
        return cropped;
    }

    public static BitmapSource ToBitmapSource(Drawing.Bitmap bitmap)
    {
        var handle = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            NativeMethods.DeleteObject(handle);
        }
    }

    public static BitmapSource ToDownscaledBitmapSource(Drawing.Bitmap bitmap, int maxEdge)
    {
        var longEdge = Math.Max(bitmap.Width, bitmap.Height);
        if (longEdge <= maxEdge)
        {
            return ToBitmapSource(bitmap);
        }

        var scale = maxEdge / (double)longEdge;
        var width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        var height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
        using var preview = new Drawing.Bitmap(width, height, DrawingImaging.PixelFormat.Format24bppRgb);
        using (var graphics = Drawing.Graphics.FromImage(preview))
        {
            graphics.InterpolationMode = Drawing2D.InterpolationMode.Low;
            graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighSpeed;
            graphics.CompositingQuality = Drawing2D.CompositingQuality.HighSpeed;
            graphics.DrawImage(bitmap, 0, 0, width, height);
        }

        return ToBitmapSource(preview);
    }

    public static Drawing.Bitmap CropFromScreens(
        IReadOnlyList<(Drawing.Rectangle Bounds, Drawing.Bitmap Freeze)> frames,
        Drawing.Rectangle virtualRect)
    {
        var width = Math.Max(1, virtualRect.Width);
        var height = Math.Max(1, virtualRect.Height);
        var result = new Drawing.Bitmap(width, height, DrawingImaging.PixelFormat.Format24bppRgb);
        using var graphics = Drawing.Graphics.FromImage(result);
        graphics.Clear(Drawing.Color.Black);

        foreach (var (bounds, freeze) in frames)
        {
            var part = Drawing.Rectangle.Intersect(bounds, virtualRect);
            if (part.Width <= 0 || part.Height <= 0)
            {
                continue;
            }

            var source = new Drawing.Rectangle(part.X - bounds.X, part.Y - bounds.Y, part.Width, part.Height);
            var dest = new Drawing.Rectangle(part.X - virtualRect.X, part.Y - virtualRect.Y, part.Width, part.Height);
            graphics.DrawImage(freeze, dest, source, Drawing.GraphicsUnit.Pixel);
        }

        return result;
    }
}
