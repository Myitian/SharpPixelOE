using System.Text;

namespace SharpPixelOE.CPU;

public class Array2D<T>
{
    public readonly T[]? UnderlyingArray;
    public readonly Memory<T> Memory;
    public readonly int XLength;
    public readonly int YLength;
    public readonly int Length;

    public Span<T> Span
        => Memory.Span;
    public Span<T> this[int y]
        => Memory.Span.Slice(y * XLength, XLength);
    public T this[int x, int y]
    {
        get => Memory.Span[y * XLength + x];
        set => Memory.Span[y * XLength + x] = value;
    }
    public Slice RootSlice => new(this, 0, 0, XLength, YLength);

    public Array2D(int xLen, int yLen)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xLen);
        ArgumentOutOfRangeException.ThrowIfNegative(yLen);
        long size = (long)xLen * yLen;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, int.MaxValue);
        Length = (int)size;
        Memory = UnderlyingArray = GC.AllocateUninitializedArray<T>(Length);
        XLength = xLen;
        YLength = yLen;
    }
    public Array2D(int xLen, int yLen, Memory<T> memory)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xLen);
        ArgumentOutOfRangeException.ThrowIfNegative(yLen);
        long size = (long)xLen * yLen;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, memory.Length);
        Length = (int)size;
        Memory = memory[..Length];
        XLength = xLen;
        YLength = yLen;
    }
    public Array2D(int xLen, int yLen, T[] array) : this(xLen, yLen, array.AsMemory())
    {
        UnderlyingArray = array;
    }

    public void Clear()
    {
        Span.Clear();
    }
    public void Fill(T value)
    {
        Span.Fill(value);
    }
    public void PadEdgeFrom(Array2D<T> src, int padWidthXStart, int padWidthXEnd, int padWidthYStart, int padWidthYEnd)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(padWidthXStart);
        ArgumentOutOfRangeException.ThrowIfNegative(padWidthXEnd);
        ArgumentOutOfRangeException.ThrowIfNegative(padWidthYStart);
        ArgumentOutOfRangeException.ThrowIfNegative(padWidthYEnd);
        ArgumentOutOfRangeException.ThrowIfNotEqual(padWidthXStart + src.XLength + padWidthXEnd, XLength);
        ArgumentOutOfRangeException.ThrowIfNotEqual(padWidthYStart + src.YLength + padWidthYEnd, YLength);
        if ((padWidthXStart | padWidthXEnd | padWidthYStart | padWidthYEnd) == 0)
        {
            if (!src.Span.Overlaps(Span, out int offset) || offset != 0)
                src.Span.CopyTo(Span);
            return;
        }
        if (src.XLength == 0 || src.YLength == 0 || XLength == 0 || YLength == 0)
        {
            Span.Clear();
            return;
        }
        for (int y = 0; y < src.YLength; y++)
        {
            Span<T> line = this[padWidthYStart + y];
            Span<T> main = line.Slice(padWidthXStart, src.XLength);
            src[y].CopyTo(main);
            line[..padWidthXStart].Fill(main[0]);
            line[(padWidthXStart + src.XLength)..].Fill(main[^1]);
        }
        ReadOnlySpan<T> firstLine = this[padWidthYStart];
        for (int y = 0; y < padWidthYStart; y++)
        {
            firstLine.CopyTo(this[y]);
        }
        ReadOnlySpan<T> lastLine = this[padWidthYStart + src.YLength - 1];
        for (int y = padWidthYStart + src.YLength; y < YLength; y++)
        {
            lastLine.CopyTo(this[y]);
        }
    }
    public Slice GetSlice(int xOffset, int yOffset, int xLength, int yLength)
    {
        return new(this, xOffset, yOffset, xLength, yLength);
    }
    public ReadOnlySpan<T> GetFlatSlice(int xOffset, int yOffset, int xLength, int yLength)
    {
        if (xLength == 0 || yLength == 0)
            return [];
        if (yLength == 1)
            return this[yOffset].Slice(xOffset, xLength);
        T[] array = GC.AllocateUninitializedArray<T>(xLength * yLength);
        for (int y = 0; y < yLength; y++)
            this[yOffset + y].Slice(xOffset, xLength).CopyTo(array.AsSpan(y * xLength));
        return array;
    }
    public bool GetFlatSlice(Span<T> destination, int xOffset, int yOffset, int xLength, int yLength)
    {
        if (destination.Length < xLength * yLength)
            return false;
        for (int y = 0; y < yLength; y++)
            this[yOffset + y].Slice(xOffset, xLength).CopyTo(destination[(y * xLength)..]);
        return true;
    }
    public Array2D<T> Copy()
    {
        Array2D<T> result = new(XLength, YLength);
        Memory.CopyTo(result.Memory);
        return result;
    }

    public override string ToString()
    {
        const int threshold = 8;
        const int left = threshold / 2;
        const int right = threshold - left;

        if (YLength == 0)
            return $"Array2D[{XLength}, {YLength}][]";
        StringBuilder sb = new("Array2D");
        sb.Append($"[{XLength}, {YLength}][");
        if (YLength > threshold)
        {
            for (int y = 0; y < left; y++)
            {
                AppendRow(sb, y);
            }
            sb.AppendLine().Append("...");
            for (int y = YLength - right; y < YLength; y++)
            {
                AppendRow(sb, y);
            }
        }
        else
        {
            for (int y = 0; y < YLength; y++)
            {
                AppendRow(sb, y);
            }
        }
        return sb.AppendLine().Append(']').ToString();

        void AppendRow(StringBuilder sb, int y)
        {
            const int threshold = 8;
            const int left = threshold / 2;
            const int right = threshold - left;
            if (XLength > threshold)
            {
                for (int x = 0; x < left; x++)
                {
                    if (x == 0)
                        sb.AppendLine().Append('[');
                    else
                        sb.Append(", ");
                    sb.Append(this[x, y]);
                }
                sb.Append(", ... ");
                for (int x = XLength - right; x < XLength; x++)
                {
                    sb.Append(", ").Append(this[x, y]);
                }
                sb.Append(']');
            }
            else
            {
                for (int x = 0; x < XLength; x++)
                {
                    if (x == 0)
                        sb.AppendLine().Append('[');
                    else
                        sb.Append(", ");
                    sb.Append(this[x, y]);
                }
                sb.Append(']');
            }
        }
    }

    public readonly struct Slice(Array2D<T> array2D, int xOffset, int yOffset, int xLength, int yLength)
    {
        public readonly Array2D<T> Array2D = array2D;
        public readonly int XOffset = xOffset;
        public readonly int YOffset = yOffset;
        public readonly int XLength = xLength;
        public readonly int YLength = yLength;

        public readonly Span<T> this[int y] // Array2D[y + YOffset].Slice(XOffset, XLength);
            => Array2D.Memory.Span.Slice((y + YOffset) * Array2D.XLength + XOffset, XLength);
        public readonly T this[int x, int y] // this[y][x];
            => Array2D.Memory.Span[(y + YOffset) * Array2D.XLength + XOffset + x];
        public readonly T At(int index)
        {
            (int y, int x) = int.DivRem(index, XLength);
            return this[x, y];
        }
        public readonly void Fill(T value)
        {
            for (int y = 0; y < YLength; y++)
                this[y].Fill(value);
        }
    }
}