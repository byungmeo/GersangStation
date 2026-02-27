using System.Text.Json.Serialization;

namespace Core.Models;

public sealed class PresetList
{
    // Perset은 4개 고정
    [JsonPropertyName("presets")]
    public Preset[] Presets { get; set; } =
    [
    new(),
    new(),
    new(),
    new()
    ];
}

public sealed class Preset
{
    // 콤보박스는 3개 고정이기 때문에 배열 크기는 3만큼만
    [JsonPropertyName("items")]
    public PresetItem[] Items { get; set; } =
    [
    new(),
    new(),
    new()
    ];
}

public sealed class PresetItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}