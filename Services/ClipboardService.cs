using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using DrawingImaging = System.Drawing.Imaging;
using Forms = System.Windows.Forms;

namespace Clipper.Services;

public static class ClipboardService
{
    [DllImport("ole32.dll")]
    private static extern int OleFlushClipboard();

    public static void CopyImage(Drawing.Bitmap source)
    {
        var pngBytes = EncodePng(source);
        using var dib = To24Bpp(source);
        using var pngStream = new MemoryStream(pngBytes);

        var data = new Forms.DataObject();
        data.SetImage(dib);
        data.SetData("PNG", false, pngStream);

        Exception? last = null;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Forms.Clipboard.SetDataObject(data, true, 10, 100);
                OleFlushClipboard();
                if (Forms.Clipboard.ContainsImage() || Forms.Clipboard.ContainsData("PNG"))
                {
                    return;
                }
            }
            catch (ExternalException ex)
            {
                last = ex;
                Thread.Sleep(50);
            }
        }

        throw last ?? new InvalidOperationException("The snip could not be copied to the clipboard.");
    }

    public static void CopyGif(byte[] gifBytes, Drawing.Bitmap previewFrame)
    {
        using var dib = To24Bpp(previewFrame);

        var data = new Forms.DataObject();
        data.SetImage(dib);
        data.SetData("GIF", false, new MemoryStream(gifBytes));
        data.SetData("image/gif", false, new MemoryStream(gifBytes));

        Exception? last = null;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Forms.Clipboard.SetDataObject(data, true, 10, 100);
                OleFlushClipboard();
                if (Forms.Clipboard.ContainsImage()
                    || Forms.Clipboard.ContainsData("GIF")
                    || Forms.Clipboard.ContainsData("image/gif"))
                {
                    return;
                }
            }
            catch (ExternalException ex)
            {
                last = ex;
                Thread.Sleep(50);
            }
        }

        throw last ?? new InvalidOperationException("The GIF could not be copied to the clipboard.");
    }

    public static BitmapSource Preview(Drawing.Bitmap bitmap) =>
        ScreenCaptureService.ToDownscaledBitmapSource(bitmap, ScreenCaptureService.ToastPreviewMaxEdge);

    public static void CopyText(string text)
    {
        Exception? last = null;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Forms.Clipboard.SetText(text);
                OleFlushClipboard();
                if (Forms.Clipboard.ContainsText() && Forms.Clipboard.GetText() == text)
                {
                    return;
                }
            }
            catch (ExternalException ex)
            {
                last = ex;
                Thread.Sleep(50);
            }
        }

        throw last ?? new InvalidOperationException("The link could not be copied to the clipboard.");
    }

    private static byte[] EncodePng(Drawing.Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, DrawingImaging.ImageFormat.Png);
        return stream.ToArray();
    }

    private static Drawing.Bitmap To24Bpp(Drawing.Bitmap source)
    {
        if (source.PixelFormat == DrawingImaging.PixelFormat.Format24bppRgb)
        {
            return (Drawing.Bitmap)source.Clone();
        }

        var copy = new Drawing.Bitmap(source.Width, source.Height, DrawingImaging.PixelFormat.Format24bppRgb);
        using var graphics = Drawing.Graphics.FromImage(copy);
        graphics.Clear(Drawing.Color.White);
        graphics.DrawImage(source, 0, 0, source.Width, source.Height);
        return copy;
    }
}
