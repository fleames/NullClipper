using Drawing = System.Drawing;

namespace Clipper.Services;

public enum SnipMode
{
    Rectangle,
    Freeform,
    Window
}

public enum CaptureKind
{
    Snip,
    Gif
}

public sealed class CaptureSession
{
    public SnipMode Mode { get; private set; } = SnipMode.Rectangle;

    public CaptureKind Kind { get; private set; } = CaptureKind.Snip;

    public event Action<SnipMode>? ModeChanged;

    public event Action<CaptureKind>? KindChanged;

    public List<(Drawing.Rectangle Bounds, Drawing.Bitmap Freeze)> Frames { get; } = [];

    public List<Drawing.Rectangle> Windows { get; } = [];

    public void SetMode(SnipMode mode)
    {
        if (Mode == mode)
        {
            return;
        }

        Mode = mode;
        ModeChanged?.Invoke(mode);
    }

    public void SetKind(CaptureKind kind)
    {
        if (Kind == kind)
        {
            return;
        }

        Kind = kind;
        if (kind == CaptureKind.Gif && Mode == SnipMode.Freeform)
        {
            SetMode(SnipMode.Rectangle);
        }

        KindChanged?.Invoke(kind);
    }
}
