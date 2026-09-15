using System.Diagnostics;
using System.IO;
using AnimatedGif;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using DrawingImaging = System.Drawing.Imaging;

namespace Clipper.Services;

public static class GifLimits
{
    public const int Fps = 12;
    public const int MaxDurationSeconds = 8;
    public const int MaxLongEdge = 640;
}

public sealed record GifCaptureResult(byte[] Bytes, Drawing.Bitmap Preview, int Width, int Height, int FrameCount);

public static class GifCaptureService
{
    public static async Task<GifCaptureResult?> RecordAsync(
        Drawing.Rectangle region,
        Action<TimeSpan>? onElapsed,
        Func<Task>? beforeFrame,
        Func<Task>? afterFrame,
        Action? onEncoding,
        CancellationToken cancellationToken)
    {
        var frames = new List<Drawing.Bitmap>();
        var interval = TimeSpan.FromMilliseconds(1000.0 / GifLimits.Fps);
        var max = TimeSpan.FromSeconds(GifLimits.MaxDurationSeconds);
        var clock = Stopwatch.StartNew();

        try
        {
            while (clock.Elapsed < max)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var frameClock = Stopwatch.StartNew();

                if (beforeFrame is not null)
                {
                    await beforeFrame().ConfigureAwait(true);
                }

                Drawing.Bitmap? raw = null;
                try
                {
                    raw = ScreenCaptureService.CaptureRect(region);
                    frames.Add(ScaleIfNeeded(raw));
                    raw = null;
                }
                finally
                {
                    raw?.Dispose();
                    if (afterFrame is not null)
                    {
                        await afterFrame().ConfigureAwait(true);
                    }
                }

                onElapsed?.Invoke(clock.Elapsed);

                var wait = interval - frameClock.Elapsed;
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, cancellationToken).ConfigureAwait(true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stop / Esc — encode whatever we have.
        }

        onElapsed?.Invoke(clock.Elapsed);
        if (frames.Count == 0)
        {
            return null;
        }

        onEncoding?.Invoke();
        byte[] bytes;
        try
        {
            bytes = await Task.Run(() => Encode(frames), CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            // Preview is cloned before dispose below.
        }

        var frameCount = frames.Count;
        var preview = (Drawing.Bitmap)frames[0].Clone();
        foreach (var frame in frames)
        {
            frame.Dispose();
        }

        return new GifCaptureResult(bytes, preview, preview.Width, preview.Height, frameCount);
    }

    private static Drawing.Bitmap ScaleIfNeeded(Drawing.Bitmap source)
    {
        var longEdge = Math.Max(source.Width, source.Height);
        var width = source.Width;
        var height = source.Height;
        if (longEdge > GifLimits.MaxLongEdge)
        {
            var scale = GifLimits.MaxLongEdge / (double)longEdge;
            width = Math.Max(2, (int)Math.Round(source.Width * scale));
            height = Math.Max(2, (int)Math.Round(source.Height * scale));
        }

        width &= ~1;
        height &= ~1;
        width = Math.Max(2, width);
        height = Math.Max(2, height);

        if (width == source.Width && height == source.Height)
        {
            return source;
        }

        var scaled = new Drawing.Bitmap(width, height, DrawingImaging.PixelFormat.Format32bppArgb);
        using (var graphics = Drawing.Graphics.FromImage(scaled))
        {
            graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality;
            graphics.DrawImage(source, 0, 0, width, height);
        }

        source.Dispose();
        return scaled;
    }

    private static byte[] Encode(IReadOnlyList<Drawing.Bitmap> frames)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nullclipper-{Guid.NewGuid():N}.gif");
        try
        {
            var delay = (int)Math.Round(1000.0 / GifLimits.Fps);
            using (var gif = new AnimatedGifCreator(path, delay))
            {
                foreach (var frame in frames)
                {
                    gif.AddFrame(frame, delay, GifQuality.Bit8);
                }
            }

            return File.ReadAllBytes(path);
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Temp cleanup is best-effort.
            }
        }
    }
}
