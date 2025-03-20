using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CPUArray2D = SharpPixelOE.CPU.Array2D<uint>;
using CPUDownscaleMethod = SharpPixelOE.CPU.DownscaleMethod;
using CPUPixelOE = SharpPixelOE.CPU.PixelOE;
using GPUArray2D = SharpPixelOE.GPU.Array2D<uint>;
using GPUDownscaleMethod = SharpPixelOE.GPU.DownscaleMethod;
using GPUPixelOE = SharpPixelOE.GPU.PixelOE;
namespace SharpPixelOE_GUI.WPF;

public partial class MainWindow : Window
{
    public const string OpenFileDialogFilter = "所有支持的图像文件|*.bmp;*.dib;*.jpg;*.jpeg;*.jpe;*.jfif;*.gif;*.tif;*.tiff;*.png;*.ico;*.wdp"
        + "|位图文件|*.bmp;*.dib"
        + "|JPEG|*.jpg;*.jpeg;*.jpe;*.jfif"
        + "|GIF|*.gif"
        + "|TIFF|*.tif;*.tiff"
        + "|PNG|*.png"
        + "|ICO|*.ico"
        + "|HD 照片|*.wdp"
        + "|所有文件|*.*";
    public const string SaveFileDialogFilter = "位图文件|*.bmp;*.dib"
        + "|JPEG|*.jpg;*.jpeg;*.jpe;*.jfif"
        + "|GIF|*.gif"
        + "|TIFF|*.tif;*.tiff"
        + "|PNG|*.png"
        + "|HD 照片|*.wdp";

    public ViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = new(this);
        InitializeComponent();
    }

    public bool IsConverting = false;

    private void BeginConvert()
    {
        IsConverting = true;
        ViewModel.ConvertButtonString = "处理中……";
        Task.Run(() =>
        {
            try
            {
                if (ViewModel.SourceImage is not BitmapSource bmp)
                    return;
                int selected = ViewModel.SelectedDeviceIndex;
                Device device = ViewModel.Devices[selected];
                if (device is CPUDevice)
                {
                    CPUArray2D img = Dispatcher.Invoke(() =>
                    {
                        uint[] buffer = GC.AllocateUninitializedArray<uint>(bmp.PixelWidth * bmp.PixelHeight);
                        bmp.CopyPixels(buffer, bmp.PixelWidth * 4, 0);
                        return new CPUArray2D(bmp.PixelWidth, bmp.PixelHeight, buffer);
                    });
                    CPUArray2D result = CPUPixelOE.Pixelize(
                        img,
                        patchSize: ViewModel.PatchSize,
                        pixelSize: ViewModel.PixelSize,
                        thickness: ViewModel.Thickness,
                        downscaleFunc: CPUDownscaleMethod.GetDownscaleFunc(ViewModel.DownscaleMode),
                        noUpscale: !ViewModel.IsUpscaleEnabled,
                        noDownscale: !ViewModel.IsDownscaleEnabled);
                    uint[] buffer = result.UnderlyingArray ?? result.Memory.ToArray();
                    Dispatcher.BeginInvoke(() =>
                    {
                        ViewModel.ResultImage = BitmapSource.Create(result.XLength, result.YLength, 96, 96, PixelFormats.Bgra32, null, buffer, result.XLength * 4);
                    });
                }
                else
                {
                    Accelerator accelerator = ViewModel.Accelerators[selected] ?? device.CreateAccelerator(ViewModel.Context);
                    AcceleratorStream stream = accelerator.DefaultStream;
                    GPUArray2D img = Dispatcher.Invoke(() =>
                    {
                        uint[] buffer = GC.AllocateUninitializedArray<uint>(bmp.PixelWidth * bmp.PixelHeight);
                        bmp.CopyPixels(buffer, bmp.PixelWidth * 4, 0);
                        return new GPUArray2D(accelerator, bmp.PixelWidth, bmp.PixelHeight, buffer);
                    });
                    GPUArray2D result = GPUPixelOE.Pixelize(
                        accelerator,
                        stream,
                        img,
                        patchSize: ViewModel.PatchSize,
                        pixelSize: ViewModel.PixelSize,
                        thickness: ViewModel.Thickness,
                        downscaleFunc: GPUDownscaleMethod.GetDownscaleFunc(ViewModel.DownscaleMode),
                        noUpscale: !ViewModel.IsUpscaleEnabled,
                        noDownscale: !ViewModel.IsDownscaleEnabled);
                    img.Dispose();
                    uint[] buffer = GC.AllocateUninitializedArray<uint>(result.XLength * result.YLength);
                    result.RawView.CopyToCPU(stream, buffer);
                    stream.Synchronize();
                    result.Dispose();
                    Dispatcher.BeginInvoke(() =>
                    {
                        ViewModel.ResultImage = BitmapSource.Create(result.XLength, result.YLength, 96, 96, PixelFormats.Bgra32, null, buffer, result.XLength * 4);
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    MessageBox.Show(ex.Message);
                });
            }
            finally
            {
                ViewModel.ConvertButtonString = "转换";
                IsConverting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        });
    }

    private void CMD_Load_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = Uri.TryCreate(ViewModel.SourceImagePath, UriKind.RelativeOrAbsolute, out _);
    }

    private void CMD_Save_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ViewModel.ResultImage is not null;
    }

    private void CMD_Convert_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ViewModel.SourceImage is not null && !IsConverting;
    }

    private void CMD_Browse_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            CheckFileExists = true,
            Filter = OpenFileDialogFilter
        };
        if (dialog.ShowDialog() is true)
        {
            ViewModel.SourceImagePath = dialog.FileName;
        }
    }

    private void CMD_Open_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            CheckFileExists = true,
            Filter = OpenFileDialogFilter
        };
        if (dialog.ShowDialog() is true)
        {
            ViewModel.SourceImagePath = dialog.FileName;
            if (!Uri.TryCreate(ViewModel.SourceImagePath, UriKind.RelativeOrAbsolute, out Uri? uri))
                return;
            try
            {
                FormatConvertedBitmap img = new(new BitmapImage(uri), PixelFormats.Bgra32, null, 0);
                if (img.CanFreeze)
                    img.Freeze();
                _ = img.PixelWidth;
                ViewModel.SourceImage = img;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }

    private void CMD_Load_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!Uri.TryCreate(ViewModel.SourceImagePath, UriKind.RelativeOrAbsolute, out Uri? uri))
            return;
        try
        {
            BitmapImage img = new(uri);
            _ = img.PixelWidth;
            ViewModel.SourceImage = img;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    private void CMD_Save_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (ViewModel.ResultImage is not BitmapSource img)
            return;
        SaveFileDialog dialog = new()
        {
            Filter = SaveFileDialogFilter,
            FilterIndex = 5
        };
        if (dialog.ShowDialog() is true)
        {
            try
            {
                using FileStream fs = File.Open(dialog.FileName, FileMode.Create, FileAccess.Write, FileShare.Read);
                BitmapEncoder encoder = dialog.FilterIndex switch
                {
                    1 => new BmpBitmapEncoder(),
                    2 => new JpegBitmapEncoder(),
                    3 => new GifBitmapEncoder(),
                    4 => new TiffBitmapEncoder(),
                    5 => new PngBitmapEncoder(),
                    6 => new WmpBitmapEncoder(),
                    _ => throw new NotSupportedException(),
                };
                encoder.Frames.Add(BitmapFrame.Create(img));
                encoder.Save(fs);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }

    private void CMD_Convert_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        BeginConvert();
    }

    private void CMD_Info_SharpPixelOE_Executed(object sender, ExecutedRoutedEventArgs e)
    {
    }

    private void CMD_Info_PixelOE_Executed(object sender, ExecutedRoutedEventArgs e)
    {

    }

    private void CMD_License_SharpPixelOE_Executed(object sender, ExecutedRoutedEventArgs e)
    {

    }

    private void CMD_License_PixelOE_Executed(object sender, ExecutedRoutedEventArgs e)
    {

    }
}