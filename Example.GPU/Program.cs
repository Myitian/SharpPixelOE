using ILGPU;
using ILGPU.Runtime;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing;
using SharpPixelOE.GPU;
using Myitian.Drawing;
using System.Collections.Immutable;
using ILGPU.Runtime.CPU;

namespace Example.GPU;

internal class Program
{
    static void Main(string[] args)
    {
        Stopwatch sw = new();
        using Context ctx = Context.Create(b => b.Default().EnableAlgorithms());
        ImmutableArray<Device> devices = ctx.Devices;
        Console.WriteLine("Select a device:");
        for (int i = 0; i < devices.Length; i++)
        {
            Device device = devices[i];
            Console.WriteLine($"[{i}] {device.Name}{(device is CPUDevice ? "  !!! Not recommended, use SharpPixelOE (without .GPU) instead." : "")}");
        }
        int selected;
        Console.WriteLine("Enter device index:");
        while (!int.TryParse(Console.ReadLine(), out selected)) ;
        using Accelerator accelerator = ctx.Devices[selected].CreateAccelerator(ctx);
        using AcceleratorStream stream = accelerator.CreateStream();
        while (true)
        {
            Console.WriteLine("Open image:");
            Bitmap bmp = new(Console.ReadLine().AsSpan().Trim().Trim('"').ToString());
            Console.WriteLine("Processing...");
            using DirectBitmap dbmp = new(bmp);
            bmp.Dispose();
            sw.Restart();
            Array2D<uint> img = new(accelerator, dbmp.Width, dbmp.Height, dbmp.Data);
            Array2D<uint> result = PixelOE.Pixelize(
                accelerator,
                stream,
                img,
                256,
                DownscaleMethod.Center,
                6,
                pixelSize: 1,
                thickness: 2);
            img.Dispose();
            using DirectBitmap rbmp = new(result.XLength, result.YLength);
            result.RawView.CopyToCPU(stream, rbmp.Data.Span);
            stream.Synchronize();
            result.Dispose();
            sw.Stop();
            Console.WriteLine($"Done in {sw.ElapsedMilliseconds}ms!");
            Console.WriteLine("Save to:");
            rbmp.Bitmap.Save(Console.ReadLine().AsSpan().Trim().Trim('"').ToString(), ImageFormat.Png);
            Console.WriteLine("Done!");
            Console.WriteLine();
        }
    }
}
