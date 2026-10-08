using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Game;

/// <summary>
/// One membership as it sits on disk.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="MultiplayerRecovery"/> because the token is not stored the way it is
/// held. <see cref="ProtectedToken"/> carries it sealed with DPAPI to the current user account on
/// Windows. <see cref="TokenStore"/> names the operating-system store that holds it on macOS
/// (<c>keychain</c>) and Linux (<c>secret-service</c>), and <see cref="TokenAccount"/> the name it
/// is filed under there; see <see cref="RecoveryTokenProtection"/>. <see cref="Token"/> carries it in clear where
/// neither is available. Exactly one of the three is set. A file written by a build that predates
/// the sealed forms has only <see cref="Token"/>, which is why reading that is still supported, and
/// why the first load that can seal it rewrites the file without it.
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
/// <para>
/// <see cref="Spectating"/> is additive in the same way and for the same reason. It is written only
/// for a spectator's membership, and those live in a file of their own, which a build that does not
/// know the field never opens.
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
    MultiplayerRecoveryFailure? LastFailure = null,
    bool? Spectating = null,
    string? TokenStore = null,
    string? TokenAccount = null);

internal sealed record MultiplayerRecoveryHistory(
    int FormatVersion,
    IReadOnlyList<PersistedRecovery> Sessions)
{
    /// <summary>
    /// Version 6 added <see cref="PersistedRecovery.TokenStore"/> and
    /// <see cref="PersistedRecovery.TokenAccount"/>; 5 added
    /// <see cref="PersistedRecovery.LastFailure"/>; 4 added <see cref="PersistedRecovery.SessionName"/> and
    /// <see cref="PersistedRecovery.LastUpdatedAt"/>; 3 added
    /// <see cref="PersistedRecovery.ProtectedToken"/>; 2 is still read. Every field these versions
    /// added is optional, so an older file reads back as a membership that simply knows less about
    /// itself rather than one that cannot be resumed.
    /// </summary>
    internal const int CurrentFormatVersion = 6;
    internal const int OldestReadableFormatVersion = 2;

    /// <summary>
    /// The newest version without <see cref="PersistedRecovery.TokenStore"/>, which a file is still
    /// written as when no membership in it uses that field.
    /// </summary>
    /// <remarks>
    /// A build of version 5 would read a token kept in an operating-system store as a membership
    /// without a token, drop it, and on its next save write the file without it: the seat would be
    /// gone. Stamping such a file 6 makes that build leave it alone instead. A file with no such
    /// membership, which is every file on Windows, stays readable by that build, so going back one
    /// build there costs nothing.
    /// </remarks>
    internal const int FormatVersionWithoutTokenStore = 5;

    internal static int FormatVersionFor(IEnumerable<PersistedRecovery> sessions) =>
        sessions.Any(session => session.TokenStore is not null)
            ? CurrentFormatVersion
            : FormatVersionWithoutTokenStore;
}

/// <summary>Atomic local record of the seat needed to resume an interrupted online match.</summary>
/// <remarks>
/// A membership token is a full capability for that seat until the match is retired, so this file is
/// worth what a password is worth. Two things bound what it holds. Retired memberships are dropped
/// rather than written back, so a finished match's token stops existing on disk at the next save;
/// and the token is kept out of the file's clear text where the platform can do that (DPAPI on
/// Windows, the Keychain on macOS, the Secret Service on Linux; see
/// <see cref="RecoveryTokenProtection"/>), so another account on the same machine, or a copy of the
/// file, does not carry the seat. Neither defends against something already running as the player,
/// which nothing local can. Where no store answers, the token stays in clear in a file only its
/// owner can read, because the alternative is losing the reconnect this file exists for, and
/// <see cref="KeepsTokensInClear"/> lets the interface say so.
/// </remarks>
public static class MultiplayerRecoveryStore
{
    private const long MaximumFileBytes = 131072;

    /// <summary>
    /// Memberships kept that this build can resume.
    /// </summary>
    /// <remarks>
    /// Retired ones are already dropped, so this bounds only how many matches one player can have
    /// running at once, and that is a handful. The old bound of 32 bounded the file rather than the
    /// number of live tokens it was worth holding.
    /// </remarks>
    private const int MaximumSessions = 8;

    /// <summary>
    /// Memberships kept from other session versions, bounded apart from <see cref="MaximumSessions"/>.
    /// </summary>
    /// <remarks>
    /// This build never shows one of these, so they must not take a slot from a match it can play:
    /// after a session-version bump a player holding old seats would otherwise see fewer rows than
    /// the cap allows. They are still kept, because a build of their version can take the seat.
    /// </remarks>
    private const int MaximumOtherVersionSessions = 8;

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
    /// same fact as "there are no seats": a partial write from a build that crashed or a file
    /// half-synced by a backup tool arrive here, and a hand repair can get the matches back — if
    /// the file still exists. It is kept beside the original with a <c>.corrupt</c> suffix, and the last good file
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
    /// <para>
    /// A history written by a build NEWER than this one is neither of those, and is left exactly
    /// where it is: it is well formed, it holds that build's seats, and the player who goes back to
    /// that build expects to find them. This build shows no seats from it, and
    /// <see cref="TrySaveAll"/> refuses to write over it (or over its <c>.bak</c>), so a quick look
    /// with an older build no longer wipes the newer one's online seats.
    /// </para>
    /// <para>
    /// A primary that is empty or unreadable falls back to the <c>.bak</c> generation. The per-turn
    /// stamp is written without an fsync, so a power loss can leave a renamed-in but empty file,
    /// and the previous generation beside it still holds every seat.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<MultiplayerRecovery> LoadAll(string path)
        => LoadAll(path, RecoveryTokenProtection.Platform);

    /// <summary>
    /// <see cref="LoadAll(string)"/> with the token protection named, which is what tests use to
    /// keep the operating system's own stores out of their way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A membership whose token sits in an operating-system store that does not answer now (the
    /// keyring is locked, the player dismissed its prompt, no keyring is running this session) is
    /// not offered, because there is no token to offer it with, and it is not dropped either: it
    /// is held back and written back unchanged by every save, until a load finds the store
    /// answering again. A store that answers and has no such token is the one case that drops it.
    /// </para>
    /// <para>
    /// A file that still holds a token in clear where this platform can protect it is rewritten at
    /// once, and its <c>.bak</c> generation with it, so the clear token does not wait for the next
    /// turn to leave the disk.
    /// </para>
    /// </remarks>
    internal static IReadOnlyList<MultiplayerRecovery> LoadAll(
        string path, RecoveryTokenProtection protection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(protection);
        var ledger = LedgerFor(path);
        var scope = AccountScope(path);
        var primary = Read(path, setAsideWhenCorrupt: true, protection, ledger, scope);
        if (primary.Kind == ReadKind.Read) NewerHistory.NoteCurrent(path);
        var loaded = primary;
        if (primary.Kind == ReadKind.Corrupt)
        {
            // The backup is only read, never set aside: it is the last copy there is.
            var backup = Read(path + ".bak", setAsideWhenCorrupt: false, protection, ledger, scope);
            loaded = backup.Kind == ReadKind.Read ? backup : ReadResult.Of(ReadKind.Corrupt);
        }
        if (loaded.Kind != ReadKind.Read) return loaded.Recoveries;
        lock (ledger.Gate)
        {
            ledger.HeldBack = loaded.HeldBack;
            ledger.KeptInClear = loaded.HasClearTokens;
        }
        if (loaded.HasClearTokens && protection.CanSeal) SealClearTokens(path, loaded.Recoveries, protection);
        return loaded.Recoveries;
    }

    /// <summary>
    /// Whether the last load or save of this history left a token in clear in the file.
    /// </summary>
    /// <remarks>
    /// True only where no store took the token: a Linux system without libsecret or a running
    /// keyring, a store that refused the write, or a platform with none. The Unfinished Sessions
    /// screen reads it to say so in one line.
    /// </remarks>
    public static bool KeepsTokensInClear(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var ledger = LedgerFor(path);
        lock (ledger.Gate) return ledger.KeptInClear;
    }

    /// <summary>
    /// Rewrites a history that still carries clear tokens, and replaces its backup with the result.
    /// </summary>
    /// <remarks>
    /// The save copies the old file to <c>.bak</c> before writing, which would leave the clear
    /// token there for one more generation; once the new file holds no clear token, the backup is
    /// replaced by a copy of it. A save that could not seal every token leaves the backup alone,
    /// since it then protects nothing.
    /// </remarks>
    private static void SealClearTokens(
        string path, IReadOnlyList<MultiplayerRecovery> recoveries, RecoveryTokenProtection protection)
    {
        if (!TrySaveAll(path, recoveries, durable: true, protection)) return;
        if (KeepsTokensInClear(path)) return;
        try
        {
            File.Copy(path, path + ".bak", overwrite: true);
            RestrictToOwner(path + ".bak");
        }
        catch
        {
            // The primary is sealed; a backup that cannot be replaced keeps the old generation.
        }
    }

    private enum ReadKind
    {
        /// <summary>No file, or one that could not be opened just now.</summary>
        Absent,
        Read,
        /// <summary>A history from a newer build; kept, and not written over.</summary>
        Newer,
        Corrupt
    }

    /// <param name="HeldBack">Memberships whose token store did not answer; see <see cref="LoadAll(string, RecoveryTokenProtection)"/>.</param>
    /// <param name="HasClearTokens">Whether any membership kept carries its token in clear.</param>
    private readonly record struct ReadResult(
        ReadKind Kind,
        IReadOnlyList<MultiplayerRecovery> Recoveries,
        PersistedRecovery[] HeldBack,
        bool HasClearTokens)
    {
        public static ReadResult Of(ReadKind kind) => new(kind, [], [], false);
    }

    private static ReadResult Read(
        string path,
        bool setAsideWhenCorrupt,
        RecoveryTokenProtection protection,
        TokenLedger ledger,
        string scope)
    {
        var (kind, bytes) = ReadBytes(path, setAsideWhenCorrupt);
        if (bytes is null) return ReadResult.Of(kind);
        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.TryGetProperty("Sessions", out _))
            {
                if (StoredFormatVersion(document) > MultiplayerRecoveryHistory.CurrentFormatVersion)
                {
                    return ReadResult.Of(ReadKind.Newer);
                }
                var history = JsonSerializer.Deserialize<MultiplayerRecoveryHistory>(bytes, JsonOptions);
                if (history is null
                    || history.FormatVersion < MultiplayerRecoveryHistory.OldestReadableFormatVersion)
                {
                    throw new JsonException("Not a readable recovery history.");
                }
                var revived = new List<MultiplayerRecovery?>(history.Sessions.Count);
                var clear = new HashSet<MultiplayerRecovery>();
                var heldBack = new List<PersistedRecovery>();
                foreach (var stored in history.Sessions)
                {
                    var outcome = Revive(stored, protection, ledger, scope);
                    revived.Add(outcome.Recovery);
                    if (outcome.Recovery is not null && stored.Token is not null) clear.Add(outcome.Recovery);
                    if (outcome.HeldBack is { } held
                        && heldBack.Count < MaximumSessions + MaximumOtherVersionSessions)
                        heldBack.Add(held);
                }
                var kept = Keepable(revived);
                return new ReadResult(ReadKind.Read, kept, heldBack.ToArray(), kept.Any(clear.Contains));
            }
            // Version 1 contained one bare membership. Reading it here makes the upgrade lossless.
            var recovery = JsonSerializer.Deserialize<MultiplayerRecovery>(bytes, JsonOptions);
            var single = Keepable([recovery]);
            return new ReadResult(ReadKind.Read, single, [], single.Length > 0);
        }
        catch
        {
            // The bytes are here and they are not a history this build can make sense of.
            if (setAsideWhenCorrupt) SetAside(path);
            return ReadResult.Of(ReadKind.Corrupt);
        }
    }

    /// <summary>The history's <c>FormatVersion</c>, or null when it has no numeric one.</summary>
    private static int? StoredFormatVersion(JsonDocument document) =>
        document.RootElement.TryGetProperty("FormatVersion", out var version)
        && version.ValueKind == JsonValueKind.Number
        && version.TryGetInt32(out var number)
            ? number
            : null;

    /// <summary>
    /// Whether the file is a history from a newer build. Asked before every save, and answered from
    /// the file's stamp while it is still the one this build last wrote or read; see
    /// <see cref="NewerBuildFileGuard"/>.
    /// </summary>
    private static readonly NewerBuildFileGuard NewerHistory = new(
        "FormatVersion", MultiplayerRecoveryHistory.CurrentFormatVersion, requiredProperty: "Sessions");

    /// <summary>
    /// The file's bytes, or null when there are none to parse.
    /// </summary>
    /// <remarks>
    /// Three ways to have nothing, and only one of them says anything about the contents: there is
    /// no file, the file is far too large to be one of these — set aside here, where its size was
    /// measured — or the read itself would not go through, which is retried and then left alone.
    /// </remarks>
    private static (ReadKind Kind, byte[]? Bytes) ReadBytes(string path, bool setAsideWhenCorrupt)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists) return (ReadKind.Absent, null);
                if (file.Length > MaximumFileBytes)
                {
                    if (setAsideWhenCorrupt) SetAside(path);
                    return (ReadKind.Corrupt, null);
                }
                return (ReadKind.Read, File.ReadAllBytes(path));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt >= ReadAttempts) return (ReadKind.Absent, null);
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
            // It may hold tokens in clear from an older build, and it stays on disk.
            RestrictToOwner(path + ".corrupt");
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
    /// flushed: a reader sees either the old file or the new one (or, after a power loss without
    /// <paramref name="durable"/>, an empty one, which <see cref="LoadAll"/> reads past to the
    /// <c>.bak</c>). <paramref name="durable"/> adds
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
        => TrySaveAll(path, recoveries, durable, RecoveryTokenProtection.Platform);

    /// <summary>
    /// <see cref="TrySaveAll(string, IEnumerable{MultiplayerRecovery}, bool)"/> with the token
    /// protection named.
    /// </summary>
    /// <remarks>
    /// A token goes into the operating-system store before the file that names the store is
    /// written, so the file never points at a token that is not there yet. After the file is
    /// written, every token this process put in (or found in) the store for a membership the file
    /// no longer holds is removed from the store, which is how a forgotten, left or finished seat
    /// stops existing there too. A store that refuses the token leaves it in clear in the file.
    /// </remarks>
    internal static bool TrySaveAll(
        string path,
        IEnumerable<MultiplayerRecovery> recoveries,
        bool durable,
        RecoveryTokenProtection protection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(recoveries);
        ArgumentNullException.ThrowIfNull(protection);
        var kept = Keepable(recoveries);
        // A newer build's history is not this build's to replace: neither it nor the .bak copy of
        // it would survive the write below, and this build could not have read its seats back.
        if (NewerHistory.IsNewer(path, MaximumFileBytes)) return false;
        var ledger = LedgerFor(path);
        lock (ledger.Gate)
        {
            var sessions = kept.Select(recovery => Persist(path, recovery, protection, ledger)).ToList();
            var heldBack = ledger.HeldBack
                .Where(held => !sessions.Any(session => SameSeat(session, held)))
                .Take(MaximumSessions + MaximumOtherVersionSessions)
                .ToArray();
            sessions.AddRange(heldBack);
            if (!TryWrite(path, sessions, durable)) return false;
            ledger.HeldBack = heldBack;
            ledger.KeptInClear = sessions.Any(session => session.Token is not null);
            ForgetStoredTokens(sessions, protection, ledger);
            return true;
        }
    }

    private static bool TryWrite(string path, IReadOnlyList<PersistedRecovery> sessions, bool durable)
    {
        var temporaryPath = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            // The previous file, kept for one generation. A save that drops a membership this
            // build could not read is the other way the seats go, and this is what is left to
            // recover them from.
            try
            {
                if (File.Exists(path))
                {
                    File.Copy(path, path + ".bak", overwrite: true);
                    RestrictToOwner(path + ".bak");
                }
            }
            catch
            {
                // A backup that cannot be written must not stop the save it was taken for.
            }
            File.Delete(temporaryPath);
            using (var stream = new FileStream(temporaryPath, OwnerOnlyCreate))
            {
                JsonSerializer.Serialize(stream, new MultiplayerRecoveryHistory(
                    MultiplayerRecoveryHistory.FormatVersionFor(sessions), sessions), JsonOptions);
                stream.Flush(flushToDisk: durable);
            }
            File.Move(temporaryPath, path, overwrite: true);
            NewerHistory.NoteCurrent(path);
            return true;
        }
        catch
        {
            try { File.Delete(temporaryPath); } catch { }
            return false;
        }
    }

    /// <summary>
    /// The memberships worth holding: well formed, still live, and within the cap for their kind.
    /// </summary>
    /// <remarks>
    /// A completed match's token opens nothing anybody wants, and the server retires the match
    /// anyway, so holding it is a live credential on disk for no benefit. Filtering on the way in as
    /// well as on the way out is what retires one an older build left behind. Order is kept, so
    /// the newest of each kind survive the caps.
    /// </remarks>
    private static MultiplayerRecovery[] Keepable(IEnumerable<MultiplayerRecovery?> recoveries)
    {
        var kept = new List<MultiplayerRecovery>(MaximumSessions);
        var compatible = 0;
        var otherVersion = 0;
        foreach (var recovery in recoveries)
        {
            if (!IsValid(recovery) || !recovery!.CanReconnect) continue;
            if (recovery.IsCompatible)
            {
                if (compatible == MaximumSessions) continue;
                compatible++;
            }
            else
            {
                if (otherVersion == MaximumOtherVersionSessions) continue;
                otherVersion++;
            }
            kept.Add(recovery);
        }
        return kept.ToArray();
    }

    /// <summary>
    /// How a save creates the file it writes: on Linux and macOS readable and writable by its owner
    /// only, because it can hold a token in clear and always holds the session passwords.
    /// </summary>
    private static readonly FileStreamOptions OwnerOnlyCreate = CreateOptions();

    private static FileStreamOptions CreateOptions()
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None
        };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return options;
    }

    /// <summary>Narrows a copied file to its owner on Linux and macOS. Best effort.</summary>
    private static void RestrictToOwner(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // The file is still written; a mode that cannot be narrowed is no reason to fail.
        }
    }

    /// <summary>
    /// What this process knows about the tokens one history keeps in an operating-system store.
    /// </summary>
    /// <remarks>
    /// <see cref="Stored"/> is every account this process wrote or found in the store, with the
    /// token it holds, so an unchanged token is not written again on every turn's save and a
    /// membership the history drops can have its token removed. One ledger per history path, for
    /// the life of the process.
    /// </remarks>
    private sealed class TokenLedger
    {
        public readonly object Gate = new();
        public readonly Dictionary<string, string> Stored = new(StringComparer.Ordinal);

        /// <summary>The account each seat's token is filed under, by <see cref="SeatKey"/>.</summary>
        public readonly Dictionary<string, string> Accounts = new(StringComparer.Ordinal);

        /// <summary>
        /// Every account whose token the store refused, with that token. The saves run on the game
        /// thread every turn, and a keyring that refused once (a dismissed unlock or access
        /// prompt) would otherwise prompt again, blocking the frame, at each of them. A token that
        /// changes is offered again; an unchanged one waits for the next process.
        /// </summary>
        public readonly Dictionary<string, string> Refused = new(StringComparer.Ordinal);
        public PersistedRecovery[] HeldBack = [];
        public bool KeptInClear;
    }

    private static readonly ConcurrentDictionary<string, TokenLedger> Ledgers = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private static TokenLedger LedgerFor(string path) =>
        Ledgers.GetOrAdd(Path.GetFullPath(path), _ => new TokenLedger());

    /// <summary>
    /// Forgets what this process learned about the history's tokens, as a new process starts out.
    /// For tests.
    /// </summary>
    internal static void ForgetLedger(string path) => Ledgers.TryRemove(Path.GetFullPath(path), out _);

    /// <summary>
    /// The name a new seat's token is filed under in an operating-system store.
    /// </summary>
    /// <remarks>
    /// It starts with a digest of the history's own path, so two data roots on one account (a
    /// portable copy beside an installed one) never remove each other's tokens, and goes on to
    /// name the seat. The name is written into the file beside the store's, and a load reads the
    /// token by the name on file, so a data root that moves keeps its seats. Nothing in it is
    /// secret.
    /// </remarks>
    private static string NewAccount(string historyPath, MultiplayerRecovery recovery) =>
        $"{AccountScope(historyPath)}{recovery.MatchId}/{recovery.PlayerId}@{recovery.Server.ToLowerInvariant()}";

    /// <summary>
    /// The prefix every account <see cref="NewAccount"/> makes for this history starts with: a
    /// digest of the history's path and a slash.
    /// </summary>
    private static string AccountScope(string historyPath)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(historyPath)));
        return Convert.ToHexStringLower(digest, 0, 8) + "/";
    }

    /// <summary>What tells one seat from another, as <see cref="SameSeat"/> compares them.</summary>
    private static string SeatKey(string server, string matchId, string playerId) =>
        $"{matchId}|{playerId}|{server.ToLowerInvariant()}";

    private static bool SameSeat(PersistedRecovery left, PersistedRecovery right) =>
        string.Equals(left.Server, right.Server, StringComparison.OrdinalIgnoreCase)
        && left.MatchId == right.MatchId
        && left.PlayerId == right.PlayerId;

    /// <summary>
    /// Removes from the store every token this process knows of that the history no longer names.
    /// </summary>
    private static void ForgetStoredTokens(
        IReadOnlyList<PersistedRecovery> sessions,
        RecoveryTokenProtection protection,
        TokenLedger ledger)
    {
        if (protection.Store is not { } store || ledger.Stored.Count == 0) return;
        var named = sessions
            .Where(session => session.TokenStore == store.Name && session.TokenAccount is not null)
            .Select(session => session.TokenAccount!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var account in ledger.Stored.Keys.Where(account => !named.Contains(account)).ToArray())
        {
            if (!Guarded(() => store.TryDelete(account))) continue;
            ledger.Stored.Remove(account);
            foreach (var seat in ledger.Accounts.Where(pair => pair.Value == account).ToArray())
                ledger.Accounts.Remove(seat.Key);
        }
    }

    /// <summary>
    /// A store call that cannot take the game down: a library missing an entry point, or any
    /// other failure inside it, answers false.
    /// </summary>
    private static bool Guarded(Func<bool> call)
    {
        try
        {
            return call();
        }
        catch
        {
            return false;
        }
    }

    private static PersistedRecovery Persist(
        string historyPath,
        MultiplayerRecovery recovery,
        RecoveryTokenProtection protection,
        TokenLedger ledger)
    {
        if (protection.Store is { } store)
        {
            var seat = SeatKey(recovery.Server, recovery.MatchId, recovery.PlayerId);
            if (!ledger.Accounts.TryGetValue(seat, out var account))
                account = NewAccount(historyPath, recovery);
            var known = ledger.Stored.TryGetValue(account, out var held) && held == recovery.Token;
            var refused = !known
                && ledger.Refused.TryGetValue(account, out var declined) && declined == recovery.Token;
            if (known
                || (!refused && Guarded(() => store.TryStore(account, Label(recovery), recovery.Token))))
            {
                ledger.Refused.Remove(account);
                ledger.Stored[account] = recovery.Token;
                ledger.Accounts[seat] = account;
                return Persisted(recovery, token: null, protectedToken: null, store.Name, account);
            }
            ledger.Refused[account] = recovery.Token;
            return Persisted(recovery, recovery.Token, protectedToken: null, tokenStore: null, tokenAccount: null);
        }
        var sealedToken = protection.UseDpapi ? Protect(recovery.Token) : null;
        return Persisted(recovery,
            token: sealedToken is null ? recovery.Token : null,
            protectedToken: sealedToken,
            tokenStore: null,
            tokenAccount: null);
    }

    /// <summary>What a keychain or keyring browser shows for the item.</summary>
    private static string Label(MultiplayerRecovery recovery) =>
        recovery.SessionName.Length > 0
            ? $"Chaos Overlords online seat: {recovery.SessionName}"
            : $"Chaos Overlords online seat: match {recovery.MatchId}";

    private static PersistedRecovery Persisted(
        MultiplayerRecovery recovery,
        string? token,
        string? protectedToken,
        string? tokenStore,
        string? tokenAccount)
    {
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
            Token: token,
            ProtectedToken: protectedToken,
            Password: recovery.Password.Length > 0 ? recovery.Password : null,
            SessionVersion: recovery.SessionVersion,
            SessionName: recovery.SessionName.Length > 0 ? recovery.SessionName : null,
            LastUpdatedAt: recovery.LastUpdatedAt,
            LastFailure: recovery.LastFailure,
            Spectating: recovery.Spectating ? true : null,
            TokenStore: tokenStore,
            TokenAccount: tokenAccount);
    }

    /// <summary>One stored membership as a load reads it.</summary>
    /// <param name="Recovery">The membership, when its token could be read.</param>
    /// <param name="HeldBack">
    /// The stored form, when its token sits in a store that did not answer, so the next save can
    /// write it back unchanged.
    /// </param>
    private readonly record struct Revived(MultiplayerRecovery? Recovery, PersistedRecovery? HeldBack);

    /// <remarks>
    /// A token found under an account of another data root's scope is read but not adopted: the
    /// ledger does not record it, so the next save files the token under this root's own account
    /// and this root never deletes the other one. A copied data root then leaves the original's
    /// items alone, and a moved one leaves its old items behind in the store.
    /// </remarks>
    private static Revived Revive(
        PersistedRecovery stored,
        RecoveryTokenProtection protection,
        TokenLedger ledger,
        string scope)
    {
        string? token;
        if (stored.TokenStore is { } storeName)
        {
            if (stored.TokenAccount is not { Length: > 0 and <= 512 } account) return default;
            var lookup = SecretLookup.Unavailable;
            string? found = null;
            if (protection.Store is { } store && store.Name == storeName)
            {
                try
                {
                    lookup = store.TryLookup(account, out found);
                }
                catch
                {
                    lookup = SecretLookup.Unavailable;
                }
            }
            switch (lookup)
            {
                case SecretLookup.Found when !string.IsNullOrEmpty(found):
                    if (account.StartsWith(scope, StringComparison.Ordinal))
                    {
                        lock (ledger.Gate)
                        {
                            ledger.Stored[account] = found;
                            ledger.Accounts[SeatKey(stored.Server, stored.MatchId, stored.PlayerId)] = account;
                        }
                    }
                    token = found;
                    break;
                case SecretLookup.Missing:
                    // The store answered and the token is gone: removed by hand, or the keyring was
                    // reset. Nothing can resume this seat.
                    return default;
                default:
                    return IsHoldable(stored) ? new Revived(null, stored) : default;
            }
        }
        else
        {
            token = stored.ProtectedToken is { } sealedToken
                ? protection.UseDpapi ? Unprotect(sealedToken) : null
                : stored.Token;
        }
        // A sealed token that will not open belongs to another account, or to a machine this profile
        // was copied from. There is nothing to resume with, so the membership is dropped rather than
        // offered as a reconnect that would answer 401.
        if (string.IsNullOrEmpty(token)) return default;
        return new Revived(Membership(stored, token), null);
    }

    private static MultiplayerRecovery Membership(PersistedRecovery stored, string token) =>
        new(
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
            stored.LastFailure,
            stored.Spectating ?? false);

    /// <summary>
    /// Whether a membership whose token could not be read is worth writing back: well formed
    /// apart from the token, and not retired.
    /// </summary>
    private static bool IsHoldable(PersistedRecovery stored)
    {
        try
        {
            var placeholder = Membership(stored, "held");
            return IsValid(placeholder) && placeholder.CanReconnect;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Seals a token to the current user account with DPAPI, or answers null where that fails.
    /// </summary>
    /// <remarks>
    /// Windows only. macOS and Linux keep the token in an operating-system store instead; see
    /// <see cref="RecoveryTokenProtection"/>.
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
