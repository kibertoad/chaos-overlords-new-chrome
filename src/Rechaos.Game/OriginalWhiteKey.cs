using Microsoft.Xna.Framework;

namespace Rechaos.Game;

internal static class OriginalWhiteKey
{
    public static void Apply(Color[] colors)
    {
        // RULE-GFX-003, FND-PLATFORM-008: only maximum RGB555 white is keyed.
        // A maximum five-bit channel expands to 248 or 255; the next lower
        // channel stays below this threshold in either representation.
        for (var index = 0; index < colors.Length; index++)
            if (colors[index].R >= 248 && colors[index].G >= 248 && colors[index].B >= 248)
                colors[index] = Color.Transparent;
    }
}
