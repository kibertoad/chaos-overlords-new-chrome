using System.Reflection;
using Microsoft.Xna.Framework.Media;

namespace Rechaos.Game;

/// <summary>Compatibility with the pinned MonoGame DesktopGL streaming backend.</summary>
internal static class DesktopGlSoundtrackCompletionState
{
    private static readonly Type StreamerType = typeof(Song).Assembly.GetType(
        "Microsoft.Xna.Framework.Audio.OggStreamer", throwOnError: true)!;
    private static readonly PropertyInfo Instance = StreamerType.GetProperty("Instance",
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new MissingMemberException(StreamerType.FullName, "Instance");
    private static readonly FieldInfo PendingFinish = StreamerType.GetField("pendingFinish",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(StreamerType.FullName, "pendingFinish");

    /// <summary>Whether the streaming worker holds an end of stream it has not yet delivered.</summary>
    internal static bool Pending => (bool)PendingFinish.GetValue(Instance.GetValue(null))!;

    internal static void ClearBeforeNewSong()
    {
        // RULE-AUDIO-001, RULE-AUDIO-002: every newly started track must run to its end.
        // MonoGame 3.8.5.1 shares this EOF flag across streams and explicit Stop does
        // not clear it. The previous transport has stopped and removed its stream,
        // synchronizing with its worker's prepare mutex. No new stream is registered
        // until MediaPlayer.Play, so this reset cannot erase the new song's EOF.
        PendingFinish.SetValue(Instance.GetValue(null), false);
    }
}
