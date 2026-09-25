namespace Rechaos.Game;

/// <summary>
/// Keeps two running copies of the rebuild (DEV-UI-015) from writing the shared autosave at the
/// same time, and from trusting a primary the other copy wrote.
/// </summary>
/// <remarks>
/// Every write and every load of the autosave holds an exclusive handle on a lock file beside it
/// (<c>autosave.rchsave.lock</c>). A second process that finds the handle taken retries every
/// <see cref="RetryInterval"/> until <see cref="DefaultTimeout"/> passes, then gives up with an
/// <see cref="IOException"/>, which the autosave reports as a failed write. The operating system
/// releases the handle when a process ends, so a crashed copy never leaves the lock held.
/// <para>
/// A write that would keep the existing primary as the backup without reading it back first
/// (<c>trustExistingPrimary</c>) is allowed only while the primary still has the length and
/// last-write time this process recorded after its own write. Once another copy has replaced the
/// file, the stamp differs and the primary is loaded and checked before it is demoted.
/// </para>
/// </remarks>
internal sealed class AutoSaveFileGuard
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(50);

    private readonly string _path;
    private readonly TimeSpan _timeout;
    private FileStamp? _written;

    public AutoSaveFileGuard(string path, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = System.IO.Path.GetFullPath(path);
        _timeout = timeout ?? DefaultTimeout;
    }

    /// <summary>The autosave file this guard protects.</summary>
    public string Path => _path;

    public string LockPath => LockPathFor(_path);

    public static string LockPathFor(string path) => System.IO.Path.GetFullPath(path) + ".lock";

    /// <summary>
    /// Takes the lock, lets the caller write, and records the stamp of the primary it left.
    /// </summary>
    /// <param name="trustExistingPrimary">What the queue would trust without this check.</param>
    /// <param name="write">The write, told whether it may trust the existing primary.</param>
    public void Write(bool trustExistingPrimary, Action<bool> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        using var held = Acquire(_path, _timeout);
        var trust = trustExistingPrimary && _written is { } stamp && stamp == FileStamp.Read(_path);
        // Whatever happens next, the recorded stamp no longer describes a file this process has
        // verified until the write below finishes.
        _written = null;
        write(trust);
        _written = FileStamp.Read(_path);
    }

    /// <summary>Runs a read of the autosave under the same lock the writers take.</summary>
    public T Read<T>(Func<T> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        using var held = Acquire(_path, _timeout);
        return read();
    }

    /// <summary>Opens the lock file exclusively, retrying while another process holds it.</summary>
    public static IDisposable Acquire(string path, TimeSpan timeout)
    {
        var lockPath = LockPathFor(path);
        var directory = System.IO.Path.GetDirectoryName(lockPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            try
            {
                return new FileStream(
                    lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(RetryInterval);
            }
            catch (IOException exception)
            {
                throw new IOException(
                    "Another copy of the game is using the autosave.", exception);
            }
        }
    }

    private readonly record struct FileStamp(long Length, DateTime LastWriteUtc)
    {
        public static FileStamp? Read(string path)
        {
            var info = new FileInfo(path);
            return info.Exists ? new FileStamp(info.Length, info.LastWriteTimeUtc) : null;
        }
    }
}
