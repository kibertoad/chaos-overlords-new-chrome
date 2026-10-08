using System.Text.Json.Serialization;
using Rechaos.Core.Assets;

namespace Rechaos.Core.Persistence;

/// <summary>
/// The source-generated JSON contracts of every document <c>Rechaos.Core</c> reads or writes: the
/// bundled definitions, native saves and replays.
/// </summary>
/// <remarks>
/// <para>
/// The contracts carry no options of their own. Each reader passes the options it always used, with
/// this context as their type resolver, so the bytes are the ones the reflection-based serializer
/// wrote: <c>JsonContractTests</c> holds every document to that, and pins the definition
/// fingerprint saves are checked against.
/// </para>
/// <para>
/// Reflection-based binding does not survive trimming, which strips the constructor parameter names
/// records are bound by, and the coordination server's resolver is a trimmed WebAssembly build
/// (docs/MULTIPLAYER.md, "Resolving turns on the server"). The project is marked AOT-compatible,
/// so a call that would fall back to reflection fails the build.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(OriginalData))]
[JsonSerializable(typeof(NativeSaveDocument))]
[JsonSerializable(typeof(ReplayDocument))]
internal sealed partial class CoreJsonContext : JsonSerializerContext;
