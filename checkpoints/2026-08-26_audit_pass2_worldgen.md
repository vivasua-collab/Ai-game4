# АУДИТ-2: Архитектура + Модуль мира (World / Tile / NPC-спавн)

**Дата:** 2026-08-26, 15:00–15:45 MSK
**Проход:** 2 из 3 (минимум), основной поток ИИ
**HEAD на старте:** `b8ddda1`
**Scope:** аудита-1 (архитектура — инварианты удержаны) + новые модули:
World (TimeService, WorldService), Tile (TileService, ResourceService —
выборочно), NPC-спавн-контур (фазы AnimalSpawn/HumanNPCSpawn/GroupSpawn,
NPCSpawnerService/AnimalService — структура и id-политики).

---

## Сводка

- **Файлов прочитано:** World/Tile полностью (988 строк), 3 спавн-фазы
  полностью, AnimalService/NPCSpawnerService — структура/ключевые методы.
- **Находок:** 7 (2 бага контента, 1 stale-state баг, 1 dead-code, 3 minor).
- **Исправлено:** 4 (B-1, B-2, B-6, B-7).

---

## Находки

### B-1 [BUG-CONTENT] Травы (Herb): двойной ролл → вероятность 0.01% вместо 1%

**Файл:** `Modules/Tile/TileService.cs` (Generate, Step 5).

Условие трав: `if (objType == null && terrain == Grass && rng.Next(0,100) < 1)`
— ролл 1% уже сделан; затем общий гейт `if (objType.HasValue &&
rng.Next(0,100) < chance)` роллил ЕЩЁ РАЗ с chance=1 → фактическая
вероятность 1% × 1% = 0.01%. На карте 50×50 (~1300 травяных тайлов)
математическое ожидание трав ≈ 0.13 шт — травы практически не существовали.

**Фикс:** `chance = 100` (ролл уже пройден — паттерн ore vein, у которого
chance=100 по той же причине). Теперь ~1% → ~13 трав на 50×50.

### B-2 [BUG] TryHarvest: событие с очищенным ResourceId

**Файл:** `Modules/Tile/TileService.cs` (TryHarvest).

При истощении ресурса updated.ResourceId очищается ДО публикации
ResourceHarvestedEvent — подписчики получали пустой id ресурса
(урон UI-тостам/логам «собрано: »).

**Фикс:** событие публикуется с исходным `tile.ResourceId`.

### B-6 [BUG-STALE] GroupSpawnPhase: _placedGroupCentres не сбрасывается между сборками

**Файл:** `Entry/Phases/GroupSpawnPhase.cs`.

Фаза — синглтон; `RunAssembly` вызывается и на NewGame, и на Load
(GameSession:91/131). Список центров групп накапливался между прогонами →
spacing-чек (minSpacing 20) гонялся по призракам прошлой сборки, новые
группы выталкивались в угол карты.

**Фикс:** `_placedGroupCentres.Clear()` в начале ExecuteAsync.

### B-7 [DEAD-CODE] NPCSpawnPhase — не регистрируемый стаб

**Файл:** `Entry/Phases/NPCSpawnPhase.cs` (удалён).

Заменён AnimalSpawnPhase (Phase 5), в SceneAssemblyRegistrar не
регистрировался, в коде упоминался только в комментариях. Удалён вместе
с .uid. Комментарии-упоминания оставлены как история.

### B-3 [MINOR] WorldService.GetLocation: BiomeType.Plains хардкодом

V1-стаб (задокументирован в коде). Для тест-полигона допустимо; при
мультилокациях — брать биом из LocationData. Не чиним (стаб объявлен).

### B-4 [MINOR] HumanNPCSpawnPhase: сид = loc.Seed + offset + (long)role + spawned

Роли с разными enum-значениями могут дать коллизию сидов при определённых
комбинациях (role+spawned) → два NPC с одинаковым сидом (статистические
двойники). На текущем наборе ролей не встречается. Отмечено как риск.

### B-5 [MINOR] TileService.SmoothBiomes: counts[16] без guard

Массив фиксированного размера 16 при 9 значениях BiomeType (+ legacy
алиасы с теми же значениями). При росте enum > 16 — выход за границы.
Рекомендация: Debug.Assert(BiomeTypeCount <= 16). Не чиним (не срабатывает).

---

## Что проверено и чисто

- **TimeService:** DeltaTime=1.0 фикс (2026-08-25) корректен и
  документирован; AdvanceTick не на интерфейсе — DI-cast внутри модуля
  (паттерн задокументирован).
- **WorldService:** реестры локаций/фракций, TryTravel → TravelStarted →
  LocationChanged (порядок корректный); стабы объявлены.
- **TileService.Generate:** noise-based генерация (fBm + domain warp),
  biome smoothing (мажоритарное правило, без аллокаций на тайл),
  beach-генерация, scatter объектов — логика корректна кроме B-1.
- **AnimalSpawnPhase:** ClearAnimals перед спавном (защита от дублей при
  re-assembly) ✓.
- **HumanNPCSpawnPhase:** детерминированные сиды (prime-offset 104729),
  walkable + дистанция от игрока, пропуск при неудаче, диалоги по ролям ✓.
- **GroupSpawnPhase:** spacing между группами + fallback, patrol routes с
  clamp и walkable-фолбэком, try/catch вокруг спавна животных ✓ (кроме B-6).
- **AnimalService:** lifecycle задокументирован (Start-фолбэк / Phase /
  Tick), wander RNG отдельным стримом, монотонные id ✓.

---

## Журнал фиксов

| Файл | Изменение |
|------|-----------|
| Modules/Tile/TileService.cs | Herb chance 1→100 (двойной ролл устранён); TryHarvest event — исходный ResourceId |
| Entry/Phases/GroupSpawnPhase.cs | _placedGroupCentres.Clear() на каждой сборке |
| Entry/Phases/NPCSpawnPhase.cs(+uid) | Удалён (dead code) |

**Верификация:** dotnet build 0 errors; GODOT_NEWGAME=1 — все фазы 1→14,
группы спавнятся (2 группы на 50×50), регрессий нет.

---

*Аудит-2 завершён. Следующий проход (аудит-3): + боевой контур
(Combat / Qi / Techniques / Formation / Body / Trade) — файл
`2026-08-26_audit_pass3_combat_qi.md`.*
