using System.Numerics.Tensors;
using System.Runtime.InteropServices;

namespace SharpPixelOE.CPU;

class Outline
{
    public static Array2D<float> ExpansionWeight(
        Array2D<uint> imgPackedBGRA,
        int k = 8,
        int stride = 2,
        float avg_scale = 10,
        float dist_scale = 3)
    {
        stride = Math.Max(stride, 1);
        int width = imgPackedBGRA.XLength;
        int height = imgPackedBGRA.YLength;
        int size = width * height;
        Array2D<float> imgL = ImageUtils.PackedBGRAToL(imgPackedBGRA);
        TensorPrimitives.Divide(imgL.Span, 100, imgL.Span);
        (int paddedMMWidth, int paddedMMHeight) = Utils.CalculatePadSize(width, height, k, stride);
        (int resultWidth, int resultHeight) = Utils.CalculateResultSize(paddedMMWidth, paddedMMHeight, k, stride);
        int sliceSize = k * k * 4;
        Span<float> sliceBuffer = sliceSize <= 1024 ?
            stackalloc float[sliceSize] :
            GC.AllocateUninitializedArray<float>(sliceSize);

        Array2D<float> avgPadBuffer = Utils.ApplyChunkPad(imgL, k * 2, stride);
        Array2D<float> avgL = new(resultWidth, resultHeight);
        Utils.ApplyChunk(imgL, avgL, avgPadBuffer, sliceBuffer, k * 2, stride, Utils.Median);
        Array2D<float> mmPadBuffer = Utils.ApplyChunkPad(imgL, k, stride);
        //!!!return mmPadBuffer;
        Array2D<float> minL = new(resultWidth, resultHeight);
        Utils.ApplyChunk(imgL, minL, mmPadBuffer, sliceBuffer, k, stride, Utils.Min);
        Array2D<float> maxL = new(resultWidth, resultHeight);
        Utils.ApplyChunk(imgL, maxL, mmPadBuffer, sliceBuffer, k, stride, Utils.Max);
        float[] bright_dist = GC.AllocateUninitializedArray<float>(size);
        TensorPrimitives.Subtract(maxL.Span, avgL.Span, bright_dist);
        float[] dark_dist = GC.AllocateUninitializedArray<float>(size);
        TensorPrimitives.Subtract(avgL.Span, minL.Span, dark_dist);
        TensorPrimitives.Subtract(bright_dist, dark_dist, bright_dist);
        TensorPrimitives.Multiply(bright_dist, dist_scale, bright_dist);

        float[] weight = dark_dist;
        TensorPrimitives.Add(avgL.Span, -0.5f, avgL.Span);
        TensorPrimitives.Multiply(avgL.Span, avg_scale, weight);
        TensorPrimitives.Subtract(weight, bright_dist, weight);
        TensorPrimitives.Sigmoid(weight, weight);

        Array2D<float> result = new(avgL.XLength, avgL.YLength, weight);
        Array2D<float> tmp = ImageUtils.ResizeSimpleFP32(result, width, height, InterpolationMethod.Nearest);
        Array2D<float> tmp2 = new(width / 2, height / 2, weight);
        ImageUtils.ResizeSimpleFP32To(tmp, tmp2, InterpolationMethod.Bilinear);
        ImageUtils.ResizeSimpleFP32To(tmp2, tmp, InterpolationMethod.Bilinear);
        float min = TensorPrimitives.Min(result.Span);
        float max = TensorPrimitives.Max(result.Span);
        TensorPrimitives.Subtract(result.Span, min, result.Span);
        TensorPrimitives.Divide(result.Span, max, result.Span);
        return tmp;
    }

    public readonly static Array3x3<bool> KernelExpansion = new(true, true, true, true, true, true, true, true, true);
    public readonly static Array3x3<bool> KernelSmoothing = new(false, true, false, true, true, true, false, true, false);
    public static (Array2D<uint>, Array2D<float>) OutlineExpansion(
        Array2D<uint> imgPackedBGRA,
        int erode = 2,
        int dilate = 2,
        int k = 16,
        float avg_scale = 10,
        float dist_scale = 3)
    {
        Array2D<float> weight = ExpansionWeight(imgPackedBGRA, k, k / 4 * 2, avg_scale, dist_scale);

        Array2D<uint> imgErode = imgPackedBGRA.Copy();
        Array2D<uint> imgDilate = imgPackedBGRA.Copy();
        Array2D<uint> tmp = new(imgPackedBGRA.XLength, imgPackedBGRA.YLength);

        if (ImageUtils.Morph(imgErode, tmp, in KernelExpansion, ImageUtils.ErodePacked4xU8Kernel, erode))
            (imgErode, tmp) = (tmp, imgErode);
        if (ImageUtils.Morph(imgDilate, tmp, in KernelExpansion, ImageUtils.DilatePacked4xU8Kernel, dilate))
            (imgDilate, tmp) = (tmp, imgDilate);
        Array2D<uint> output = new(imgPackedBGRA.XLength, imgPackedBGRA.YLength);

        //!!!Array2D<uint> output = new(weight.XLength, weight.YLength);
        //!!!for (int i = 0; i < output.Length; i++)
        //!!!{
        //!!!    float ff = weight.Span[i];
        //!!!    uint b = (byte)Math.Clamp(ff * 255, 0, 255);
        //!!!    output.Span[i] = b | (b << 8) | (b << 16) | 0xFF000000u;
        //!!!}
        //!!!return (output, weight);
        //!!!return (imgDilate, weight);
        //Parallel.For(0, output.Length,
        //    i => OutlineExpansionKernel(i, imgErode.Span, imgDilate.Span, imgPackedBGRA.Span, weight.Span, output.Span));
        for (int i = 0; i < output.Length; i++)
            OutlineExpansionKernel(i, imgErode.Span, imgDilate.Span, imgPackedBGRA.Span, weight.Span, output.Span);
        //!!!return (output, weight);
        if (ImageUtils.Morph(output, tmp, in KernelSmoothing, ImageUtils.ErodePacked4xU8Kernel, erode))
            (output, tmp) = (tmp, output);
        if (ImageUtils.Morph(output, tmp, in KernelSmoothing, ImageUtils.DilatePacked4xU8Kernel, dilate * 2))
            (output, tmp) = (tmp, output);
        if (ImageUtils.Morph(output, tmp, in KernelSmoothing, ImageUtils.ErodePacked4xU8Kernel, erode))
            (output, tmp) = (tmp, output);
        TensorPrimitives.Multiply(weight.Span, 2f, weight.Span);
        TensorPrimitives.Add(weight.Span, -1f, weight.Span);
        TensorPrimitives.Abs(weight.Span, weight.Span);
        TensorPrimitives.Multiply(weight.Span, 255f, weight.Span);
        Array2D<byte> tmpA = new(imgPackedBGRA.XLength, imgPackedBGRA.YLength);
        Array2D<byte> tmpB = new(imgPackedBGRA.XLength, imgPackedBGRA.YLength);
        TensorPrimitives.ConvertTruncating<float, byte>(weight.Span, tmpA.Span);
        if (ImageUtils.Morph(tmpA, tmpB, in KernelExpansion, ImageUtils.DilateU8Kernel, dilate))
            tmpA = tmpB;
        TensorPrimitives.ConvertTruncating<byte, float>(tmpA.Span, weight.Span);
        TensorPrimitives.Divide(weight.Span, 255f, weight.Span);
        return (output, weight);
    }
    private static void OutlineExpansionKernel(
        int i,
        ReadOnlySpan<uint> imgErode,
        ReadOnlySpan<uint> imgDilate,
        ReadOnlySpan<uint> img,
        ReadOnlySpan<float> weight,
        Span<uint> output)
    {
        ReadOnlySpan<byte> imgErodeB = MemoryMarshal.AsBytes(imgErode);
        ReadOnlySpan<byte> imgDilateB = MemoryMarshal.AsBytes(imgDilate);
        ReadOnlySpan<byte> imgB = MemoryMarshal.AsBytes(img);
        Span<byte> outputB = MemoryMarshal.AsBytes(output);
        float weightF = weight[i];
        float origWeight = Utils.Sigmoid((weightF - 0.5f) * 5f) * 0.25f;
        i *= 4;
        int a = i, b = i + 1, c = i + 2, d = i + 3;
        outputB[a] = (byte)((imgErodeB[a] * weightF + imgDilateB[a] * (1f - weightF)) * (1f - origWeight) + imgB[a] * origWeight);
        outputB[b] = (byte)((imgErodeB[b] * weightF + imgDilateB[b] * (1f - weightF)) * (1f - origWeight) + imgB[b] * origWeight);
        outputB[c] = (byte)((imgErodeB[c] * weightF + imgDilateB[c] * (1f - weightF)) * (1f - origWeight) + imgB[c] * origWeight);
        outputB[d] = (byte)((imgErodeB[d] * weightF + imgDilateB[d] * (1f - weightF)) * (1f - origWeight) + imgB[d] * origWeight);
    }
}
