using System.Runtime.InteropServices;

namespace SharpPixelOE.CPU;

public static partial class ImageUtils
{
    public static Array2D<uint> ResizePacked4xU8(Array2D<uint> src, int dstWidth, int dstHeight, InterpolationMethod method)
    {
        return ResizePacked4xU8(src, dstWidth, dstHeight, method switch
        {
            InterpolationMethod.Nearest => Packed4xU8NearestKernel,
            InterpolationMethod.Bicubic => Packed4xU8BicubicKernel,
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        });
    }
    public static Array2D<uint> ResizePacked4xU8(Array2D<uint> src, int dstWidth, int dstHeight, SimpleInterpolationKernel<byte> method)
    {
        if (SimpleResizePreprocess(src, dstWidth, dstHeight, out Array2D<uint> dst))
            return dst;
        int srcW = src.XLength;
        int srcH = src.YLength;
        ReadOnlySpan<byte> srcSpan = MemoryMarshal.AsBytes(src.Span);
        Span<byte> dstSpan = MemoryMarshal.AsBytes(dst.Span);
        for (int y = 0; y < dstHeight; y++)
            for (int x = 0; x < dstWidth; x++)
                method(x, y, srcW, srcH, dstWidth, dstHeight, srcSpan, dstSpan);
        return dst;
    }
    public static void Packed4xU8NearestKernel(
        int dstX, int dstY,
        int srcW, int srcH,
        int dstW, int dstH,
        scoped ReadOnlySpan<byte> src,
        scoped Span<byte> dst)
    {
        int srcW2 = srcW * 2;
        int srcH2 = srcH * 2;
        int dstW2 = dstW * 2;
        int dstH2 = dstH * 2;
        int dstXa = dstX * 2 + 1;
        int dstYa = dstY * 2 + 1;
        int mX = dstXa * srcW2;
        int mY = dstYa * srcH2;
        int srcX = mX / dstW2 / 2;
        int srcY = mY / dstH2 / 2;
        ReadOnlySpan<int> srcI = MemoryMarshal.Cast<byte, int>(src);
        Span<int> dstI = MemoryMarshal.Cast<byte, int>(dst);
        dstI[dstX + dstY * dstW] = srcI[srcX + srcY * srcW];
    }
    public static void Packed4xU8BicubicKernel(
        int dstX, int dstY,
        int srcW, int srcH,
        int dstW, int dstH,
        scoped ReadOnlySpan<byte> src,
        scoped Span<byte> dst)
    {
        int srcWm1 = srcW - 1;
        int srcHm1 = srcH - 1;
        int dstWm1 = dstW - 1;
        int dstHm1 = dstH - 1;
        int srcXi;
        int srcYi;
        float srcXf;
        float srcYf;
        if (srcW == 1 || dstW == 1)
        {
            srcXi = 0;
            srcXf = 0;
        }
        else
        {
            int dstWa = dstX * srcWm1;
            srcXi = dstWa / dstWm1;
            srcXf = dstWa / (float)dstWm1;
        }
        if (srcH == 1 || dstH == 1)
        {
            srcYi = 0;
            srcYf = 0;
        }
        else
        {
            int dstHa = dstY * srcHm1;
            srcYi = dstHa / dstHm1;
            srcYf = dstHa / (float)dstHm1;
        }
        float fX = srcXf - srcXi;
        float fY = srcYf - srcYi;
        Span<float> data = stackalloc float[4];
        int offset = (dstY * dstW + dstX) * 4;
        for (int c = 0; c < 4; c++)
        {
            for (int i = -1; i < 3;)
            {
                int offs = Math.Clamp(i + srcYi, 0, srcHm1) * srcW;
                int x0 = Math.Clamp(srcXi - 1, 0, srcWm1);
                int x1 = Math.Clamp(srcXi, 0, srcWm1);
                int x2 = Math.Clamp(srcXi + 1, 0, srcWm1);
                int x3 = Math.Clamp(srcXi + 2, 0, srcWm1);
                byte v0 = src[(offs + x0) * 4 + c];
                byte v1 = src[(offs + x1) * 4 + c];
                byte v2 = src[(offs + x2) * 4 + c];
                byte v3 = src[(offs + x3) * 4 + c];
                data[++i] = CubicPolate(v0, v1, v2, v3, fX);
            }
            dst[offset + c] = (byte)Math.Clamp(CubicPolate(data[0], data[1], data[2], data[3], fY), 0f, 255f);
        }
    }
}