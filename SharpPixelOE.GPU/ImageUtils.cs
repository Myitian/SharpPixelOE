using ILGPU.Runtime;
using System.Runtime.CompilerServices;

namespace SharpPixelOE.GPU;

public static partial class ImageUtils
{
    public static Array2D<float> PackedBGRAToPlanarLabA(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<uint> imgPackedBGRA)
    {
        Array2D<float> result = new(accelerator, imgPackedBGRA.XLength, imgPackedBGRA.YLength * 4);
        ColorspaceConvert.PackedBGRAToPlanarLabA(accelerator, stream, imgPackedBGRA.RawView, result.RawView);
        return result;
    }
    public static Array2D<float> PackedBGRAToL(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<uint> imgPackedBGRA)
    {
        Array2D<float> result = new(accelerator, imgPackedBGRA.XLength, imgPackedBGRA.YLength);
        ColorspaceConvert.PackedBGRAToL(accelerator, stream, imgPackedBGRA.RawView, result.RawView);
        return result;
    }
    public static Array2D<uint> PlanarLabAToPackedBGRA(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<float> imgPlanarLabA)
    {
        Array2D<uint> result = new(accelerator, imgPlanarLabA.XLength, imgPlanarLabA.YLength / 4);
        ColorspaceConvert.PlanarLabAToPackedBGRA(accelerator, stream, imgPlanarLabA.RawView, result.RawView);
        return result;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CubicPolate(float v0, float v1, float v2, float v3, float f)
    {
        return v1 + 0.5f * f * (v2 - v0 + f * (2f * v0 - 5f * v1 + 4f * v2 - v3 + f * (3f * (v1 - v2) + v3 - v0)));
    }
}
