using System.Text.Json;
using System.Text.Json.Serialization;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Session;

namespace Rechaos.Multiplayer.Protocol;

/// <summary>
/// The shapes <see cref="WireJson"/> reads and writes that are not generated contracts:
/// the roster as the resolver receives it, the opaque <c>gameSettings</c> blob and the record the
/// game writes into it.
/// </summary>
/// <remarks>
/// The contracts themselves, one entry per generated record and enum, are in
/// <see cref="WireJsonContext"/>, generated beside <c>WireContracts.cs</c>. <c>JsonContractTests</c>
/// checks that every generated type resolves.
/// </remarks>
[JsonSerializable(typeof(PlayerView[]))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, JsonElement>))]
[JsonSerializable(typeof(MultiplayerGameSettings.Wire))]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
internal sealed partial class WireShapesJsonContext : JsonSerializerContext;
