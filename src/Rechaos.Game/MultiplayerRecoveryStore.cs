using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Game;

/// <summary>One seat a player can still return to, as the interface reads it.</summary>
/// <remarks>
/// <para>
/// Two of the names here are easy to confuse. <c>DisplayName</c> is the player's own name in that
/// match; <c>SessionName</c> is the match's own, which is what a list of sessions has to be read by.
/// The match's name reaches every member on the wire, so it is kept for every member rather than
/// only for the host who typed it.
/// </para>
/// <para>
/// <c>LastUpdatedAt</c> is when this seat last had turn data stored against it, which is what tells
/// two unfinished sessions apart when both are still resumable. It is null, and <c>SessionName</c>
/// empty, in a record written by a build that stored neither.
/// </para>
/// </remarks>
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
    bool Completed,
    string Password = "",
    int SessionVersion = MultiplayerSessionVersion.Initial,
    string SessionName = "",
    DateTimeOffset? LastUpdatedAt = null,
    MultiplayerRecoveryFailure? LastFailure = null)
{
    public const int CurrentFormatVersion = 1;

    /// <summary>Whether this build plays the session this seat belongs to.</summary>
    /// <remarks>
    /// The membership stays worth keeping either way — the seat is still held, and a build of that
    /// session version can take it — so this is asked beside <see cref="CanReconnect"/> rather than
    /// folded into it. The browser of this build lists only what it can resume.
    /// </remarks>
    public bool IsCompatible => MultiplayerSessionVersion.CanResume(SessionVersion);

    public bool ShouldSuggestReconnect => !CleanExit && !Completed && IsCompatible;
    public bool CanReconnect => !Completed;

    /// <summary>The seat is still live and this build can carry the match on.</summary>
    public bool CanResume => CanReconnect && IsCompatible;
}

/// <summary>
/// The last non-recoverable online failure for a saved membership.
/// </summary>
/// <remarks>
/// This is a deliberately small forensic breadcrumb, not a replay or a transport capture. It
/// carries only machine-readable protocol context that can be safely included in a diagnostics
/// export; credentials, player names, match settings and orders never belong here.
/// </remarks>
public sealed record MultiplayerRecoveryFailure(
    DateTimeOffset OccurredAt,
    string Stage,
    string? Operation,
    int? HttpStatus,
    string? Reason,
    string? RequestId,
    int? PlanningTurn,
    int? LastEventSequence);

/// <summary>
/// One membership as it sits on disk.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="MultiplayerRecovery"/> because the token is not stored the way it is
/// held: <see cref="ProtectedToken"/> carries it sealed to the current user account where the
/// platform offers that, and <see cref="Token"/> carries it in clear where it does not. Exactly one
/// of the two is set. A file written by a build that predates the sealed form has only
/// <see cref="Token"/>, which is why reading that is still supported.
/// </para>
/// <para>
/// <see cref="Password"/> is stored in the clear, unlike the token. It opens one session's door to
/// anyone the player was going to read it out to anyway, where the token is that seat itself; and
/// the reason to keep it is that the player who resumes has to be able to read it out again.
/// A file from a build that did not write it has none, which reads back as a session without one.
/// </para>
/// <para>
/// <see cref="SessionVersion"/> is kept so the browser can leave out a seat this build cannot take
/// before the game dials the server for it. It is additive in both directions, which is why it does not
/// move <see cref="MultiplayerRecoveryHistory.CurrentFormatVersion"/>: a build that does not know
/// the field ignores it and keeps its reconnects, and a build that does reads a file without one
/// as <see cref="MultiplayerSessionVersion.Initial"/>, the only version that can have been stored
/// before the field existed. The file is a hint either way — the match view settles it.
/// </para>
/// </remarks>
internal sealed record PersistedRecovery(
    int FormatVersion,
    string Server,
    string MatchId,
    string PlayerId,
    string JoinCode,
    string DisplayName,
    bool IsHost,
    bool CleanExit,
    bool Completed,
    string? Token = null,
    string? ProtectedToken = null,
    string? Password = null,
    int? SessionVersion = null,
    string? SessionName = null,
    DateTimeOffset? LastUpdatedAt = null,
    MultiplayerRecoveryFailure? LastFailure = null);

internal sealed record MultiplayerRecoveryHistory(
    int FormatVersion,
    IReadOnlyList<PersistedRecovery> Sessions)
{
    /// <summary>
    /// Version 5 added <see cref="PersistedRecovery.LastFailure"/>; 4 added <see cref="PersistedRecovery.SessionName"/> and
    /// <see cref="PersistedRecovery.LastUpdatedAt"/>; 3 added
    /// <see cref="PersistedRecovery.ProtectedToken"/>; 2 is still read. Every field either version
    /// added is optional, so an older file reads back as a membership that simply knows less about
    /// itself rather than one that cannot be resumed.
    /// </summary>
    internal const int CurrentFormatVersion = 5;
    internal const int OldestReadableFormatVersion = 2;
}

/// <summary>Atomic local record of the seat needed to resume an interrupted online match.</summary>
/// <remarks>
/// A membership token is a full capability for that seat until the match is retired, so this file is
/// worth what a password is worth. Two things bound what it holds. Retired memberships are dropped
/// rather than written back, so a finished match's token stops existing on disk at the next save;
/// and the token is sealed to the current user account where the platform can do that, so another
/// account on the same machine cannot read it out of the file. Neither defends against something
/// already running as the player, which nothing local can, and a platform with no keystore keeps the
/// clear token, because the alternative is losing the reconnect this file exists for.
/// </remarks>
public static class MultiplayerRecoveryStore
{
    private const long MaximumFileBytes = 131072;

    /// <summary>
    /// Memberships kept.
    /// </summary>
    /// <remarks>
    /// Retired ones are already dropped, so this bounds only how many matches one player can have
    /// running at once, and that is a handful. The old bound of 32 bounded the file rather than the
    /// number of live tokens it was worth holding.
    /// </remarks>
    private const int MaximumSessions = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        RespectRequiredConstructorParameters = true
    };

    public static MultiplayerRecovery? Load(string path)
        => LoadAll(path).FirstOrDefault();

    /// <summary>How many times a read that could not open the file is tried again, and how far apart.</summary>
    /// <remarks>
    /// A backup tool or a virus scanner holding the file open for a moment is the whole of what
    /// this covers, and a moment is what it waits. Two retries at 25ms is bounded at 50ms on a
    /// startup path, and the alternative is a player being shown no previous sessions.
    /// </remarks>
    private const int ReadAttempts = 3;

    private static readonly TimeSpan ReadRetryDelay = TimeSpan.FromMilliseconds(25);

    /// <summary>
    /// The memberships on file, or none when the file cannot be read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A file whose CONTENTS this cannot read is MOVED ASIDE rather than left where the next save
    /// will overwrite it. It holds live seats in running matches, and "the parse threw" is not the
    /// same fact as "there are no seats": a partial write from a build that crashed, a file
    /// half-synced by a backup tool, or a format from a build newer than this one all arrive here,
    /// and a player who updates the game again would get their matches back — if the file still
    /// existed. It is kept beside the original with a <c>.corrupt</c> suffix, and the last good file
    /// this writes is kept as <c>.bak</c>, so both are there for a later build or for a hand repair.
    /// </para>
    /// <para>
    /// Failing to READ the bytes at all is the opposite fact and is handled the opposite way. A
    /// backup tool or a virus scanner holding the file open answers with an <see cref="IOException"/>
    /// that says nothing whatever about what the file holds, and a moment later the same file reads
    /// perfectly — so it is retried briefly and then left exactly where it is. Renaming it to
    /// <c>.corrupt</c> over a transient lock took a player's live seats off the previous-sessions
    /// screen and out of the only file that knew about them.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<MultiplayerRecovery> LoadAll(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (ReadBytesOrNull(path) is not { } bytes) return [];
        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.TryGetProperty("Sessions", out _))
            {
                var history = JsonSerializer.Deserialize<MultiplayerRecoveryHistory>(bytes, JsonOptions);
                if (history is null
                    || history.FormatVersion < MultiplayerRecoveryHistory.OldestReadableFormatVersion
                    || history.FormatVersion > MultiplayerRecoveryHistory.CurrentFormatVersion)
                {
                    return [];
                }
                return Keepable(history.Sessions.Select(Revive));
            }
            // Version 1 contained one bare membership. Reading it here makes the upgrade lossless.
            var recovery = JsonSerializer.Deserialize<MultiplayerRecovery>(bytes, JsonOptions);
            return Keepable([recovery]);
        }
        catch
        {
            // The bytes are here and they are not a history this build can make sense of.
            SetAside(path);
            return [];
        }
    }

    /// <summary>
    /// The file's bytes, or null when there are none to parse.
    /// </summary>
    /// <remarks>
    /// Three ways to have nothing, and only one of them says anything about the contents: there is
    /// no file, the file is far too large to be one of these — set aside here, where its size was
    /// measured — or the read itself would not go through, which is retried and then left alone.
    /// </remarks>
    private static byte[]? ReadBytesOrNull(string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists) return null;
                if (file.Length > MaximumFileBytes)
                {
                    SetAside(path);
                    return null;
                }
                return File.ReadAllBytes(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt >= ReadAttempts) return null;
                Thread.Sleep(ReadRetryDelay);
            }
        }
    }

    /// <summary>Moves an unreadable history out of the way of the next save. Best effort.</summary>
    private static void SetAside(string path)
    {
        try
        {
            File.Move(path, path + ".corrupt", overwrite: true);
        }
        catch
        {
            // A file that cannot even be renamed is one nothing here can do anything about; the
            // player loses the reconnect either way, and failing the load loudly would take the
            // whole online screen with it.
        }
    }

    public static bool TrySave(string path, MultiplayerRecovery recovery, bool durable = false)
        => TrySaveAll(path, [recovery], durable);

    /// <summary>
    /// Writes the memberships through a temporary file and an atomic rename.
    /// </summary>
    /// <param name="path">Where the history lives.</param>
    /// <param name="recoveries">What to keep; retired memberships are dropped on the way in.</param>
    /// <param name="durable">
    /// Whether to wait for the bytes to reach the disk before renaming.
    /// </param>
    /// <remarks>
    /// <para>
    /// The rename is what makes the file never torn, and it does that whether or not the write was
    /// flushed: a reader sees either the old file or the new one. <paramref name="durable"/> adds
    /// the guarantee that the new one survives losing power, and that costs an fsync.
    /// </para>
    /// <para>
    /// Which is worth paying for depends on what the write says. Every resolved turn stamps this
    /// file with a fresh <c>LastUpdatedAt</c>, on the game thread, in the frame the new turn
    /// appears — and losing the last stamp costs only the order of a list on the previous-sessions
    /// screen. The clean-exit and completed marks are different: they are the difference between
    /// offering a player a reconnect and telling them a match is over, so those are written
    /// durably.
    /// </para>
    /// </remarks>
    public static bool TrySaveAll(
        string path,
        IEnumerable<MultiplayerRecovery> recoveries,
        bool durable = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(recoveries);
        var sessions = Keepable(recoveries).Select(Persist).ToArray();
        var temporaryPath = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            // The previous file, kept for one generation. A save that drops a membership this
            // build could not read is the other way the seats go, and this is what is left to
            // recover them from.
            try
            {
                if (File.Exists(path)) File.Copy(path, path + ".bak", overwrite: true);
            }
            catch
            {
                // A backup that cannot be written must not stop the save it was taken for.
            }
            using (var stream = new FileStream(
                       temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new MultiplayerRecoveryHistory(
                    MultiplayerRecoveryHistory.CurrentFormatVersion, sessions), JsonOptions);
                stream.Flush(flushToDisk: durable);
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

    /// <summary>
    /// The memberships worth holding: well formed, still resumable, and no more than the cap.
    /// </summary>
    /// <remarks>
    /// A completed match's token opens nothing anybody wants, and the server retires the match
    /// anyway, so holding it is a live credential on disk for no benefit. Filtering on the way in as
    /// well as on the way out is what retires one an older build left behind.
    /// </remarks>
    private static MultiplayerRecovery[] Keepable(IEnumerable<MultiplayerRecovery?> recoveries) =>
        recoveries
            .Where(recovery => IsValid(recovery) && recovery!.CanReconnect)
            .Select(recovery => recovery!)
            .Take(MaximumSessions)
            .ToArray();

    private static PersistedRecovery Persist(MultiplayerRecovery recovery)
    {
        var sealedToken = Protect(recovery.Token);
        return new PersistedRecovery(
            recovery.FormatVersion,
            recovery.Server,
            recovery.MatchId,
            recovery.PlayerId,
            recovery.JoinCode,
            recovery.DisplayName,
            recovery.IsHost,
            recovery.CleanExit,
            recovery.Completed,
            Token: sealedToken is null ? recovery.Token : null,
            ProtectedToken: sealedToken,
            Password: recovery.Password.Length > 0 ? recovery.Password : null,
            SessionVersion: recovery.SessionVersion,
            SessionName: recovery.SessionName.Length > 0 ? recovery.SessionName : null,
            LastUpdatedAt: recovery.LastUpdatedAt,
            LastFailure: recovery.LastFailure);
    }

    private static MultiplayerRecovery? Revive(PersistedRecovery stored)
    {
        var token = stored.ProtectedToken is { } sealedToken
            ? Unprotect(sealedToken)
            : stored.Token;
        // A sealed token that will not open belongs to another account, or to a machine this profile
        // was copied from. There is nothing to resume with, so the membership is dropped rather than
        // offered as a reconnect that would answer 401.
        if (string.IsNullOrEmpty(token)) return null;
        return new MultiplayerRecovery(
            stored.FormatVersion,
            stored.Server,
            stored.MatchId,
            stored.PlayerId,
            token,
            stored.JoinCode,
            stored.DisplayName,
            stored.IsHost,
            stored.CleanExit,
            stored.Completed,
            stored.Password ?? string.Empty,
            stored.SessionVersion ?? MultiplayerSessionVersion.Initial,
            stored.SessionName ?? string.Empty,
            stored.LastUpdatedAt,
            stored.LastFailure);
    }

    /// <summary>
    /// Seals a token to the current user account, or answers null where the platform cannot.
    /// </summary>
    /// <remarks>
    /// DPAPI on Windows. The macOS Keychain and libsecret are the equivalents elsewhere and both
    /// want a native dependency the game does not otherwise carry, so those platforms keep the clear
    /// token for now, under the user's own data root.
    /// </remarks>
    private static string? Protect(string token)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            return Convert.ToBase64String(ProtectedData.Protect(
                Encoding.UTF8.GetBytes(token),
                optionalEntropy: null,
                DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static string? Unprotect(string sealedToken)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(
                Convert.FromBase64String(sealedToken),
                optionalEntropy: null,
                DataProtectionScope.CurrentUser));
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return null;
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
            DisplayName.Length: > 0 and <= 32,
            Password.Length: <= 128,
            SessionVersion: >= 0,
            SessionName.Length: <= 64,
            LastFailure: null or
            {
                Stage.Length: > 0 and <= 48,
                Operation: null or { Length: <= 96 },
                Reason: null or { Length: <= 96 },
                RequestId: null or { Length: <= 128 },
                PlanningTurn: null or >= 0,
                LastEventSequence: null or >= 0
            }
        }
        && recovery.Server.Length <= 256
        && Uri.TryCreate(recovery.Server, UriKind.Absolute, out var server)
        && server.Scheme is "http" or "https";
}
