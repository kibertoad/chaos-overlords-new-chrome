using System.Runtime.InteropServices;
using System.Text;

namespace Rechaos.Game;

/// <summary>Copies short game text through the SDL clipboard used by DesktopGL.</summary>
public static class DesktopClipboard
{
    /// <summary>How much clipboard is read before trimming, so surrounding space is not the value.</summary>
    private const int ReadAheadCharacters = 64;

    public static bool TryGetText(out string text, int maximumCharacters)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumCharacters, 1);
        text = string.Empty;
        IntPtr pointer = IntPtr.Zero;
        try
        {
            pointer = GetNativeText();
            if (pointer == IntPtr.Zero) return false;
            // Read past the field width, then trim and filter, then cut. Cutting first turned a
            // join code copied from chat as " ABCD2345" into "ABCD234", with the status line
            // cheerfully reporting that the code had been pasted.
            var raw = ReadBoundedUtf8(pointer, Math.Max(maximumCharacters, ReadAheadCharacters));
            var filtered = new StringBuilder(raw.Length);
            foreach (var character in raw.Trim())
                if (!char.IsControl(character)) filtered.Append(character);
            var value = filtered.ToString().Trim();
            text = value.Length <= maximumCharacters ? value : value[..maximumCharacters];
            return text.Length > 0;
        }
        catch (Exception exception) when (exception is DllNotFoundException
            or EntryPointNotFoundException or BadImageFormatException)
        {
            return false;
        }
        finally
        {
            if (pointer != IntPtr.Zero) FreeNative(pointer);
        }
    }

    private static string ReadBoundedUtf8(IntPtr pointer, int maximumCharacters)
    {
        var maximumBytes = checked(maximumCharacters * 4);
        var length = 0;
        while (length < maximumBytes && Marshal.ReadByte(pointer, length) != 0) length++;
        if (length == 0) return string.Empty;
        var bytes = new byte[length];
        Marshal.Copy(pointer, bytes, 0, length);
        var value = Encoding.UTF8.GetString(bytes);
        return value.Length <= maximumCharacters ? value : value[..maximumCharacters];
    }

    public static bool TrySetText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        try
        {
            return SetNativeText(text) == 0;
        }
        catch (Exception exception) when (exception is DllNotFoundException
            or EntryPointNotFoundException or BadImageFormatException)
        {
            return false;
        }
    }

    private static int SetNativeText(string text)
    {
        if (OperatingSystem.IsWindows()) return SetWindowsClipboardText(text);
        if (OperatingSystem.IsLinux()) return SetLinuxClipboardText(text);
        if (OperatingSystem.IsMacOS()) return SetMacClipboardText(text);
        return -1;
    }

    private static IntPtr GetNativeText()
    {
        if (OperatingSystem.IsWindows()) return GetWindowsClipboardText();
        if (OperatingSystem.IsLinux()) return GetLinuxClipboardText();
        if (OperatingSystem.IsMacOS()) return GetMacClipboardText();
        return IntPtr.Zero;
    }

    private static void FreeNative(IntPtr pointer)
    {
        if (OperatingSystem.IsWindows()) FreeWindows(pointer);
        else if (OperatingSystem.IsLinux()) FreeLinux(pointer);
        else if (OperatingSystem.IsMacOS()) FreeMac(pointer);
    }

    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "SDL_SetClipboardText")]
    private static extern int SetWindowsClipboardText(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    [DllImport("libSDL2-2.0.so.0", CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "SDL_SetClipboardText")]
    private static extern int SetLinuxClipboardText(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    [DllImport("libSDL2-2.0.0.dylib", CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "SDL_SetClipboardText")]
    private static extern int SetMacClipboardText(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "SDL_GetClipboardText")]
    private static extern IntPtr GetWindowsClipboardText();

    [DllImport("libSDL2-2.0.so.0", CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "SDL_GetClipboardText")]
    private static extern IntPtr GetLinuxClipboardText();

    [DllImport("libSDL2-2.0.0.dylib", CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "SDL_GetClipboardText")]
    private static extern IntPtr GetMacClipboardText();

    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_free")]
    private static extern void FreeWindows(IntPtr pointer);

    [DllImport("libSDL2-2.0.so.0", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_free")]
    private static extern void FreeLinux(IntPtr pointer);

    [DllImport("libSDL2-2.0.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_free")]
    private static extern void FreeMac(IntPtr pointer);
}
