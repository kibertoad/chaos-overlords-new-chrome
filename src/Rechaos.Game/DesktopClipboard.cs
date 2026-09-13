using System.Runtime.InteropServices;

namespace Rechaos.Game;

/// <summary>Copies short game text through the SDL clipboard used by DesktopGL.</summary>
public static class DesktopClipboard
{
    public static bool TryGetText(out string text)
    {
        text = string.Empty;
        IntPtr pointer = IntPtr.Zero;
        try
        {
            pointer = GetNativeText();
            if (pointer == IntPtr.Zero) return false;
            text = Marshal.PtrToStringUTF8(pointer)?.Trim() ?? string.Empty;
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
