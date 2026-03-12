using Core.Models;
using System.Text.Json.Serialization;

namespace Core;

/// <summary>
/// 앱 로컬 데이터 저장용 JSON 직렬화 메타데이터를 제공합니다.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(List<Account>))]
[JsonSerializable(typeof(AllServerClientSettings))]
[JsonSerializable(typeof(PresetList))]
[JsonSerializable(typeof(List<BrowserFavorite>))]
internal sealed partial class AppDataJsonSerializerContext : JsonSerializerContext
{
}
