using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;

namespace SharpPixelOE.CPU;

internal static class Utils
{
    public static Array2D<T> ApplyChunkPad<T>(
        Array2D<T> array,
        int kernel,
        int stride) where T : unmanaged
    {
        int kShift = Math.Max(kernel - stride, 0);
        int padPatternStart = kShift / 2;
        int padPatternEnd = kShift - padPatternStart;
        Array2D<T> padBuffer = new(array.XLength + kShift, array.YLength + kShift);
        padBuffer.PadEdgeFrom(array, padPatternStart, padPatternEnd, padPatternStart, padPatternEnd);
        return padBuffer;
    }
    public static void ApplyChunk<T>(Array2D<T> array2D, Array2D<T> arrayResult, Array2D<T> padBuffer, Span<T> sliceBuffer, int kernel, int stride, Func<Span<T>, T> func)
    {
        int kShift = Math.Max(kernel - stride, 0);
        int padPatternStart = kShift / 2;
        int padPatternEnd = kShift / 2 + kShift % 2;
        padBuffer.PadEdgeFrom(array2D, padPatternStart, padPatternEnd, padPatternStart, padPatternEnd);
        for (int y = 0, yi = 0, yEnd = padBuffer.YLength - kernel; y <= yEnd; y += stride, yi++)
        {
            for (int x = 0, xi = 0, xEnd = padBuffer.XLength - kernel; x <= xEnd; x += stride, xi++)
            {
                padBuffer.GetFlatSlice(sliceBuffer, x, y, kernel, kernel);
                T v = func(sliceBuffer[..(kernel * kernel)]);
                arrayResult[xi, yi] = v;
            }
        }
    }
    public static (int, int) CalculatePadSize(int xLength, int yLength, int kernel, int stride)
    {
        int kShift = Math.Max(kernel - stride, 0);
        return (xLength + kShift, yLength + kShift);
    }
    public static (int, int) CalculateResultSize(int xPaddedLength, int yPaddedLength, int kernel, int stride)
    {
        int xLength = xPaddedLength >= kernel ? (xPaddedLength - kernel) / stride + 1 : 0;
        int yLength = yPaddedLength >= kernel ? (yPaddedLength - kernel) / stride + 1 : 0;
        return (xLength, yLength);
    }
    public static T FindPixel<T>(scoped Span<T> chunks) where T : INumber<T>
    {
        int half = chunks.Length / 2;
        T mid = chunks[half];
        chunks.Sort();
        T med = chunks[half];
        T mu = TensorPrimitives.Sum<T>(chunks) / T.CreateSaturating(chunks.Length);
        T min = chunks[0];
        T max = chunks[^1];
        T maxi_med = max - med;
        T med_mini = med - min;
        if (med < mu && maxi_med > med_mini)
            return min;
        if (med > mu && maxi_med < med_mini)
            return max;
        return mid;
    }
    public static T Max<T>(scoped Span<T> chunks) where T : INumber<T>
    {
        return TensorPrimitives.Max<T>(chunks);
    }
    public static T Median<T>(scoped Span<T> chunks)
    {
        chunks.Sort();
        return chunks[chunks.Length / 2];
    }
    public static T Middle<T>(scoped Span<T> chunks)
    {
        return chunks[chunks.Length / 2];
    }
    public static T Min<T>(scoped Span<T> chunks) where T : INumber<T>
    {
        return TensorPrimitives.Min<T>(chunks);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sigmoid(float x)
    {
        return 1 / (1 + MathF.Exp(-x));
    }
}