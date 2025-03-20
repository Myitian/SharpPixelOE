using ILGPU;
using ILGPU.Runtime;

namespace SharpPixelOE.GPU;

public static partial class DownscaleMethod
{
    public static Array2D<float> ContrastBased(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<float> imgPlanarLabA,
        int patchSize)
    {
        int width = imgPlanarLabA.XLength;
        int height = imgPlanarLabA.YLength / 4;
        (int resultWidth, int resultHeight) = Utils.CalculateResultSize(width, height, patchSize, patchSize);
        Array2D<float> resultLabA = new(accelerator, resultWidth, resultHeight * 4);

        using (MemoryBuffer1D<float, Stride1D.Dense> tmp = accelerator.Allocate1D<float>((long)width * height * patchSize * patchSize))
        {
            stream.Synchronize();
            ArrayView<float> tmp1D = tmp.View.BaseView;
            // L
            ArrayView2D<float, Stride2D.DenseX> srcChannel = imgPlanarLabA.SliceY(0, height);
            ArrayView2D<float, Stride2D.DenseX> dstChannel = resultLabA.SliceY(0, resultHeight);
            Utils.ApplyChunk<float, Utils.FindPixelOp>(accelerator, stream, srcChannel, dstChannel, tmp1D, patchSize, patchSize);
            // a
            srcChannel = imgPlanarLabA.SliceY(height, height);
            dstChannel = resultLabA.SliceY(resultHeight, resultHeight);
            Utils.ApplyChunk<float, Utils.MedianOp<float>>(accelerator, stream, srcChannel, dstChannel, tmp1D, patchSize, patchSize);
            // b
            srcChannel = imgPlanarLabA.SliceY(height * 2, height);
            dstChannel = resultLabA.SliceY(resultHeight * 2, resultHeight);
            Utils.ApplyChunk<float, Utils.MedianOp<float>>(accelerator, stream, srcChannel, dstChannel, tmp1D, patchSize, patchSize);
            // A
            srcChannel = imgPlanarLabA.SliceY(height * 3, height);
            dstChannel = resultLabA.SliceY(resultHeight * 3, resultHeight);
            Utils.ApplyChunk<float, Utils.FindPixelOp>(accelerator, stream, srcChannel, dstChannel, tmp1D, patchSize, patchSize);
            stream.Synchronize();
        }
        return resultLabA;
    }
}
