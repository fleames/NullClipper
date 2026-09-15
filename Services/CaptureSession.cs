using Drawing = System.Drawing;

namespace Clipper.Services;

public enum SnipMode
{
    Rectangle,
    Freeform,
    Window
}

public sealed class CaptureSession
{
    public SnipMode Mode { get; private set; } = SnipMode.Rectangle;

    public event Action<SnipMode>? ModeChanged;

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
}
