using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using DrawingImaging = System.Drawing.Imaging;

namespace Clipper.Services;

/// <summary>
/// Removes what should not be shared. Uses a pixelate (mosaic) pass rather than a
/// gaussian blur: blur is a convolution, and a short radius can be partially inverted
/// or read through, so it is the wrong tool for hiding a key, a name or an email.
/// Mosaic throws the pixels away inside each block and keeps nothing recoverable.
/// </summary>
public static class ImageRedaction
{
    /// <summary>Block edge in pixels at 1:1 — roughly a letter of 11 px text.</summary>
    public const int DefaultBlockSize = 12;

    public static void Pixelate(Drawing.Bitmap bitmap, IEnumerable<Drawing.Rectangle> areas, int blockSize = DefaultBlockSize)
    {
        var bounds = new Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
        foreach (var area in areas)
        {
            var target = Drawing.Rectangle.Intersect(bounds, area);
            if (target.Width < 2 || target.Height < 2)
            {
                continue;
            }

            // Shrink to one pixel per block with averaging (Low), then put it back with
            // NearestNeighbor so each block is a flat colour rather than a soft gradient.
            var smallWidth = Math.Max(1, target.Width / blockSize);
            var smallHeight = Math.Max(1, target.Height / blockSize);

            using var small = new Drawing.Bitmap(smallWidth, smallHeight, DrawingImaging.PixelFormat.Format24bppRgb);
            using (var shrink = Drawing.Graphics.FromImage(small))
            {
                shrink.InterpolationMode = Drawing2D.InterpolationMode.Low;
                shrink.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighSpeed;
                shrink.CompositingQuality = Drawing2D.CompositingQuality.HighSpeed;
                shrink.DrawImage(
                    bitmap,
                    new Drawing.Rectangle(0, 0, smallWidth, smallHeight),
                    target,
                    Drawing.GraphicsUnit.Pixel);
            }

            using var restore = Drawing.Graphics.FromImage(bitmap);
            restore.InterpolationMode = Drawing2D.InterpolationMode.NearestNeighbor;
            restore.PixelOffsetMode = Drawing2D.PixelOffsetMode.Half;
            restore.SmoothingMode = Drawing2D.SmoothingMode.None;
            restore.CompositingMode = Drawing2D.CompositingMode.SourceCopy;
            restore.DrawImage(
                small,
                target,
                new Drawing.Rectangle(0, 0, smallWidth, smallHeight),
                Drawing.GraphicsUnit.Pixel);
        }
    }
}
