using System.Drawing;

namespace SharpPixelOE.CPU;

public static class PixelOE
{
    public static Array2D<uint> Pixelize(
        Array2D<uint> img,
        DownscaleFunc downscaleFunc,
        int patchSize = 16,
        int? pixelSize = null,
        int thickness = 2,
        bool colorMatching = true,
        double contrast = 1,
        double saturation = 1,
        int? colors = null,
        ColorQuantMethod colorQuantMethod = ColorQuantMethod.K_Means,
        bool colorsWithWeight = false,
        bool noUpscale = false,
        bool noDownscale = false)
    {
        Size size = new(img.XLength / patchSize, img.YLength / patchSize);
        return Pixelize(
            img,
            size,
            downscaleFunc,
            patchSize,
            pixelSize,
            thickness,
            colorMatching,
            contrast,
            saturation,
            colors,
            colorQuantMethod,
            colorsWithWeight,
            noUpscale,
            noDownscale);
    }
    public static Array2D<uint> Pixelize(
        Array2D<uint> img,
        int targetSize,
        DownscaleFunc downscaleFunc,
        int patchSize = 16,
        int? pixelSize = null,
        int thickness = 2,
        bool colorMatching = true,
        double contrast = 1,
        double saturation = 1,
        int? colors = null,
        ColorQuantMethod colorQuantMethod = ColorQuantMethod.K_Means,
        bool colorsWithWeight = false,
        bool noUpscale = false,
        bool noDownscale = false)
    {
        double ratio = (double)img.XLength / img.YLength;
        double targetOrgSize = Math.Sqrt(targetSize * targetSize / ratio);
        Size size = new((int)(targetOrgSize * ratio), (int)targetOrgSize);
        return Pixelize(
            img,
            size,
            downscaleFunc,
            patchSize,
            pixelSize,
            thickness,
            colorMatching,
            contrast,
            saturation,
            colors,
            colorQuantMethod,
            colorsWithWeight,
            noUpscale,
            noDownscale);
    }
    public static Array2D<uint> Pixelize(
        Array2D<uint> img,
        Size targetSize,
        DownscaleFunc downscaleFunc,
        int patchSize = 16,
        int? pixelSize = null,
        int thickness = 2,
        bool colorMatching = true,
        double contrast = 1,
        double saturation = 1,
        int? colors = null,
        ColorQuantMethod colorQuantMethod = ColorQuantMethod.K_Means,
        bool colorsWithWeight = false,
        bool noUpscale = false,
        bool noDownscale = false)
    {
        // bool weightedColor = colors is not null && colorsWithWeight;
        pixelSize ??= patchSize;
        int targetW = targetSize.Width * patchSize;
        int targetH = targetSize.Height * patchSize;
        // double targetSizeD = Math.Sqrt(targetW * targetW / (double)(patchSize * patchSize));

        Array2D<uint> imgPackedBGRA = ImageUtils.ResizePacked4xU8(img, targetW, targetH, InterpolationMethod.Bicubic);

        Array2D<float>? weight = null;
        if (thickness > 0)
            (imgPackedBGRA, weight) = Outline.OutlineExpansion(imgPackedBGRA, thickness, thickness, patchSize, 9, 4);
        /* USAGE NOT IMPLEMENTED
        else if (weightedColor)
        {
            weight = Outline.ExpansionWeight(imgPackedBGRA, patchSize, (patchSize / 4) * 2, 9, 4);
            TensorPrimitives.Multiply(weight.Span, 2f, weight.Span);
            TensorPrimitives.Add(weight.Span, -1f, weight.Span);
            TensorPrimitives.Abs(weight.Span, weight.Span);
        }
        if (colorMatching)
        {
            // img = match_color(img, org_img)
        }
        */

        if (noDownscale)
            return imgPackedBGRA;
        Array2D<float> imgPlanarLabA = ImageUtils.PackedBGRAToPlanarLabA(imgPackedBGRA);
        Array2D<float> img_sm = downscaleFunc(imgPlanarLabA, patchSize);

        /* USAGE NOT IMPLEMENTED
        Array2D<float>? weightMat = null;
        if (weightedColor)
        {
            Debug.Assert(weight is not null);
            weightMat = ImageUtils.ResizeSimpleFP32(weight, img_sm.XLength, img_sm.YLength, InterpolationMethod.Bilinear);
            double weightGamma = targetSizeD / 512;
            TensorPrimitives.Pow(weightMat.Span, (float)weightGamma, weightMat.Span);
        }
        if (colors.HasValue)
        {
            // img_sm_c = color_quant(
            //     img_sm,
            //     colors,
            //     weight_mat,
            //     # TODO: How to get more reasonable repeat times?
            //     int((patch_size * colors) * *0.5),
            //     color_quant_method,
            // )
            // img_sm = match_color(img_sm_c, img_sm, 3);
        }
        if (contrast != 1 || saturation != 1)
        {
            // img_sm = color_styling(img_sm, saturation, contrast)
        }
        */

        Array2D<uint> resultPackedBGRA = ImageUtils.PlanarLabAToPackedBGRA(img_sm);
        return noUpscale ? resultPackedBGRA : ImageUtils.ResizePacked4xU8(
            resultPackedBGRA,
            resultPackedBGRA.XLength * pixelSize.Value,
            resultPackedBGRA.YLength * pixelSize.Value,
            InterpolationMethod.Nearest);
    }
}
