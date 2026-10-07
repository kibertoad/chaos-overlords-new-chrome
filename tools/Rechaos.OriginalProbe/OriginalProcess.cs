using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Rechaos.OriginalProbe;

/// <summary>
/// Runs the original executable under the Windows debugging interface so that breakpoints can
/// record arguments and results and read or write its memory. The executable is 32-bit, so thread
/// contexts go through the WOW64 calls. Only the thread that started the process receives its
/// debug events, so every call on this object has to come from that thread. Disposing it kills
/// the process.
/// </summary>
internal sealed class OriginalProcess : IDisposable
{
    private readonly Dictionary<uint, Breakpoint> _breakpoints = [];
    private readonly Dictionary<int, IntPtr> _threads = [];
    private readonly Dictionary<int, uint> _rearm = [];
    private readonly List<CodePatch> _patches = [];
    private readonly IntPtr _event = Marshal.AllocHGlobal(Native.DebugEventSize);
    private IntPtr _process;
    private bool _started;

    public int ProcessId { get; private set; }
    public bool Exited { get; private set; }
    public int ExitCode { get; private set; }
    public DateTime LastBreakpointUtc { get; private set; } = DateTime.UtcNow;

    /// <summary>Exceptions the probe did not handle itself, with their code and address.</summary>
    public List<string> Log { get; } = [];

    public static OriginalProcess Start(string executable, string workingDirectory)
    {
        var process = new OriginalProcess();
        var startup = new Native.StartupInfo { Cb = Marshal.SizeOf<Native.StartupInfo>() };
        var commandLine = new StringBuilder($"\"{executable}\"");
        if (!Native.CreateProcessW(
                null, commandLine, IntPtr.Zero, IntPtr.Zero, false, Native.DebugOnlyThisProcess,
                IntPtr.Zero, workingDirectory, ref startup, out var information))
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                $"Cannot start {executable}. The installed path asks for administrator rights; run a staged copy with --executable (docs/VALIDATION.md).");
        process._process = information.Process;
        process.ProcessId = information.ProcessId;
        Native.CloseHandle(information.Thread);
        return process;
    }

    /// <summary>Stops at <paramref name="address"/> each time it runs, until removed.</summary>
    // A quiet breakpoint leaves LastBreakpointUtc alone, for code that runs while the game waits
    // for input, such as the planning clock.
    public void SetBreakpoint(uint address, Action<BreakContext> handler, bool oneShot = false, bool quiet = false)
    {
        if (_breakpoints.TryGetValue(address, out var existing))
        {
            existing.Handlers.Add(new BreakpointHandler(handler, oneShot, quiet));
            return;
        }

        var breakpoint = new Breakpoint(address);
        breakpoint.Handlers.Add(new BreakpointHandler(handler, oneShot, quiet));
        _breakpoints[address] = breakpoint;
        if (_started) Arm(breakpoint);
    }

    /// <summary>
    /// Writes <paramref name="replacement"/> over the code bytes at <paramref name="address"/>
    /// once the image is mapped, when they still hold <paramref name="expected"/>. A patch that
    /// finds other bytes leaves them alone and adds a line to <see cref="Log"/>.
    /// </summary>
    public void Patch(uint address, byte[] expected, byte[] replacement)
    {
        if (expected.Length != replacement.Length)
            throw new ArgumentException("A patch replaces as many bytes as it expects.", nameof(replacement));
        var patch = new CodePatch(address, expected, replacement);
        _patches.Add(patch);
        if (_started) Apply(patch);
    }

    /// <summary>Handles debug events until <paramref name="until"/> holds, the process exits or the time runs out.</summary>
    public bool RunUntil(Func<bool> until, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!Exited)
        {
            if (until()) return true;
            if (DateTime.UtcNow > deadline) return false;
            PumpOne(50);
        }

        return until();
    }

    public void Pump(TimeSpan duration) => RunUntil(() => false, duration);

    public byte[] Read(uint address, int length)
    {
        var buffer = new byte[length];
        if (!Native.ReadProcessMemory(_process, (IntPtr)address, buffer, length, out var read) || read != length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot read {length} bytes at 0x{address:X8}.");
        return buffer;
    }

    public short ReadInt16(uint address) => BitConverter.ToInt16(Read(address, 2));

    public int ReadInt32(uint address) => BitConverter.ToInt32(Read(address, 4));

    public void Write(uint address, byte[] bytes)
    {
        if (!Native.WriteProcessMemory(_process, (IntPtr)address, bytes, bytes.Length, out var written)
            || written != bytes.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot write {bytes.Length} bytes at 0x{address:X8}.");
    }

    public IntPtr FindMainWindow()
    {
        var found = IntPtr.Zero;
        Native.EnumWindows((window, _) =>
        {
            Native.GetWindowThreadProcessId(window, out var owner);
            if (owner != ProcessId || !Native.IsWindowVisible(window)) return true;
            found = window;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    public void Dispose()
    {
        if (!Exited && _process != IntPtr.Zero)
        {
            Native.TerminateProcess(_process, 1);
            // Let the exit event arrive so the process is gone before the handle closes.
            for (var i = 0; i < 100 && !Exited; i++) PumpOne(50);
        }

        // The thread handles came with debug events, and continuing the exit event closed them.
        if (_process != IntPtr.Zero) Native.CloseHandle(_process);
        Marshal.FreeHGlobal(_event);
    }

    private void PumpOne(uint milliseconds)
    {
        if (!Native.WaitForDebugEvent(_event, milliseconds)) return;
        var code = (uint)Marshal.ReadInt32(_event, 0);
        var threadId = Marshal.ReadInt32(_event, 8);
        var status = Native.DbgContinue;
        switch (code)
        {
            case Native.CreateProcessDebugEvent:
                CloseIfSet(Marshal.ReadIntPtr(_event, 16));
                // The event's process and thread handles belong to the system, which closes them
                // when the exit events are continued; only the image file handle is the debugger's.
                _threads[threadId] = Marshal.ReadIntPtr(_event, 32);
                _started = true;
                foreach (var patch in _patches) Apply(patch);
                foreach (var breakpoint in _breakpoints.Values) Arm(breakpoint);
                break;
            case Native.CreateThreadDebugEvent:
                _threads[threadId] = Marshal.ReadIntPtr(_event, 16);
                break;
            case Native.ExitThreadDebugEvent:
                _threads.Remove(threadId);
                break;
            case Native.LoadDllDebugEvent:
                CloseIfSet(Marshal.ReadIntPtr(_event, 16));
                break;
            case Native.ExitProcessDebugEvent:
                Exited = true;
                ExitCode = Marshal.ReadInt32(_event, 16);
                break;
            case Native.ExceptionDebugEvent:
                status = HandleException(threadId);
                break;
        }

        Native.ContinueDebugEvent(ProcessId, threadId, status);
    }

    private uint HandleException(int threadId)
    {
        var exception = (uint)Marshal.ReadInt32(_event, 16);
        var address = (uint)Marshal.ReadInt64(_event, 32);
        if (exception is Native.StatusSingleStep or Native.StatusWx86SingleStep
            && _rearm.Remove(threadId, out var rearm))
        {
            if (_breakpoints.TryGetValue(rearm, out var again)) Arm(again);
            return Native.DbgContinue;
        }

        if (exception is not (Native.StatusBreakpoint or Native.StatusWx86Breakpoint))
        {
            if (Log.Count < 200) Log.Add($"exception 0x{exception:X8} at 0x{address:X8}");
            return Native.DbgExceptionNotHandled;
        }
        if (!_breakpoints.TryGetValue(address, out var breakpoint))
        {
            // The loader's own breakpoints on attach.
            if (Log.Count < 200) Log.Add($"foreign breakpoint 0x{exception:X8} at 0x{address:X8}");
            return Native.DbgContinue;
        }

        var thread = _threads[threadId];
        var context = new BreakContext(this, thread, address);
        context.Eip = address;
        if (!breakpoint.Quiet) LastBreakpointUtc = DateTime.UtcNow;
        foreach (var handler in breakpoint.Handlers.ToArray())
        {
            if (handler.OneShot) breakpoint.Handlers.Remove(handler);
            handler.Action(context);
        }

        Disarm(breakpoint);
        if (breakpoint.Handlers.Count == 0)
            _breakpoints.Remove(address);
        else
        {
            // Run the original instruction, then put the breakpoint back on the single step.
            context.EFlags |= 0x100;
            _rearm[threadId] = address;
        }

        context.Commit();
        return Native.DbgContinue;
    }

    private void Apply(CodePatch patch)
    {
        var found = Read(patch.Address, patch.Expected.Length);
        if (!found.AsSpan().SequenceEqual(patch.Expected))
        {
            Log.Add($"Patch at 0x{patch.Address:X8} skipped: expected {Convert.ToHexString(patch.Expected)}, found {Convert.ToHexString(found)}.");
            return;
        }
        Write(patch.Address, patch.Replacement);
        Native.FlushInstructionCache(_process, (IntPtr)patch.Address, patch.Replacement.Length);
    }

    private void Arm(Breakpoint breakpoint)
    {
        if (breakpoint.Armed) return;
        breakpoint.Original = Read(breakpoint.Address, 1)[0];
        Write(breakpoint.Address, [0xCC]);
        Native.FlushInstructionCache(_process, (IntPtr)breakpoint.Address, 1);
        breakpoint.Armed = true;
    }

    private void Disarm(Breakpoint breakpoint)
    {
        if (!breakpoint.Armed) return;
        Write(breakpoint.Address, [breakpoint.Original]);
        Native.FlushInstructionCache(_process, (IntPtr)breakpoint.Address, 1);
        breakpoint.Armed = false;
    }

    private static void CloseIfSet(IntPtr handle)
    {
        if (handle != IntPtr.Zero) Native.CloseHandle(handle);
    }

    private sealed record CodePatch(uint Address, byte[] Expected, byte[] Replacement);

    private sealed record BreakpointHandler(Action<BreakContext> Action, bool OneShot, bool Quiet);

    private sealed class Breakpoint(uint address)
    {
        public uint Address { get; } = address;
        public byte Original { get; set; }
        public bool Armed { get; set; }
        public List<BreakpointHandler> Handlers { get; } = [];

        // The breakpoint is quiet only while every handler still on it is, so it turns quiet again
        // once a one-shot handler that was not quiet has fired and been removed.
        public bool Quiet => Handlers.TrueForAll(handler => handler.Quiet);
    }
}

/// <summary>The registers of the thread that hit a breakpoint, written back when it resumes.</summary>
internal sealed class BreakContext
{
    private readonly IntPtr _thread;
    private readonly byte[] _context = new byte[Native.Wow64ContextSize];

    public BreakContext(OriginalProcess process, IntPtr thread, uint address)
    {
        Process = process;
        _thread = thread;
        Address = address;
        BitConverter.GetBytes(Native.Wow64ContextFull).CopyTo(_context, 0);
        if (!Native.Wow64GetThreadContext(thread, _context))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot read the thread context.");
    }

    public OriginalProcess Process { get; }
    public uint Address { get; }

    public uint Eax { get => Get(0xB0); set => Set(0xB0, value); }
    public uint Ecx { get => Get(0xAC); set => Set(0xAC, value); }
    public uint Edx { get => Get(0xA8); set => Set(0xA8, value); }
    public uint Ebx { get => Get(0xA4); set => Set(0xA4, value); }
    public uint Esi { get => Get(0xA0); set => Set(0xA0, value); }
    public uint Edi { get => Get(0x9C); set => Set(0x9C, value); }
    public uint Ebp { get => Get(0xB4); set => Set(0xB4, value); }
    public uint Eip { get => Get(0xB8); set => Set(0xB8, value); }
    public uint EFlags { get => Get(0xC0); set => Set(0xC0, value); }
    public uint Esp { get => Get(0xC4); set => Set(0xC4, value); }

    /// <summary>The 32-bit stack argument <paramref name="index"/> at a function's first instruction.</summary>
    public int Argument(int index) => Process.ReadInt32(Esp + 4 + (uint)index * 4);

    public uint ReturnAddress => (uint)Process.ReadInt32(Esp);

    /// <summary>A copy of every register, which <see cref="Restore"/> puts back.</summary>
    public byte[] Save() => (byte[])_context.Clone();

    public void Restore(byte[] saved) => saved.CopyTo(_context, 0);

    public void Commit()
    {
        if (!Native.Wow64SetThreadContext(_thread, _context))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot write the thread context.");
    }

    private uint Get(int offset) => BitConverter.ToUInt32(_context, offset);

    private void Set(int offset, uint value) => BitConverter.GetBytes(value).CopyTo(_context, offset);
}
