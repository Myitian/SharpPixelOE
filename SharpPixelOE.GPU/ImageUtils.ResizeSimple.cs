using ILGPU;
using ILGPU.Runtime;

namespace SharpPixelOE.GPU;

public static partial class ImageUtils
{
    public static bool SimpleResizePreprocess<T>(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<T> src,
        int dstWidth,
        int dstHeight,
        out Array2D<T> dst) where T : unmanaged
    {
        int srcWidth = src.XLength, srcHeight = src.YLength;
        if (srcWidth == dstWidth && srcHeight == dstHeight)
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
    public static bool SimpleResizePreprocess<T>(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<T> src,
        Array2D<T> dst) where T : unmanaged
    {
        int srcWidth = src.XLength, srcHeight = src.YLength;
        int dstWidth = dst.XLength, dstHeight = dst.YLength;
        if (srcWidth == dstWidth && srcHeight == dstHeight)
        {
            src.CopyTo(stream, dst);
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
    public static Array2D<float> ResizeSimpleFP32(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<float> src,
        int dstWidth,
        int dstHeight,
        InterpolationMethod method)
    {
        return ResizeSimpleFP32(accelerator, stream, src, dstWidth, dstHeight, method switch
        {
            InterpolationMethod.Nearest => SimpleFP32NearestKernel,
            InterpolationMethod.Bilinear => SimpleFP32BilinearKernel,
            InterpolationMethod.Bicubic => SimpleFP32BicubicKernel,
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        });
    }
    public static void ResizeSimpleFP32To(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<float> src,
        Array2D<float> dst,
        InterpolationMethod method)
    {
        ResizeSimpleFP32To(accelerator, stream, src, dst, method switch
        {
            InterpolationMethod.Nearest => SimpleFP32NearestKernel,
            InterpolationMethod.Bilinear => SimpleFP32BilinearKernel,
            InterpolationMethod.Bicubic => SimpleFP32BicubicKernel,
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        });
    }
    public static Array2D<float> ResizeSimpleFP32(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<float> src,
        int dstWidth,
        int dstHeight,
        Action<Index2D, ArrayView2D<float, Stride2D.DenseX>, ArrayView2D<float, Stride2D.DenseX>> method)
    {
        if (SimpleResizePreprocess(accelerator, stream, src, dstWidth, dstHeight, out Array2D<float> dst))
            return dst;
        var kernel = accelerator.LoadAutoGroupedKernel(method);
        kernel(stream, dst.MemoryBuffer.IntExtent, src.View, dst.View);
        return dst;
    }
    public static void ResizeSimpleFP32To(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<float> src,
        Array2D<float> dst,
        Action<Index2D, ArrayView2D<float, Stride2D.DenseX>, ArrayView2D<float, Stride2D.DenseX>> method)
    {
        if (SimpleResizePreprocess(accelerator, stream, src, dst))
            return;
        var kernel = accelerator.LoadAutoGroupedKernel(method);
        kernel(stream, dst.MemoryBuffer.IntExtent, src.View, dst.View);
    }
    public static void SimpleFP32NearestKernel(
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
    }
    public static void SimpleFP32BilinearKernel(
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
        int x0 = IntrinsicMath.Clamp(srcXi, 0, srcWm1);
        int x1 = IntrinsicMath.Clamp(srcXi + 1, 0, srcWm1);
        int y0 = IntrinsicMath.Clamp(srcYi, 0, srcHm1);
        int y1 = IntrinsicMath.Clamp(srcYi + 1, 0, srcHm1);
        float v0 = Utils.Lerp(src[x0, y0], src[x1, y0], fX);
        float v1 = Utils.Lerp(src[x0, y1], src[x1, y1], fX);
        dst[dstIdx] = Utils.Lerp(v0, v1, fY);
    }
    public static void SimpleFP32BicubicKernel(
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
        float d0, d1, d2, d3;
        {
            float v0 = src[x0, y0];
            float v1 = src[x1, y0];
            float v2 = src[x2, y0];
            float v3 = src[x3, y0];
            d0 = CubicPolate(v0, v1, v2, v3, fX);
        }
        {
            float v0 = src[x0, y1];
            float v1 = src[x1, y1];
            float v2 = src[x2, y1];
            float v3 = src[x3, y1];
            d1 = CubicPolate(v0, v1, v2, v3, fX);
        }
        {
            float v0 = src[x0, y2];
            float v1 = src[x1, y2];
            float v2 = src[x2, y2];
            float v3 = src[x3, y2];
            d2 = CubicPolate(v0, v1, v2, v3, fX);
        }
        {
            float v0 = src[x0, y3];
            float v1 = src[x1, y3];
            float v2 = src[x2, y3];
            float v3 = src[x3, y3];
            d3 = CubicPolate(v0, v1, v2, v3, fX);
        }
        dst[dstIdx] = (byte)IntrinsicMath.Clamp(CubicPolate(d0, d1, d2, d3, fY), 0f, 255f);
    }
}