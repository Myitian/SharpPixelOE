using ILGPU;
using ILGPU.Runtime;
using System.Runtime.InteropServices;

namespace SharpPixelOE.GPU;

public static partial class ImageUtils
{
    public static bool Morph<T>(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<T> first,
        Array2D<T> second,
        in Array3x3<bool> kernelMat,
        Action<Index2D, ArrayView<byte>, ArrayView2D<T, Stride2D.DenseX>, ArrayView2D<T, Stride2D.DenseX>> morphKernel,
        int iterations) where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfZero(first.XLength);
        ArgumentOutOfRangeException.ThrowIfZero(first.YLength);
        ArgumentOutOfRangeException.ThrowIfNotEqual(first.XLength, second.XLength);
        ArgumentOutOfRangeException.ThrowIfNotEqual(first.YLength, second.YLength);
        ReadOnlySpan<bool> kernelSpan = kernelMat;
        using MemoryBuffer1D<byte, Stride1D.Dense> kernelView = accelerator.Allocate1D<byte>(9);
        kernelView.View.BaseView.CopyFromCPU(stream, MemoryMarshal.AsBytes(kernelSpan));
        var kernel = accelerator.LoadAutoGroupedKernel(morphKernel);
        bool isSecond = false;
        while (iterations-- > 0)
        {
            isSecond = !isSecond;
            kernel(stream, first.View.IntExtent, kernelView.View.BaseView, first.View, second.View);
            (first, second) = (second, first);
        }
        return isSecond;
    }
    public static void Morph<T>(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<T> src,
        Array2D<T> dst,
        in Array3x3<bool> kernelMat,
        Action<Index2D, ArrayView<byte>, ArrayView2D<T, Stride2D.DenseX>, ArrayView2D<T, Stride2D.DenseX>> morphKernel) where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfZero(src.XLength);
        ArgumentOutOfRangeException.ThrowIfZero(src.YLength);
        ArgumentOutOfRangeException.ThrowIfNotEqual(src.XLength, dst.XLength);
        ArgumentOutOfRangeException.ThrowIfNotEqual(src.YLength, dst.YLength);
        ReadOnlySpan<bool> kernelSpan = kernelMat;
        using MemoryBuffer1D<byte, Stride1D.Dense> kernelView = accelerator.Allocate1D<byte>(9);
        kernelView.View.BaseView.CopyFromCPU(stream, MemoryMarshal.AsBytes(kernelSpan));
        var kernel = accelerator.LoadAutoGroupedKernel(morphKernel);
        kernel(stream, src.View.IntExtent, kernelView.View.BaseView, src.View, dst.View);
    }
    public static void ErodePacked4xU8Kernel(
        Index2D idx,
        ArrayView<byte> kernel,
        ArrayView2D<uint, Stride2D.DenseX> src,
        ArrayView2D<uint, Stride2D.DenseX> dst)
    {
        uint dstVB0 = 0xFF000000u;
        uint dstVB1 = 0x00FF0000u;
        uint dstVB2 = 0x0000FF00u;
        uint dstVB3 = 0x000000FFu;
        Index2D ext = src.IntExtent;
        int w1 = ext.X - 1;
        int h1 = ext.Y - 1;
        for (int ya = -1; ya < 2; ya++)
        {
            int y = IntrinsicMath.Clamp(ya + idx.Y, 0, h1);
            for (int xa = -1; xa < 2; xa++)
            {
                if (kernel[xa + 1 + (ya + 1) * 3] != 0)
                {
                    uint srcV = src[IntrinsicMath.Clamp(xa + idx.X, 0, w1), y];
                    uint srcVB0 = srcV & 0xFF000000u;
                    uint srcVB1 = srcV & 0x00FF0000u;
                    uint srcVB2 = srcV & 0x0000FF00u;
                    uint srcVB3 = srcV & 0x000000FFu;
                    dstVB0 = IntrinsicMath.Min(srcVB0, dstVB0);
                    dstVB1 = IntrinsicMath.Min(srcVB1, dstVB1);
                    dstVB2 = IntrinsicMath.Min(srcVB2, dstVB2);
                    dstVB3 = IntrinsicMath.Min(srcVB3, dstVB3);
                }
            }
        }
        dst[idx] = dstVB0 | dstVB1 | dstVB2 | dstVB3;
    }
    public static void ErodeU8Kernel(
        Index2D idx,
        ArrayView<byte> kernel,
        ArrayView2D<byte, Stride2D.DenseX> src,
        ArrayView2D<byte, Stride2D.DenseX> dst)
    {
        byte dstV = byte.MaxValue;
        Index2D ext = src.IntExtent;
        int w1 = ext.X - 1;
        int h1 = ext.Y - 1;
        for (int ya = -1; ya < 2; ya++)
        {
            int y = IntrinsicMath.Clamp(ya + idx.Y, 0, h1);
            for (int xa = -1; xa < 2; xa++)
            {
                if (kernel[xa + 1 + (ya + 1) * 3] != 0)
                {
                    byte srcV = src[IntrinsicMath.Clamp(xa + idx.X, 0, w1), y];
                    dstV = IntrinsicMath.Min(srcV, dstV);
                }
            }
        }
        dst[idx] = dstV;
    }
    public static void DilatePacked4xU8Kernel(
        Index2D idx,
        ArrayView<byte> kernel,
        ArrayView2D<uint, Stride2D.DenseX> src,
        ArrayView2D<uint, Stride2D.DenseX> dst)
    {
        uint dstVB0 = 0;
        uint dstVB1 = 0;
        uint dstVB2 = 0;
        uint dstVB3 = 0;
        Index2D ext = src.IntExtent;
        int w1 = ext.X - 1;
        int h1 = ext.Y - 1;
        for (int ya = -1; ya < 2; ya++)
        {
            int y = IntrinsicMath.Clamp(ya + idx.Y, 0, h1);
            for (int xa = -1; xa < 2; xa++)
            {
                if (kernel[xa + 1 + (ya + 1) * 3] != 0)
                {
                    uint srcV = src[IntrinsicMath.Clamp(xa + idx.X, 0, w1), y];
                    uint srcVB0 = srcV & 0xFF000000u;
                    uint srcVB1 = srcV & 0x00FF0000u;
                    uint srcVB2 = srcV & 0x0000FF00u;
                    uint srcVB3 = srcV & 0x000000FFu;
                    dstVB0 = IntrinsicMath.Max(srcVB0, dstVB0);
                    dstVB1 = IntrinsicMath.Max(srcVB1, dstVB1);
                    dstVB2 = IntrinsicMath.Max(srcVB2, dstVB2);
                    dstVB3 = IntrinsicMath.Max(srcVB3, dstVB3);
                }
            }
        }
        dst[idx] = dstVB0 | dstVB1 | dstVB2 | dstVB3;
    }
    public static void DilateU8Kernel(
        Index2D idx,
        ArrayView<byte> kernel,
        ArrayView2D<byte, Stride2D.DenseX> src,
        ArrayView2D<byte, Stride2D.DenseX> dst)
    {
        byte dstV = byte.MinValue;
        Index2D ext = src.IntExtent;
        int w1 = ext.X - 1;
        int h1 = ext.Y - 1;
        for (int ya = -1; ya < 2; ya++)
        {
            int y = IntrinsicMath.Clamp(ya + idx.Y, 0, h1);
            for (int xa = -1; xa < 2; xa++)
            {
                if (kernel[xa + 1 + (ya + 1) * 3] != 0)
                {
                    byte srcV = src[IntrinsicMath.Clamp(xa + idx.X, 0, w1), y];
                    dstV = IntrinsicMath.Max(srcV, dstV);
                }
            }
        }
        dst[idx] = dstV;
    }
}
