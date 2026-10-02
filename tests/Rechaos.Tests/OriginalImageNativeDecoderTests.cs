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
        // Both decodes use the size the extractor uses, and only the pixels
        // the file defines are compared; FND-GFX-005 separately records the
        // oversized rectangle requested for PX06008.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows GDI reference requires Windows.");
        var files = OriginalFormatFiles.Require("FMT-GFX-002");
        var px16 = Px16Counterparts();
        OriginalFormatFiles.CheckEach(files, file =>
        {
            var documented = DocumentedSize(file.Name);
            var size = Px08RebuildSize(file.Name, px16);
            var original = file.ReadAllBytes();
            var decoded = Px08BmpDecoder.Decode(original, size.Width, size.Height);
            var info = original.AsSpan(14, 1064).ToArray();
            BitConverter.TryWriteBytes(info.AsSpan(4), size.Width);
            BitConverter.TryWriteBytes(info.AsSpan(8), size.Height);
            BitConverter.TryWriteBytes(info.AsSpan(12), (short)1);
            var outputInfo = decoded.AsSpan(14, 1064).ToArray();
            var bitmap = CreateDIBSection(IntPtr.Zero, outputInfo, 0, out var pixels, IntPtr.Zero, 0);
            if (bitmap == IntPtr.Zero)
                throw new InvalidDataException($"CreateDIBSection failed with error {Marshal.GetLastPInvokeError()}");
            try
            {
                var lines = SetDIBits(IntPtr.Zero, bitmap, 0, (uint)size.Height, original.AsSpan(1078).ToArray(), info, 0);
                if (lines != size.Height)
                    throw new InvalidDataException(
                        $"SetDIBits set {lines} lines, expected {size.Height} (error {Marshal.GetLastPInvokeError()})");
                if (!GdiFlush()) throw new InvalidDataException("GdiFlush failed");
                var stride = (size.Width + 3) & ~3;
                var native = new byte[stride * size.Height];
                Marshal.Copy(pixels, native, 0, native.Length);
                var width = Math.Min(documented.Width, size.Width);
                for (var y = 0; y < Math.Min(documented.Height, size.Height); y++)
                {
                    var expected = native.AsSpan(y * stride, width);
                    var actual = decoded.AsSpan(1078 + y * stride, width);
                    var x = expected.CommonPrefixLength(actual);
                    if (x < width)
                        throw new InvalidDataException(
                            $"pixel ({x}, bottom-up row {y}) is {actual[x]}, SetDIBits gives {expected[x]}");
                }
            }
            finally
            {
                DeleteObject(bitmap);
            }
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
