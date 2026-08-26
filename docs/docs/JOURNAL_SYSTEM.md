# Система журнала (Journal System)

**Создано:** 2026-04-03
**Статус:** Теоретические изыскания
**Приоритет:** Средний

---

## Обзор

Журнал хранит информацию, собранную игроком во время игры:
- Записи о персонажах
- Информация о локациях
- Изученные техники
- Открытые элементы лора
- Прогресс квестов

---

## Архитектура

### Компоненты

```
JournalSystem
├── JournalManager           # Управление журналом
├── JournalEntry            # Запись журнала
├── JournalCategory         # Категория записей
├── LoreEntry               # Запись лора
└── JournalUI               # Интерфейс
```

---

## Категории журнала

### Основные категории

| Категория | Описание | Иконка |
|-----------|----------|--------|
| `Characters` | Персонажи (NPC, враги) | 👤 |
| `Locations` | Локации и области | 🗺️ |
| `Techniques` | Изученные техники | ⚔️ |
| `Creatures` | Существа и монстры | 🐉 |
| `Items` | Редкие предметы | 💎 |
| `Lore` | История мира | 📜 |
| `Factions` | Фракции и секты | 🏴 |
| `Notes` | Заметки игрока | 📝 |

---

## Структуры данных

### JournalCategory

```csharp
// Создано: 2026-04-03

/// <summary>
/// Категория журнала.
/// </summary>
public enum JournalCategory
{
    Characters,     // Персонажи
    Locations,      // Локации
    Techniques,     // Техники
    Creatures,      // Существа
    Items,          // Предметы
    Lore,           // Лор
    Factions,       // Фракции
    Notes           // Заметки
}

/// <summary>
/// Редкость записи.
/// </summary>
public enum EntryRarity
{
    Common,         // Обычная
    Uncommon,       // Необычная
    Rare,           // Редкая
    Epic,           // Эпическая
    Legendary,      // Легендарная
    Mythic          // Мифическая — см. DATA_MODELS.md §6 (6 уровней редкости)
}
```

### JournalEntry

```csharp
/// <summary>
/// Запись журнала.
/// </summary>
[System.Serializable]
public class JournalEntry
{
    [Header("Identity")]
    public string id;
    public string title;
    public JournalCategory category;
    public EntryRarity rarity;

    [Header("Content")]
    [TextArea] public string description;
    [TextArea] public string extendedDescription;  // Открывается при заполнении

    [Header("Discovery")]
    public bool isDiscovered;
    public System.DateTime discoveryDate;
    public string discoveryLocation;

    [Header("Progress")]
    public int completionLevel;        // 0-100%
    public List<string> unlockedFacts; // Открытые факты

    [Header("Related")]
    public List<string> relatedEntries;
    public List<string> tags;

    [Header("Visual")]
    public Sprite icon;
    public Color categoryColor;

    /// <summary>
    /// Процент заполненности.
    /// </summary>
    public float CompletionPercent => completionLevel / 100f;
}
```

### LoreEntry

```csharp
/// <summary>
/// Запись лора (история мира).
/// </summary>
[System.Serializable]
public class LoreEntry : JournalEntry
{
    [Header("Lore Specific")]
    public string era;                 // Эра/период
    public List<string> keyFigures;    // Ключевые фигуры
    public List<string> locations;     // Упомянутые локации
    public List<string> events;        // Связанные события

    [Header("Hidden Content")]
    public string hiddenText;          // Текст, открывающийся при условии
    public string unlockCondition;     // Условие открытия
}
```

---

## Реализация

### JournalManager.cs

```csharp
// Создано: 2026-04-03
// Теоретическая реализация

using UnityEngine;
using System.Collections.Generic;

public class JournalManager : MonoBehaviour
{
    public static JournalManager Instance { get; private set; }

    #region Configuration

    [Header("Entries Database")]
    [SerializeField] private List<JournalEntry> allEntries;

    [Header("Settings")]
    [SerializeField] private bool autoDiscover = true;

    #endregion

    #region State

    private Dictionary<string, JournalEntry> entryDict;
    private Dictionary<JournalCategory, List<JournalEntry>> categoryDict;

    // Статистика
    private int totalDiscovered;
    private int totalEntries;

    #endregion

    #region Events

    public event System.Action<JournalEntry> OnEntryDiscovered;
    public event System.Action<JournalEntry, int> OnEntryProgress;
    public event System.Action<JournalCategory> OnCategoryUpdated;

    #endregion

    #region Initialization

    private void Awake()
    {
        Instance = this;
        InitializeDictionaries();
    }

    private void InitializeDictionaries()
    {
        entryDict = new Dictionary<string, JournalEntry>();
        categoryDict = new Dictionary<JournalCategory, List<JournalEntry>>();

        foreach (var category in System.Enum.GetValues(typeof(JournalCategory)))
        {
            categoryDict[(JournalCategory)category] = new List<JournalEntry>();
        }

        foreach (var entry in allEntries)
        {
            entryDict[entry.id] = entry;
            categoryDict[entry.category].Add(entry);
        }

        totalEntries = allEntries.Count;
    }

    #endregion

    #region Discovery

    /// <summary>
    /// Открывает запись журнала.
    /// </summary>
    public bool DiscoverEntry(string entryId, string location = null)
    {
        if (!entryDict.TryGetValue(entryId, out var entry))
        {
            Debug.LogWarning($"[Journal] Entry not found: {entryId}");
            return false;
        }

        if (entry.isDiscovered)
        {
            // Уже открыто — обновляем прогресс
            UpdateProgress(entry, 10);
            return false;
        }

        entry.isDiscovered = true;
        entry.discoveryDate = System.DateTime.Now;
        entry.discoveryLocation = location ?? "Unknown";
        entry.completionLevel = 10; // Базовый прогресс

        totalDiscovered++;

        OnEntryDiscovered?.Invoke(entry);
        OnCategoryUpdated?.Invoke(entry.category);

        Debug.Log($"[Journal] Discovered: {entry.title}");

        return true;
    }

    /// <summary>
    /// Обновляет прогресс записи.
    /// </summary>
    public void UpdateProgress(JournalEntry entry, int progress)
    {
        entry.completionLevel = Mathf.Min(100, entry.completionLevel + progress);

        OnEntryProgress?.Invoke(entry, entry.completionLevel);

        if (entry.completionLevel >= 100)
        {
            Debug.Log($"[Journal] Entry completed: {entry.title}");
        }
    }

    /// <summary>
    /// Добавляет факт к записи.
    /// </summary>
    public void AddFact(string entryId, string fact)
    {
        if (!entryDict.TryGetValue(entryId, out var entry)) return;

        if (!entry.unlockedFacts.Contains(fact))
        {
            entry.unlockedFacts.Add(fact);
            UpdateProgress(entry, 15);
        }
    }

    #endregion

    #region Queries

    /// <summary>
    /// Получает запись по ID.
    /// </summary>
    public JournalEntry GetEntry(string entryId)
    {
        return entryDict.TryGetValue(entryId, out var entry) ? entry : null;
    }

    /// <summary>
    /// Получает все записи категории.
    /// </summary>
    public List<JournalEntry> GetEntriesByCategory(JournalCategory category)
    {
        return categoryDict.TryGetValue(category, out var entries) ? entries : new List<JournalEntry>();
    }

    /// <summary>
    /// Получает открытые записи категории.
    /// </summary>
    public List<JournalEntry> GetDiscoveredEntries(JournalCategory category)
    {
        return GetEntriesByCategory(category).FindAll(e => e.isDiscovered);
    }

    /// <summary>
    /// Получает связанные записи.
    /// </summary>
    public List<JournalEntry> GetRelatedEntries(string entryId)
    {
        var entry = GetEntry(entryId);
        if (entry == null) return new List<JournalEntry>();

        var related = new List<JournalEntry>();
        foreach (var relatedId in entry.relatedEntries)
        {
            var relatedEntry = GetEntry(relatedId);
            if (relatedEntry != null && relatedEntry.isDiscovered)
            {
                related.Add(relatedEntry);
            }
        }

        return related;
    }

    /// <summary>
    /// Ищет записи по тегу.
    /// </summary>
    public List<JournalEntry> SearchByTag(string tag)
    {
        return allEntries.FindAll(e => e.isDiscovered && e.tags.Contains(tag));
    }

    /// <summary>
    /// Ищет записи по тексту.
    /// </summary>
    public List<JournalEntry> SearchByText(string searchText)
    {
        searchText = searchText.ToLower();
        return allEntries.FindAll(e =>
            e.isDiscovered &&
            (e.title.ToLower().Contains(searchText) ||
             e.description.ToLower().Contains(searchText))
        );
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Получает статистику открытия.
    /// </summary>
    public float GetOverallProgress()
    {
        return totalEntries > 0 ? (float)totalDiscovered / totalEntries : 0f;
    }

    /// <summary>
    /// Получает статистику по категории.
    /// </summary>
    public float GetCategoryProgress(JournalCategory category)
    {
        var entries = GetEntriesByCategory(category);
        if (entries.Count == 0) return 0f;

        int discovered = entries.Count(e => e.isDiscovered);
        return (float)discovered / entries.Count;
    }

    /// <summary>
    /// Получает общее количество записей.
    /// </summary>
    public (int discovered, int total) GetEntryCount()
    {
        return (totalDiscovered, totalEntries);
    }

    #endregion

    #region Save/Load

    /// <summary>
    /// Получает данные для сохранения.
    /// </summary>
    public JournalSaveData GetSaveData()
    {
        var data = new JournalSaveData();

        foreach (var entry in allEntries)
        {
            if (entry.isDiscovered)
            {
                data.discoveredEntries.Add(entry.id);
                data.completionData[entry.id] = entry.completionLevel;
                data.factsData[entry.id] = entry.unlockedFacts;
            }
        }

        return data;
    }

    /// <summary>
    /// Загружает данные.
    /// </summary>
    public void LoadSaveData(JournalSaveData data)
    {
        foreach (var entryId in data.discoveredEntries)
        {
            if (entryDict.TryGetValue(entryId, out var entry))
            {
                entry.isDiscovered = true;

                if (data.completionData.TryGetValue(entryId, out int completion))
                {
                    entry.completionLevel = completion;
                }

                if (data.factsData.TryGetValue(entryId, out var facts))
                {
                    entry.unlockedFacts = facts;
                }

                totalDiscovered++;
            }
        }
    }

    #endregion
}

/// <summary>
/// Данные сохранения журнала.
/// </summary>
[System.Serializable]
public class JournalSaveData
{
    public List<string> discoveredEntries = new List<string>();
    public Dictionary<string, int> completionData = new Dictionary<string, int>();
    public Dictionary<string, List<string>> factsData = new Dictionary<string, List<string>>();
}
```

---

## Примеры записей

### Персонажи

```json
{
  "entries": [
    {
      "id": "npc_elder_zhang",
      "title": "Старейшина Чжан",
      "category": "Characters",
      "rarity": "Rare",
      "description": "Старейшина секты Небесного Меча. Известен своей мудростью и строгим характером.",
      "extendedDescription": "Полная история открывается при достижении 100% прогресса...",
      "tags": ["elder", "sect", "heavenly_sword"],
      "relatedEntries": ["sect_heavenly_sword", "technique_sword_arts"]
    },
    {
      "id": "enemy_shadow_cultivator",
      "title": "Теневой культиватор",
      "category": "Characters",
      "rarity": "Uncommon",
      "description": "Таинственный культиватор, практикующий запрещённые техники.",
      "tags": ["enemy", "shadow", "forbidden"]
    }
  ]
}
```

### Локации

```json
{
  "entries": [
    {
      "id": "location_jade_peak",
      "title": "Нефритовый пик",
      "category": "Locations",
      "rarity": "Epic",
      "description": "Священная гора, где концентрация Ци в 10 раз выше обычного.",
      "tags": ["mountain", "sacred", "high_qi"],
      "relatedEntries": ["faction_jade_palace", "item_jade_essence"]
    },
    {
      "id": "location_demon_valley",
      "title": "Долина демонов",
      "category": "Locations",
      "rarity": "Legendary",
      "description": "???",
      "extendedDescription": "Открывается при первом посещении..."
    }
  ]
}
```

### Лор

```json
{
  "entries": [
    {
      "id": "lore_origin",
      "title": "Происхождение культивации",
      "category": "Lore",
      "rarity": "Legendary",
      "description": "Древняя легенда о первых культиваторах...",
      "era": "Эпоха Зарождения",
      "keyFigures": ["Первокультиватор", "Небесный Император"],
      "events": ["Первый прорыв", "Разделение миров"]
    }
  ]
}
```

---

## UI журнала

### Главное окно

```
┌─────────────────────────────────────────────┐
│                    ЖУРНАЛ                   │
├─────────────────────────────────────────────┤
│ 👤 Персонажи    [12/45]  ████░░░░░░ 27%    │
│ 🗺️ Локации      [8/20]   ████░░░░░░ 40%    │
│ ⚔️ Техники      [15/50]  █████░░░░░ 30%    │
│ 🐉 Существа     [5/30]   ██░░░░░░░░ 17%    │
│ 💎 Предметы     [20/80]  █████░░░░░ 25%    │
│ 📜 История      [3/25]   █░░░░░░░░░ 12%    │
│ 🏴 Фракции      [4/15]   █████░░░░░ 27%    │
│ 📝 Заметки      [2]      ---               │
├─────────────────────────────────────────────┤
│           [Поиск...] 🔍                     │
└─────────────────────────────────────────────┘
```

### Запись персонажа

```
┌─────────────────────────────────────────────┐
│ 👤 Старейшина Чжан                    ⭐⭐⭐ │
├─────────────────────────────────────────────┤
│                                             │
│  [Портрет]                                 │
│                                             │
│  Статус: Глава Совета Старейшин             │
│  Фракция: Секта Небесного Меча             │
│  Уровень: Глас Небес (8)                    │
│                                             │
│  ─────────────────────────────────────────  │
│                                             │
│  "Старейшина секты Небесного Меча.          │
│   Известен своей мудростью и строгим        │
│   характером. За его спиной более 500       │
│   лет культивации..."                       │
│                                             │
│  ─────────────────────────────────────────  │
│  Прогресс: ████████████░░░░░░ 65%          │
│                                             │
│  Открытые факты:                            │
│  • Обучил 1000+ учеников                    │
│  • Владеет техникой "Меч Небес"            │
│  • Скрытый факт [???]                       │
│                                             │
│  Связанные записи:                          │
│  → Секта Небесного Меча                    │
│  → Техника: Меч Небес                      │
└─────────────────────────────────────────────┘
```

### Запись локации

```
┌─────────────────────────────────────────────┐
│ 🗺️ Нефратовый пик                     ⭐⭐⭐⭐│
├─────────────────────────────────────────────┤
│                                             │
│  [Карта локации]                           │
│                                             │
│  Регион: Центральные горы                   │
│  Опасность: Высокая                         │
│  Концентрация Ци: ×10                      │
│                                             │
│  ─────────────────────────────────────────  │
│                                             │
│  "Священная гора, где концентрация Ци       │
│   в 10 раз выше обычного. Здесь             │
│   расположена штаб-квартира Дворца          │
│   Нефритового Неба..."                      │
│                                             │
│  ─────────────────────────────────────────  │
│  Прогресс: ████████████████░░ 80%          │
│                                             │
│  Интересные места:                          │
│  ✓ Главная вершина                          │
│  ✓ Пещера медитации                         │
│  ✓ Древний алтарь                           │
│  ○ Скрытая долина [???]                    │
│                                             │
│  Известные NPC:                             │
│  → Мастер Ли (учитель)                      │
│  → Охранник Ван                             │
└─────────────────────────────────────────────┘
```

---

## Заметки игрока

Игрок может создавать свои заметки:

```csharp
/// <summary>
/// Заметка игрока.
/// </summary>
[System.Serializable]
public class PlayerNote : JournalEntry
{
    public System.DateTime createdDate;
    public System.DateTime lastModifiedDate;

    public void UpdateContent(string newContent)
    {
        description = newContent;
        lastModifiedDate = System.DateTime.Now;
    }
}

// В JournalManager
public void CreateNote(string title, string content)
{
    var note = new PlayerNote
    {
        id = $"note_{System.DateTime.Now.Ticks}",
        title = title,
        description = content,
        category = JournalCategory.Notes,
        createdDate = System.DateTime.Now,
        lastModifiedDate = System.DateTime.Now
    };

    allEntries.Add(note);
    entryDict[note.id] = note;
    categoryDict[JournalCategory.Notes].Add(note);
}
```

---

## Интеграция

### Точки открытия

| Событие | Действие |
|---------|----------|
| Встреча с NPC | `DiscoverEntry("npc_" + npcId)` |
| Посещение локации | `DiscoverEntry("location_" + locId)` |
| Изучение техники | `DiscoverEntry("technique_" + techId)` |
| Убийство существа | `DiscoverEntry("creature_" + mobId)` |
| Находка предмета | `DiscoverEntry("item_" + itemId)` |
| Прогресс квеста | `AddFact(entryId, "quest_fact")` |

---

*Документ создан: 2026-04-03*
*Статус: Теоретические изыскания*
