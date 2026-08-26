> **⚠️ ARCHIVED (2026-07-14):** Setup завершён. План от 2026-03-30 устарел — см. docs/SETUP_GUIDE.md v2.0.

# 🚀 Unity Project Setup — План подготовки

**Версия:** 1.0
**Дата:** 2026-03-30
**Проект:** Cultivation World Simulator
**Unity:** 6000.3 (2D Core с URP)

---

## 📋 Что можно сделать БЕЗ Unity

| Действие | Возможно | Способ |
|----------|----------|--------|
| Создать структуру папок | ✅ Да | Документация + Git |
| Написать C# скрипты | ✅ Да | Текстовые файлы в Git |
| Создать JSON конфигурации | ✅ Да | Текстовые файлы в Git |
| Создать .asset файлы | ❌ Нет | Только через Unity Editor |
| Создать .unity сцены | ❌ Нет | Только через Unity Editor |
| Создать .prefab файлы | ❌ Нет | Только через Unity Editor |
| Настроить Project Settings | ❌ Нет | Только через Unity Editor |

---

## 🎯 Рекомендуемый подход

### Вариант A: Подготовка в Git (СЕЙЧАС)

```
Я создаю в репозитории:
├── UnityProject/              # Папка для Unity проекта
│   ├── Assets/
│   │   ├── Scripts/           # C# скрипты (готовы к копированию)
│   │   ├── Data/              # JSON конфигурации
│   │   └── Docs/              # Документация
│   └── ProjectSettings/       # Инструкции по настройке
└── docs/                      # Документация (уже есть)
```

**Когда получите доступ к Unity:**
1. Создать новый проект с 2D URP шаблоном
2. Скопировать содержимое `UnityProject/Assets/` в проект
3. Настроить Project Settings по инструкции

### Вариант B: Unity Package (ТРЕБУЕТ UNITY)

Создать custom package со скриптами — но это требует Unity для тестирования.

---

## 📁 Рекомендуемая структура проекта Unity

```
CultivationWorldSimulator/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/                    # Ядро игры
│   │   │   ├── GameInitializer.cs
│   │   │   ├── GameSettings.cs
│   │   │   └── Constants.cs
│   │   │
│   │   ├── Data/                    # ScriptableObjects
│   │   │   ├── ScriptableObjects/
│   │   │   │   ├── TechniqueData.cs
│   │   │   │   ├── ItemData.cs
│   │   │   │   ├── MaterialData.cs
│   │   │   │   └── NPCPresetData.cs
│   │   │   └── RuntimeData.cs
│   │   │
│   │   ├── Combat/                  # Боевая система
│   │   │   ├── DamageCalculator.cs
│   │   │   ├── QiBuffer.cs
│   │   │   └── LevelSuppression.cs
│   │   │
│   │   ├── Qi/                      # Система Ци
│   │   │   ├── QiController.cs
│   │   │   └── QiDensity.cs
│   │   │
│   │   ├── Body/                    # Система тела
│   │   │   ├── BodyController.cs
│   │   │   └── BodyPart.cs
│   │   │
│   │   ├── NPC/                     # NPC и AI
│   │   │   ├── NPCController.cs
│   │   │   └── NPCAI.cs
│   │   │
│   │   ├── World/                   # Мир
│   │   │   ├── WorldController.cs
│   │   │   └── LocationController.cs
│   │   │
│   │   ├── Inventory/               # Инвентарь
│   │   │   ├── InventoryController.cs
│   │   │   └── EquipmentController.cs
│   │   │
│   │   ├── Save/                    # Сохранения
│   │   │   ├── SaveManager.cs
│   │   │   └── JsonSerialization.cs
│   │   │
│   │   └── UI/                      # Интерфейс
│   │       ├── UIManager.cs
│   │       └── HUDController.cs
│   │
│   ├── ScriptableObjects/           # Asset файлы (создаются в Unity)
│   │   ├── Config/
│   │   │   ├── CultivationLevels.asset
│   │   │   └── GameSettings.asset
│   │   ├── Techniques/
│   │   ├── Items/
│   │   └── Materials/
│   │
│   ├── Prefabs/
│   │   ├── Player/
│   │   ├── NPC/
│   │   ├── Items/
│   │   └── UI/
│   │
│   ├── Scenes/
│   │   ├── MainMenu.unity
│   │   ├── GameWorld.unity
│   │   └── Combat.unity
│   │
│   ├── Art/
│   │   ├── Sprites/
│   │   ├── UI/
│   │   └── Effects/
│   │
│   ├── Audio/
│   │   ├── Music/
│   │   └── SFX/
│   │
│   ├── Data/                        # JSON файлы
│   │   ├── Techniques/
│   │   ├── Items/
│   │   ├── Materials/
│   │   └── NPCPresets/
│   │
│   └── Resources/                   # Для Resources.Load
│       └── Data/
│
├── Packages/
│   └── manifest.json
│
├── ProjectSettings/
│   └── (создаются Unity)
│
└── README.md
```

---

## 🔧 Что я могу создать СЕЙЧАС

### 1. Базовые C# скрипты (без MonoBehaviours)

| Файл | Описание |
|------|----------|
| `Constants.cs` | Константы игры |
| `DataStructures.cs` | Структуры данных |
| `Formulas.cs` | Формулы расчётов |

### 2. ScriptableObject классы

| Файл | Описание |
|------|----------|
| `TechniqueData.cs` | Данные техники |
| `ItemData.cs` | Данные предмета |
| `MaterialData.cs` | Данные материала |
| `NPCPresetData.cs` | Пресет NPC |
| `CultivationLevelData.cs` | Уровень культивации |

### 3. JSON конфигурации

| Файл | Описание |
|------|----------|
| `techniques.json` | Пресеты техник |
| `items.json` | Пресеты предметов |
| `materials.json` | Пресеты материалов |
| `cultivation_levels.json` | Уровни культивации |

### 4. Инструкции

| Файл | Описание |
|------|----------|
| `SETUP_GUIDE.md` | Пошаговая настройка проекта |
| `IMPORT_GUIDE.md` | Импорт данных в Unity |

---

## ❓ Что требуется от вас

### Решите:

1. **Название папки проекта?**
   - `CultivationWorldSimulator`
   - `CultivationGame`
   - Другое?

2. **Создать скрипты СЕЙЧАС?**
   - Да, создать базовые классы в Git
   - Нет, подождать до доступа к Unity

3. **Какие системы приоритетны?**
   - Combat (боевая система)
   - Qi (система Ци)
   - Body (система тела)
   - Inventory (инвентарь)
   - NPC (NPC и AI)

4. **Нужны ли JSON данные?**
   - Да, создать JSON с пресетами техник/предметов
   - Нет, вводить данные вручную в Unity

---

## 📝 Следующие шаги

### Если вы хотите создать скрипты сейчас:

1. Я создам папку `UnityProject/` в репозитории
2. Напишу базовые C# классы
3. Создам JSON конфигурации
4. Напишу инструкции по импорту

### Когда получите доступ к Unity:

1. Создать проект: `New Project → 2D Core (URP)`
2. Скопировать `UnityProject/Assets/Scripts/`
3. Скопировать `UnityProject/Assets/Data/`
4. Создать ScriptableObject assets по инструкции

---

## ⚠️ Ограничения

Без Unity Editor невозможно:
- Проверить компиляцию кода
- Создать сцену
- Настроить рендеринг 2D URP
- Создать Sprite Atlas
- Настроить физику 2D

**Рекомендация:** Подготовить код в Git, затем импортировать в Unity.

---

*Документ создан: 2026-03-30*
*Статус: Ожидание решения пользователя*
