using ILGPU;
using ILGPU.Runtime;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SharpPixelOE.GPU;

internal static class Utils
{
    public interface IApplyChunkOp1D<TSelf, TValue> where TSelf : IApplyChunkOp1D<TSelf, TValue> where TValue : unmanaged
    {
        public abstract static TValue Apply(ArrayView<TValue> chunk);
    }
    public interface IApplyChunkOp2D<TSelf, TValue> where TSelf : IApplyChunkOp2D<TSelf, TValue> where TValue : unmanaged
    {
        public abstract static TValue Apply(ArrayView2D<TValue, Stride2D.DenseX> chunk);
    }
    public struct MaxOp<T> : IApplyChunkOp2D<MaxOp<T>, T> where T : unmanaged, IComparisonOperators<T, T, bool>
    {
        public static T Apply(ArrayView2D<T, Stride2D.DenseX> chunk)
        {
            int w = chunk.IntExtent.X;
            int h = chunk.IntExtent.Y;
            if (w == 0 || h == 0)
                return default;
            T m = chunk[0, 0];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    T v = chunk[x, y];
                    m = v > m ? v : m;
                }
            }
            return m;
        }
    }
    public struct MinOp<T> : IApplyChunkOp2D<MinOp<T>, T> where T : unmanaged, IComparisonOperators<T, T, bool>
    {
        public static T Apply(ArrayView2D<T, Stride2D.DenseX> chunk)
        {
            int w = chunk.IntExtent.X;
            int h = chunk.IntExtent.Y;
            if (w == 0 || h == 0)
                return default;
            T m = chunk[0, 0];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    T v = chunk[x, y];
                    m = v < m ? v : m;
                }
            }
            return m;
        }
    }
    public struct MiddleOp<T> : IApplyChunkOp2D<MiddleOp<T>, T> where T : unmanaged
    {
        public static T Apply(ArrayView2D<T, Stride2D.DenseX> chunk)
        {
            int w = chunk.IntExtent.X;
            int h = chunk.IntExtent.Y;
            if (w == 0 || h == 0)
                return default;
            int i = w * h / 2;
            return chunk[i % w, i / w];
        }
    }
    public struct MedianOp<T> : IApplyChunkOp1D<MedianOp<T>, T> where T : unmanaged, IComparisonOperators<T, T, bool>
    {
        public static T Apply(ArrayView<T> chunk)
        {
            int len = chunk.IntLength;
            if (len == 0)
                return default;
            SortArray(chunk);
            return chunk[len / 2];
        }
    }
    public struct FindPixelOp : IApplyChunkOp1D<FindPixelOp, float>
    {
        public static float Apply(ArrayView<float> chunk)
        {
            int len = chunk.IntLength;
            if (len == 0)
                return default;
            int half = len / 2;
            float mid = chunk[half];
            SortArray(chunk);
            float med = chunk[half];
            float mu = Sum(chunk) / len;
            float min = chunk[0];
            float max = chunk[len - 1];
            float maxi_med = max - med;
            float med_mini = med - min;
            if (med < mu && maxi_med > med_mini)
                return min;
            if (med > mu && maxi_med < med_mini)
                return max;
            return mid;
        }
    }

    public static Array2D<T> ApplyChunkPad<T>(
        Accelerator accelerator,
        AcceleratorStream stream,
        ArrayView2D<T, Stride2D.DenseX> array,
        int kernel,
        int stride) where T : unmanaged
    {
        int kShift = IntrinsicMath.Max(kernel - stride, 0);
        int padPatternStart = kShift / 2;
        int padPatternEnd = kShift - padPatternStart;
        Array2D<T> padBuffer = new(accelerator, array.IntExtent.X + kShift, array.IntExtent.Y + kShift);
        padBuffer.PadEdgeFrom(stream, array, padPatternStart, padPatternEnd, padPatternStart, padPatternEnd);
        return padBuffer;
    }
    public static Array2D<T> ApplyChunkPad<T>(
        AcceleratorStream stream,
        Array2D<T> padBuffer,
        ArrayView2D<T, Stride2D.DenseX> array,
        int kernel,
        int stride) where T : unmanaged
    {
        int kShift = IntrinsicMath.Max(kernel - stride, 0);
        int padPatternStart = kShift / 2;
        int padPatternEnd = kShift - padPatternStart;
        padBuffer.PadEdgeFrom(stream, array, padPatternStart, padPatternEnd, padPatternStart, padPatternEnd);
        return padBuffer;
    }
    public static void ApplyChunk<T, TOp>(
        Accelerator accelerator,
        AcceleratorStream stream,
        ArrayView2D<T, Stride2D.DenseX> arrayPadded,
        ArrayView2D<T, Stride2D.DenseX> arrayResult,
        int kernel,
        int stride) where T : unmanaged where TOp : IApplyChunkOp2D<TOp, T>
    {
        var kernelFunc = accelerator.LoadAutoGroupedKernel<Index2D, ArrayView2D<T, Stride2D.DenseX>, ArrayView2D<T, Stride2D.DenseX>, int, int>(ApplyChunkKernel<T, TOp>);
        kernelFunc(stream, arrayResult.IntExtent, arrayPadded, arrayResult, kernel, stride);
    }
    public static void ApplyChunk<T, TOp>(
        Accelerator accelerator,
        AcceleratorStream stream,
        ArrayView2D<T, Stride2D.DenseX> arrayPadded,
        ArrayView2D<T, Stride2D.DenseX> arrayResult,
        ArrayView<T> arrayTemp,
        int kernel,
        int stride) where T : unmanaged where TOp : IApplyChunkOp1D<TOp, T>
    {
        var kernelFunc = accelerator.LoadAutoGroupedKernel<Index2D, ArrayView2D<T, Stride2D.DenseX>, ArrayView2D<T, Stride2D.DenseX>, ArrayView<T>, int, int>(ApplyChunkKernel<T, TOp>);
        kernelFunc(stream, arrayResult.IntExtent, arrayPadded, arrayResult, arrayTemp, kernel, stride);
    }
    public static void ApplyChunkKernel<T, TOp>(
        Index2D idx,
        ArrayView2D<T, Stride2D.DenseX> arrayPadded,
        ArrayView2D<T, Stride2D.DenseX> arrayResult,
        int kernel,
        int stride) where T : unmanaged where TOp : IApplyChunkOp2D<TOp, T>
    {
        ArrayView2D<T, Stride2D.DenseX> slice = arrayPadded.SubView(new(idx.X * stride, idx.Y * stride), new(kernel));
        T v = TOp.Apply(slice);
        arrayResult[idx] = v;
    }
    public static void ApplyChunkKernel<T, TOp>(
        Index2D idx,
        ArrayView2D<T, Stride2D.DenseX> arrayPadded,
        ArrayView2D<T, Stride2D.DenseX> arrayResult,
        ArrayView<T> arrayTemp,
        int kernel,
        int stride) where T : unmanaged where TOp : IApplyChunkOp1D<TOp, T>
    {
        int srcX = idx.X * stride;
        int srcY = idx.Y * stride;
        long kernel2 = kernel * kernel;
        long offset = (idx.X + idx.Y * arrayResult.Extent.X) * kernel2;
        ArrayView<T> slice = arrayTemp.SubView(offset, kernel2);
        for (int y = 0, i = 0; y < kernel; y++)
            for (int x = 0; x < kernel; x++)
                slice[i++] = arrayPadded[srcX + x, srcY + y];
        T v = TOp.Apply(slice);
        arrayResult[idx] = v;
    }
    public static (int, int) CalculatePadSize(int xLength, int yLength, int kernel, int stride)
    {
        int kShift = IntrinsicMath.Max(kernel - stride, 0);
        return (xLength + kShift, yLength + kShift);
    }
    public static (int, int) CalculateResultSize(int xPaddedLength, int yPaddedLength, int kernel, int stride)
    {
        int xLength = xPaddedLength >= kernel ? (xPaddedLength - kernel) / stride + 1 : 0;
        int yLength = yPaddedLength >= kernel ? (yPaddedLength - kernel) / stride + 1 : 0;
        return (xLength, yLength);
    }
    public static void SortArray<T>(ArrayView<T> array) where T : unmanaged, IComparisonOperators<T, T, bool>
    {
        int L = 0, R = array.IntLength - 1;
        bool flag;
        for (int i = L; i < R; i++)
        {
            flag = true;
            for (int j = R; j > i; j--)
            {
                int iL = j - 1;
                int iR = j;
                T vL = array[iL];
                T vR = array[iR];
                if (vR < vL)
                {
                    array[iL] = vR;
                    array[iR] = vL;
                    flag = false;
                }
            }
            if (flag)
                break;
        }
    }
    public static T Sum<T>(ArrayView<T> array) where T : unmanaged, IAdditionOperators<T, T, T>
    {
        T sum = default;
        int len = array.IntLength;
        while (len-- > 0)
            sum += array[len];
        return sum;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sigmoid(float x)
    {
        return 1 / (1 + MathF.Exp(-x));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float value1, float value2, float amount)
    {
        return value1 + (value2 - value1) * amount;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vector3 x, Vector3 y)
    {
        return x.X * y.X + x.Y * y.Y + x.Z * y.Z;
    }
}