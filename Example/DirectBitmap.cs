using System.Buffers;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Myitian.Drawing;

public class DirectBitmap : IDisposable
{
    public Bitmap Bitmap { get; private set; }
    public Memory<uint> Data { get; private set; }
    public bool Disposed { get; private set; }
    public int Height { get; private set; }
    public int Width { get; private set; }
    public uint this[int x, int y]
    {
        get => GetPixel(x, y);
        set => SetPixel(x, y, value);
    }
    protected MemoryHandle Handle { get; private set; }

    public int Count => Width * Height;

    public uint this[int index]
    {
        get => Data.Span[index];
        set => Data.Span[index] = value;
    }

    public DirectBitmap(Bitmap src) : this(src, new Rectangle(Point.Empty, src.Size)) { }
    public unsafe DirectBitmap(Bitmap src, Rectangle rect) : this(rect.Width, rect.Height)
    {
        BitmapData srcL = src.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        ReadOnlySpan<uint> span = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<uint>(srcL.Scan0.ToPointer()), Data.Length);
        span.CopyTo(Data.Span);
        src.UnlockBits(srcL);
    }
    public DirectBitmap(Bitmap src, Size size) : this(size.Width, size.Height)
    {
        using Graphics g = Graphics.FromImage(Bitmap);
        using ImageAttributes attr = new();
        attr.SetWrapMode(WrapMode.TileFlipXY);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage(src, new(0, 0, Width, Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attr);
    }
    public DirectBitmap(DirectBitmap src, Rectangle rect) : this(rect.Width, rect.Height)
    {
        int srcOffset = rect.Top * src.Width + rect.Left;
        int thisOffset = 0;
        for (int y = 0; y < Height; y++, srcOffset += src.Width, thisOffset += Width)
            src.Data.Slice(srcOffset, Width).CopyTo(Data.Slice(thisOffset));
    }
    public DirectBitmap(int width, int height, Color col) : this(width, height)
    {
        Data.Span.Fill((uint)col.ToArgb());
    }
    public unsafe DirectBitmap(int width, int height)
    {
        Width = width;
        Height = height;
        Data = GC.AllocateUninitializedArray<uint>(Width * Height, true);
        Handle = Data.Pin();
        Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, new(Handle.Pointer));
    }
    public unsafe DirectBitmap(int width, int height, Memory<uint> memory)
    {
        Width = width;
        Height = height;
        Data = memory;
        Handle = Data.Pin();
        Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, new(Handle.Pointer));
    }

    public void SetPixel(int x, int y, uint col)
    {
        Data.Span[x + (y * Width)] = col;
    }

    public uint GetPixel(int x, int y)
    {
        return Data.Span[x + (y * Width)];
    }

    public DirectBitmap Crop(Rectangle rect)
        => new(this, rect);
    public DirectBitmap Resize(Size size)
    {
        DirectBitmap result = new(size.Width, size.Height);
        using Graphics g = Graphics.FromImage(result.Bitmap);
        g.DrawImage(Bitmap, 0, 0, size.Width, size.Height);
        return result;
    }
    public void CopyTo(DirectBitmap dst, Rectangle rect)
    {
        int dstOffset = rect.Top * dst.Width + rect.Left;
        int thisOffset = 0;
        for (int y = 0; y < Height; y++, dstOffset += dst.Width, thisOffset += Width)
            Data.Slice(thisOffset, rect.Width).CopyTo(dst.Data.Slice(dstOffset));
    }
    public void CopyTo(DirectBitmap dst, Point lt)
    {
        CopyTo(dst, new Rectangle(lt.X, lt.Y, Math.Min(Width, dst.Width - lt.X), Math.Min(Height, dst.Height - lt.Y)));
    }
    public void CopyFromGDIPlus(Bitmap src, Rectangle dst)
    {
        using Graphics g = Graphics.FromImage(Bitmap);
        using ImageAttributes attr = new();
        attr.SetWrapMode(WrapMode.TileFlipXY);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage(src, dst, 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attr);
    }
    public void CopyFrom(Bitmap src, Rectangle dst)
    {
        using DirectBitmap dbmp = new(src, dst.Size);
        dbmp.CopyTo(this, dst.Location);
    }
    public void Fill(uint color)
    {
        Fill(color);
    }
    public void Fill(Color color)
    {
        Fill((uint)color.ToArgb());
    }
    public void Fill(uint color, Rectangle rect)
    {
        int offset = rect.Top * Width + rect.Left;
        if (rect.Width == Width)
        {
            Data.Span.Slice(offset, rect.Width * rect.Height).Fill(color);
        }
        else
        {
            for (int y = 0; y < rect.Height; y++, offset += Width)
                Data.Span.Slice(offset, rect.Width).Fill(color);
        }
    }
    public void Fill(Color color, Rectangle rect)
    {
        Fill((uint)color.ToArgb(), rect);
    }

    public void Dispose()
    {
        if (Disposed)
            return;
        Disposed = true;
        Bitmap.Dispose();
        Handle.Dispose();
        GC.SuppressFinalize(this);
    }
}
