using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.Runtime;

namespace SharpPixelOE.GPU;

class Outline
{
    public static Array2D<float> ExpansionWeight(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<uint> imgPackedBGRA,
        int k = 8,
        int stride = 2,
        float avgScale = 10,
        float distScale = 3)
    {
        stride = Math.Max(stride, 1);
        int width = imgPackedBGRA.XLength;
        int height = imgPackedBGRA.YLength;
        int size = width * height;
        (int paddedMMWidth, int paddedMMHeight) = Utils.CalculatePadSize(width, height, k, stride);
        (int resultWidth, int resultHeight) = Utils.CalculateResultSize(paddedMMWidth, paddedMMHeight, k, stride);

        Array2D<float> result;
        //!!!Array2D<float> avgL = new(accelerator, resultWidth, resultHeight);
        //!!!Array2D<float> minL = new(accelerator, resultWidth, resultHeight);
        //!!!Array2D<float> maxL = new(accelerator, resultWidth, resultHeight);
        //!!!Array2D<float> imgL = ImageUtils.PackedBGRAToL(accelerator, stream, imgPackedBGRA).Divide(stream, 100f);
        using (Array2D<float> avgL = new(accelerator, resultWidth, resultHeight))
        using (Array2D<float> minL = new(accelerator, resultWidth, resultHeight))
        using (Array2D<float> maxL = new(accelerator, resultWidth, resultHeight))
        {
            using (Array2D<float> imgL = ImageUtils.PackedBGRAToL(accelerator, stream, imgPackedBGRA).DivideBy(stream, 100f))
            using (AcceleratorStream mmStream = accelerator.CreateStream())
            {
                stream.Synchronize();
                //!!!using Array2D<float> mmPadBuffer = Utils.ApplyChunkPad(accelerator, mmStream, imgL.View, k, stride);
                using Array2D<float> mmPadBuffer = Utils.ApplyChunkPad(accelerator, mmStream, imgL.View, k, stride);
                mmStream.Synchronize();
                //!!!return mmPadBuffer;
                Utils.ApplyChunk<float, Utils.MinOp<float>>(accelerator, mmStream, mmPadBuffer.View, minL.View, k, stride);
                Utils.ApplyChunk<float, Utils.MaxOp<float>>(accelerator, mmStream, mmPadBuffer.View, maxL.View, k, stride);

                using Array2D<float> avgPadBuffer = Utils.ApplyChunkPad(accelerator, stream, imgL.View, k * 2, stride);
                using MemoryBuffer1D<float, Stride1D.Dense> tmp1D = accelerator.Allocate1D<float>(avgL.Length * k * k * 4);
                Utils.ApplyChunk<float, Utils.MedianOp<float>>(accelerator, stream, avgPadBuffer.View, avgL.View, tmp1D.View.BaseView, k * 2, stride);
                mmStream.Synchronize();
                stream.Synchronize();
            }
            using Array2D<float> weight = new(accelerator, resultWidth, resultHeight);
            var kernel_1 = accelerator.LoadAutoGroupedKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>, float, float>(ExpansionWeightKernel_1);
            kernel_1(stream, weight.Length, avgL.RawView, minL.RawView, maxL.RawView, weight.RawView, avgScale, distScale);
            result = ImageUtils.ResizeSimpleFP32(accelerator, stream, weight, width, height, InterpolationMethod.Nearest);
            stream.Synchronize();
        }
        using Array2D<float> tmp = ImageUtils.ResizeSimpleFP32(accelerator, stream, result, width / 2, height / 2, InterpolationMethod.Bilinear);
        ImageUtils.ResizeSimpleFP32To(accelerator, stream, tmp, result, InterpolationMethod.Bilinear);

        float min = accelerator.Reduce<float, MinFloat>(stream, result.RawView);
        float max = accelerator.Reduce<float, MaxFloat>(stream, result.RawView);
        var kernel_2 = accelerator.LoadAutoGroupedKernel<Index1D, ArrayView<float>, float, float>(ExpansionWeightKernel_2);
        kernel_2(stream, result.Length, result.RawView, min, max);
        return result;
    }
    public static void ExpansionWeightKernel_1(
        Index1D idx,
        ArrayView<float> avgL,
        ArrayView<float> minL,
        ArrayView<float> maxL,
        ArrayView<float> arrayWeight,
        float avgScale,
        float distScale)
    {
        float avgLv = avgL[idx];
        float minLv = minL[idx];
        float maxLv = maxL[idx];
        float brightDist = maxLv - avgLv;
        float darkDist = avgLv - minLv;
        float weight = (avgLv - 0.5f) * avgScale - (brightDist - darkDist) * distScale;
        arrayWeight[idx] = Utils.Sigmoid(weight);
    }
    public static void ExpansionWeightKernel_2(
        Index1D idx,
        ArrayView<float> weight,
        float min,
        float max)
    {
        weight[idx] = (weight[idx] - min) / max;
    }

    public readonly static Array3x3<bool> KernelExpansion = new([true, true, true, true, true, true, true, true, true]);
    public readonly static Array3x3<bool> KernelSmoothing = new([false, true, false, true, true, true, false, true, false]);
    public static (Array2D<uint>, Array2D<float>) OutlineExpansion(
        Accelerator accelerator,
        AcceleratorStream stream,
        Array2D<uint> imgPackedBGRA,
        int erode = 2,
        int dilate = 2,
        int k = 16,
        float avg_scale = 10,
        float dist_scale = 3)
    {
        Array2D<uint>? imgErode, imgDilate;
        Array2D<float> weight;

        Array2D<uint>? tmp = new(accelerator, imgPackedBGRA.XLength, imgPackedBGRA.YLength);
        using (AcceleratorStream streamE = accelerator.CreateStream())
        using (AcceleratorStream streamD = accelerator.CreateStream())
        {
            stream.Synchronize();
            weight = ExpansionWeight(accelerator, stream, imgPackedBGRA, k, k / 4 * 2, avg_scale, dist_scale);

            imgErode = imgPackedBGRA.Copy(streamE);
            if (ImageUtils.Morph(accelerator, streamE, imgErode, tmp, in KernelExpansion, ImageUtils.ErodePacked4xU8Kernel, erode))
                (imgErode, tmp) = (tmp, imgErode);

            Array2D<uint> tmpD = new(accelerator, imgPackedBGRA.XLength, imgPackedBGRA.YLength);
            imgDilate = imgPackedBGRA.Copy(streamD);
            if (ImageUtils.Morph(accelerator, streamD, imgDilate, tmpD, in KernelExpansion, ImageUtils.DilatePacked4xU8Kernel, dilate))
                (imgDilate, tmpD) = (tmpD, imgDilate);
            tmpD.Dispose();

            streamE.Synchronize();
            streamD.Synchronize();
        }

        Array2D<uint> output = new(accelerator, imgPackedBGRA.XLength, imgPackedBGRA.YLength);
        //!!!Array2D<uint> output = new(accelerator, weight.XLength, weight.YLength);
        //!!!var test = accelerator.LoadAutoGroupedKernel<Index1D, ArrayView<float>, ArrayView<uint>>((i, f, u) =>
        //!!!{
        //!!!    float ff = f[i];
        //!!!    uint b = (byte)IntrinsicMath.Clamp(ff * 255, 0, 255);
        //!!!    u[i] = b | (b << 8) | (b << 16) | 0xFF000000u;
        //!!!});
        //!!!test(stream, output.Length, weight.RawView, output.RawView);
        //!!!return (output, weight);

        var kernel_1 = accelerator.LoadAutoGroupedKernel<Index1D, ArrayView<uint>, ArrayView<uint>, ArrayView<uint>, ArrayView<float>, ArrayView<uint>>(OutlineExpansionKernel_1);
        kernel_1(stream, output.Length, imgErode.RawView, imgDilate.RawView, imgPackedBGRA.RawView, weight.RawView, output.RawView);
        //!!!return (output, weight);
        if (ImageUtils.Morph(accelerator, stream, output, tmp, in KernelSmoothing, ImageUtils.ErodePacked4xU8Kernel, erode))
            (output, tmp) = (tmp, output);
        if (ImageUtils.Morph(accelerator, stream, output, tmp, in KernelSmoothing, ImageUtils.DilatePacked4xU8Kernel, dilate * 2))
            (output, tmp) = (tmp, output);
        if (ImageUtils.Morph(accelerator, stream, output, tmp, in KernelSmoothing, ImageUtils.ErodePacked4xU8Kernel, erode))
            (output, tmp) = (tmp, output);
        tmp.Dispose();
        tmp = null;
        imgErode.Dispose();
        imgErode = null;
        imgDilate.Dispose();
        imgDilate = null;

        using Array2D<byte> tmpA = new(accelerator, imgPackedBGRA.XLength, imgPackedBGRA.YLength);
        using Array2D<byte> tmpB = new(accelerator, imgPackedBGRA.XLength, imgPackedBGRA.YLength);
        var kernel_2 = accelerator.LoadAutoGroupedKernel<Index1D, ArrayView<float>, ArrayView<byte>>(OutlineExpansionKernel_2);
        kernel_2(stream, weight.Length, weight.RawView, tmpA.RawView);
        bool isB = ImageUtils.Morph(accelerator, stream, tmpA, tmpB, in KernelExpansion, ImageUtils.DilateU8Kernel, dilate);
        var kernel_3 = accelerator.LoadAutoGroupedKernel<Index1D, ArrayView<byte>, ArrayView<float>>(OutlineExpansionKernel_3);
        kernel_3(stream, weight.Length, isB ? tmpB.RawView : tmpA.RawView, weight.RawView);
        return (output, weight);
    }
    private static void OutlineExpansionKernel_1(
        Index1D idx,
        ArrayView<uint> imgErode,
        ArrayView<uint> imgDilate,
        ArrayView<uint> img,
        ArrayView<float> weight,
        ArrayView<uint> output)
    {
        ArrayView<byte> imgErodeB = imgErode.Cast<byte>();
        ArrayView<byte> imgDilateB = imgDilate.Cast<byte>();
        ArrayView<byte> imgB = img.Cast<byte>();
        ArrayView<byte> outputB = output.Cast<byte>();
        float weightF = weight[idx];
        float origWeight = Utils.Sigmoid((weightF - 0.5f) * 5f) * 0.25f;
        int i = idx.X * 4;
        int a = i, b = i + 1, c = i + 2, d = i + 3;
        outputB[a] = (byte)((imgErodeB[a] * weightF + imgDilateB[a] * (1f - weightF)) * (1f - origWeight) + imgB[a] * origWeight);
        outputB[b] = (byte)((imgErodeB[b] * weightF + imgDilateB[b] * (1f - weightF)) * (1f - origWeight) + imgB[b] * origWeight);
        outputB[c] = (byte)((imgErodeB[c] * weightF + imgDilateB[c] * (1f - weightF)) * (1f - origWeight) + imgB[c] * origWeight);
        outputB[d] = (byte)((imgErodeB[d] * weightF + imgDilateB[d] * (1f - weightF)) * (1f - origWeight) + imgB[d] * origWeight);
    }
    private static void OutlineExpansionKernel_2(
        Index1D idx,
        ArrayView<float> weight,
        ArrayView<byte> weightU8)
    {
        weightU8[idx] = (byte)(IntrinsicMath.Abs(weight[idx] * 2 - 1) * 255);
    }
    private static void OutlineExpansionKernel_3(
        Index1D idx,
        ArrayView<byte> weightU8,
        ArrayView<float> weight)
    {
        weight[idx] = weightU8[idx] / 255f;
    }
}
