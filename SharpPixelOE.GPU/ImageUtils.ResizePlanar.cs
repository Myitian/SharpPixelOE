using ILGPU;
using ILGPU.Runtime;

namespace SharpPixelOE.GPU;

public static partial class ImageUtils
{
    public static bool Planar4ChannelResizePreprocess<T>(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<T> src,
        int dstWidth,
        int dstHeight,
        out Array2D<T> dst) where T : unmanaged
    {
        int srcWidth = src.XLength, srcHeight = src.YLength / 4;
        if (srcWidth == dstWidth && srcHeight / 4 == dstHeight)
        {
            dst = src.Copy(stream);
            return true;
        }
        dst = new(accelerator, dstWidth, dstHeight);
        if (srcWidth == 0 || srcHeight == 0 || dstWidth == 0 || dstHeight == 0)
        {
            dst.Clear(stream);
            return true;
        }
        return false;
    }
    public static Array2D<float> ResizePlanar4xFP32(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<float> src,
        int dstWidth,
        int dstHeight, InterpolationMethod method)
    {
        return ResizePlanar4xFP32(accelerator, stream, src, dstWidth, dstHeight, method switch
        {
            InterpolationMethod.Nearest => Planar4xFP32NearestKernel,
            InterpolationMethod.Bicubic => Planar4xFP32BicubicKernel,
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        });
    }
    public static Array2D<float> ResizePlanar4xFP32(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<float> src,
        int dstWidth,
        int dstHeight,
        Action<Index2D, ArrayView2D<float, Stride2D.DenseX>, ArrayView2D<float, Stride2D.DenseX>> method)
    {
        if (Planar4ChannelResizePreprocess(accelerator, stream, src, dstWidth, dstHeight, out Array2D<float> dst))
            return dst;
        var kernel = accelerator.LoadAutoGroupedKernel(method);
        kernel(stream, new(dst.XLength, dst.YLength / 4), src.View, dst.View);
        return dst;
    }
    public static void Planar4xFP32NearestKernel(
        Index2D dstIdx,
        ArrayView2D<float, Stride2D.DenseX> src,
        ArrayView2D<float, Stride2D.DenseX> dst)
    {
        Index2D srcExt = src.IntExtent;
        Index2D dstExt = dst.IntExtent;
        int srcW2 = srcExt.X * 2;
        int srcH2 = srcExt.Y * 2;
        int dstW2 = dstExt.X * 2;
        int dstH2 = dstExt.Y * 2;
        int dstXa = dstIdx.X * 2 + 1;
        int dstYa = dstIdx.Y * 2 + 1;
        int mX = dstXa * srcW2;
        int mY = dstYa * srcH2;
        int srcX = mX / dstW2 / 2;
        int srcY = mY / dstH2 / 2;
        dst[dstIdx] = src[srcX, srcY];
        dst[dstIdx.X, dstIdx.Y + dstExt.Y] = src[srcX, srcY + srcExt.Y];
        dst[dstIdx.X, dstIdx.Y + dstExt.Y * 2] = src[srcX, srcY + srcExt.Y * 2];
        dst[dstIdx.X, dstIdx.Y + dstExt.Y * 3] = src[srcX, srcY + srcExt.Y * 3];
    }
    public static void Planar4xFP32BicubicKernel(
        Index2D dstIdx,
        ArrayView2D<float, Stride2D.DenseX> src,
        ArrayView2D<float, Stride2D.DenseX> dst)
    {
        Index2D srcExt = src.IntExtent;
        Index2D dstExt = dst.IntExtent;
        int srcWm1 = srcExt.X - 1;
        int srcHm1 = srcExt.Y - 1;
        int dstWm1 = dstExt.X - 1;
        int dstHm1 = dstExt.Y - 1;
        int srcXi;
        int srcYi;
        float srcXf;
        float srcYf;
        if (srcWm1 == 0 || dstWm1 == 1)
        {
            srcXi = 0;
            srcXf = 0;
        }
        else
        {
            int dstWa = dstIdx.X * srcWm1;
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
            int dstHa = dstIdx.Y * srcHm1;
            srcYi = dstHa / dstHm1;
            srcYf = dstHa / (float)dstHm1;
        }
        float fX = srcXf - srcXi;
        float fY = srcYf - srcYi;
        int y0 = IntrinsicMath.Clamp(srcYi - 1, 0, srcHm1);
        int y1 = IntrinsicMath.Clamp(srcYi, 0, srcHm1);
        int y2 = IntrinsicMath.Clamp(srcYi + 1, 0, srcHm1);
        int y3 = IntrinsicMath.Clamp(srcYi + 2, 0, srcHm1);
        int x0 = IntrinsicMath.Clamp(srcXi - 1, 0, srcWm1);
        int x1 = IntrinsicMath.Clamp(srcXi, 0, srcWm1);
        int x2 = IntrinsicMath.Clamp(srcXi + 1, 0, srcWm1);
        int x3 = IntrinsicMath.Clamp(srcXi + 2, 0, srcWm1);
        for (int c = 0, srcOffset = 0, dstOffset = 0; c < 4; c++, srcOffset += srcExt.Y, dstOffset += dstExt.Y)
        {
            int y0a = y0 + srcOffset;
            int y1a = y1 + srcOffset;
            int y2a = y2 + srcOffset;
            int y3a = y3 + srcOffset;
            float d0, d1, d2, d3;
            {
                float v0 = src[x0, y0a];
                float v1 = src[x1, y0a];
                float v2 = src[x2, y0a];
                float v3 = src[x3, y0a];
                d0 = CubicPolate(v0, v1, v2, v3, fX);
            }
            {
                float v0 = src[x0, y1a];
                float v1 = src[x1, y1a];
                float v2 = src[x2, y1a];
                float v3 = src[x3, y1a];
                d1 = CubicPolate(v0, v1, v2, v3, fX);
            }
            {
                float v0 = src[x0, y2a];
                float v1 = src[x1, y2a];
                float v2 = src[x2, y2a];
                float v3 = src[x3, y2a];
                d2 = CubicPolate(v0, v1, v2, v3, fX);
            }
            {
                float v0 = src[x0, y3a];
                float v1 = src[x1, y3a];
                float v2 = src[x2, y3a];
                float v3 = src[x3, y3a];
                d3 = CubicPolate(v0, v1, v2, v3, fX);
            }
            dst[dstIdx.X, dstIdx.Y + dstOffset] = (byte)IntrinsicMath.Clamp(CubicPolate(d0, d1, d2, d3, fY), 0f, 255f);
        }
    }
}