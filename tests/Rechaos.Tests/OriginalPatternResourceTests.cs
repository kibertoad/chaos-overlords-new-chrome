using System.Runtime.InteropServices;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalPatternResourceTests
{
    [Fact]
    public void EveryMaskBitMatchesTheOriginalExecutableBitmap()
    {
        // FND-UI-031, FND-GFX-006: compare all mask bits with actual resources,
        // including their bottom-up DIB rows and four-byte row alignment.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows resource reader requires Windows.");
        var path = OriginalGameFiles.Require(OriginalFormatFiles.Build, "Chaos Overlords.exe",
            "a82da6843188901e1d4fce1c76a925ef");
        // Load as data only; never execute the original during this comparison.
        var module = LoadLibraryEx(path, IntPtr.Zero, 2);
        Assert.NotEqual(IntPtr.Zero, module);
        try
        {
            foreach (var id in new[] { 143, 146, 147 })
            {
                var resource = FindResource(module, (IntPtr)id, (IntPtr)2);
                Assert.NotEqual(IntPtr.Zero, resource);
                var size = SizeofResource(module, resource);
                var pointer = LockResource(LoadResource(module, resource));
                Assert.NotEqual(IntPtr.Zero, pointer);
                var dib = new byte[checked((int)size)];
                Marshal.Copy(pointer, dib, 0, dib.Length);
                Assert.Equal(40, BitConverter.ToInt32(dib, 0));
                Assert.Equal(8, BitConverter.ToInt32(dib, 4));
                Assert.Equal(8, BitConverter.ToInt32(dib, 8));
                Assert.Equal(1, BitConverter.ToInt16(dib, 14));
                Assert.Equal(0, BitConverter.ToInt32(dib, 16));
                for (var y = 0; y < 8; y++)
                for (var x = 0; x < 8; x++)
                    Assert.Equal((dib[48 + (7 - y) * 4] & (0x80 >> x)) != 0,
                        OriginalPatternMask.PreservesDestination(id, x, y));
            }
        }
        finally { Assert.True(FreeLibrary(module)); }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadLibraryExW")]
    private static extern IntPtr LoadLibraryEx(string file, IntPtr reserved, uint flags);
    [DllImport("kernel32.dll", EntryPoint = "FindResourceW")]
    private static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
    [DllImport("kernel32.dll")]
    private static extern uint SizeofResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LockResource(IntPtr resource);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr module);
}
