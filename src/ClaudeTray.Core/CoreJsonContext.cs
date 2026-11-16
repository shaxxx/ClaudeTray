using System.Text.Json.Serialization;

namespace ClaudeTray.Core;

/// <summary>JSON bez refleksije (Native AOT). Datoteka stanja koristi camelCase.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SessionRecord))]
[JsonSerializable(typeof(HookInput))]
public partial class CoreJsonContext : JsonSerializerContext;
