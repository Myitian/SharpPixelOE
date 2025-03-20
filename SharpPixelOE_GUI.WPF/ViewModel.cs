using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using SharpPixelOE;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Text;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace SharpPixelOE_GUI.WPF;

public class ViewModel(MainWindow mainWindow) : INotifyPropertyChanged
{
    public static Context Context { get; } = Context.Create(b => b.Default().EnableAlgorithms());
    public static ImmutableArray<Device> Devices { get; } = Context.Devices;
    public static string[] DeviceNames { get; } = [.. Devices.Select(d => d is CPUDevice ? "CPU" : d.Name)];
    public static Accelerator?[] Accelerators { get; } = new Accelerator[Devices.Length];

    private void DownloadCompleted(object? sender, EventArgs e)
        => mainWindow.Dispatcher.BeginInvoke(OnSourceImageChanged);
    public BitmapSource? SourceImage
    {
        get;
        set
        {
            if (field is not null)
                field.DownloadCompleted -= DownloadCompleted;
            field = null;
            OnSourceImageChanged();
            if (value is not null)
            {
                field = value;
                value.DownloadCompleted += DownloadCompleted;
                OnSourceImageChanged();
            }
        }
    }
    public string? SourceImageText
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(SourceImageText));
        }
    } = null;
    public BitmapSource? ResultImage
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(ResultImage));
            OnPropertyChanged(nameof(ImageSizeInfo));
        }
    }
    public string ImageSizeInfo
    {
        get
        {
            StringBuilder sb = new();
            if (SourceImage is null)
                sb.AppendLine("源图尺寸：");
            else
                sb.AppendLine($"源图尺寸：{SourceImage.PixelWidth}x{SourceImage.PixelHeight}");
            if (ResultImage is null)
                sb.Append("输出尺寸：");
            else
                sb.Append($"输出尺寸：{ResultImage.PixelWidth}x{ResultImage.PixelHeight}");
            return sb.ToString();
        }
    }
    public string SourceImagePath
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(SourceImagePath));
            CommandManager.InvalidateRequerySuggested();
        }
    } = "";
    public string ConvertButtonString
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(ConvertButtonString));
        }
    } = "转换";
    public int SelectedDeviceIndex
    {
        get;
        set
        {
            field = Math.Clamp(value, 0, DeviceNames.Length);
            OnPropertyChanged(nameof(SelectedDeviceIndex));
        }
    } = 0;
    public int PatchSizeMax { get; } = 256;
    public int PatchSize
    {
        get;
        set
        {
            field = Math.Clamp(value, 1, PatchSizeMax);
            OnPropertyChanged(nameof(PatchSize));
            if (SyncPixelSizeWithPatchSize)
                PixelSize = field;
            else
                OnEstimatedOutputSizeChanged();
        }
    } = 6;
    public int ThicknessMax { get; } = 256;
    public int Thickness
    {
        get;
        set
        {
            field = Math.Clamp(value, 0, ThicknessMax);
            OnPropertyChanged(nameof(Thickness));
        }
    } = 1;
    public bool IsDownscaleEnabled
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(IsDownscaleEnabled));
        }
    } = true;
    public bool IsUpscaleEnabled
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(IsUpscaleEnabled));
            OnPropertyChanged(nameof(EstimatedRealOutputSizeInfo));
        }
    } = true;
    public DownscaleMode DownscaleMode
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(DownscaleMode));
        }
    } = DownscaleMode.Contrast;
    public bool SyncPixelSizeWithPatchSize
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(SyncPixelSizeWithPatchSize));
            if (field)
                PixelSize = PatchSize;
        }
    }
    public int PixelSizeMax { get; } = 256;
    public int PixelSize
    {
        get;
        set
        {
            field = Math.Clamp(value, 1, PixelSizeMax);
            OnPropertyChanged(nameof(PixelSize));
            OnEstimatedOutputSizeChanged();
        }
    } = 6;
    public SizeMode OutputSizeMode
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(OutputSizeMode));
            OnEstimatedOutputSizeChanged();
        }
    }
    public int OutputEquivalentSquareSideLengthMax => SourceImage?.PixelWidth * SourceImage?.PixelHeight ?? 1;
    public double OutputEquivalentSquareSideLength
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(OutputEquivalentSquareSideLength));
            if (OutputSizeMode is SizeMode.EquivalentSquareSideLength)
                OnEstimatedOutputSizeChanged();
        }
    } = 1;
    private int _outputWidth = 1;
    public int OutputWidthMax => SourceImage?.PixelWidth ?? 1;
    public int OutputWidth
    {
        get => _outputWidth;
        set => UpdateOutputWidth(value, KeepScale);
    }
    private int _outputHeight = 1;
    public int OutputHeightMax => SourceImage?.PixelHeight ?? 1;
    public int OutputHeight
    {
        get => _outputHeight;
        set => UpdateOutputHeight(value, KeepScale);
    }
    public bool KeepScale
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(KeepScale));
        }
    } = true;
    public string EstimatedRealOutputSizeInfo
    {
        get
        {
            return IsUpscaleEnabled ?
                $"估计输出尺寸：{OutputWidth * PixelSize}x{OutputHeight * PixelSize}" :
                $"估计输出尺寸：{OutputWidth}x{OutputHeight}";
        }
    }


    /// <summary>
    /// 属性已改变
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>
    /// 属性已改变
    /// </summary>
    protected void OnPropertyChanged(string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    protected void OnSourceImageChanged()
    {
        OnPropertyChanged(nameof(SourceImage));
        OnPropertyChanged(nameof(ImageSizeInfo));
        OnPropertyChanged(nameof(OutputEquivalentSquareSideLengthMax));
        OnPropertyChanged(nameof(OutputWidthMax));
        OnPropertyChanged(nameof(OutputHeightMax));
        OnEstimatedOutputSizeChanged();
    }
    protected void OnEstimatedOutputSizeChanged()
    {
        switch (OutputSizeMode)
        {
            case SizeMode.Auto:
                UpdateOutputWidth((SourceImage?.PixelWidth ?? 0) / PatchSize, false);
                UpdateOutputHeight((SourceImage?.PixelHeight ?? 0) / PatchSize, false);
                OutputEquivalentSquareSideLength = Math.Sqrt(OutputWidth * OutputHeight);
                break;
            case SizeMode.EquivalentSquareSideLength:
                double ratio = (double?)SourceImage?.PixelWidth / SourceImage?.PixelHeight ?? double.NaN;
                double targetOrgSize = Math.Sqrt(OutputEquivalentSquareSideLength * OutputEquivalentSquareSideLength / ratio);
                if (targetOrgSize is double.NaN)
                {
                    UpdateOutputWidth(1, false);
                    UpdateOutputHeight(1, false);
                }
                else
                {
                    UpdateOutputWidth((int)(targetOrgSize * ratio), false);
                    UpdateOutputHeight((int)targetOrgSize, false);
                }
                break;
            case SizeMode.WidthHeight:
                OutputEquivalentSquareSideLength = Math.Sqrt(OutputWidth * OutputHeight);
                break;
        }
        OnPropertyChanged(nameof(EstimatedRealOutputSizeInfo));
    }
    internal void UpdateOutputWidth(int outputWidth, bool keepScale)
    {
        _outputWidth = Math.Clamp(outputWidth, 1, OutputWidthMax);
        OnPropertyChanged(nameof(OutputWidth));
        OnPropertyChanged(nameof(EstimatedRealOutputSizeInfo));
        if (!keepScale && OutputSizeMode is SizeMode.WidthHeight)
            OnEstimatedOutputSizeChanged();
        if (keepScale && SourceImage is not null)
            UpdateOutputHeight((int)Math.Round((double)SourceImage.PixelHeight / SourceImage.PixelWidth * _outputWidth), false);
    }
    internal void UpdateOutputHeight(int outputHeight, bool keepScale)
    {
        _outputHeight = Math.Clamp(outputHeight, 1, OutputHeightMax);
        OnPropertyChanged(nameof(OutputHeight));
        OnPropertyChanged(nameof(EstimatedRealOutputSizeInfo));
        if (!keepScale && OutputSizeMode is SizeMode.WidthHeight)
            OnEstimatedOutputSizeChanged();
        if (keepScale && SourceImage is not null)
            UpdateOutputWidth((int)Math.Round((double)SourceImage.PixelWidth / SourceImage.PixelHeight * _outputHeight), false);
    }

    public static void CleanUp()
    {
        Context.Dispose();
    }
}
public enum SizeMode
{
    Auto,
    EquivalentSquareSideLength,
    WidthHeight
}