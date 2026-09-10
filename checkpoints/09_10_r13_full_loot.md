# R13 FULL-LOOT — спаун NPC через генерацию, трупы, полный лут (2026-09-10)

## Задача (от пользователя)
1. NPC спаун через генерацию
2. Доступ к инвентарю мёртвого NPC
3. Система full loot

## Что сделано

### 1. «NPC спаун через генерацию» — NPCSpawnCompositionService
- `game/src/Modules/NPC/NPCSpawnCompositionService.cs` — процедурный генератор
  состава населения локации: тип локации (Farm/Village/Sect = мирное ядро;
  WildLands/Dungeon/Secret = дикое) + DangerLevel + сид → список
  SpawnRequest(role, level, species). Детерминированно (SeededRandom,
  prime-offset 31337). Кап 12 NPC.
- HumanNPCSpawnPhase: хардкод-массив SpawnRoles[7] УДАЛЁН → GenerateStartup(loc).
  Диалоги по-прежнему маппятся по роли.
- **ReinforcementTick** (вызывается из NPCModule.Tick каждые 45 игровых сек):
  если живых < 60% от целевого населения → спавн 1 сгенерированного NPC на
  walkable-тайле ≥12 тайлов от игрока (вне поля зрения). Мир восполняет
  потери: полный full-loot цикл (убил → обыскал → новый NPC приходит).
- Reset() для повторной сборки сцены (ReAssembly PASS).

### 2. «Доступ к инвентарю мёртвого NPC» — CorpseService + CorpseData
- `Core/Data/CorpseData.cs` (CorpseItem: SlotId Guid + ItemId + Count +
  Rarity + Source), `Core/Messaging/Contracts/CorpseContracts.cs`
  (CorpseCreated/Removed/LootedEvent), `Core/Interfaces/ICorpseService.cs`.
- `Modules/NPC/CorpseService.cs`: подписка на NPCDeathEvent → снапшот
  экипировки + инвентаря + духовных камней (таблица 4.1: L1-2: 1-3 осколка,
  L3-4: 2-5, L5+: 1-2 фрагмента) в CorpseData на месте смерти.
- **Замена Этапа 3**: NPCModule.OnNPCDeathForLoot (1-2 СЛУЧАЙНЫХ предмета на
  землю — реальная экипировка NPC исчезала) удалён. Теперь ЧЕСТНЫЙ лут.
- TTL: трупы живут 1440 игровых секунд (1 день), RemoveOldCorpses из тика.
- Гварды: труп для ЖИВОГО NPC не создаётся (fake-death события QA);
  дедуп повторных смертей одного NPC.
- SlotId-адресность (паттерн R10 TOCTOU): TryTakeItem(corpseId, Guid).
- Выдача лута — ItemAddRequestEvent (EVT-02 command-паттерн, InventoryModule
  внутри, overflow → земля).

### 3. «Full loot» — LootWindow + E-флоу
- `Adapter/UI/LootWindow.cs`: модальное окно 760×520 (паттерн TradeWindow):
  слева содержимое трупа (группы: ношеное/камни/карманы; ЛКМ — взять
  SlotId-адресно; hover; тултипы; индикаторы редкости), справа инвентарь
  игрока (вес/объём), кнопка «✦ Забрать всё (N)» + «Уйти».
- GameWorldController: E рядом с трупом (труп ближе живого NPC) → пауза +
  LootWindow; Esc/CorpseRemovedEvent → закрыть + резюм (авторитетная точка);
  модальные гварды (_UnhandledInput modalOpen, SetOverUI).
- `Adapter/Scene/CorpseSpriteRenderer.cs`: маркеры трупов — «саван» + крест +
  золотой лут-бейдж (есть лут) + нейм-плейт «Имя · N§»; под живыми NPC.
- EventLogWindow: «☠ Имя: можно обыскать (N поз., E)» / «☠ Обыскано: …».

### 4. Фикс «фантомных» предметов (найден QA)
- `Modules/Generator/ClassicLootSeeder.cs`: material_iron_scrap,
  material_spirit_stone_shard, spirit_stone_shard, spirit_stone_fragment —
  использовались с фазы 4.1 (FillInventory, CombatLootService), но НИКОГДА не
  регистрировались в ItemDatabase → взятие молча отклонялось
  (InventoryModule «не найден»). Сидер вызывается из GeneratorModule.Start().
- CorpseService: неизвестные БД предметы в труп не попадают (P1-5 паттерн,
  предупреждение в лог).

## QA (все headless, GODOT_NEWGAME=1)
- **GODOT_LOOT_DEBUG=1 → VERDICT: PASS** (новый LootSimDebug):
  генерация состава (12 NPC/6 ролей/детерминизм), труп-контейнер,
  окно (строки/шапка/кнопка), SlotId-взятие, double-take отказ, full loot
  (инвентарь вырос), удаление трупа, TTL, ReinforcementTick (forceCheck).
- Регрессии: KILLFEED, STORAGE, TRASHDROP, CONTEXT, SAVELOAD, QUEST,
  COMBAT_SIM, REASSEMBLY — **все PASS**.
- Build: dotnet build — 0 errors (348 warnings — прежний набор).

## Управление (для игрока)
- Убить NPC → на месте смерти труп (маркер с крестом и золотой точкой).
- **E** рядом с трупом → окно обыска (пауза).
- **ЛКМ** по строке — взять предмет; **«Забрать всё»** — полный лут.
- **Esc** / «Уйти» — закрыть. Трупы исчезают через игровой день.

## Файлы
Новые: CorpseData.cs, CorpseContracts.cs, ICorpseService.cs, CorpseService.cs,
NPCSpawnCompositionService.cs, LootWindow.cs, CorpseSpriteRenderer.cs,
LootSimDebug.cs, ClassicLootSeeder.cs (9).
Изменённые: NPCModuleServices.cs, NPCModule.cs, HumanNPCSpawnPhase.cs,
GeneratorModule.cs, GameWorldController.cs, SceneBuilder.cs,
EventLogWindow.cs (7).
