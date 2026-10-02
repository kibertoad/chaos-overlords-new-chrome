using System.Runtime.InteropServices;
using Rechaos.Extractor;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalImageFileTests
{
    [Fact]
    public void OriginalPx16ColorsMatchRuntimeDecoderAndWindowsSetDIBits()
    {
        // FMT-GFX-001, FND-PLATFORM-002: compare file-defined RGB555 pixels
        // through the original upload API and the bundled runtime BMP decoder.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows GDI reference requires Windows.");
        var files = OriginalFormatFiles.Require("FMT-GFX-001");
        var assembly = typeof(Microsoft.Xna.Framework.Graphics.Texture2D).Assembly;
        var resultType = assembly.GetType("StbImageSharp.ImageResult", throwOnError: true)!;
        var componentsType = assembly.GetType("StbImageSharp.ColorComponents", throwOnError: true)!;
        var decode = resultType.GetMethod("FromStream", [typeof(Stream), componentsType])!;
        OriginalFormatFiles.CheckEach(files, file =>
        {
            var size = DocumentedSize(file.Name);
            var repaired = file.ReadAllBytes();
            BmpRepair.Repair(repaired, size.Width, size.Height, 16);
            using var stream = new MemoryStream(repaired);
            var result = decode.Invoke(null, [stream, Enum.Parse(componentsType, "RedGreenBlueAlpha")])!;
            var rgba = (byte[])resultType.GetProperty("Data")!.GetValue(result)!;
            var keyed = new Microsoft.Xna.Framework.Color[size.Width * size.Height];
            for (var p = 0; p < keyed.Length; p++)
                keyed[p] = new Microsoft.Xna.Framework.Color(rgba[p * 4], rgba[p * 4 + 1],
                    rgba[p * 4 + 2], rgba[p * 4 + 3]);
            Rechaos.Game.OriginalWhiteKey.Apply(keyed);
            var info = repaired.AsSpan(14, 40).ToArray();
            var outputInfo = (byte[])info.Clone();
            BitConverter.TryWriteBytes(outputInfo.AsSpan(14), (short)32);
            BitConverter.TryWriteBytes(outputInfo.AsSpan(20), size.Width * size.Height * 4);
            var bitmap = CreateDIBSection(IntPtr.Zero, outputInfo, 0, out var pixels, IntPtr.Zero, 0);
            Assert.NotEqual(IntPtr.Zero, bitmap);
            try
            {
                Assert.Equal(size.Height, SetDIBits(IntPtr.Zero, bitmap, 0, (uint)size.Height,
                    repaired.AsSpan(54).ToArray(), info, 1));
                Assert.True(GdiFlush());
                var native = new byte[size.Width * size.Height * 4];
                Marshal.Copy(pixels, native, 0, native.Length);
                var stride = (size.Width * 2 + 3) & ~3;
                for (var y = 0; y < size.Height; y++)
                for (var x = 0; x < size.Width; x++)
                {
                    var bottom = (y * size.Width + x) * 4;
                    var top = ((size.Height - 1 - y) * size.Width + x) * 4;
                    if (native[bottom] != rgba[top + 2] || native[bottom + 1] != rgba[top + 1]
                        || native[bottom + 2] != rgba[top])
                        Assert.Fail($"RGB mismatch at ({x}, bottom-up {y}).");
                    // RULE-GFX-003, FMT-GFX-001: the expected key comes from
                    // the packed file pixel, so a decoder that moves a near-white
                    // channel across the production threshold fails here. RGB555
                    // leaves bit 15 unused and both decoders drop it.
                    var packed = BitConverter.ToUInt16(repaired, 54 + y * stride + x * 2);
                    var expected = (packed & 0x7fff) == 0x7fff ? Microsoft.Xna.Framework.Color.Transparent
                        : new Microsoft.Xna.Framework.Color(rgba[top], rgba[top + 1], rgba[top + 2], rgba[top + 3]);
                    if (keyed[top / 4] != expected)
                        Assert.Fail($"White key mismatch at ({x}, bottom-up {y}): packed 0x{packed:X4}, "
                            + $"expected {expected}, got {keyed[top / 4]}.");
                }
            }
            finally { Assert.True(DeleteObject(bitmap)); }
        });
    }

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
