using System.Text.Json;

namespace Rechaos.Game;

public sealed record MultiplayerRecovery(
    int FormatVersion,
    string Server,
    string MatchId,
    string PlayerId,
    string Token,
    string JoinCode,
    string DisplayName,
    bool IsHost,
    bool CleanExit,
    bool Completed)
{
    public const int CurrentFormatVersion = 1;
    public bool ShouldSuggestReconnect => !CleanExit && !Completed;
    public bool CanReconnect => !Completed;
}

internal sealed record MultiplayerRecoveryHistory(
    int FormatVersion,
    IReadOnlyList<MultiplayerRecovery> Sessions)
{
    internal const int CurrentFormatVersion = 2;
}

/// <summary>Atomic local record of the seat needed to resume an interrupted online match.</summary>
public static class MultiplayerRecoveryStore
{
    private const long MaximumFileBytes = 131072;
    private const int MaximumSessions = 32;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        RespectRequiredConstructorParameters = true
    };

    public static MultiplayerRecovery? Load(string path)
        => LoadAll(path).FirstOrDefault();

    public static IReadOnlyList<MultiplayerRecovery> LoadAll(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > MaximumFileBytes) return [];
            var bytes = File.ReadAllBytes(path);
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.TryGetProperty("Sessions", out _))
            {
                var history = JsonSerializer.Deserialize<MultiplayerRecoveryHistory>(bytes, JsonOptions);
                return history?.FormatVersion == MultiplayerRecoveryHistory.CurrentFormatVersion
                    ? history.Sessions.Where(IsValid).Take(MaximumSessions).ToArray()
                    : [];
            }
            // Version 1 contained one bare membership. Reading it here makes the upgrade lossless.
            var recovery = JsonSerializer.Deserialize<MultiplayerRecovery>(bytes, JsonOptions);
            return IsValid(recovery) ? [recovery!] : [];
        }
        catch
        {
            return [];
        }
    }

    public static bool TrySave(string path, MultiplayerRecovery recovery)
        => TrySaveAll(path, [recovery]);

    public static bool TrySaveAll(string path, IEnumerable<MultiplayerRecovery> recoveries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(recoveries);
        var sessions = recoveries.Where(IsValid).Take(MaximumSessions).ToArray();
        var temporaryPath = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using (var stream = new FileStream(
                       temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new MultiplayerRecoveryHistory(
                    MultiplayerRecoveryHistory.CurrentFormatVersion, sessions), JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, path, overwrite: true);
            return true;
        }
        catch
        {
            try { File.Delete(temporaryPath); } catch { }
            return false;
        }
    }

    private static bool IsValid(MultiplayerRecovery? recovery) =>
        recovery is
        {
            FormatVersion: MultiplayerRecovery.CurrentFormatVersion,
            MatchId.Length: > 0 and <= 128,
            PlayerId.Length: > 0 and <= 128,
            Token.Length: > 0 and <= 512,
            JoinCode.Length: > 0 and <= 32,
            DisplayName.Length: > 0 and <= 32
        }
        && recovery.Server.Length <= 256
        && Uri.TryCreate(recovery.Server, UriKind.Absolute, out var server)
        && server.Scheme is "http" or "https";
}
