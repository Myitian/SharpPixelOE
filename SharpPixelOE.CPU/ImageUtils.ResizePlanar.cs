using System.Diagnostics;

namespace SharpPixelOE.CPU;

public static partial class ImageUtils
{
    public static bool Planar4ChannelResizePreprocess<T>(Array2D<T> src, int dstWidth, int dstHeight, out Array2D<T> dst)
    {
        int srcWidth = src.XLength, srcHeight = src.YLength / 4;
        if (srcWidth == dstWidth && srcHeight / 4 == dstHeight)
        {
            dst = src.Copy();
            return true;
        }
        dst = new(dstWidth, dstHeight * 4);
        if (srcWidth == 0 || srcHeight == 0 || dstWidth == 0 || dstHeight == 0)
        {
            dst.Clear();
            return true;
        }
        return false;
    }
    public static Array2D<float> ResizePlanar4xFP32(Array2D<float> src, int dstWidth, int dstHeight, InterpolationMethod method)
    {
        return ResizePlanar4xFP32(src, dstWidth, dstHeight, method switch
        {
            InterpolationMethod.Nearest => Planar4xFP32NearestKernel,
            InterpolationMethod.Bicubic => Planar4xFP32BicubicKernel,
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        });
    }
    public static Array2D<float> ResizePlanar4xFP32(Array2D<float> src, int dstWidth, int dstHeight, InterpolationKernel<float> method)
    {
        if (Planar4ChannelResizePreprocess(src, dstWidth, dstHeight, out Array2D<float> dst))
            return dst;
        int srcW = src.XLength;
        int srcH = src.YLength;
        int dstW = dst.XLength;
        int dstH = dst.YLength;
        ReadOnlySpan<float> srcSpan = src.Span;
        Span<float> dstSpan = dst.Span;
        for (int y = 0; y < dstH; y++)
            for (int x = 0; x < dstW; x++)
                method(x, y, srcW, srcH, dstW, dstH, srcSpan, dstSpan);
        return dst;
    }
    public static void Planar4xFP32NearestKernel(
        int dstX, int dstY,
        int srcW, int srcH,
        int dstW, int dstH,
        scoped ReadOnlySpan<float> src,
        scoped Span<float> dst)
    {
        int srcHr = srcH / 4;
        int dstHr = dstH / 4;
        int dstYq = dstY / dstHr;
        int dstYr = dstY % dstHr;
        int srcYoffs = dstYq * srcHr;
        int dstYoffs = dstYq * dstHr;

        int srcW2 = srcW * 2;
        int srcH2 = srcHr * 2;
        int dstW2 = dstW * 2;
        int dstH2 = dstHr * 2;
        int dstXa = dstX * 2 + 1;
        int dstYa = dstYr * 2 + 1;
        int mX = dstXa * srcW2;
        int mY = dstYa * srcH2;
        int srcX = mX / dstW2 / 2;
        int srcYr = mY / dstH2 / 2;
        dst[dstX + (dstYoffs + dstYr) * dstW] = src[srcX + (srcYoffs + srcYr) * srcW];
    }
    public static void Planar4xFP32BicubicKernel(
        int dstX, int dstY,
        int srcW, int srcH,
        int dstW, int dstH,
        scoped ReadOnlySpan<float> src,
        scoped Span<float> dst)
    {
        int srcHr = srcH / 4;
        int dstHr = dstH / 4;
        int dstYq = dstY / dstHr;
        int dstYr = dstY % dstHr;
        int srcYoffs = dstYq * srcHr;
        int dstYoffs = dstYq * dstHr;

        int srcWm1 = srcW - 1;
        int srcHm1 = srcHr - 1;
        int dstWm1 = dstW - 1;
        int dstHm1 = dstHr - 1;
        int srcXi;
        int srcYi;
        float srcXf;
        float srcYf;
        if (srcWm1 == 0 || dstWm1 == 0)
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
        if (srcHm1 == 0 || dstHm1 == 0)
        {
            srcYi = 0;
            srcYf = 0;
        }
        else
        {
            int dstHa = dstYr * srcHm1;
            srcYi = dstHa / dstHm1;
            srcYf = dstHa / (float)dstHm1;
        }
        float fX = srcXf - srcXi;
        float fY = srcYf - srcYi;
        int y0 = Math.Clamp(srcYi - 1, 0, srcHm1);
        int y1 = Math.Clamp(srcYi, 0, srcHm1);
        int y2 = Math.Clamp(srcYi + 1, 0, srcHm1);
        int y3 = Math.Clamp(srcYi + 2, 0, srcHm1);
        int x0 = Math.Clamp(srcXi - 1, 0, srcWm1);
        int x1 = Math.Clamp(srcXi, 0, srcWm1);
        int x2 = Math.Clamp(srcXi + 1, 0, srcWm1);
        int x3 = Math.Clamp(srcXi + 2, 0, srcWm1);
        int y0a = (y0 + srcYoffs) * srcW;
        int y1a = (y1 + srcYoffs) * srcW;
        int y2a = (y2 + srcYoffs) * srcW;
        int y3a = (y3 + srcYoffs) * srcW;
        float d0, d1, d2, d3;
        {
            float v0 = src[x0 + y0a];
            float v1 = src[x1 + y0a];
            float v2 = src[x2 + y0a];
            float v3 = src[x3 + y0a];
            d0 = CubicPolate(v0, v1, v2, v3, fX);
        }
        {
            float v0 = src[x0 + y1a];
            float v1 = src[x1 + y1a];
            float v2 = src[x2 + y1a];
            float v3 = src[x3 + y1a];
            d1 = CubicPolate(v0, v1, v2, v3, fX);
        }
        {
            float v0 = src[x0 + y2a];
            float v1 = src[x1 + y2a];
            float v2 = src[x2 + y2a];
            float v3 = src[x3 + y2a];
            d2 = CubicPolate(v0, v1, v2, v3, fX);
        }
        {
            float v0 = src[x0 + y3a];
            float v1 = src[x1 + y3a];
            float v2 = src[x2 + y3a];
            float v3 = src[x3 + y3a];
            d3 = CubicPolate(v0, v1, v2, v3, fX);
        }
        dst[dstX + (dstYr + dstYoffs) * dstW] = CubicPolate(d0, d1, d2, d3, fY);
    }
}