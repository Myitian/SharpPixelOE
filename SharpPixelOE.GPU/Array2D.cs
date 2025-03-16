using ILGPU;
using ILGPU.Runtime;
using System.Numerics;

namespace SharpPixelOE.GPU;

public class Array2D<T> : IDisposable where T : unmanaged
{
    public readonly Accelerator Accelerator;
    public readonly MemoryBuffer2D<T, Stride2D.DenseX> MemoryBuffer;
    public readonly ArrayView2D<T, Stride2D.DenseX> View;
    public readonly ArrayView<T> RawView;
    public readonly int XLength;
    public readonly int YLength;
    public readonly int Length;

    public Array2D(Accelerator accelerator, int xLen, int yLen)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xLen);
        ArgumentOutOfRangeException.ThrowIfNegative(yLen);
        long size = (long)xLen * yLen;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, int.MaxValue);
        Length = (int)size;
        XLength = xLen;
        YLength = yLen;
        Accelerator = accelerator;
        MemoryBuffer = accelerator.Allocate2DDenseX<T>(new(xLen, yLen));
        RawView = MemoryBuffer.AsArrayView<T>(0, Length);
        View = MemoryBuffer.View;
    }
    public Array2D(Accelerator accelerator, int xLen, int yLen, ReadOnlyMemory<T> memory)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xLen);
        ArgumentOutOfRangeException.ThrowIfNegative(yLen);
        long size = (long)xLen * yLen;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, int.MaxValue);
        Length = (int)size;
        XLength = xLen;
        YLength = yLen;
        Accelerator = accelerator;
        MemoryBuffer = accelerator.Allocate2DDenseX<T>(new(xLen, yLen));
        RawView = MemoryBuffer.AsArrayView<T>(0, Length);
        RawView.CopyFromCPU(memory.Span);
        View = MemoryBuffer.View;
    }

    public Array2D<T> Clear(AcceleratorStream stream)
    {
        var kernel = Accelerator.LoadAutoGroupedKernel<Index1D, ArrayView<T>>(ClearKernel);
        kernel(stream, Length, RawView);
        return this;
    }
    public Array2D<T> Fill(AcceleratorStream stream, T value)
    {
        var kernel = Accelerator.LoadAutoGroupedKernel<Index1D, ArrayView<T>, T>(FillKernel);
        kernel(stream, Length, RawView, value);
        return this;
    }
    public static void ClearKernel(Index1D idx, ArrayView<T> view)
    {
        view[idx] = default;
    }
    public static void FillKernel(Index1D idx, ArrayView<T> view, T value)
    {
        view[idx] = value;
    }
    public void PadEdgeFrom(
        AcceleratorStream stream,
        ArrayView2D<T, Stride2D.DenseX> src,
        int padWidthXStart,
        int padWidthXEnd,
        int padWidthYStart,
        int padWidthYEnd)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(padWidthXStart);
        ArgumentOutOfRangeException.ThrowIfNegative(padWidthXEnd);
        ArgumentOutOfRangeException.ThrowIfNegative(padWidthYStart);
        ArgumentOutOfRangeException.ThrowIfNegative(padWidthYEnd);
        ArgumentOutOfRangeException.ThrowIfNotEqual(padWidthXStart + src.IntExtent.X + padWidthXEnd, XLength);
        ArgumentOutOfRangeException.ThrowIfNotEqual(padWidthYStart + src.IntExtent.Y + padWidthYEnd, YLength);
        if (src.IntExtent.X == 0 || src.IntExtent.Y == 0 || XLength == 0 || YLength == 0)
        {
            Clear(stream);
            return;
        }
        var kernel = Accelerator.LoadAutoGroupedKernel<Index2D, Index2D, ArrayView2D<T, Stride2D.DenseX>, ArrayView2D<T, Stride2D.DenseX>>(PadEdgeKernel);
        kernel(stream, MemoryBuffer.IntExtent, new(padWidthXStart, padWidthYStart), src, View);
    }
    public static void PadEdgeKernel(
        Index2D dstIdx,
        Index2D offset,
        ArrayView2D<T, Stride2D.DenseX> src,
        ArrayView2D<T, Stride2D.DenseX> dst)
    {
        dst[dstIdx] = src[Index2D.Clamp(dstIdx - offset, Index2D.Zero, src.IntExtent - Index2D.One)];
    }
    public Array2D<T> Copy(AcceleratorStream stream)
    {
        Array2D<T> copy = new(Accelerator, XLength, YLength);
        CopyTo(stream, copy);
        return copy;
    }
    public ArrayView2D<T, Stride2D.DenseX> SliceY(int y, int height)
    {
        return RawView.SubView(y * XLength, height * XLength).AsGeneral().As2DDenseXView(new(XLength, height));
    }
    public void CopyTo(AcceleratorStream stream, Array2D<T> dst)
    {
        MemoryBuffer.CopyTo(stream, dst.MemoryBuffer);
    }

    public void Dispose()
    {
        MemoryBuffer.Dispose();
        GC.SuppressFinalize(this);
    }
}
public static class Array2DExtension
{
    public static Array2D<T> DivideBy<T>(this Array2D<T> self, AcceleratorStream stream, T value) where T : unmanaged, IDivisionOperators<T, T, T>
    {
        var kernel = self.Accelerator.LoadAutoGroupedKernel<Index1D, ArrayView<T>, T>(DivideKernel);
        kernel(stream, self.Length, self.RawView, value);
        return self;
    }
    public static void DivideKernel<T>(Index1D idx, ArrayView<T> view, T value) where T : unmanaged, IDivisionOperators<T, T, T>
    {
        view[idx] /= value;
    }
}