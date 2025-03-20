using Myitian.Drawing;
using SharpPixelOE.CPU;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;

namespace ConsoleApp1;

internal class Program
{
    static void Main(string[] args)
    {
        Stopwatch sw = new();
        while (true)
        {
            Console.WriteLine("Open image:");
            Bitmap bmp = new(Console.ReadLine().AsSpan().Trim().Trim('"').ToString());
            Console.WriteLine("Processing...");
            using DirectBitmap dbmp = new(bmp);
            bmp.Dispose();
            sw.Restart();
            Array2D<uint> img = new(dbmp.Width, dbmp.Height, dbmp.Data);
            Array2D<uint> result = PixelOE.Pixelize(
                    img,
                    256,
                    DownscaleMethod.ContrastBased,
                    6,
                    pixelSize: 20,
                    thickness: 2);
            using DirectBitmap rbmp = new(result.XLength, result.YLength, result.Memory);
            sw.Stop();
            Console.WriteLine($"Done in {sw.ElapsedMilliseconds}ms!");
            Console.WriteLine("Save to:");
            rbmp.Bitmap.Save(Console.ReadLine().AsSpan().Trim().Trim('"').ToString(), ImageFormat.Png);
            Console.WriteLine("Done!");
            Console.WriteLine();
        }
    }
}
