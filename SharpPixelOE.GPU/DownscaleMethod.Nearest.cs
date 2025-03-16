using ILGPU.Runtime;

namespace SharpPixelOE.GPU;

public static partial class DownscaleMethod
{
    public static Array2D<float> Nearest(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<float> imgPlanarLabA,
        int patchSize)
    {
        int width = imgPlanarLabA.XLength;
        int height = imgPlanarLabA.YLength / 4;
        (int paddedWidth, int paddedHeight) = Utils.CalculatePadSize(width, height, patchSize, patchSize);
        (int resultWidth, int resultHeight) = Utils.CalculateResultSize(paddedWidth, paddedHeight, patchSize, patchSize);
        return ImageUtils.ResizePlanar4xFP32(accelerator, stream, imgPlanarLabA, resultWidth, resultHeight, InterpolationMethod.Nearest);
    }
}
