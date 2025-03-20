using System.Runtime.InteropServices;

namespace SharpPixelOE.CPU;

public static partial class ImageUtils
{
    public static bool Morph<T>(Array2D<T> first, Array2D<T> second, in Array3x3<bool> kernel, MorphKernel<T> kernelFunc, int iterations = 1)
    {
        ArgumentOutOfRangeException.ThrowIfZero(first.XLength);
        ArgumentOutOfRangeException.ThrowIfZero(first.YLength);
        ArgumentOutOfRangeException.ThrowIfNotEqual(first.XLength, second.XLength);
        ArgumentOutOfRangeException.ThrowIfNotEqual(first.YLength, second.YLength);
        bool isSecond = false;
        while (iterations-- > 0)
        {
            isSecond = !isSecond;
            Morph(first, in kernel, second, kernelFunc);
            (first, second) = (second, first);
        }
        return isSecond;
    }
    public static void Morph<T>(Array2D<T> src, in Array3x3<bool> kernel, Array2D<T> dst, MorphKernel<T> kernelFunc)
    {
        ArgumentOutOfRangeException.ThrowIfZero(src.XLength);
        ArgumentOutOfRangeException.ThrowIfZero(src.YLength);
        ArgumentOutOfRangeException.ThrowIfNotEqual(src.XLength, dst.XLength);
        ArgumentOutOfRangeException.ThrowIfNotEqual(src.YLength, dst.YLength);
        //!!! Slower !!!
        // Array3x3<bool> kernelL = kernel;
        // Parallel.For(0, src.Length, i =>
        // {
        //     int w = src.XLength, h = src.YLength;
        //     (int y, int x) = Math.DivRem(i, w);
        //     kernelFunc(x, y, w, h, in kernelL, src.Span, dst.Span);
        // });
        int w = src.XLength, h = src.YLength;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                kernelFunc(x, y, w, h, in kernel, src.Span, dst.Span);
    }
    public static void ErodePacked4xU8Kernel(
        int x, int y,
        int w, int h,
        in Array3x3<bool> kernel,
        ReadOnlySpan<uint> src,
        Span<uint> dst)
    {
        uint dstV = uint.MaxValue;
        Span<byte> dstVB = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref dstV, 1));
        for (int ya = -1; ya < 2; ya++)
        {
            for (int xa = -1; xa < 2; xa++)
            {
                if (kernel[xa + 1, ya + 1])
                {
                    int offset = Math.Clamp(ya + y, 0, h - 1) * w + Math.Clamp(xa + x, 0, w - 1);
                    uint srcV = src[offset];
                    Span<byte> srcVB = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref srcV, 1));
                    dstVB[0] = Math.Min(srcVB[0], dstVB[0]);
                    dstVB[1] = Math.Min(srcVB[1], dstVB[1]);
                    dstVB[2] = Math.Min(srcVB[2], dstVB[2]);
                    dstVB[3] = Math.Min(srcVB[3], dstVB[3]);
                }
            }
        }
        dst[y * w + x] = dstV;
    }
    public static void ErodeU8Kernel(
        int x, int y,
        int w, int h,
        in Array3x3<bool> kernel,
        ReadOnlySpan<byte> src,
        Span<byte> dst)
    {
        byte dstV = byte.MaxValue;
        for (int ya = -1; ya < 2; ya++)
        {
            for (int xa = -1; xa < 2; xa++)
            {
                if (kernel[xa + 1, ya + 1])
                {
                    int offset = Math.Clamp(ya + y, 0, h - 1) * w + Math.Clamp(xa + x, 0, w - 1);
                    dstV = Math.Min(src[offset], dstV);
                }
            }
        }
        dst[y * w + x] = dstV;
    }
    public static void DilatePacked4xU8Kernel(
        int x, int y,
        int w, int h,
        in Array3x3<bool> kernel,
        ReadOnlySpan<uint> src,
        Span<uint> dst)
    {
        uint dstV = uint.MinValue;
        Span<byte> dstVB = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref dstV, 1));
        for (int ya = -1; ya < 2; ya++)
        {
            for (int xa = -1; xa < 2; xa++)
            {
                if (kernel[xa + 1, ya + 1])
                {
                    int offset = Math.Clamp(ya + y, 0, h - 1) * w + Math.Clamp(xa + x, 0, w - 1);
                    uint srcV = src[offset];
                    Span<byte> srcVB = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref srcV, 1));
                    dstVB[0] = Math.Max(srcVB[0], dstVB[0]);
                    dstVB[1] = Math.Max(srcVB[1], dstVB[1]);
                    dstVB[2] = Math.Max(srcVB[2], dstVB[2]);
                    dstVB[3] = Math.Max(srcVB[3], dstVB[3]);
                }
            }
        }
        dst[y * w + x] = dstV;
    }
    public static void DilateU8Kernel(
        int x, int y,
        int w, int h,
        in Array3x3<bool> kernel,
        ReadOnlySpan<byte> src,
        Span<byte> dst)
    {
        byte dstV = byte.MinValue;
        for (int ya = -1; ya < 2; ya++)
        {
            for (int xa = -1; xa < 2; xa++)
            {
                if (kernel[xa + 1, ya + 1])
                {
                    int offset = Math.Clamp(ya + y, 0, h - 1) * w + Math.Clamp(xa + x, 0, w - 1);
                    dstV = Math.Max(src[offset], dstV);
                }
            }
        }
        dst[y * w + x] = dstV;
    }
}
public delegate void MorphKernel<T>(
    int x, int y,
    int w, int h,
    in Array3x3<bool> kernel,
    ReadOnlySpan<T> src,
    Span<T> dst);
