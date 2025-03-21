namespace SharpPixelOE.CPU;

public static partial class ImageUtils
{
    public static bool SimpleResizePreprocess<T>(Array2D<T> src, int dstWidth, int dstHeight, out Array2D<T> dst)
    {
        int srcWidth = src.XLength, srcHeight = src.YLength;
        if (srcWidth == dstWidth && srcHeight == dstHeight)
        {
            dst = src.Copy();
            return true;
        }
        dst = new(dstWidth, dstHeight);
        if (srcWidth == 0 || srcHeight == 0 || dstWidth == 0 || dstHeight == 0)
        {
            dst.Clear();
            return true;
        }
        if (srcWidth == 1 && srcHeight == 1)
        {
            dst.Fill(src.Span[0]);
            return true;
        }
        return false;
    }
    public static bool SimpleResizePreprocess<T>(Array2D<T> src, Array2D<T> dst)
    {
        int srcWidth = src.XLength, srcHeight = src.YLength;
        int dstWidth = dst.XLength, dstHeight = dst.YLength;
        if (srcWidth == 0 || srcHeight == 0 || dstWidth == 0 || dstHeight == 0)
        {
            dst.Clear();
            return true;
        }
        if (srcWidth == 1 && srcHeight == 1)
        {
            dst.Fill(src.Span[0]);
            return true;
        }
        return false;
    }
    public static Array2D<float> ResizeSimpleFP32(Array2D<float> src, int dstWidth, int dstHeight, InterpolationMethod method)
    {
        return ResizeSimpleFP32(src, dstWidth, dstHeight, method switch
        {
            InterpolationMethod.Nearest => SimpleFP32NearestKernel,
            InterpolationMethod.Bilinear => SimpleFP32BilinearKernel,
            InterpolationMethod.Bicubic => SimpleFP32BicubicKernel,
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        });
    }
    public static void ResizeSimpleFP32To(Array2D<float> src, Array2D<float> dst, InterpolationMethod method)
    {
        ResizeSimpleFP32To(src, dst, method switch
        {
            InterpolationMethod.Nearest => SimpleFP32NearestKernel,
            InterpolationMethod.Bilinear => SimpleFP32BilinearKernel,
            InterpolationMethod.Bicubic => SimpleFP32BicubicKernel,
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        });
    }
    public static Array2D<float> ResizeSimpleFP32(Array2D<float> src, int dstWidth, int dstHeight, InterpolationKernel<float> method)
    {
        if (SimpleResizePreprocess(src, dstWidth, dstHeight, out Array2D<float> dst))
            return dst;
        int srcWidth = src.XLength, srcHeight = src.YLength;
        ReadOnlySpan<float> srcSpan = src.Span;
        Span<float> dstSpan = dst.Span;
        for (int y = 0; y < dstHeight; y++)
            for (int x = 0; x < dstWidth; x++)
                method(x, y, srcWidth, srcHeight, dstWidth, dstHeight, srcSpan, dstSpan);
        return dst;
    }
    public static void ResizeSimpleFP32To(Array2D<float> src, Array2D<float> dst, InterpolationKernel<float> method)
    {
        if (SimpleResizePreprocess(src, dst))
            return;
        int srcWidth = src.XLength, srcHeight = src.YLength;
        int dstWidth = dst.XLength, dstHeight = dst.YLength;
        ReadOnlySpan<float> srcSpan = src.Span;
        Span<float> dstSpan = dst.Span;
        for (int y = 0; y < dstHeight; y++)
            for (int x = 0; x < dstWidth; x++)
                method(x, y, srcWidth, srcHeight, dstWidth, dstHeight, srcSpan, dstSpan);
    }
    public static void SimpleFP32NearestKernel(
        int dstX, int dstY,
        int srcW, int srcH,
        int dstW, int dstH,
        scoped ReadOnlySpan<float> src,
        scoped Span<float> dst)
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
        dst[dstX + dstY * dstW] = src[srcX + srcY * srcW];
    }
    public static void SimpleFP32BilinearKernel(
        int dstX, int dstY,
        int srcW, int srcH,
        int dstW, int dstH,
        scoped ReadOnlySpan<float> src,
        scoped Span<float> dst)
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
        int offset = dstY * dstW + dstX;
        int x0 = Math.Clamp(srcXi, 0, srcWm1);
        int x1 = Math.Clamp(srcXi + 1, 0, srcWm1);
        int y0 = Math.Clamp(srcYi, 0, srcHm1) * srcW;
        int y1 = Math.Clamp(srcYi + 1, 0, srcHm1) * srcW;
        float v0 = float.Lerp(src[y0 + x0], src[y0 + x1], fX);
        float v1 = float.Lerp(src[y1 + x0], src[y1 + x1], fX);
        dst[offset] = float.Lerp(v0, v1, fY);
    }
    public static void SimpleFP32BicubicKernel(
        int dstX, int dstY,
        int srcW, int srcH,
        int dstW, int dstH,
        scoped ReadOnlySpan<float> src,
        scoped Span<float> dst)
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
        int offset = dstY * dstW + dstX;
        for (int i = -1; i < 3;)
        {
            int offs = Math.Clamp(i + srcYi, 0, srcHm1) * srcW;
            int x0 = Math.Clamp(srcXi - 1, 0, srcWm1);
            int x1 = Math.Clamp(srcXi, 0, srcWm1);
            int x2 = Math.Clamp(srcXi + 1, 0, srcWm1);
            int x3 = Math.Clamp(srcXi + 2, 0, srcWm1);
            float v0 = src[offs + x0];
            float v1 = src[offs + x1];
            float v2 = src[offs + x2];
            float v3 = src[offs + x3];
            data[++i] = CubicPolate(v0, v1, v2, v3, fX);
        }
        dst[offset] = CubicPolate(data[0], data[1], data[2], data[3], fY);
    }
}
public delegate void InterpolationKernel<T>(
    int dstX, int dstY,
    int srcW, int srcH,
    int dstW, int dstH,
    scoped ReadOnlySpan<T> src,
    scoped Span<T> dst);