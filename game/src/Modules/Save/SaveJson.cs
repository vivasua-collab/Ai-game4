#nullable enable
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CultivationGame.Modules.Save;

/// <summary>
/// R11 P0-Save (внешнее ревью 2026-09-09): единые опции JSON для ВСЕГО
/// persistence-конвейера (запись file-handler'ом, чтение file-handler'ом,
/// типизированная конверсия блоков в агрегаторе).
///
/// Почему каждый флаг обязателен:
/// <list type="bullet">
/// <item><b>IncludeFields</b> — почти все XxxSaveData описаны публичными
///   ПОЛЯМИ (InventorySaveData.slots, BodySaveData.parts, …). System.Text.Json
///   по умолчанию НЕ сериализует поля — блоки превращались в «{}» и данные
///   терялись ещё на записи. (TechniqueServiceState уже переведён на свойства
///   прошлым аудитом 08-28 — смешение полей/свойств поддерживается.)</item>
/// <item><b>CamelCase policy</b> — исторический формат файла (Adapter-хендлер).</item>
/// <item><b>PropertyNameCaseInsensitive</b> — чтение сейвов, записанных без
///   naming-политики (Modules-хендлер ранее писал PascalCase) — прямая
///   совместимость обоих писателей.</item>
/// <item><b>WhenWritingNull</b> — компактность; null-блоки не пишутся.</item>
/// <item><b>WriteIndented</b> — читаемость файла (сейвы маленькие).</item>
/// </list>
/// </summary>
public static class SaveJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        IncludeFields = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
