using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

// FND-GFX-006: the scratch surface keeps the default black pen of a new memory DC, and
// Rectangle fills the interior with a solid brush of the fill colour. The game fills with white
// (the flash) and with black (the dims and the sector shade), so both brushes are compared.
// These tests compare the fill pixels only; the pattern compositor's raster operations are not
// exercised.
public sealed class NativePatternFillTests
{
    // Distinct initialization makes a missing border or fill write observable.
    private const byte Unwritten = 0x7f;

    [Theory]
    [InlineData(1, 5)]
    [InlineData(5, 1)]
    [InlineData(2, 3)]
    [InlineData(13, 11)]
    [InlineData(64, 64)]
    [InlineData(97, 34)]
    [InlineData(52, 50)]
    [InlineData(120, 64)]
    [InlineData(432, 416)]
    public void PatternFillsMatchNativeRectanglePixels(int width, int height)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows GDI reference requires Windows.");
        foreach (var fill in new[] { Color.White, Color.Black })
        {
            var native = NativeRectangle(width, height, fill);
            foreach (var mask in new[] { OriginalPatternMask.Half, OriginalPatternMask.Sparse, OriginalPatternMask.Dense })
            {
                var expected = new Color[width * height];
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    // The DIB is bottom-up and stored as BGRA.
                    var offset = ((height - 1 - y) * width + x) * 4;
                    expected[y * width + x] = OriginalPatternMask.PreservesDestination(mask, x, y)
                        ? Color.Transparent : new Color(native[offset + 2], native[offset + 1], native[offset]);
                }
                Assert.Equal(expected, OriginalPatternMask.ShadedRectangle(mask, width, height, fill, Color.Black));
            }
        }
    }

    [Fact]
    public void NativeRectangleWritesNothingForOnePixelWhereTheHelperDrawsItsOutline()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows GDI reference requires Windows.");
        // GDI leaves a 1-by-1 Rectangle unwritten. No game caller fills that size, so the
        // helper keeps drawing the outline there and this records the difference. Bitmap 143
        // is the only pattern of the three that replaces the corner pixel.
        Assert.All(NativeRectangle(1, 1, Color.White), value => Assert.Equal(Unwritten, value));
        Assert.Equal(Color.Black, OriginalPatternMask.ShadedRectangle(
            OriginalPatternMask.Half, 1, 1, Color.White, Color.Black)[0]);
    }

    private static byte[] NativeRectangle(int width, int height, Color fill)
    {
        var length = width * height * 4;
        var info = new byte[40];
        BitConverter.TryWriteBytes(info.AsSpan(0), 40);
        BitConverter.TryWriteBytes(info.AsSpan(4), width);
        BitConverter.TryWriteBytes(info.AsSpan(8), height);
        BitConverter.TryWriteBytes(info.AsSpan(12), (short)1);
        BitConverter.TryWriteBytes(info.AsSpan(14), (short)32);
        var dc = CreateCompatibleDC(IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, dc);
        var bitmap = IntPtr.Zero;
        var old = IntPtr.Zero;
        var brush = IntPtr.Zero;
        var oldBrush = IntPtr.Zero;
        try
        {
            bitmap = CreateDIBSection(dc, info, 0, out var pointer, IntPtr.Zero, 0);
            Assert.NotEqual(IntPtr.Zero, bitmap);
            var initial = new byte[length];
            Array.Fill(initial, Unwritten);
            Marshal.Copy(initial, 0, pointer, length);
            old = SelectObject(dc, bitmap);
            Assert.NotEqual(IntPtr.Zero, old);
            // COLORREF is 0x00BBGGRR.
            brush = CreateSolidBrush((uint)(fill.R | fill.G << 8 | fill.B << 16));
            Assert.NotEqual(IntPtr.Zero, brush);
            oldBrush = SelectObject(dc, brush);
            Assert.NotEqual(IntPtr.Zero, oldBrush);
            Assert.True(Rectangle(dc, 0, 0, width, height));
            Assert.True(GdiFlush());
            var native = new byte[length];
            Marshal.Copy(pointer, native, 0, length);
            return native;
        }
        finally
        {
            // Cleanup does not assert, so a failure above is reported as it happened.
            if (oldBrush != IntPtr.Zero) SelectObject(dc, oldBrush);
            if (brush != IntPtr.Zero) DeleteObject(brush);
            if (old != IntPtr.Zero) SelectObject(dc, old);
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            DeleteDC(dc);
        }
    }

    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, byte[] info,
        uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Rectangle(IntPtr dc, int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GdiFlush();
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteDC(IntPtr dc);
}
