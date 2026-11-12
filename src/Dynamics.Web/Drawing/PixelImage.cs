using Dynamics.Core.General;
using Dynamics.Core.Julia;
using Microsoft.JSInterop;

namespace Dynamics.Web.Drawing;

/// <summary>
/// RGBA picture of a square diagram (VB: the bitmap of the Julia and Mandelbrot sets).
/// Pixels use the VB coordinates 1..size; only the changed rectangle is sent to the canvas.
/// </summary>
public sealed class PixelImage(int size)
{
    private readonly byte[] _rgba = new byte[size * size * 4];
    private int _minX = int.MaxValue;
    private int _minY = int.MaxValue;
    private int _maxX = -1;
    private int _maxY = -1;

    public void Clear(int argb = Argb.White)
    {
        for (var i = 0; i < size * size; i++)
        {
            Write(i * 4, argb);
        }

        MarkDirty(0, 0);
        MarkDirty(size - 1, size - 1);
    }

    /// <summary>Sets the VB pixel (p, q) at its bitmap position (see SpiralScan); pixels outside are ignored.</summary>
    public void Set(int p, int q, int argb)
    {
        var (x, y) = SpiralScan.BitmapPosition(p, q);
        if (x < 0 || y < 0 || x >= size || y >= size)
        {
            return;
        }

        Write((y * size + x) * 4, argb);
        MarkDirty(x, y);
    }

    /// <summary>VB DrawCoordinateSystem: axes through 0 if 0 is inside the ranges (1 pixel, black).</summary>
    public void DrawAxes(CoordinateTransform transform)
    {
        var (axisX, axisY) = transform.MathToPixel(0.0, 0.0);
        if (transform.MathXInterval.IsNumberInInterval(0.0))
        {
            var p = (int)Math.Round(axisX);
            for (var q = 1; q <= size; q++)
            {
                Set(p, q, Argb.Black);
            }
        }

        if (transform.MathYInterval.IsNumberInInterval(0.0))
        {
            var q = (int)Math.Round(axisY);
            for (var p = 1; p <= size; p++)
            {
                Set(p, q, Argb.Black);
            }
        }
    }

    /// <summary>
    /// VB DrawPoint(MathPoint, Brush, wide): a filled circle of about "wide" pixels radius, only if the
    /// point is inside the ranges (e.g. the roots of the Newton iteration).
    /// </summary>
    public void FillCircle(CoordinateTransform transform, MathPoint center, int radius, int argb)
    {
        if (!transform.IsInside(center))
        {
            return;
        }

        var (cx, cy) = transform.MathToPixel(center);
        for (var y = (int)Math.Floor(cy - radius); y <= (int)Math.Ceiling(cy + radius); y++)
        {
            for (var x = (int)Math.Floor(cx - radius); x <= (int)Math.Ceiling(cx + radius); x++)
            {
                var dx = x + 0.5 - cx;
                var dy = y + 0.5 - cy;
                if (dx * dx + dy * dy <= radius * radius && x >= 0 && y >= 0 && x < size && y < size)
                {
                    Write((y * size + x) * 4, argb);
                    MarkDirty(x, y);
                }
            }
        }
    }

    /// <summary>Sends the changed rectangle to the pixel-exact canvas.</summary>
    public async Task FlushAsync(IJSObjectReference module, string canvasId)
    {
        if (_maxX < 0)
        {
            return;
        }

        var w = _maxX - _minX + 1;
        var h = _maxY - _minY + 1;
        var data = new byte[w * h * 4];
        for (var row = 0; row < h; row++)
        {
            Buffer.BlockCopy(_rgba, ((_minY + row) * size + _minX) * 4, data, row * w * 4, w * 4);
        }

        var (x, y) = (_minX, _minY);
        _minX = _minY = int.MaxValue;
        _maxX = _maxY = -1;
        await module.InvokeVoidAsync("putImage", canvasId, x, y, w, h, data);
    }

    private void Write(int offset, int argb)
    {
        _rgba[offset] = (byte)Argb.Red(argb);
        _rgba[offset + 1] = (byte)Argb.Green(argb);
        _rgba[offset + 2] = (byte)Argb.Blue(argb);
        _rgba[offset + 3] = 255;
    }

    private void MarkDirty(int x, int y)
    {
        _minX = Math.Min(_minX, x);
        _minY = Math.Min(_minY, y);
        _maxX = Math.Max(_maxX, x);
        _maxY = Math.Max(_maxY, y);
    }
}
