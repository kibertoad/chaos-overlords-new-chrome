using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class NativePatternFillTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    [InlineData(13, 11)]
    [InlineData(64, 64)]
    [InlineData(97, 34)]
    [InlineData(52, 50)]
    [InlineData(120, 64)]
    [InlineData(432, 416)]
    public void NativeRectangleReferenceRecordsBoundaryAndMatchesNondegeneratePatternFills(int width, int height)
    {
        // FND-GFX-006: the scratch surface retains its default black pen;
        // Rectangle's white brush fills only the interior. Compare native fill
        // pixels; the pattern compositor's raster operations are not exercised.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows GDI reference requires Windows.");
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
        try
        {
            bitmap = CreateDIBSection(dc, info, 0, out var pointer, IntPtr.Zero, 0);
            Assert.NotEqual(IntPtr.Zero, bitmap);
            // Distinct initialization makes a missing border/fill write observable.
            Marshal.Copy(Enumerable.Repeat((byte)0x7f, width * height * 4).ToArray(), 0,
                pointer, width * height * 4);
            old = SelectObject(dc, bitmap);
            Assert.NotEqual(IntPtr.Zero, old);
            Assert.True(Rectangle(dc, 0, 0, width, height));
            Assert.True(GdiFlush());
            var native = new byte[width * height * 4];
            Marshal.Copy(pointer, native, 0, native.Length);
            if (width == 1 && height == 1)
            {
                // Native boundary observation, not evidence of an original-game
                // caller: GDI writes nothing here. Current game fills are larger.
                Assert.All(native, value => Assert.Equal((byte)0x7f, value));
                // Preserve the known helper limitation explicitly rather than
                // claiming that this degenerate rectangle matches GDI.
                Assert.NotEqual(new Color(127, 127, 127), OriginalPatternMask.ShadedRectangle(
                    OriginalPatternMask.Half, 1, 1, Color.White, Color.Black)[0]);
                return;
            }
            foreach (var mask in new[] { OriginalPatternMask.Half, OriginalPatternMask.Sparse, OriginalPatternMask.Dense })
            {
                var actual = OriginalPatternMask.ShadedRectangle(mask, width, height, Color.White, Color.Black);
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var offset = ((height - 1 - y) * width + x) * 4;
                    var expected = OriginalPatternMask.PreservesDestination(mask, x, y)
                        ? Color.Transparent : new Color(native[offset + 2], native[offset + 1], native[offset]);
                    Assert.Equal(expected, actual[y * width + x]);
                }
            }
        }
        finally
        {
            if (old != IntPtr.Zero) SelectObject(dc, old);
            if (bitmap != IntPtr.Zero) Assert.True(DeleteObject(bitmap));
            Assert.True(DeleteDC(dc));
        }
    }

    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, byte[] info,
        uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Rectangle(IntPtr dc, int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GdiFlush();
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteDC(IntPtr dc);
}
