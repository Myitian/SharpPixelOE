using ILGPU;
using ILGPU.Runtime;
using System.Runtime.CompilerServices;

namespace SharpPixelOE.GPU;

public class ColorspaceConvert
{
    public static void PackedBGRAToPlanarLabA(
        Accelerator accelerator,
        AcceleratorStream stream,
        ArrayView<uint> bgra,
        ArrayView<float> laba)
    {
        stream.Synchronize();
        var kernel = accelerator.LoadAutoGroupedKernel<Index1D, ArrayView<uint>, ArrayView<float>>(PackedBGRAToPlanarLabAKernel);
        kernel(stream, bgra.IntLength, bgra, laba);
        stream.Synchronize();
    }
    public static void PackedBGRAToL(
        Accelerator accelerator,
        AcceleratorStream stream,
        ArrayView<uint> bgra,
        ArrayView<float> l)
    {
        var kernel = accelerator.LoadAutoGroupedKernel<Index1D, ArrayView<uint>, ArrayView<float>>(PackedBGRAToLKernel);
        kernel(stream, bgra.IntLength, bgra, l);
    }
    public static void PlanarLabAToPackedBGRA(
        Accelerator accelerator,
        AcceleratorStream stream,
        ArrayView<float> laba,
        ArrayView<uint> bgra)
    {
        var kernel = accelerator.LoadAutoGroupedKernel<Index1D, ArrayView<float>, ArrayView<uint>>(PlanarLabAToPackedBGRAKernel);
        kernel(stream, laba.IntLength / 4, laba, bgra);
    }
    public static void PackedBGRAToPlanarLabAKernel(Index1D i, ArrayView<uint> bgra, ArrayView<float> laba)
    {
        const float epsilon = 216f / 24389f;
        const float kappa = 24389f / 27f;
        const float Xr = 0.950470f;
        const float Zr = 1.088830f;
        Vector3 RGB2X = new(0.4124564f, 0.3575761f, 0.1804375f);
        Vector3 RGB2Y = new(0.2126729f, 0.7151522f, 0.0721750f);
        Vector3 RGB2Z = new(0.0193339f, 0.1191920f, 0.9503041f);

        ArrayView<byte> bgraSpan = bgra.Cast<byte>();
        int offset = i.X * 4;
        int length = bgra.IntLength;
        laba[length * 3 + i] = bgraSpan[offset + 3] / 255f;
        Vector3 rgb = new(
            InverseCompanding_sRGB(bgraSpan[offset + 2] / 255f),
            InverseCompanding_sRGB(bgraSpan[offset + 1] / 255f),
            InverseCompanding_sRGB(bgraSpan[offset] / 255f));
        float xr = Utils.Dot(RGB2X, rgb) / Xr;
        float yr = Utils.Dot(RGB2Y, rgb);
        float zr = Utils.Dot(RGB2Z, rgb) / Zr;
        float fx = xr > epsilon ? MathF.Pow(xr, 1f / 3f) : (kappa * xr + 16f) / 116f;
        float fy = yr > epsilon ? MathF.Pow(yr, 1f / 3f) : (kappa * yr + 16f) / 116f;
        float fz = zr > epsilon ? MathF.Pow(zr, 1f / 3f) : (kappa * zr + 16f) / 116f;
        laba[i] = 116f * fy - 16f;
        laba[length + i] = 500f * (fx - fy);
        laba[length * 2 + i] = 200f * (fy - fz);
    }
    public static void PackedBGRAToLKernel(Index1D i, ArrayView<uint> bgra, ArrayView<float> l)
    {
        const float epsilon = 216f / 24389f;
        const float kappa = 24389f / 27f;
        Vector3 RGB2Y = new(0.2126729f, 0.7151522f, 0.0721750f);

        ArrayView<byte> bgraSpan = bgra.Cast<byte>();
        int offset = i.X * 4;
        Vector3 rgb = new(
            InverseCompanding_sRGB(bgraSpan[offset + 2] / 255f),
            InverseCompanding_sRGB(bgraSpan[offset + 1] / 255f),
            InverseCompanding_sRGB(bgraSpan[offset] / 255f));
        float yr = Utils.Dot(RGB2Y, rgb);
        float fy = yr > epsilon ? MathF.Pow(yr, 1f / 3f) : (kappa * yr + 16f) / 116f;
        l[i] = 116f * fy - 16f;
    }
    public static void PlanarLabAToPackedBGRAKernel(Index1D i, ArrayView<float> laba, ArrayView<uint> bgra)
    {
        const float epsilon = 216f / 24389f;
        const float kappa = 24389f / 27f;
        const float Xr = 0.950470f;
        const float Zr = 1.088830f;
        Vector3 XYZ2R = new(3.2404542f, -1.5371385f, -0.4985314f);
        Vector3 XYZ2G = new(-0.9692660f, 1.8760108f, 0.0415560f);
        Vector3 XYZ2B = new(0.0556434f, -0.2040259f, 1.0572252f);

        ArrayView<byte> bgraSpan = bgra.Cast<byte>();
        int offset = i.X * 4;
        int length = laba.IntLength / 4;
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
        bgraSpan[offset] = (byte)IntrinsicMath.Clamp(Companding_sRGB(Utils.Dot(XYZ2B, xyz)) * 255f, 0f, 255f);
        bgraSpan[offset + 1] = (byte)IntrinsicMath.Clamp(Companding_sRGB(Utils.Dot(XYZ2G, xyz)) * 255f, 0f, 255f);
        bgraSpan[offset + 2] = (byte)IntrinsicMath.Clamp(Companding_sRGB(Utils.Dot(XYZ2R, xyz)) * 255f, 0f, 255f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float InverseCompanding_sRGB(float v)
    {
        return v <= 0.04045f ? v / 12.92f : MathF.Pow((v + 0.055f) / 1.055f, 2.4f);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Companding_sRGB(float v)
    {
        return v <= 0.0031308f ? v * 12.92f : MathF.Pow(v, 1f / 2.4f) * 1.055f - 0.055f;
    }
}
