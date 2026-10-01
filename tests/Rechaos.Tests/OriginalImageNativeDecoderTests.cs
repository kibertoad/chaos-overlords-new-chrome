using System.Runtime.InteropServices;
using Rechaos.Extractor;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalImageFileTests
{
    [Fact]
    public void OriginalPx08PixelsMatchWindowsSetDIBits()
    {
        // RULE-GFX-001, FMT-GFX-002, FND-PLATFORM-002: the original fixes
        // dimensions and planes, then delegates decoding to SetDIBits.
        // Compare file-defined pixels only; FND-GFX-005 separately records
        // the oversized rectangle requested for PX06008.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows GDI reference requires Windows.");
        var files = OriginalFormatFiles.Require("FMT-GFX-002");
        OriginalFormatFiles.CheckEach(files, file =>
        {
            var size = DocumentedSize(file.Name);
            var original = file.ReadAllBytes();
            var decoded = Px08BmpDecoder.Decode(original, size.Width, size.Height);
            var info = original.AsSpan(14, 1064).ToArray();
            BitConverter.TryWriteBytes(info.AsSpan(4), size.Width);
            BitConverter.TryWriteBytes(info.AsSpan(8), size.Height);
            BitConverter.TryWriteBytes(info.AsSpan(12), (short)1);
            var outputInfo = decoded.AsSpan(14, 1064).ToArray();
            var bitmap = CreateDIBSection(IntPtr.Zero, outputInfo, 0, out var pixels, IntPtr.Zero, 0);
            Assert.NotEqual(IntPtr.Zero, bitmap);
            try
            {
                Assert.Equal(size.Height, SetDIBits(IntPtr.Zero, bitmap, 0, (uint)size.Height,
                    original.AsSpan(1078).ToArray(), info, 0));
                Assert.True(GdiFlush());
                var stride = (size.Width + 3) & ~3;
                var native = new byte[stride * size.Height];
                Marshal.Copy(pixels, native, 0, native.Length);
                for (var y = 0; y < size.Height; y++)
                    Assert.True(native.AsSpan(y * stride, size.Width).SequenceEqual(
                        decoded.AsSpan(1078 + y * stride, size.Width)), $"Pixel mismatch on bottom-up row {y}.");
            }
            finally { Assert.True(DeleteObject(bitmap)); }
        });
    }

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(IntPtr dc, byte[] info, uint usage,
        out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int SetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines,
        byte[] bits, byte[] info, uint usage);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GdiFlush();
}
