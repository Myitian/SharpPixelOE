using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpPixelOE;

public class ColorspaceConvert
{
    public static readonly Vector3 RGB2X = new(0.4124564f, 0.3575761f, 0.1804375f);
    public static readonly Vector3 RGB2Y = new(0.2126729f, 0.7151522f, 0.0721750f);
    public static readonly Vector3 RGB2Z = new(0.0193339f, 0.1191920f, 0.9503041f);
    public static readonly Vector3 XYZ2R = new(3.2404542f, -1.5371385f, -0.4985314f);
    public static readonly Vector3 XYZ2G = new(-0.9692660f, 1.8760108f, 0.0415560f);
    public static readonly Vector3 XYZ2B = new(0.0556434f, -0.2040259f, 1.0572252f);
    public static void PackedBGRAToPlanarLabA(scoped ReadOnlySpan<uint> bgra, scoped Span<float> laba)
    {
        int length = bgra.Length;
        for (int i = 0; i < length; i++)
            PackedBGRAToPlanarLabAKernel(i, bgra, laba);
    }
    public static void PackedBGRAToL(scoped ReadOnlySpan<uint> bgra, scoped Span<float> laba)
    {
        int length = bgra.Length;
        for (int i = 0; i < length; i++)
            PackedBGRAToLKernel(i, bgra, laba);
    }
    public static void PlanarLabAToPackedBGRA(scoped ReadOnlySpan<float> laba, scoped Span<uint> bgra)
    {
        int length = bgra.Length;
        for (int i = 0; i < length; i++)
            PlanarLabAToPackedBGRAKernel(i, laba, bgra);
    }
    public static void PackedBGRAToPlanarLabAKernel(int i, scoped ReadOnlySpan<uint> bgra, scoped Span<float> laba)
    {
        const float epsilon = 216f / 24389f;
        const float kappa = 24389f / 27f;
        const float Xr = 0.950470f;
        const float Zr = 1.088830f;
        ReadOnlySpan<byte> bgraSpan = MemoryMarshal.AsBytes(bgra);
        int offset = i * 4;
        int length = bgraSpan.Length / 4;
        laba[length * 3 + i] = bgraSpan[offset + 3] / 255f;
        Vector3 rgb = new(
            InverseCompanding_sRGB(bgraSpan[offset + 2] / 255f),
            InverseCompanding_sRGB(bgraSpan[offset + 1] / 255f),
            InverseCompanding_sRGB(bgraSpan[offset] / 255f));
        float xr = Vector3.Dot(RGB2X, rgb) / Xr;
        float yr = Vector3.Dot(RGB2Y, rgb);
        float zr = Vector3.Dot(RGB2Z, rgb) / Zr;
        float fx = xr > epsilon ? MathF.Cbrt(xr) : (kappa * xr + 16f) / 116f;
        float fy = yr > epsilon ? MathF.Cbrt(yr) : (kappa * yr + 16f) / 116f;
        float fz = zr > epsilon ? MathF.Cbrt(zr) : (kappa * zr + 16f) / 116f;
        laba[i] = 116f * fy - 16f;
        laba[length + i] = 500f * (fx - fy);
        laba[length * 2 + i] = 200f * (fy - fz);
    }
    public static void PackedBGRAToLKernel(int i, scoped ReadOnlySpan<uint> bgra, scoped Span<float> l)
    {
        const float epsilon = 216f / 24389f;
        const float kappa = 24389f / 27f;
        ReadOnlySpan<byte> bgraSpan = MemoryMarshal.AsBytes(bgra);
        int offset = i * 4;
        Vector3 rgb = new(
            InverseCompanding_sRGB(bgraSpan[offset + 2] / 255f),
            InverseCompanding_sRGB(bgraSpan[offset + 1] / 255f),
            InverseCompanding_sRGB(bgraSpan[offset] / 255f));
        float yr = Vector3.Dot(RGB2Y, rgb);
        float fy = yr > epsilon ? MathF.Cbrt(yr) : (kappa * yr + 16f) / 116f;
        l[i] = 116f * fy - 16f;
    }
    public static void PlanarLabAToPackedBGRAKernel(int i, scoped ReadOnlySpan<float> laba, scoped Span<uint> bgra)
    {
        const float epsilon = 216f / 24389f;
        const float kappa = 24389f / 27f;
        const float Xr = 0.950470f;
        const float Zr = 1.088830f;
        Span<byte> bgraSpan = MemoryMarshal.AsBytes(bgra);
        int offset = i * 4;
        int length = laba.Length / 4;
        bgraSpan[offset + 3] = (byte)(laba[length * 3 + i] * 255f);
        float L = laba[i];
        float fy = (L + 16f) / 116f;
        float fx = laba[length + i] / 500f + fy;
        float fz = fy - laba[length * 2 + i] / 200f;
        float fx3 = fx * fx * fx;
        float fy3 = fy * fy * fy;
        float fz3 = fz * fz * fz;
        float xr = fx3 > epsilon ? fx3 : (116f * fx - 16f) / kappa;
        float yr = fy3 > epsilon ? fy3 : L / kappa;
        float zr = fz3 > epsilon ? fz3 : (116f * fx - 16f) / kappa;
        Vector3 xyz = new(xr * Xr, yr, zr * Zr);
        bgraSpan[offset] = (byte)Math.Clamp(Companding_sRGB(Vector3.Dot(XYZ2B, xyz)) * 255f, 0f, 255f);
        bgraSpan[offset + 1] = (byte)Math.Clamp(Companding_sRGB(Vector3.Dot(XYZ2G, xyz)) * 255f, 0f, 255f);
        bgraSpan[offset + 2] = (byte)Math.Clamp(Companding_sRGB(Vector3.Dot(XYZ2R, xyz)) * 255f, 0f, 255f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static float InverseCompanding_sRGB(float v)
    {
        return v <= 0.04045f ? v / 12.92f : MathF.Pow((v + 0.055f) / 1.055f, 2.4f);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static float Companding_sRGB(float v)
    {
        return v <= 0.0031308f ? v * 12.92f : MathF.Pow(v, 1f / 2.4f) * 1.055f - 0.055f;
    }
}
