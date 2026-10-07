using System.Diagnostics;
using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace Rechaos.OriginalProbe;

/// <summary>
/// A copy of the drawing area taken while the setup name editor, dialog 139, was open (SCR-SETUP-003):
/// the name step and the <c>{SHOT}</c> token it was taken at, the edit control's text and selection
/// at the time, the dialog's window rectangle and the edit control's client rectangle in drawing-area
/// pixels, the distinct images the copies showed as files, and each copy's time in milliseconds from
/// the first with the index of the image it showed. <see cref="ScreenAgrees"/> says whether a copy of
/// the desktop at the same place showed the same pixels as the window copies composed.
/// </summary>
internal sealed record NameShotRecord(
    int Entry, int Shot, string Text, int SelectionStart, int SelectionEnd, int[] DialogRect, int[] EditRect,
    List<string> Files, List<int[]> Samples, bool ScreenAgrees);

internal sealed partial class NewGameSession
{
    private readonly List<NameShotRecord> _nameShots = [];

    // {SHOT} in name:TOKENS: copies of the drawing area with the dialog over it, taken every 20 ms
    // for 1.2 seconds so that both phases of the caret's blink are kept with the times they changed
    // (SRC-WIN32-CARETS). The game window and the dialog are copied from their own device contexts,
    // which DWM keeps whole whatever covers them, and composed at the dialog's place; a copy of the
    // desktop is taken as well and only compared.
    private void CaptureNameDialog(IntPtr window, IntPtr dialog, IntPtr edit, int shot)
    {
        const int width = CaptureFixture.Width, height = CaptureFixture.Height;
        var entry = _nameEntries.Count;
        var origin = new Native.Point();
        Native.ClientToScreen(window, ref origin);
        Native.GetWindowRect(dialog, out var dialogRect);
        Native.GetClientRect(edit, out var editClient);
        var editOrigin = new Native.Point();
        Native.ClientToScreen(edit, ref editOrigin);
        var (text, start, end) = ReadEditState(edit);
        var images = new List<byte[]>();
        var hashes = new List<UInt128>();
        var samples = new List<int[]>();
        var screenAgrees = true;
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 1200)
        {
            var at = (int)clock.ElapsedMilliseconds;
            var composed = CopyWindow(window, false, width, height, 0, 0);
            var dialogCopy = CopyWindow(dialog, true, dialogRect.Right - dialogRect.Left,
                dialogRect.Bottom - dialogRect.Top, 0, 0);
            var screen = CopyWindow(IntPtr.Zero, false, width, height, origin.X, origin.Y);
            Paste(composed, width, height, dialogCopy, dialogRect.Right - dialogRect.Left,
                dialogRect.Left - origin.X, dialogRect.Top - origin.Y);
            // The desktop copy of the first sample that disagrees is kept to show the difference.
            if (screenAgrees && !composed.AsSpan().SequenceEqual(screen))
            {
                screenAgrees = false;
                WriteBitmap(Path.Combine(outputDirectory, $"name-{entry}-{shot}-desktop.bmp"), width, height, screen);
            }
            var hash = XxHash128.HashToUInt128(composed);
            var index = hashes.IndexOf(hash);
            if (index < 0)
            {
                index = hashes.Count;
                hashes.Add(hash);
                images.Add(composed);
            }
            samples.Add([at, index]);
            _process.Pump(TimeSpan.FromMilliseconds(20));
        }
        var files = new List<string>();
        for (var index = 0; index < images.Count; index++)
        {
            var file = $"name-{entry}-{shot}-{index}.bmp";
            WriteBitmap(Path.Combine(outputDirectory, file), width, height, images[index]);
            files.Add(file);
        }
        _nameShots.Add(new NameShotRecord(entry, shot, text, start, end,
            [dialogRect.Left - origin.X, dialogRect.Top - origin.Y, dialogRect.Right - dialogRect.Left, dialogRect.Bottom - dialogRect.Top],
            [editOrigin.X - origin.X, editOrigin.Y - origin.Y, editClient.Right, editClient.Bottom],
            files, samples, screenAgrees));
    }

    // {PRESSxx} in name:TOKENS: a press and release of the left button on the edit control at client
    // point (xx, middle of the control), as the dialog's message loop would deliver them.
    private void PressEdit(IntPtr edit, int x)
    {
        Native.GetClientRect(edit, out var client);
        var point = PointParameter(x, client.Bottom / 2);
        Native.PostMessageW(edit, Native.WmLButtonDown, 1, point);
        _process.Pump(TimeSpan.FromSeconds(0.12));
        Native.PostMessageW(edit, Native.WmLButtonUp, IntPtr.Zero, point);
        _process.Pump(TimeSpan.FromSeconds(0.12));
    }

    // The edit control's text (WM_GETTEXT) and selection (EM_GETSEL), asked with a timeout so that a
    // stopped original cannot hold the probe.
    private static (string Text, int Start, int End) ReadEditState(IntPtr edit)
    {
        // The control is an ANSI window, and the ANSI calls reach it without the conversion of
        // positions that the Unicode calls go through.
        const uint wmGetText = 0x000D, emGetSel = 0x00B0, abortIfHung = 0x0002;
        var buffer = Marshal.AllocHGlobal(512);
        try
        {
            Native.SendMessageTimeoutA(edit, wmGetText, 512, buffer, abortIfHung, 1000, out var length);
            var text = Marshal.PtrToStringAnsi(buffer, (int)length) ?? "";
            Native.SendMessageTimeoutA(edit, emGetSel, IntPtr.Zero, IntPtr.Zero, abortIfHung, 1000, out var selection);
            return (text, (int)(selection & 0xFFFF), (int)((selection >> 16) & 0xFFFF));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // A top-down 32-bit copy of a window's client area, of the whole window with its frame when
    // whole is set, or of the desktop at (x, y) for window zero.
    private static byte[] CopyWindow(IntPtr window, bool whole, int width, int height, int x, int y)
    {
        var info = new byte[40];
        BitConverter.GetBytes(40).CopyTo(info, 0);
        BitConverter.GetBytes(width).CopyTo(info, 4);
        BitConverter.GetBytes(-height).CopyTo(info, 8);
        BitConverter.GetBytes((short)1).CopyTo(info, 12);
        BitConverter.GetBytes((short)32).CopyTo(info, 14);
        var source = whole ? Native.GetWindowDC(window) : Native.GetDC(window);
        var memory = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var old = IntPtr.Zero;
        try
        {
            if (source == IntPtr.Zero) throw new InvalidOperationException("Cannot acquire a device context to copy.");
            memory = Native.CreateCompatibleDC(source);
            bitmap = Native.CreateDIBSection(source, info, 0, out var bits, IntPtr.Zero, 0);
            if (memory == IntPtr.Zero || bitmap == IntPtr.Zero || bits == IntPtr.Zero)
                throw new InvalidOperationException("Cannot allocate a copy.");
            old = Native.SelectObject(memory, bitmap);
            if (!Native.BitBlt(memory, 0, 0, width, height, source, x, y, 0x00CC0020))
                throw new InvalidOperationException("A copy failed.");
            Native.GdiFlush();
            var pixels = new byte[width * height * 4];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            return pixels;
        }
        finally
        {
            if (old != IntPtr.Zero) Native.SelectObject(memory, old);
            if (bitmap != IntPtr.Zero) Native.DeleteObject(bitmap);
            if (memory != IntPtr.Zero) Native.DeleteDC(memory);
            if (source != IntPtr.Zero) Native.ReleaseDC(window, source);
        }
    }

    private static void Paste(byte[] target, int width, int height, byte[] source, int sourceWidth, int x, int y)
    {
        var sourceHeight = source.Length / 4 / sourceWidth;
        for (var row = 0; row < sourceHeight; row++)
        {
            if (y + row < 0 || y + row >= height) continue;
            for (var column = 0; column < sourceWidth; column++)
            {
                if (x + column < 0 || x + column >= width) continue;
                Array.Copy(source, (row * sourceWidth + column) * 4, target, ((y + row) * width + x + column) * 4, 4);
            }
        }
    }
}

internal static partial class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X, Y;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowDC(IntPtr window);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    public static extern bool ClientToScreen(IntPtr window, ref Point point);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessageTimeoutA(IntPtr window, uint message, IntPtr wParam, IntPtr lParam,
        uint flags, uint timeout, out nint result);
}
