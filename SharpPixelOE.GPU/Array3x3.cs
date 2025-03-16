using System.Runtime.CompilerServices;

namespace SharpPixelOE.GPU;

[InlineArray(9)]
public struct Array3x3<T> where T : struct
{
    private T _;
    public T this[int x, int y]
    {
        readonly get => this[y * 3 + x];
        set => this[y * 3 + x] = value;
    }

    public Array3x3(params scoped ReadOnlySpan<T> span)
    {
        span.CopyTo(this);
    }
}