using ILGPU;
using ILGPU.Runtime;

namespace SharpPixelOE.GPU;

public static partial class DownscaleMethod
{
    public static Array2D<float> Center(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<float> imgPlanarLabA,
        int patchSize)
    {
        int width = imgPlanarLabA.XLength;
        int height = imgPlanarLabA.YLength / 4;
        (int resultWidth, int resultHeight) = Utils.CalculateResultSize(width, height, patchSize, patchSize);
        Array2D<float> resultLabA = new(accelerator, resultWidth, resultHeight * 4);
        // L
        using AcceleratorStream sL = accelerator.CreateStream();
        ArrayView2D<float, Stride2D.DenseX> srcChannel = imgPlanarLabA.SliceY(0, height);
        ArrayView2D<float, Stride2D.DenseX> dstChannel = resultLabA.SliceY(0, resultHeight);
        Utils.ApplyChunk<float, Utils.MiddleOp<float>>(accelerator, sL, srcChannel, dstChannel, patchSize, patchSize);
        // A
        using AcceleratorStream sAlpha = accelerator.CreateStream();
        srcChannel = imgPlanarLabA.SliceY(height * 3, height);
        dstChannel = resultLabA.SliceY(resultHeight * 3, resultHeight);
        Utils.ApplyChunk<float, Utils.MiddleOp<float>>(accelerator, sAlpha, srcChannel, dstChannel, patchSize, patchSize);
        // a, b
        using (MemoryBuffer1D<float, Stride1D.Dense> tmp = accelerator.Allocate1D<float>(dstChannel.Length * patchSize * patchSize))
        {
            // a
            srcChannel = imgPlanarLabA.SliceY(height, height);
            dstChannel = resultLabA.SliceY(resultHeight, resultHeight);
            Utils.ApplyChunk<float, Utils.MedianOp<float>>(accelerator, stream, srcChannel, dstChannel, tmp.View.BaseView, patchSize, patchSize);
            // b
            srcChannel = imgPlanarLabA.SliceY(height * 2, height);
            dstChannel = resultLabA.SliceY(resultHeight * 2, resultHeight);
            Utils.ApplyChunk<float, Utils.MedianOp<float>>(accelerator, stream, srcChannel, dstChannel, tmp.View.BaseView, patchSize, patchSize);
            stream.Synchronize();
        }
        sL.Synchronize();
        sAlpha.Synchronize();

        return resultLabA;
    }
}
