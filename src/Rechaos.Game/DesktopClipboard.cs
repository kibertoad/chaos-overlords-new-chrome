using System.Runtime.InteropServices;

namespace Rechaos.Game;

/// <summary>Copies short game text through the SDL clipboard used by DesktopGL.</summary>
public static class DesktopClipboard
{
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
}
