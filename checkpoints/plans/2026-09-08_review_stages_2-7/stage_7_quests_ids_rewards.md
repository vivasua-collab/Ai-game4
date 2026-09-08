# Этап 7 — Генерация, registry предметов, система квестов

> Статус: ⬜ план → исполнение. Файлы: ItemDatabaseService/ItemGeneratorService/EquipmentGenerator/
> TechniqueGeneratorService/TechniqueRegistry/VerificationService/DeduplicationService, QuestService/
> QuestProgressTracker/QuestRewardService/QuestModule/QuestConfig, Data/QuestData/QuestObjective,
> AnimalService/NPCSpawnerService, StartingGearPhase, IQuestService/IQuestRewardService.

## Находки ревью → план валидации и фикса

### P0-1. Все 4 стандартных квеста: TargetId не совпадают с реальными event'ами
**Клейм (детально):**
1. Квест на волков ждёт TargetId="wolf"; animal spawn создаёт `animal_{species}_{n}` (animal_wolf_1); EnemyKilledEvent передаёт ID погибшей сущности → "wolf" ≠ "animal_wolf_1", прогресс не растёт.
2. Квест на железную руду ждёт "iron"; реальный ID — material_iron_ore; ItemAddedEvent.ItemId ≠ "iron".
3. Квест на лес ждёт "forest"; WorldModule регистрирует только test_polygon; travel физически не переключает локацию (этап 6).
4. Квест на старейшины ждёт elder_01; стартовый NPC-spawn создаёт merchant/guards/passersby/enemies, elder нет; E-path публикует ли NPCInteractedEvent вообще?
**Валидация:** прочитать QuestConfig/QuestService default quests, QuestProgressTracker (как сравниваются ID), AnimalService (формат ID), StartingGearPhase/NPCSpawnerService (кто спавнится), WorldModule (локации), GameWorldController (публикация NPCInteractedEvent).
**Фикс:**
- Не использовать entity instance IDs как semantic-цели квестов:
  - убийства: в EnemyKilledEvent добавить SpeciesId/EnemyKind ("wolf") отдельно от EntityId; трекер сравнивает по нему (fallback на парсинг animal_wolf_1 → wolf, если события менять рискованно — но правильный путь — payload);
  - добыча: TargetId = material_iron_ore (canonical);
  - диалог: role/tag (NPCRole.Elder) или спавн конкретного elder при scene assembly; E-path обязан публиковать NPCInteractedEvent;
  - travel-квест: не регистрировать до появления реальной локации и рабочего перехода (отключить/пометить «в разработке»).
- End-to-end QA: accept → runtime event → objective completed → quest completed → reward.

### P1-2. Награда quest_gather_iron "steel" — несуществующий ItemData
**Клейм:** reward TargetId="steel", Amount=2; QuestRewardService публикует ItemAddRequestEvent и помечает выданной; InventoryModule не находит "steel" в ItemDatabase (steel есть только в MaterialService как материалная характеристика) → предмет молча пропадает.
**Валидация:** grep "steel" в ItemDatabaseService (есть ли item), QuestConfig rewards.
**Фикс:**
- До публикации item reward: IItemDatabaseService.TryGetItem; не помечать reward выданной, пока не подтверждено.
- Зарегистрировать material_steel_ingot в ItemDatabase ЛИБО сменить reward на существующий canonical ID (по контенту: стальной слиток логичен; проверить лор-доки).
- Тост при неудаче выдачи (не молчать).

### P1-3. Диалог старейшины не связан с StartQuest
**Клейм:** ветка «мне нужны задания» только меняет node; StartQuest зовётся только из QuestWindow UI; narrative обещает квест, но игровой контракт не связан.
**Валидация:** прочитать DialogueService (nodes старейшины, DialogueChoice), QuestWindow, grep StartQuest.
**Фикс:**
- В DialogueChoice добавить action (например QuestId).
- Выбор «Конечно, помогу» → StartQuest через command/event; показать результат (успех/prerequisite/лимит).
- После фикса P0-1 квест «поговори со старейшиной» получает реальный elder + NPCInteractedEvent путь.

### P2-4. QuestConfig auto-generation — мёртвая конфигурация
**Клейм:** EnableAutoGeneration/AutoGenerateIntervalTicks/AutoGenerateChance объявлены, но QuestModule.Tick пуст, QuestService не использует.
**Валидация:** grep использования полей.
**Фикс:** удалить поля (не создавать ложное ожидание) — в духе решения пользователя «легаси не держать». ИЛИ реализовать scheduler, если это заглушка будущей фазы — по решению: удалить, добавит сложности без контента.

### P2-5. RequiredCultivationLevel не проверяется при принятии
**Клейм:** поле есть, StartQuest проверяет наличие/статус/лимит/prerequisite, но не уровень.
**Валидация:** прочитать StartQuest и источник текущего уровня (IQiDataProvider).
**Фикс:** проверка через кэш/данные текущего игрока при StartQuest; отказ с причиной. Если поле не используется контентом — всё равно добавить проверку (дёшево, честный контракт).

## QA-план

- Новый хук `GODOT_QUEST_DEBUG`: полный цикл каждого квеста (accept → событие → прогресс → complete → reward в инвентаре).
- Проверить: волки → SpeciesId; руда → material_iron_ore; старейшина → спавн + NPCInteractedEvent; travel-квест скрыт.
- Регрессия DIALOGUE/GEN + полная.

## Коммит
`fix(review-7): quests — semantic targets, real rewards, dialogue-quest link` + push.
