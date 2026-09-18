using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FolderIconChanger.Services;

/// <summary>
/// Generates real multi-resolution Windows .ICO files (32-bit RGBA, PNG-compressed
/// entries, which Windows Vista and later render natively).
/// </summary>
public static class IconGenerator
{
    /// <summary>Resolutions embedded in every generated icon.</summary>
    public static readonly int[] IconSizes = { 16, 24, 32, 48, 64, 128, 256 };

    /// <summary>Largest square canvas ever rendered while composing an icon.</summary>
    private const int ComposeCap = 1024;

    /// <summary>
    /// Fits the source image into a square transparent canvas, preserving the
    /// original aspect ratio (same logic used for the final icon).
    /// </summary>
    public static BitmapSource ComposeSquare(BitmapSource source)
    {
        int srcW = source.PixelWidth;
        int srcH = source.PixelHeight;
        int maxDim = Math.Max(srcW, srcH);
        int side = Math.Clamp(maxDim, 16, ComposeCap);

        double scale = side / (double)maxDim;
        double w = srcW * scale;
        double h = srcH * scale;
        double x = (side - w) / 2.0;
        double y = (side - h) / 2.0;

        BitmapSource hq = PrepareForDraw(source);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(hq, new Rect(x, y, w, h));
        }

        var rtb = new RenderTargetBitmap(side, side, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    /// <summary>Renders <paramref name="source"/> stretched onto an exact <paramref name="size"/> square.</summary>
    public static BitmapSource RenderToSquare(BitmapSource source, int size)
    {
        BitmapSource hq = PrepareForDraw(source);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(hq, new Rect(0, 0, size, size));
        }

        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    /// <summary>
    /// Returns a version of <paramref name="source"/> that has high-quality
    /// resampling enabled and is safe to draw. Clones frozen bitmaps because
    /// their attached RenderOptions cannot be mutated once frozen.
    /// </summary>
    private static BitmapSource PrepareForDraw(BitmapSource source)
    {
        if (!source.IsFrozen)
        {
            RenderOptions.SetBitmapScalingMode(source, BitmapScalingMode.HighQuality);
            return source;
        }

        var clone = source.Clone();
        RenderOptions.SetBitmapScalingMode(clone, BitmapScalingMode.HighQuality);
        clone.Freeze();
        return clone;
    }

    /// <summary>Builds a complete .ICO file from a composed square canvas.</summary>
    public static byte[] BuildIco(BitmapSource composedSquare)
    {
        var baseSquare = RenderToSquare(composedSquare, 256);

        var entries = new List<(byte Width, byte Height, byte[] Data)>();
        foreach (int size in IconSizes)
        {
            var frame = RenderToSquare(baseSquare, size);
            byte marker = (byte)(size == 256 ? 0 : size);
            entries.Add((marker, marker, EncodePng(frame)));
        }

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            // ICONDIR
            writer.Write((ushort)0);            // reserved
            writer.Write((ushort)1);            // type: icon
            writer.Write((ushort)entries.Count);

            // ICONDIRENTRY table
            int offset = 6 + (16 * entries.Count);
            foreach (var entry in entries)
            {
                writer.Write(entry.Width);
                writer.Write(entry.Height);
                writer.Write((byte)0);          // palette colors
                writer.Write((byte)0);          // reserved
                writer.Write((ushort)1);        // planes
                writer.Write((ushort)32);       // bits per pixel
                writer.Write((uint)entry.Data.Length);
                writer.Write((uint)offset);
                offset += entry.Data.Length;
            }

            // image blobs
            foreach (var entry in entries)
            {
                writer.Write(entry.Data);
            }
        }

        return ms.ToArray();
    }

    private static byte[] EncodePng(BitmapSource frame)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(frame));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}