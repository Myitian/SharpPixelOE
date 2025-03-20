namespace SharpPixelOE.CPU;

public static partial class DownscaleMethod
{
    public static DownscaleFunc GetDownscaleFunc(DownscaleMode mode)
    {
        return mode switch
        {
            DownscaleMode.Bicubic => Bicubic,
            DownscaleMode.Nearest => Nearest,
            DownscaleMode.Center => Center,
            DownscaleMode.Contrast => ContrastBased,
            _ => throw new NotSupportedException()
        };
    }
}
public delegate Array2D<float> DownscaleFunc(Array2D<float> imgPlanarLabA, int patchSize);
