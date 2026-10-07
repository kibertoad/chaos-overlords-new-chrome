using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Rechaos.Game;

/// <summary>What an operating-system secret store answered when asked for a seat's token.</summary>
internal enum SecretLookup
{
    /// <summary>The store holds the token.</summary>
    Found,

    /// <summary>The store answered and holds nothing under that name.</summary>
    Missing,

    /// <summary>
    /// The store did not answer: it is locked, the player dismissed its prompt, or there is no
    /// store running. This says nothing about whether the token is there.
    /// </summary>
    Unavailable
}

/// <summary>
/// A per-user secret store outside the game's own files: the macOS Keychain or a Secret Service
/// keyring on Linux.
/// </summary>
/// <remarks>
/// Each secret is filed under an account name inside one service of the game's own. The recovery
/// file then records only which store holds the token, under <see cref="Name"/>.
/// </remarks>
internal interface ISecretStore
{
    /// <summary>The name written into the recovery file for a token this store holds.</summary>
    string Name { get; }

    bool TryStore(string account, string label, string secret);

    SecretLookup TryLookup(string account, out string? secret);

    /// <summary>Removes the secret. True when it is gone, including when it was never there.</summary>
    bool TryDelete(string account);
}

/// <summary>How the recovery file protects the membership tokens it holds.</summary>
/// <remarks>
/// <para>
/// Windows seals the token with DPAPI and keeps the sealed bytes in the file. macOS keeps it in the
/// login Keychain and Linux in the Secret Service keyring (GNOME Keyring, KWallet and KeePassXC all
/// serve it), and the file names the store instead of holding the token. Both are reached through
/// libraries the operating system provides (Security.framework, libsecret), loaded when first
/// needed, so the game carries no native package for them.
/// </para>
/// <para>
/// Where none of these is present (a Linux system without libsecret or without a running keyring,
/// or any other platform) the token stays in the file in clear, and the file is readable by its
/// owner only. That keeps the reconnect the file exists for, and the Unfinished Sessions screen
/// says so.
/// </para>
/// </remarks>
internal sealed class RecoveryTokenProtection(bool useDpapi, ISecretStore? store)
{
    /// <summary>The service every seat of the game is filed under in an operating-system store.</summary>
    public const string ServiceName = "Chaos Overlords New Chrome online seats";

    private static readonly Lazy<RecoveryTokenProtection> PlatformDefault = new(CreateForPlatform);

    /// <summary>What this machine offers, decided once per process.</summary>
    public static RecoveryTokenProtection Platform => PlatformDefault.Value;

    public bool UseDpapi { get; } = useDpapi;

    public ISecretStore? Store { get; } = store;

    /// <summary>Whether a token can be kept anywhere but in clear.</summary>
    public bool CanSeal => UseDpapi || Store is not null;

    private static RecoveryTokenProtection CreateForPlatform()
    {
        if (OperatingSystem.IsWindows()) return new RecoveryTokenProtection(useDpapi: true, store: null);
        if (OperatingSystem.IsMacOS())
            return new RecoveryTokenProtection(useDpapi: false, KeychainSecretStore.TryCreate(ServiceName));
        if (OperatingSystem.IsLinux())
            return new RecoveryTokenProtection(useDpapi: false, SecretServiceStore.TryCreate(ServiceName));
        return new RecoveryTokenProtection(useDpapi: false, store: null);
    }
}

/// <summary>Generic password items in the macOS login Keychain, through Security.framework.</summary>
/// <remarks>
/// <para>
/// The items are filed under one service with the account naming the seat. The game is not signed
/// with a Keychain entitlement, so the items live in the login keychain rather than the data
/// protection keychain, and their access list names the executable that made them. A build that
/// replaces that executable is asked by macOS, once for each item, whether it may read it;
/// refusing answers <see cref="SecretLookup.Unavailable"/>, which keeps the seat on file without
/// offering it.
/// </para>
/// <para>
/// Only the SecItem calls and the Core Foundation types they take are used. The constants
/// (<c>kSecClass</c> and the rest) are global variables in the framework, read through
/// <see cref="NativeLibrary.GetExport"/>.
/// </para>
/// </remarks>
[SupportedOSPlatform("macos")]
internal sealed class KeychainSecretStore : ISecretStore
{
    private const string SecurityPath = "/System/Library/Frameworks/Security.framework/Security";
    private const string CoreFoundationPath =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    private const int Success = 0;
    private const int ItemNotFound = -25300;
    private const uint Utf8Encoding = 0x08000100;

    private readonly string _service;
    private readonly IntPtr _keyCallbacks;
    private readonly IntPtr _valueCallbacks;
    private readonly IntPtr _class;
    private readonly IntPtr _classGenericPassword;
    private readonly IntPtr _serviceKey;
    private readonly IntPtr _account;
    private readonly IntPtr _label;
    private readonly IntPtr _valueData;
    private readonly IntPtr _returnData;
    private readonly IntPtr _matchLimit;
    private readonly IntPtr _matchLimitOne;
    private readonly IntPtr _true;

    public string Name => "keychain";

    private KeychainSecretStore(string service, IntPtr security, IntPtr coreFoundation)
    {
        _service = service;
        _keyCallbacks = NativeLibrary.GetExport(coreFoundation, "kCFTypeDictionaryKeyCallBacks");
        _valueCallbacks = NativeLibrary.GetExport(coreFoundation, "kCFTypeDictionaryValueCallBacks");
        _true = Constant(coreFoundation, "kCFBooleanTrue");
        _class = Constant(security, "kSecClass");
        _classGenericPassword = Constant(security, "kSecClassGenericPassword");
        _serviceKey = Constant(security, "kSecAttrService");
        _account = Constant(security, "kSecAttrAccount");
        _label = Constant(security, "kSecAttrLabel");
        _valueData = Constant(security, "kSecValueData");
        _returnData = Constant(security, "kSecReturnData");
        _matchLimit = Constant(security, "kSecMatchLimit");
        _matchLimitOne = Constant(security, "kSecMatchLimitOne");
    }

    /// <summary>The store, or null when the frameworks or their constants cannot be found.</summary>
    public static KeychainSecretStore? TryCreate(string service)
    {
        if (!NativeLibrary.TryLoad(SecurityPath, out var security)
            || !NativeLibrary.TryLoad(CoreFoundationPath, out var coreFoundation))
            return null;
        try
        {
            return new KeychainSecretStore(service, security, coreFoundation);
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
    }

    /// <summary>The value of a framework global that holds a Core Foundation reference.</summary>
    private static IntPtr Constant(IntPtr library, string name)
    {
        var value = Marshal.ReadIntPtr(NativeLibrary.GetExport(library, name));
        if (value == IntPtr.Zero) throw new EntryPointNotFoundException(name);
        return value;
    }

    public bool TryStore(string account, string label, string secret)
    {
        using var scope = new CoreFoundationScope();
        var query = Query(scope, account);
        var data = scope.Own(CFDataCreate(IntPtr.Zero, Utf8(secret), Utf8Length(secret)));
        var update = scope.Own(CFDictionaryCreateMutable(IntPtr.Zero, 1, _keyCallbacks, _valueCallbacks));
        CFDictionarySetValue(update, _valueData, data);
        var status = SecItemUpdate(query, update);
        if (status == Success) return true;
        if (status != ItemNotFound) return false;
        CFDictionarySetValue(query, _valueData, data);
        CFDictionarySetValue(query, _label, scope.String(label));
        return SecItemAdd(query, IntPtr.Zero) == Success;
    }

    public SecretLookup TryLookup(string account, out string? secret)
    {
        secret = null;
        using var scope = new CoreFoundationScope();
        var query = Query(scope, account);
        CFDictionarySetValue(query, _returnData, _true);
        CFDictionarySetValue(query, _matchLimit, _matchLimitOne);
        var status = SecItemCopyMatching(query, out var result);
        if (status == ItemNotFound) return SecretLookup.Missing;
        if (status != Success || result == IntPtr.Zero) return SecretLookup.Unavailable;
        scope.Own(result);
        if (CFGetTypeID(result) != CFDataGetTypeID()) return SecretLookup.Unavailable;
        var length = checked((int)CFDataGetLength(result));
        var bytes = new byte[length];
        Marshal.Copy(CFDataGetBytePtr(result), bytes, 0, length);
        secret = System.Text.Encoding.UTF8.GetString(bytes);
        return SecretLookup.Found;
    }

    public bool TryDelete(string account)
    {
        using var scope = new CoreFoundationScope();
        var status = SecItemDelete(Query(scope, account));
        return status is Success or ItemNotFound;
    }

    private IntPtr Query(CoreFoundationScope scope, string account)
    {
        var query = scope.Own(CFDictionaryCreateMutable(IntPtr.Zero, 0, _keyCallbacks, _valueCallbacks));
        CFDictionarySetValue(query, _class, _classGenericPassword);
        CFDictionarySetValue(query, _serviceKey, scope.String(_service));
        CFDictionarySetValue(query, _account, scope.String(account));
        return query;
    }

    private static byte[] Utf8(string text) => System.Text.Encoding.UTF8.GetBytes(text);

    private static nint Utf8Length(string text) => System.Text.Encoding.UTF8.GetByteCount(text);

    /// <summary>Releases every Core Foundation object a call created, however the call ends.</summary>
    private sealed class CoreFoundationScope : IDisposable
    {
        private readonly List<IntPtr> _owned = [];

        public IntPtr Own(IntPtr reference)
        {
            if (reference == IntPtr.Zero) throw new InvalidOperationException("Core Foundation returned null.");
            _owned.Add(reference);
            return reference;
        }

        public IntPtr String(string text) =>
            Own(CFStringCreateWithCString(IntPtr.Zero, text, Utf8Encoding));

        public void Dispose()
        {
            for (var index = _owned.Count - 1; index >= 0; index--) CFRelease(_owned[index]);
            _owned.Clear();
        }
    }

    [DllImport(CoreFoundationPath)]
    private static extern IntPtr CFStringCreateWithCString(
        IntPtr allocator, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, uint encoding);

    [DllImport(CoreFoundationPath)]
    private static extern IntPtr CFDataCreate(IntPtr allocator, byte[] bytes, nint length);

    [DllImport(CoreFoundationPath)]
    private static extern nint CFDataGetLength(IntPtr data);

    [DllImport(CoreFoundationPath)]
    private static extern IntPtr CFDataGetBytePtr(IntPtr data);

    [DllImport(CoreFoundationPath)]
    private static extern nuint CFDataGetTypeID();

    [DllImport(CoreFoundationPath)]
    private static extern nuint CFGetTypeID(IntPtr reference);

    [DllImport(CoreFoundationPath)]
    private static extern IntPtr CFDictionaryCreateMutable(
        IntPtr allocator, nint capacity, IntPtr keyCallbacks, IntPtr valueCallbacks);

    [DllImport(CoreFoundationPath)]
    private static extern void CFDictionarySetValue(IntPtr dictionary, IntPtr key, IntPtr value);

    [DllImport(CoreFoundationPath)]
    private static extern void CFRelease(IntPtr reference);

    [DllImport(SecurityPath)]
    private static extern int SecItemAdd(IntPtr attributes, IntPtr result);

    [DllImport(SecurityPath)]
    private static extern int SecItemUpdate(IntPtr query, IntPtr attributesToUpdate);

    [DllImport(SecurityPath)]
    private static extern int SecItemCopyMatching(IntPtr query, out IntPtr result);

    [DllImport(SecurityPath)]
    private static extern int SecItemDelete(IntPtr query);
}

/// <summary>
/// Passwords in the Secret Service keyring on Linux, through libsecret.
/// </summary>
/// <remarks>
/// <para>
/// libsecret is called through its non-variadic entry points (<c>secret_password_storev_sync</c>
/// and its siblings), which take the attributes as a GLib hash table, because a variadic C call
/// cannot be bound portably. The schema is built once in unmanaged memory and kept for the life of
/// the process. It names two string attributes, the service and the account, which together pick
/// out one seat's token.
/// </para>
/// <para>
/// Every failure the library reports through its error out-parameter (no session bus, no keyring
/// daemon) is read as <see cref="SecretLookup.Unavailable"/>, and so is a lookup that comes back
/// empty while the item sits in a collection whose unlock prompt was dismissed; only a lookup that
/// succeeds and finds no item at all is <see cref="SecretLookup.Missing"/>.
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
internal sealed class SecretServiceStore : ISecretStore
{
    private const string LibSecret = "libsecret-1.so.0";
    private const string GLib = "libglib-2.0.so.0";
    private const string GObject = "libgobject-2.0.so.0";
    private const string SchemaName = "io.github.kibertoad.ChaosOverlordsNewChrome.Seat";
    private const string ServiceAttribute = "service";
    private const string AccountAttribute = "account";

    /// <summary>
    /// <c>sizeof(SecretSchema)</c> on a 64-bit target: the name, the flags, 32 attribute slots of
    /// a name and a type each, a reserved int, and seven reserved pointers.
    /// </summary>
    private const int SchemaSize = 8 + 8 + 32 * 16 + 8 + 7 * 8;

    /// <summary><c>SECRET_SEARCH_ALL</c>: every match, without unlocking or loading secrets.</summary>
    private const int SearchAll = 1 << 1;

    private readonly string _service;
    private readonly IntPtr _schema;
    private readonly IntPtr _stringHash;
    private readonly IntPtr _stringEqual;
    private readonly IntPtr _objectUnref;

    /// <summary>Whether <see cref="HoldsLockedItem"/> can ask; see there.</summary>
    private readonly bool _canSearch;

    public string Name => "secret-service";

    private SecretServiceStore(
        string service,
        IntPtr schema,
        IntPtr stringHash,
        IntPtr stringEqual,
        IntPtr objectUnref,
        bool canSearch)
    {
        _service = service;
        _schema = schema;
        _stringHash = stringHash;
        _stringEqual = stringEqual;
        _objectUnref = objectUnref;
        _canSearch = canSearch && objectUnref != IntPtr.Zero;
    }

    /// <summary>The store, or null when libsecret or GLib cannot be loaded.</summary>
    public static SecretServiceStore? TryCreate(string service)
    {
        if (!Environment.Is64BitProcess) return null;
        if (!NativeLibrary.TryLoad(LibSecret, out var libsecret) || !NativeLibrary.TryLoad(GLib, out var glib))
            return null;
        if (!NativeLibrary.TryGetExport(glib, "g_str_hash", out var stringHash)
            || !NativeLibrary.TryGetExport(glib, "g_str_equal", out var stringEqual))
            return null;
        // Optional: without them a dismissed unlock prompt cannot be told from a missing item.
        var canSearch = NativeLibrary.TryGetExport(libsecret, "secret_password_searchv_sync", out _)
            && NativeLibrary.TryGetExport(glib, "g_list_free_full", out _);
        var objectUnref = IntPtr.Zero;
        if (NativeLibrary.TryLoad(GObject, out var gobject))
            NativeLibrary.TryGetExport(gobject, "g_object_unref", out objectUnref);
        return new SecretServiceStore(
            service, BuildSchema(), stringHash, stringEqual, objectUnref, canSearch);
    }

    /// <summary>The schema, laid out as libsecret's <c>SecretSchema</c>, never freed.</summary>
    private static IntPtr BuildSchema()
    {
        var schema = Marshal.AllocHGlobal(SchemaSize);
        for (var offset = 0; offset < SchemaSize; offset++) Marshal.WriteByte(schema, offset, 0);
        Marshal.WriteIntPtr(schema, 0, Marshal.StringToCoTaskMemUTF8(SchemaName));
        // flags = SECRET_SCHEMA_NONE (0) at offset 8; attributes start at offset 16, 16 bytes each,
        // a name pointer then SECRET_SCHEMA_ATTRIBUTE_STRING (0). The zeroed third slot ends the list.
        Marshal.WriteIntPtr(schema, 16, Marshal.StringToCoTaskMemUTF8(ServiceAttribute));
        Marshal.WriteIntPtr(schema, 32, Marshal.StringToCoTaskMemUTF8(AccountAttribute));
        return schema;
    }

    public bool TryStore(string account, string label, string secret)
    {
        using var attributes = new Attributes(this, account);
        var stored = secret_password_storev_sync(
            _schema, attributes.Table, "default", label, secret, IntPtr.Zero, out var error);
        return Succeeded(error) && stored;
    }

    public SecretLookup TryLookup(string account, out string? secret)
    {
        secret = null;
        using var attributes = new Attributes(this, account);
        var found = secret_password_lookupv_sync(_schema, attributes.Table, IntPtr.Zero, out var error);
        if (!Succeeded(error))
        {
            if (found != IntPtr.Zero) secret_password_free(found);
            return SecretLookup.Unavailable;
        }
        if (found == IntPtr.Zero)
            return HoldsLockedItem(attributes.Table) ? SecretLookup.Unavailable : SecretLookup.Missing;
        try
        {
            secret = Marshal.PtrToStringUTF8(found);
        }
        finally
        {
            secret_password_free(found);
        }
        return secret is null ? SecretLookup.Unavailable : SecretLookup.Found;
    }

    public bool TryDelete(string account)
    {
        using var attributes = new Attributes(this, account);
        // FALSE without an error means there was nothing to remove, which is the outcome wanted.
        secret_password_clearv_sync(_schema, attributes.Table, IntPtr.Zero, out var error);
        return Succeeded(error);
    }

    /// <summary>
    /// Whether the store holds an item under these attributes that a lookup could not open.
    /// </summary>
    /// <remarks>
    /// A lookup that finds the item in a locked collection asks to unlock it, and when the player
    /// dismisses that prompt libsecret answers with no password and no error, which reads the same
    /// as an item that is not there. A search that neither unlocks nor loads secrets tells the two
    /// apart without a second prompt. <c>secret_password_searchv_sync</c> arrived in libsecret 0.19;
    /// without it the lookup's answer stands.
    /// </remarks>
    private bool HoldsLockedItem(IntPtr attributes)
    {
        if (!_canSearch) return false;
        var list = secret_password_searchv_sync(_schema, attributes, SearchAll, IntPtr.Zero, out var error);
        if (list != IntPtr.Zero) g_list_free_full(list, _objectUnref);
        // A search that fails says nothing about the item, so the lookup is not trusted either.
        return !Succeeded(error) || list != IntPtr.Zero;
    }

    private static bool Succeeded(IntPtr error)
    {
        if (error == IntPtr.Zero) return true;
        g_error_free(error);
        return false;
    }

    /// <summary>
    /// The attribute table for one seat. The table borrows the strings, so they are freed only
    /// after the table is.
    /// </summary>
    private sealed class Attributes : IDisposable
    {
        private readonly IntPtr[] _strings;

        public IntPtr Table { get; }

        public Attributes(SecretServiceStore store, string account)
        {
            _strings =
            [
                Marshal.StringToCoTaskMemUTF8(ServiceAttribute),
                Marshal.StringToCoTaskMemUTF8(store._service),
                Marshal.StringToCoTaskMemUTF8(AccountAttribute),
                Marshal.StringToCoTaskMemUTF8(account)
            ];
            Table = g_hash_table_new(store._stringHash, store._stringEqual);
            g_hash_table_insert(Table, _strings[0], _strings[1]);
            g_hash_table_insert(Table, _strings[2], _strings[3]);
        }

        public void Dispose()
        {
            g_hash_table_unref(Table);
            foreach (var pointer in _strings) Marshal.FreeCoTaskMem(pointer);
        }
    }

    [DllImport(LibSecret)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool secret_password_storev_sync(
        IntPtr schema,
        IntPtr attributes,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string password,
        IntPtr cancellable,
        out IntPtr error);

    [DllImport(LibSecret)]
    private static extern IntPtr secret_password_lookupv_sync(
        IntPtr schema, IntPtr attributes, IntPtr cancellable, out IntPtr error);

    [DllImport(LibSecret)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool secret_password_clearv_sync(
        IntPtr schema, IntPtr attributes, IntPtr cancellable, out IntPtr error);

    [DllImport(LibSecret)]
    private static extern void secret_password_free(IntPtr password);

    /// <summary>A <c>GList</c> of <c>SecretRetrievable</c> objects, or null when nothing matches.</summary>
    [DllImport(LibSecret)]
    private static extern IntPtr secret_password_searchv_sync(
        IntPtr schema, IntPtr attributes, int flags, IntPtr cancellable, out IntPtr error);

    [DllImport(GLib)]
    private static extern void g_list_free_full(IntPtr list, IntPtr freeFunction);

    [DllImport(GLib)]
    private static extern IntPtr g_hash_table_new(IntPtr hash, IntPtr equal);

    [DllImport(GLib)]
    private static extern void g_hash_table_insert(IntPtr table, IntPtr key, IntPtr value);

    [DllImport(GLib)]
    private static extern void g_hash_table_unref(IntPtr table);

    [DllImport(GLib)]
    private static extern void g_error_free(IntPtr error);
}
