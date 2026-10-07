using System.Text.Json;
using Rechaos.Game;
using Rechaos.Multiplayer.Protocol;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// How the recovery history keeps membership tokens in an operating-system store (#456): the
/// Keychain on macOS and the Secret Service on Linux, with the clear file as the fallback.
/// </summary>
/// <remarks>
/// These run against <see cref="FakeSecretStore"/> on every platform, so selection, fallback,
/// migration and forgetting are covered on the Windows machines most of this is written on.
/// <see cref="PlatformSecretStoreTests"/> exercises the real stores where they exist.
/// </remarks>
public sealed class MultiplayerRecoveryTokenStoreTests : IDisposable
{
    private readonly DirectoryInfo _directory =
        Directory.CreateTempSubdirectory("rechaos-recovery-tokens-");

    private readonly FakeSecretStore _store = new();

    private RecoveryTokenProtection Protection => new(useDpapi: false, _store);

    [Fact]
    public void TheTokenLeavesTheFileForTheStore()
    {
        var recovery = Recovery("match-1");

        Assert.True(Save([recovery]));

        var text = File.ReadAllText(Path());
        Assert.DoesNotContain(recovery.Token, text, StringComparison.Ordinal);
        var stored = Assert.Single(Sessions());
        Assert.Equal("fake", stored.GetProperty("TokenStore").GetString());
        var account = stored.GetProperty("TokenAccount").GetString()!;
        Assert.Equal(recovery.Token, _store.Secrets[account]);
        Assert.Equal(MultiplayerRecoveryHistory.CurrentFormatVersion, StoredVersion());
        Assert.False(MultiplayerRecoveryStore.KeepsTokensInClear(Path()));
        Assert.Equal([recovery], Load());
    }

    /// <summary>An unchanged token is not written again by every turn's save.</summary>
    [Fact]
    public void AnUnchangedTokenIsStoredOnce()
    {
        var recovery = Recovery("match-1");

        Assert.True(Save([recovery]));
        Assert.True(Save([recovery with { LastUpdatedAt = recovery.LastUpdatedAt!.Value.AddMinutes(1) }]));
        Assert.True(Save([recovery with { Token = "cop_rotated" }]));

        Assert.Equal(2, _store.Writes);
        Assert.Equal("cop_rotated", Assert.Single(_store.Secrets).Value);
    }

    [Fact]
    public void AStoreThatRefusesTheTokenLeavesItInClear()
    {
        _store.RefuseWrites = true;
        var recovery = Recovery("match-1");

        Assert.True(Save([recovery]));

        var stored = Assert.Single(Sessions());
        Assert.Equal(recovery.Token, stored.GetProperty("Token").GetString());
        Assert.False(stored.TryGetProperty("TokenStore", out var store) && store.ValueKind != JsonValueKind.Null);
        Assert.Equal(MultiplayerRecoveryHistory.FormatVersionWithoutTokenStore, StoredVersion());
        Assert.True(MultiplayerRecoveryStore.KeepsTokensInClear(Path()));
    }

    /// <summary>
    /// A refused token is not offered to the store again by every turn's save, since each offer
    /// can be a keyring prompt on the game thread; a new token is.
    /// </summary>
    [Fact]
    public void ARefusedTokenIsNotOfferedAgainByEverySave()
    {
        _store.RefuseWrites = true;
        var recovery = Recovery("match-1");

        Assert.True(Save([recovery]));
        Assert.True(Save([recovery with { LastUpdatedAt = recovery.LastUpdatedAt!.Value.AddMinutes(1) }]));
        Assert.Equal(1, _store.Attempts);

        _store.RefuseWrites = false;
        Assert.True(Save([recovery with { Token = "cop_rotated" }]));

        Assert.Equal(2, _store.Attempts);
        Assert.Equal("cop_rotated", Assert.Single(_store.Secrets).Value);
        Assert.False(MultiplayerRecoveryStore.KeepsTokensInClear(Path()));
    }

    [Fact]
    public void WithNoStoreTheTokenStaysInClearAndTheScreenIsTold()
    {
        var none = new RecoveryTokenProtection(useDpapi: false, store: null);
        var recovery = Recovery("match-1");

        Assert.True(MultiplayerRecoveryStore.TrySaveAll(Path(), [recovery], durable: false, none));

        Assert.Contains(recovery.Token, File.ReadAllText(Path()), StringComparison.Ordinal);
        Assert.True(MultiplayerRecoveryStore.KeepsTokensInClear(Path()));
        Assert.Equal([recovery], MultiplayerRecoveryStore.LoadAll(Path(), none));
        Assert.True(MultiplayerRecoveryStore.KeepsTokensInClear(Path()));
    }

    /// <summary>
    /// A clear token an older build wrote is moved into the store by the first load, and the
    /// backup generation loses it too.
    /// </summary>
    [Fact]
    public void TheFirstLoadMovesAClearTokenIntoTheStore()
    {
        var recovery = Recovery("match-1");
        var older = JsonSerializer.Serialize(new
        {
            FormatVersion = 5,
            Sessions = new[]
            {
                new
                {
                    recovery.FormatVersion, recovery.Server, recovery.MatchId, recovery.PlayerId,
                    recovery.JoinCode, recovery.DisplayName, recovery.IsHost, recovery.CleanExit,
                    recovery.Completed, recovery.Token, recovery.SessionVersion, recovery.SessionName,
                    recovery.LastUpdatedAt
                }
            }
        });
        File.WriteAllText(Path(), older);
        File.WriteAllText(Path() + ".bak", older);

        Assert.Equal([recovery], Load());

        Assert.DoesNotContain(recovery.Token, File.ReadAllText(Path()), StringComparison.Ordinal);
        Assert.DoesNotContain(recovery.Token, File.ReadAllText(Path() + ".bak"), StringComparison.Ordinal);
        Assert.Equal(recovery.Token, Assert.Single(_store.Secrets).Value);
        Assert.False(MultiplayerRecoveryStore.KeepsTokensInClear(Path()));
        Assert.Equal([recovery], Load());
    }

    /// <summary>A seat the history drops, by Leave, a finished match or reconciliation, leaves the store too.</summary>
    [Fact]
    public void ADroppedSeatIsRemovedFromTheStore()
    {
        var kept = Recovery("match-1");
        var left = Recovery("match-2");
        Assert.True(Save([kept, left]));
        Assert.Equal(2, _store.Secrets.Count);

        Assert.True(Save([kept, left with { Completed = true }]));

        Assert.Equal(kept.Token, Assert.Single(_store.Secrets).Value);
        Assert.Equal([kept], Load());
    }

    /// <summary>A seat loaded from the store, and then forgotten, is removed from it as well.</summary>
    [Fact]
    public void ASeatReadBackAndThenForgottenIsRemovedFromTheStore()
    {
        var recovery = Recovery("match-1");
        Assert.True(Save([recovery]));
        // A fresh process: nothing is known about the store but what the file names.
        MultiplayerRecoveryStore.ForgetLedger(Path());

        Assert.Equal([recovery], Load());
        Assert.True(Save([]));

        Assert.Empty(_store.Secrets);
    }

    /// <summary>
    /// A copied data root reads the seats the original filed, files them under its own accounts,
    /// and dropping one there leaves the original's token in the store.
    /// </summary>
    [Fact]
    public void ACopiedHistoryLeavesTheOriginalsTokensAlone()
    {
        var kept = Recovery("match-1");
        var left = Recovery("match-2");
        Assert.True(Save([kept, left]));
        var copy = System.IO.Path.Combine(_directory.FullName, "copy", "recovery.json");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(copy)!);
        File.Copy(Path(), copy);

        Assert.Equal([kept, left], MultiplayerRecoveryStore.LoadAll(copy, Protection));
        Assert.True(MultiplayerRecoveryStore.TrySaveAll(copy, [kept], durable: false, Protection));

        Assert.Equal(3, _store.Secrets.Count);
        MultiplayerRecoveryStore.ForgetLedger(Path());
        Assert.Equal([kept, left], Load());
        MultiplayerRecoveryStore.ForgetLedger(copy);
        Assert.Equal([kept], MultiplayerRecoveryStore.LoadAll(copy, Protection));
    }

    /// <summary>
    /// A store that does not answer at load hides the seat without losing it: the next save
    /// writes it back, and a later load with the store answering offers it again.
    /// </summary>
    [Fact]
    public void ASeatWhoseStoreDoesNotAnswerIsKeptForLater()
    {
        var waiting = Recovery("match-1");
        var other = Recovery("match-2");
        Assert.True(Save([waiting]));

        _store.Available = false;
        Assert.Empty(Load());
        _store.Available = true;
        Assert.True(Save([other]));

        Assert.Equal(2, Sessions().Count());
        Assert.Equal(waiting.Token, _store.Secrets.Single(pair => pair.Value == waiting.Token).Value);
        Assert.Equal([other, waiting], Load());
    }

    /// <summary>A store that answers and has no such token is the one case that drops the seat.</summary>
    [Fact]
    public void ASeatWhoseTokenIsGoneFromTheStoreIsDropped()
    {
        Assert.True(Save([Recovery("match-1")]));
        _store.Secrets.Clear();

        Assert.Empty(Load());
        Assert.True(Save([]));

        Assert.Empty(Sessions());
    }

    /// <summary>A seat filed in a store this machine does not have is held back, as if that store were locked.</summary>
    [Fact]
    public void ASeatInAStoreThisMachineLacksIsKept()
    {
        var recovery = Recovery("match-1");
        Assert.True(Save([recovery]));
        var none = new RecoveryTokenProtection(useDpapi: false, store: null);

        Assert.Empty(MultiplayerRecoveryStore.LoadAll(Path(), none));
        Assert.True(MultiplayerRecoveryStore.TrySaveAll(Path(), [], durable: false, none));

        Assert.Single(Sessions());
        Assert.Equal([recovery], Load());
    }

    /// <summary>Two data roots on one account file their tokens apart.</summary>
    [Fact]
    public void TwoHistoriesDoNotShareOrRemoveEachOthersTokens()
    {
        var recovery = Recovery("match-1");
        var second = System.IO.Path.Combine(_directory.FullName, "portable", "recovery.json");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(second)!);

        Assert.True(Save([recovery]));
        Assert.True(MultiplayerRecoveryStore.TrySaveAll(second, [recovery], durable: false, Protection));
        Assert.Equal(2, _store.Secrets.Count);
        Assert.True(MultiplayerRecoveryStore.TrySaveAll(second, [], durable: false, Protection));

        Assert.Equal([recovery], Load());
    }

    /// <summary>The file, and the backup copied from it, are readable by their owner only.</summary>
    [Fact]
    public void TheFileIsReadableByItsOwnerOnly()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes do not apply on Windows.");
        var recovery = Recovery("match-1");

        Assert.True(Save([recovery]));
        Assert.True(Save([recovery with { Token = "cop_rotated" }]));

        if (OperatingSystem.IsWindows()) return;
        var ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        Assert.Equal(ownerOnly, File.GetUnixFileMode(Path()));
        Assert.Equal(ownerOnly, File.GetUnixFileMode(Path() + ".bak"));
    }

    public void Dispose() => _directory.Delete(recursive: true);

    private bool Save(IEnumerable<MultiplayerRecovery> recoveries) =>
        MultiplayerRecoveryStore.TrySaveAll(Path(), recoveries, durable: false, Protection);

    private IReadOnlyList<MultiplayerRecovery> Load() =>
        MultiplayerRecoveryStore.LoadAll(Path(), Protection);

    private IEnumerable<JsonElement> Sessions()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path()));
        return document.RootElement.GetProperty("Sessions").EnumerateArray()
            .Select(session => session.Clone()).ToArray();
    }

    private int StoredVersion()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path()));
        return document.RootElement.GetProperty("FormatVersion").GetInt32();
    }

    private string Path() => System.IO.Path.Combine(_directory.FullName, "recovery.json");

    private static MultiplayerRecovery Recovery(string matchId) => new(
        MultiplayerRecovery.CurrentFormatVersion,
        "https://games.example.test/",
        matchId,
        "player-1",
        $"cop_secret_{matchId}",
        "CODE1234",
        "ADA",
        IsHost: true,
        CleanExit: false,
        Completed: false,
        SessionVersion: MultiplayerSessionVersion.Current,
        SessionName: "NIGHT OF THE LONG KNIVES",
        LastUpdatedAt: new DateTimeOffset(2026, 4, 17, 21, 5, 0, TimeSpan.Zero));
}

/// <summary>
/// The operating system's own store, where this machine has one that answers.
/// </summary>
/// <remarks>
/// On macOS this is the login Keychain, and on Linux the Secret Service. A machine whose store
/// refuses the write (a runner with a locked keychain or without a keyring daemon) skips the test,
/// and says so in the skip reason. <c>REQUIRE_SECRET_SERVICE=1</c> turns that skip into a
/// failure: the Linux leg of the fast gate sets it after starting an unlocked keyring, so a broken
/// binding or keyring setup fails there instead of passing as a skip.
/// Every item goes under a service of the test's own, and is removed again.
/// </remarks>
public sealed class PlatformSecretStoreTests
{
    [Fact]
    public void TheRealStoreKeepsFindsAndRemovesASecret()
    {
        var service = $"Chaos Overlords New Chrome tests {Guid.NewGuid():N}";
        ISecretStore? store = null;
        if (OperatingSystem.IsMacOS()) store = KeychainSecretStore.TryCreate(service);
        else if (OperatingSystem.IsLinux()) store = SecretServiceStore.TryCreate(service);
        SkipUnlessRequired(store is null, "This platform has no operating-system secret store to test.");

        const string account = "test/match-1/player-1@https://games.example.test/";
        var stored = store!.TryStore(account, "Chaos Overlords test seat", "cop_secret_1");
        // A runner whose keyring is locked or absent answers the write with a refusal, which the
        // game handles by keeping the token in clear; a broken binding throws instead.
        SkipUnlessRequired(!stored, "The store refused the write: no keyring is running or it is locked.");
        try
        {
            Assert.True(stored);
            Assert.Equal(SecretLookup.Found, store.TryLookup(account, out var secret));
            Assert.Equal("cop_secret_1", secret);

            Assert.True(store.TryStore(account, "Chaos Overlords test seat", "cop_secret_2"));
            Assert.Equal(SecretLookup.Found, store.TryLookup(account, out secret));
            Assert.Equal("cop_secret_2", secret);
        }
        finally
        {
            Assert.True(store.TryDelete(account));
        }
        Assert.Equal(SecretLookup.Missing, store.TryLookup(account, out _));
        Assert.True(store.TryDelete(account));
    }

    private static void SkipUnlessRequired(bool condition, string reason)
    {
        if (!condition) return;
        Assert.False(
            Environment.GetEnvironmentVariable("REQUIRE_SECRET_SERVICE") == "1",
            $"REQUIRE_SECRET_SERVICE=1, but the test would skip: {reason}");
        Assert.Skip(reason);
    }
}

/// <summary>An in-memory <see cref="ISecretStore"/> whose answers a test sets.</summary>
internal sealed class FakeSecretStore : ISecretStore
{
    public Dictionary<string, string> Secrets { get; } = new(StringComparer.Ordinal);

    /// <summary>False answers every call as a locked or absent keyring would.</summary>
    public bool Available { get; set; } = true;

    public bool RefuseWrites { get; set; }

    public int Writes { get; private set; }

    /// <summary>Every write asked for, including refused ones.</summary>
    public int Attempts { get; private set; }

    public string Name => "fake";

    public bool TryStore(string account, string label, string secret)
    {
        Attempts++;
        if (!Available || RefuseWrites) return false;
        Writes++;
        Secrets[account] = secret;
        return true;
    }

    public SecretLookup TryLookup(string account, out string? secret)
    {
        secret = null;
        if (!Available) return SecretLookup.Unavailable;
        return Secrets.TryGetValue(account, out secret) ? SecretLookup.Found : SecretLookup.Missing;
    }

    public bool TryDelete(string account)
    {
        if (!Available) return false;
        Secrets.Remove(account);
        return true;
    }
}
