﻿namespace SharpPixelOE.CPU;

public static partial class DownscaleMethod
{
    public static Array2D<float> ContrastBased(Array2D<float> imgPlanarLabA, int patchSize)
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
        Array2D<float> srcChannel = new(width, height, imgPlanarLabA.Memory);
        Array2D<float> dstChannel = new(resultWidth, resultHeight, resultLabA.Memory);
        Array2D<float> padBuffer = needPad ?
            new(paddedWidth, paddedHeight) : srcChannel;
        Utils.ApplyChunk(srcChannel, dstChannel, padBuffer, sliceBuffer, patchSize, patchSize, Utils.FindPixel);
        // a
        srcChannel = new(width, height, imgPlanarLabA.Memory[size..]);
        dstChannel = new(resultWidth, resultHeight, resultLabA.Memory[resultSize..]);
        if (!needPad)
            padBuffer = srcChannel;
        Utils.ApplyChunk(srcChannel, dstChannel, padBuffer, sliceBuffer, patchSize, patchSize, Utils.Median);
        // b
        srcChannel = new(width, height, imgPlanarLabA.Memory[(size * 2)..]);
        dstChannel = new(resultWidth, resultHeight, resultLabA.Memory[(resultSize * 2)..]);
        if (!needPad)
            padBuffer = srcChannel;
        Utils.ApplyChunk(srcChannel, dstChannel, padBuffer, sliceBuffer, patchSize, patchSize, Utils.Median);
        // A
        srcChannel = new(width, height, imgPlanarLabA.Memory[(size * 3)..]);
        dstChannel = new(resultWidth, resultHeight, resultLabA.Memory[(resultSize * 3)..]);
        if (!needPad)
            padBuffer = srcChannel;
        Utils.ApplyChunk(srcChannel, dstChannel, padBuffer, sliceBuffer, patchSize, patchSize, Utils.FindPixel);

        return resultLabA;
    }
}
