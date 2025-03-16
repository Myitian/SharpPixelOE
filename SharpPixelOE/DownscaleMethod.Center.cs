namespace SharpPixelOE;

public static partial class DownscaleMethod
{
    public static Array2D<float> Center(Array2D<float> imgPlanarLabA, int patchSize)
    {
        int width = imgPlanarLabA.XLength;
        int height = imgPlanarLabA.YLength / 4;
        int size = width * height;
        (int paddedWidth, int paddedHeight) = Utils.CalculatePadSize(width, height, patchSize, patchSize);
        (int resultWidth, int resultHeight) = Utils.CalculateResultSize(paddedWidth, paddedHeight, patchSize, patchSize);
        int resultSize = resultWidth * resultHeight;
        bool needPad = paddedWidth != width || paddedHeight != height;
        Array2D<float> resultLabA = new(resultWidth, resultHeight * 4);
        int sliceSize = patchSize * patchSize;
        Span<float> sliceBuffer = sliceSize <= 1024 ?
            stackalloc float[sliceSize] :
            GC.AllocateUninitializedArray<float>(sliceSize);

        // L
        Array2D<float> srcChannel = new(width, height, imgPlanarLabA.UnderlyingMemory);
        Array2D<float> dstChannel = new(resultWidth, resultHeight, resultLabA.UnderlyingMemory);
        Array2D<float> padBuffer = needPad ?
            new(paddedWidth, paddedHeight) : srcChannel;
        Utils.ApplyChunk(srcChannel, dstChannel, padBuffer, sliceBuffer, patchSize, patchSize, Utils.Middle);
        // a
        srcChannel = new(width, height, imgPlanarLabA.UnderlyingMemory[size..]);
        dstChannel = new(resultWidth, resultHeight, resultLabA.UnderlyingMemory[resultSize..]);
        if (!needPad)
            padBuffer = srcChannel;
        Utils.ApplyChunk(srcChannel, dstChannel, padBuffer, sliceBuffer, patchSize, patchSize, Utils.Median);
        // b
        srcChannel = new(width, height, imgPlanarLabA.UnderlyingMemory[(size * 2)..]);
        dstChannel = new(resultWidth, resultHeight, resultLabA.UnderlyingMemory[(resultSize * 2)..]);
        if (!needPad)
            padBuffer = srcChannel;
        Utils.ApplyChunk(srcChannel, dstChannel, padBuffer, sliceBuffer, patchSize, patchSize, Utils.Median);
        // A
        srcChannel = new(width, height, imgPlanarLabA.UnderlyingMemory[(size * 3)..]);
        dstChannel = new(resultWidth, resultHeight, resultLabA.UnderlyingMemory[(resultSize * 3)..]);
        if (!needPad)
            padBuffer = srcChannel;
        Utils.ApplyChunk(srcChannel, dstChannel, padBuffer, sliceBuffer, patchSize, patchSize, Utils.Middle);

        return resultLabA;
    }
}
