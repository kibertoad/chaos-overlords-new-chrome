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
        // Both decodes use the size the extractor writes into the header, and
        // only the pixels the file defines are compared, as for PX08.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows GDI reference requires Windows.");
        var files = OriginalFormatFiles.Require("FMT-GFX-001");
        var assembly = typeof(Microsoft.Xna.Framework.Graphics.Texture2D).Assembly;
        var resultType = assembly.GetType("StbImageSharp.ImageResult", throwOnError: true)!;
        var componentsType = assembly.GetType("StbImageSharp.ColorComponents", throwOnError: true)!;
        var decode = resultType.GetMethod("FromStream", [typeof(Stream), componentsType])
            ?? throw new MissingMethodException(resultType.FullName, "FromStream");
        var rgbaComponents = Enum.Parse(componentsType, "RedGreenBlueAlpha");
        OriginalFormatFiles.CheckEach(files, file =>
        {
            var documented = DocumentedSize(file.Name);
            var repaired = file.ReadAllBytes();
            var size = RebuildSize(file.Name, repaired.Length);
            BmpRepair.Repair(repaired, size.Width, size.Height, 16);
            using var stream = new MemoryStream(repaired);
            var result = decode.Invoke(null, [stream, rgbaComponents])!;
            var decodedWidth = (int)resultType.GetProperty("Width")!.GetValue(result)!;
            var decodedHeight = (int)resultType.GetProperty("Height")!.GetValue(result)!;
            if (decodedWidth != size.Width || decodedHeight != size.Height)
                throw new InvalidDataException(
                    $"the runtime decoder gives {decodedWidth} x {decodedHeight}, expected {size.Width} x {size.Height}");
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
            if (bitmap == IntPtr.Zero)
                throw new InvalidDataException($"CreateDIBSection failed with error {Marshal.GetLastPInvokeError()}");
            try
            {
                var lines = SetDIBits(IntPtr.Zero, bitmap, 0, (uint)size.Height, repaired.AsSpan(54).ToArray(), info, 1);
                if (lines != size.Height)
                    throw new InvalidDataException(
                        $"SetDIBits set {lines} lines, expected {size.Height} (error {Marshal.GetLastPInvokeError()})");
                if (!GdiFlush()) throw new InvalidDataException("GdiFlush failed");
                var native = new byte[size.Width * size.Height * 4];
                Marshal.Copy(pixels, native, 0, native.Length);
                var stride = (size.Width * 2 + 3) & ~3;
                var width = Math.Min(documented.Width, size.Width);
                for (var y = 0; y < Math.Min(documented.Height, size.Height); y++)
                for (var x = 0; x < width; x++)
                {
                    var bottom = (y * size.Width + x) * 4;
                    var top = ((size.Height - 1 - y) * size.Width + x) * 4;
                    if (native[bottom] != rgba[top + 2] || native[bottom + 1] != rgba[top + 1]
                        || native[bottom + 2] != rgba[top])
                        throw new InvalidDataException(
                            $"pixel ({x}, bottom-up row {y}) is RGB {rgba[top]}, {rgba[top + 1]}, {rgba[top + 2]}, " +
                            $"SetDIBits gives {native[bottom + 2]}, {native[bottom + 1]}, {native[bottom]}");
                    // RULE-GFX-003, FMT-GFX-001: the expected key comes from
                    // the packed file pixel, so a decoder that moves a near-white
                    // channel across the production threshold fails here. RGB555
                    // leaves bit 15 unused and both decoders drop it.
                    var packed = BitConverter.ToUInt16(repaired, 54 + y * stride + x * 2);
                    var expected = (packed & 0x7fff) == 0x7fff ? Microsoft.Xna.Framework.Color.Transparent
                        : new Microsoft.Xna.Framework.Color(rgba[top], rgba[top + 1], rgba[top + 2], rgba[top + 3]);
                    if (keyed[top / 4] != expected)
                        throw new InvalidDataException($"White key mismatch at ({x}, bottom-up {y}): packed 0x{packed:X4}, "
                            + $"expected {expected}, got {keyed[top / 4]}.");
                }
            }
            finally
            {
                DeleteObject(bitmap);
            }
        });
    }

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
