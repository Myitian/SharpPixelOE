# SharpPixelOE
A C# implemention of [PixelOE](https://github.com/KohakuBlueleaf/PixelOE), without using OpenCV or Torch.
### Currently unimplemented features (compared to PixelOE):
- Color matching
- K-Centroid downscaling
- Color quantization
- Color styling

## License
Apache-2.0

## Dependencies
.NET 9
### SharpPixelOE
- [System.Numerics.Tensors](https://www.nuget.org/packages/System.Numerics.Tensors)
### SharpPixelOE.GPU
- [ILGPU](https://github.com/m4rs-mt/ILGPU)
- ILGPU.Algorithms