using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Build;

public class RuntimeJson
{
    [JsonPropertyName("runtimes")]
    public required Dictionary<string, Dictionary<string, Dictionary<string, string>>> Runtimes { get; init; }
}
