# Система сборки сцены — Editor-time FullSceneBuilder (ЗАМОРОЖЕН)

**Версия:** 3.3-old
**Дата:** 2026-07-14
**Проект:** Cultivation World Simulator (Unity 6.3 URP 2D)
**Статус:** ⛔ ЗАМОРОЖЕН — legacy Editor-time система
**Код:** `UnityProject/Assets/Editor/SceneBuilder/` + `UnityProject/Assets/Editor/FullSceneBuilder.cs`

---

> **⚠️ ВНИМАНИЕ:** Этот документ описывает **замороженную** Editor-time систему сборки сцены.
> Она НЕ используется в runtime — для актуальной runtime-сборки см. [SCENE_BUILDER_SYSTEM.md](./SCENE_BUILDER_SYSTEM.md).
>
> Код в `Assets/Editor/` физически присутствует (Phase00-02 + Phase01B авто-запускаются при первом открытии проекта), но `FullSceneBuilder.cs` (оркестратор) заморожен и не вызывается.

---

## Обзор

### Editor-time SceneBuilder (ЗАМОРОЖЕН)

SceneBuilder — система автоматической сборки сцены в Unity Editor. Позволяет одной кнопкой (`Tools → Full Scene Builder → Build All`) создать полностью рабочую сцену с нуля: от URP-настройки до размещения NPC.

### Принцип работы (Editor-time)

Паттерн **Оркестратор + фазовые файлы**:
- **Оркестратор** (`FullSceneBuilder.cs`) — ЗАМОРОЖЕН, только регистрирует фазы и управляет запуском
- **Каждая фаза** — изолированный класс в отдельном файле, реализующий `IScenePhase`
- Фазы идемпотентны — повторный запуск безопасен, пропускает уже выполненное

---

## Структура файлов

```
Assets/Scripts/Editor/
├── FullSceneBuilder.cs                  # ОРКЕСТРАТОР (ЗАМОРОЖЕН)
└── SceneBuilder/
    ├── IScenePhase.cs                   # Интерфейс фазы
    ├── SceneBuilderConstants.cs         # Общие константы
    ├── SceneBuilderUtils.cs             # Утилиты (v3.0: +RefreshAndWait, +LoadAssetWithRetry)
    ├── Phase00URPSetup.cs              # URP Asset Setup (АВТО-ЗАПУСК при открытии проекта)
    ├── Phase01Folders.cs                # Папки (37 шт.)
    ├── Phase02TagsLayers.cs             # Теги, Physics Layers, Sorting Layers (АВТО-ЗАПУСК)
    ├── Phase03SceneCreation.cs          # Создание сцены
    ├── Phase04CameraLight.cs            # Камера + Light2D (reflection fallback)
    ├── Phase05GameManager.cs            # GameManager + системные компоненты
    ├── Phase06Player.cs                 # Player (Rigidbody2D + 8 компонентов)
    ├── Phase07UI.cs                     # Canvas + HUD + MainMenu + PauseMenu
    ├── Phase08Tilemap.cs                # Grid + Tilemap
    ├── Phase09GenerateAssets.cs         # JSON → ScriptableObjects
    ├── Phase10GenerateSprites.cs        # Процедурные спрайты тайлов
    ├── Phase11GenerateUIPrefabs.cs      # UI-префабы формаций
    ├── Phase12TMPEssentials.cs          # Импорт TMP Essentials
    ├── Phase13SaveScene.cs             # Сохранение сцены
    ├── Phase14CreateTileAssets.cs       # Tile .asset файлы
    ├── Phase15ConfigureTestLocation.cs  # Тестовая локация
    ├── Phase16InventoryData.cs          # BackpackData + Test Equipment
    ├── Phase17InventoryUI.cs            # Inventory UI — оркестратор (partial)
    │   ├── Phase17BodyDollPanel.cs      # BodyDoll + DollSlotUI + wiring
    │   ├── Phase17BackpackPanel.cs      # Backpack + StorageRing + SlotUI prefab
    │   ├── Phase17TooltipPanel.cs       # TooltipPanel + 24 SerializeField
    │   ├── Phase17DragDrop.cs           # DragDropHandler + ContextMenu
    │   ├── Phase17BodySilhouette.cs     # Процедурный силуэт тела
    │   └── Phase17InventoryLayout.cs    # Header + Belt + TabBar + SpiritStorage
    ├── Phase18InventoryComponents.cs    # SpiritStorage + StorageRing на Player
    └── Phase19NPCPlacement.cs           # 7 NPC на тестовой поляне
```

> **Примечание (2026-07-14):** Дополнительно в `Assets/Editor/SceneBuilder/` присутствуют:
> - `Phase01SpriteImport.cs` — авто-настройка импорта 184 спрайтов (АВТО-ЗАПУСК)
> - `Phase01BTmpImport.cs` — импорт TMP Essentials (АВТО-ЗАПУСК, ExecuteMenuItem фикс 06_18)
>
> Эти фазы заменили часть Phase01/Phase12 логики для автоматического запуска при первом открытии проекта.

---

## Реестр фаз

| # | Класс | Имя | Описание | ~Строк |
|---|-------|-----|----------|--------|
| 00 | Phase00URPSetup | URP Setup | URP Asset + Renderer2D + GraphicsSettings | 300 |
| 01 | Phase01Folders | Folders | Создание 37 папок | 45 |
| 02 | Phase02TagsLayers | Tags & Layers | Теги (7), Physics Layers (8), Sorting Layers (6) | 197 |
| 03 | Phase03SceneCreation | Scene Creation | Создание сцены, удаление дефолтной камеры | 38 |
| 04 | Phase04CameraLight | Camera & Light | Camera2DSetup, Light2D (reflection fallback) | 304 |
| 05 | Phase05GameManager | GameManager | GameInitializer + системные компоненты | 99 |
| 06 | Phase06Player | Player | Rigidbody2D + 8 компонентов + PlayerVisual | 220 |
| 07 | Phase07UI | UI | Canvas + EventSystem + HUD + Menu | 406 |
| 08 | Phase08Tilemap | Tilemap | Grid + Terrain/Objects + TileMapController | 211 |
| 09 | Phase09GenerateAssets | Generate Assets | JSON → ScriptableObjects (3 группы) | 80 |
| 10 | Phase10GenerateSprites | Tile Sprites | Процедурные спрайты | 32 |
| 11 | Phase11GenerateUIPrefabs | Formation UI | UI-префабы формаций | 32 |
| 12 | Phase12TMPEssentials | TMP essentials | Импорт TMP | 64 |
| 13 | Phase13SaveScene | Save Scene | EditorSceneManager.SaveScene | 49 |
| 14 | Phase14CreateTileAssets | Tile Assets | TerrainTile + ObjectTile (21 тип) | 179 |
| 15 | Phase15ConfigureTestLocation | Test Location | Камера + коллайдеры + HarvestableSpawner | 170 |
| 16 | Phase16InventoryData | Inventory Data | BackpackData (5 шт.) + Test Equipment (16 шт.) | 389 |
| 17 | Phase17InventoryUI | Inventory UI | InventoryScreen + 8 панелей + ~150 wiring (7 partial-файлов) | 249+6p |
| 18 | Phase18InventoryComponents | Inventory Components | SpiritStorage + StorageRing на Player | 170 |
| 19 | Phase19NPCPlacement | NPC Placement | 7 NPC на тестовой поляне | 146 |

---

## Интерфейс IScenePhase

```csharp
public interface IScenePhase
{
    string Name { get; }          // Короткое имя (для логирования)
    string MenuPath { get; }      // Путь в меню
    int Order { get; }            // Порядковый номер (0-19)
    bool IsNeeded();              // Проверяет, нужно ли выполнение
    string IsNeededReason => "";  // Человекочитаемая причина (для диагностики)
    void Execute();               // Выполняет фазу
}
```

---

## Карта зависимостей между фазами

```
Phase00 (URP)          → независима
Phase01 (Folders)      → независима
Phase02 (Tags)          → независима
Phase03 (Scene)         → Phase01 (Folders)
Phase04 (Camera)        → Phase00 (URP), Phase02 (Tags — Sorting Layers)
Phase05 (GameManager)   → Phase03 (Scene)
Phase06 (Player)        → Phase02 (Tags — Layer "Player"), Phase05 (GameManager)
Phase07 (UI)            → Phase03 (Scene), Phase05 (TimeController), Phase06 (Camera2DSetup)
Phase08 (Tilemap)       → Phase02 (Sorting Layers), Phase03 (Scene)
Phase09 (Assets)        → Phase01 (Folders)
Phase10 (Sprites)       → Phase01 (Folders)
Phase11 (UI Prefabs)    → Phase01 (Folders)
Phase12 (TMP)           → независима
Phase13 (Save)          → Phase03 (Scene)
Phase14 (Tile Assets)   → Phase08 (TileMapController), Phase10 (Sprites), Phase09 (SO)
Phase15 (Test Location) → Phase04 (Camera), Phase08 (Grid/TileMapController)
Phase16 (Inventory Data)→ Phase09 (SO assets), Phase01 (Folders)
Phase17 (Inventory UI)  → Phase07 (GameUI/Canvas), Phase16 (BackpackData)
Phase18 (Inv Components)→ Phase06 (Player), Phase17 (InventoryScreen), Phase16 (BackpackData)
Phase19 (NPC)           → Phase02 (Tag "NPC"), Phase06 (Player position)
```

---

## AssetDatabase: синхронизация и проблема первого прохода

### Проблема

`AssetDatabase.Refresh()` **асинхронный** — после вызова Unity запускает конвейер импорта в фоне. Если следующая фаза вызывает `LoadAssetAtPath<T>()` до завершения импорта, получает `null`.

Это было **корневой причиной** необходимости второго прохода: на втором проходе ассеты уже импортированы, и `LoadAssetAtPath` работает корректно.

### Решение (v3.0)

1. **`RefreshAndWait()`** — синхронный Refresh, гарантирует завершение импорта:
   ```csharp
   public static void RefreshAndWait()
   {
       AssetDatabase.SaveAssets();
       AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronous | ImportAssetOptions.ForceUpdate);
   }
   ```

2. **`LoadAssetWithRetry<T>()`** — надёжная загрузка с повторными попытками:
   ```csharp
   public static T LoadAssetWithRetry<T>(string path, int maxRetries = 3, int delayMs = 200) where T : Object
   ```

3. **`ImportAsset` с `ForceSynchronous`** — для файлов, записанных через `File.WriteAllBytes()`:
   ```csharp
   AssetDatabase.ImportAsset(path,
       ImportAssetOptions.ForceSynchronous | ImportAssetOptions.ForceUpdate);
   ```

### Важно: .meta файлы

**.meta файлы создаёт UnityEditor САМА** при `AssetDatabase.Refresh()` или `AssetDatabase.ImportAsset()`. Никогда не создавайте .meta файлы вручную! Если .png записан через `File.WriteAllBytes()`, Unity не узнает о нём пока не будет вызван `Refresh` или `ImportAsset`.

### Карта вызовов Refresh/ImportAsset (после фиксов)

| Фаза | Вызов | Статус |
|------|-------|--------|
| Phase00 | `RefreshAndWait()` | ✅ Синхронный |
| Phase01 | `RefreshAndWait()` | ✅ Один раз в конце (было 37+ Refresh) |
| Phase09 | `RefreshAndWait()` × 3 | ✅ Синхронный (было 3× асинхронный) |
| Phase10 | `RefreshAndWait()` | ✅ Синхронный |
| Phase11 | `RefreshAndWait()` | ✅ Синхронный |
| Phase14 | `RefreshAndWait()` | ✅ Синхронный (было асинхронный) |
| Phase16 | `RefreshAndWait()` × 2 | ✅ Синхронный |
| Phase17 | `ImportAsset(ForceSynchronous)` | ✅ Синхронный (было без флагов) |
| Utils: ReimportTileSprites | `ImportAsset(ForceSynchronous)` + `RefreshAndWait()` | ✅ Синхронный |
| Utils: EnsureDirectory | **Без Refresh** | ✅ Вызывающая фаза сама вызывает |

---

## Ключевые утилиты (SceneBuilderUtils v3.0)

### Управление сценой

| Метод | Описание |
|-------|----------|
| `EnsureSceneOpen()` | Открыть сцену + CleanMissingPrefabs (для Execute) |
| `EnsureSceneOpenRead()` | Открыть сцену без побочных эффектов (для IsNeeded) |

### Синхронизация AssetDatabase

| Метод | Описание |
|-------|----------|
| `RefreshAndWait()` | Синхронный Refresh — гарантирует завершение импорта |
| `LoadAssetWithRetry<T>(path, retries, delay)` | Надёжная загрузка с повторными попытками |

### Работа с папками

| Метод | Описание |
|-------|----------|
| `EnsureDirectory(path)` | Рекурсивное создание папок (без Refresh!) |

### Работа со свойствами

| Метод | Описание |
|-------|----------|
| `SetProperty(so, name, value)` | Установка свойства SerializedObject (int, float, bool, string, enum) |
| `SetupComponent<T>(go, setup)` | Настройка компонента через callback |
| `AssignTileProperty(so, name, path)` | Назначить TileBase из asset файла |

### UI

| Метод | Описание |
|-------|----------|
| `CreateTMPText(...)` | Создать TMP текстовый элемент |
| `CreateBar(...)` | Создать прогресс-бар (Slider) |

### Очистка

| Метод | Описание |
|-------|----------|
| `CleanMissingPrefabs()` | Удалить Missing Prefab + Missing Scripts |
| `EnsureSortingLayers()` | Создать Sorting Layers с детерминированными uniqueID |

### Загрузка TagManager

| Метод | Описание |
|-------|----------|
| `LoadTagManager()` | Безопасная загрузка TagManager.asset |

---

## Правила работы с SceneBuilder (исторические)

### ⛔ АБСОЛЮТНЫЕ ЗАПРЕТЫ (для замороженного кода)

1. **НЕ редактировать FullSceneBuilder.cs** — он заморожен
2. **НЕ редактировать PhaseNNXxx.cs** — они заморожены
3. **НЕ создавать .meta файлы вручную** — UnityEditor создаёт их сама
4. **НЕ вызывать `AssetDatabase.Refresh()` без параметров** — используйте `RefreshAndWait()`

### ✅ Исключение: Auto-run фазы

Phase00URPSetup, Phase01SpriteImport, Phase01BTmpImport, Phase02TagsLayers — **могут редактироваться**, т.к. они авто-запускаются при первом открытии проекта и поддерживают актуальное состояние окружения (GUID-rot фикс, импорт спрайтов, TMP).

Эти фазы используют `[InitializeOnLoadMethod]` + `EditorApplication.delayCall`.

---

## Известные проблемы (исторические)

1. ~~Phase17InventoryUI.cs (1793 строк)~~ — ✅ Рефакторинг выполнен (2026-05-07): разделён на 7 partial-файлов. Самый крупный — Phase17BodyDollPanel.cs (395 строк).
2. **Белые швы между terrain-тайлами** — Sprite.Create с FilterMode.Point может оставлять 1px артефакты
3. **Hero rendering behind surface** — Player может рендериться за поверхностью при неправильном порядке Sorting Layers. Покрывается Phase02
4. **Terrain tile назначения перезаписываются** — `TileMapController.EnsureTileAssets()` вызывает `ForceProceduralTerrainTile()` для terrain, перезаписывая .asset ссылки. Object tiles сохраняются.
5. ~~RuntimeSpriteLoader.cs CS0234~~ — ✅ ИСПРАВЛЕНО (2026-05-07): `ItemCategory`/`ItemRarity` были в `CultivationGame.Core`, а не `CultivationGame.Data.ScriptableObjects`.

---

## Ссылки

- **Актуальная runtime-система:** [SCENE_BUILDER_SYSTEM.md](./SCENE_BUILDER_SYSTEM.md) — RuntimeSceneBuilder + 10 фаз
- **Инструкции для Unity Editor:** ⛔ docs_asset_setup/ — ЗАМОРОЖЕН (устарел)
- **Предыдущий аудит:** [AUDIT_FullSceneBuilder_2026-04-17.md](../Legacy/docs_audit_code/AUDIT_FullSceneBuilder_2026-04-17.md)
- **Аудит 2026-05-07:** [05_07_scenebuilder_audit.md](../checkpoints_archive/05_07_scenebuilder_audit.md)
- **Архитектура проекта:** [ARCHITECTURE.md](./ARCHITECTURE.md)
- **Система инвентаря:** [INVENTORY_SYSTEM.md](./INVENTORY_SYSTEM.md)
- **Система тайлов:** [TILE_SYSTEM.md](./TILE_SYSTEM.md)
- **Горячие клавиши:** [!hotkeys.md](./!hotkeys.md)

---

*Создано: 2026-07-14 — выделено из SCENE_BUILDER_SYSTEM.md v3.3*
*Содержимое перенесено из оригинального SCENE_BUILDER_SYSTEM.md (v3.2, 2026-05-07)*
*Статус: ЗАМОРОЖЕН — для актуальной системы см. SCENE_BUILDER_SYSTEM.md*
