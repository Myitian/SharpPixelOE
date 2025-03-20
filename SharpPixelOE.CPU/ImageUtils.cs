using System.Runtime.CompilerServices;

namespace SharpPixelOE.CPU;

public static partial class ImageUtils
{
    public static Array2D<float> PackedBGRAToPlanarLabA(Array2D<uint> imgPackedBGRA)
    {
        Array2D<float> result = new(imgPackedBGRA.XLength, imgPackedBGRA.YLength * 4);
        ColorspaceConvert.PackedBGRAToPlanarLabA(imgPackedBGRA.Span, result.Span);
        return result;
    }
    public static Array2D<float> PackedBGRAToL(Array2D<uint> imgPackedBGRA)
    {
        Array2D<float> result = new(imgPackedBGRA.XLength, imgPackedBGRA.YLength);
        ColorspaceConvert.PackedBGRAToL(imgPackedBGRA.Span, result.Span);
        return result;
    }
    public static Array2D<uint> PlanarLabAToPackedBGRA(Array2D<float> imgPlanarLabA)
    {
        Array2D<uint> result = new(imgPlanarLabA.XLength, imgPlanarLabA.YLength / 4);
        ColorspaceConvert.PlanarLabAToPackedBGRA(imgPlanarLabA.Span, result.Span);
        return result;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CubicPolate(float v0, float v1, float v2, float v3, float f)
    {
        return v1 + 0.5f * f * (v2 - v0 + f * (2f * v0 - 5f * v1 + 4f * v2 - v3 + f * (3f * (v1 - v2) + v3 - v0)));
    }
}
