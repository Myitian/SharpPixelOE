using ILGPU;
using ILGPU.Runtime;

namespace SharpPixelOE.GPU;

public static partial class ImageUtils
{
    public static Array2D<uint> ResizePacked4xU8(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<uint> src,
        int dstWidth,
        int dstHeight,
        InterpolationMethod method)
    {
        return ResizePacked4xU8(accelerator, stream, src, dstWidth, dstHeight, method switch
        {
            InterpolationMethod.Nearest => Packed4xU8NearestKernel,
            InterpolationMethod.Bicubic => Packed4xU8BicubicKernel,
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        });
    }

    public static Array2D<uint> ResizePacked4xU8(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<uint> src,
        int dstWidth,
        int dstHeight,
        Action<Index2D, ArrayView2D<uint, Stride2D.DenseX>, ArrayView2D<uint, Stride2D.DenseX>> method)
    {
        if (SimpleResizePreprocess(accelerator, stream, src, dstWidth, dstHeight, out Array2D<uint> dst))
            return dst;
        var kernel = accelerator.LoadAutoGroupedKernel(method);
        kernel(stream, new(dst.XLength, dst.YLength), src.View, dst.View);
        return dst;
    }
    public static void Packed4xU8NearestKernel(
        Index2D dstIdx,
        ArrayView2D<uint, Stride2D.DenseX> src,
        ArrayView2D<uint, Stride2D.DenseX> dst)
    {
        Index2D srcExt = src.IntExtent;
        Index2D dstExt = dst.IntExtent;
        int srcW2 = srcExt.X / 2;
        int srcH2 = srcExt.Y * 2;
        int dstW2 = dstExt.X / 2;
        int dstH2 = dstExt.Y * 2;
        int dstXa = dstIdx.X * 2 + 1;
        int dstYa = dstIdx.Y * 2 + 1;
        int mX = dstXa * srcW2;
        int mY = dstYa * srcH2;
        int srcX = mX / dstW2 / 2;
        int srcY = mY / dstH2 / 2;
        dst[dstIdx] = src[srcX, srcY];
    }
    public static void Packed4xU8BicubicKernel(
        Index2D dstIdx,
        ArrayView2D<uint, Stride2D.DenseX> src,
        ArrayView2D<uint, Stride2D.DenseX> dst)
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
        int x0 = IntrinsicMath.Clamp(srcXi - 1, 0, srcWm1) * 4;
        int x1 = IntrinsicMath.Clamp(srcXi, 0, srcWm1) * 4;
        int x2 = IntrinsicMath.Clamp(srcXi + 1, 0, srcWm1) * 4;
        int x3 = IntrinsicMath.Clamp(srcXi + 2, 0, srcWm1) * 4;
        ArrayView2D<byte, Stride2D.DenseX> srcB = src.BaseView.Cast<byte>().AsGeneral().As2DDenseXView(new(srcExt.X * 4, srcExt.Y));
        ArrayView2D<byte, Stride2D.DenseX> dstB = dst.BaseView.Cast<byte>().AsGeneral().As2DDenseXView(new(dstExt.X * 4, dstExt.Y));
        for (int c = 0; c < 4; c++)
        {
            float d0, d1, d2, d3;
            {
                byte v0 = srcB[x0 + c, y0];
                byte v1 = srcB[x1 + c, y0];
                byte v2 = srcB[x2 + c, y0];
                byte v3 = srcB[x3 + c, y0];
                d0 = CubicPolate(v0, v1, v2, v3, fX);
            }
            {
                byte v0 = srcB[x0 + c, y1];
                byte v1 = srcB[x1 + c, y1];
                byte v2 = srcB[x2 + c, y1];
                byte v3 = srcB[x3 + c, y1];
                d1 = CubicPolate(v0, v1, v2, v3, fX);
            }
            {
                byte v0 = srcB[x0 + c, y2];
                byte v1 = srcB[x1 + c, y2];
                byte v2 = srcB[x2 + c, y2];
                byte v3 = srcB[x3 + c, y2];
                d2 = CubicPolate(v0, v1, v2, v3, fX);
            }
            {
                byte v0 = srcB[x0 + c, y3];
                byte v1 = srcB[x1 + c, y3];
                byte v2 = srcB[x2 + c, y3];
                byte v3 = srcB[x3 + c, y3];
                d3 = CubicPolate(v0, v1, v2, v3, fX);
            }
            dstB[dstIdx.X * 4 + c, dstIdx.Y] = (byte)IntrinsicMath.Clamp(CubicPolate(d0, d1, d2, d3, fY), 0f, 255f);
        }
    }
}