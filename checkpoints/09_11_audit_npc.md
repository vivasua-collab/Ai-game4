# АУДИТ: NPC (модуль целиком + животные + трупы + группы + save/load)

**Дата:** 2026-09-11
**HEAD на старте:** `d0b065d`
**Скоуп:** READ-ONLY. Все 22 файла `game/src/Modules/NPC/` (~6.9k строк, прочитаны
полностью). Стыки: NpcDomainResetPhase(0) / AnimalSpawn(6) / HumanNPCSpawn(7) /
GroupSpawn(8) / AbstractSceneAssemblyPhase (SkipOnLoad), GameSession
(LoadGame-порядок), Core/Data (NPCState, NPCGroup, CorpseData, AnimalInfo,
Position2D), WorldService (DeltaTime=1 игр.сек/тик), QiService (эмиттер
QiChangedEvent), CombatModule.OnAttackIntent, NPCSpriteRenderer (Adapter),
ClassicLootSeeder, ReAssemblySimDebug, docs_v2 (6 файлов).

---

## Сводка

- **Находок:** 16 (P2×7, P3×9). Критических (P1) нет — ядро R13/R14/R15/R16
  и animal-fix 09-11 держится, регрессий не найдено.
- **Главное:** домен NPC чистится при пересборке честно (R14 P2-1), но
  **AnimalService выпал из ResetWorld** — звери переживают тёплый LoadGame
  (NPC-1); NPC-движение игнорирует проходимость (NPC-2); save/load деградирует
  урон/броню NPC (NPC-3) и теряет отношения (NPC-4); смерть члена группы не
  обрабатывается (NPC-5); двойная генерация экипировки (NPC-6); фантомная
  угроза `sever_unknown` (NPC-7).
- **Код НЕ изменялся** (READ-ONLY аудит).

---

## Находки

### NPC-1 [P2] AnimalService не сбрасывается при LoadGame — ghost-animals
**Файл:** `NPCModule.cs:118-123`, `GameSession.cs:143`, `AnimalSpawnPhase.cs:40` (SkipOnLoad=true), `AnimalService.cs:226-232, 400-403, 508-532`.
**Сценарий:** тёплый LoadGame: `ResetWorld()` чистит Spawner/Corpse/Group —
AnimalService не входит ни в ResetWorld, ни в LoadGame-путь; AnimalSpawnPhase
(единственное место с ClearAnimals) на Load пропускается. Животные (вкл.
мёртвых — при смерти нет `RemoveEntity`, `IsAlive=false` навсегда в
`_animals`) и `_hostileAnimals` переживают загрузку: их нет в сейве (не
ISaveable), позиции/HP — от прошлой сессии; LoadGame другого мира оставляет
зверей чужого мира. Незакрытый аналог R14 P2-1 для животного поддомена
(NewGame чистит фаза 6, LoadGame — никто).
**Фикс:** `GameSession.LoadGame`/`NPCModule.ResetWorld` → `AnimalService.ClearAnimals()`
(уже чистит `_animals`/`_hostileAnimals`/per-entity BodyParts).

### NPC-2 [P2] NPC-движение игнорирует проходимость тайлов
**Файл:** `NPCMovementService.cs:358-370` (MoveToward без IsWalkable), `:375-395` (wander-цель без проверки).
**Сценарий:** `state.Position = pos + direction*step` — NPC ходят сквозь
стены/воду. Контраст: `AnimalService.StepTowardsTarget` (586-625) проверяет
walkable каждого подшага; спавн-фазы проверяют. R16-kiting-отход лучника
(296-301) уводит в непроходимые тайлы; stuck-детекции нет (у зверей есть).
WT-6 (TileFlags.Passable) не задействован NPC-путём.
**Фикс:** greedy-подшаги с `IsWalkable` (паттерн зверей) в MoveToward.

### NPC-3 [P2] Save/Load: урон/броня NPC деградируют (RestoreState без вклада экипировки)
**Файл:** `NPCService.cs:597-598` vs `NPCSpawnerService.cs:177-181`.
**Сценарий:** спавн: `SetTotalDamage(BaseDamage + weapon.Damage)`,
`SetTotalArmor(equipArmor(+5×слоты) + BaseDefense)`. RestoreState:
`SetTotalArmor(BaseDefense)`, `SetTotalDamage(BaseDamage)` — без оружия и
+5×слоты. После каждой загрузки NPC слабее (лучник теряет урон лука, броня
−5..−15). Round-trip (цель 9) нечестен по статам.
**Фикс:** воспроизвести формулу спавна в RestoreState или хранить вклад
экипировки в NPCSaveEntry.

### NPC-4 [P2] Отношения NPC не сериализуются; NPCSaveEntry.AttitudeScore — мёртвое поле
**Файл:** `NPCRelationshipService.cs:63-64` (не ISaveable), `NPCCombatAdapter.cs:177, 189` (-10/-20 после боя), `NPCService.cs:363, 498`.
**Сценарий:** послебоенные -10/-20 и накопленные отношения живут в
`_relationships` — не в сейве. `NPCState.AttitudeScore` сериализуется, но
пишется только 0 при спавне и не читается логикой (grep: только StateToData/
сейв) — ложная видимость сохранности. После LoadGame месть сбрасывается в
нейтрал.
**Фикс:** ISaveable для RelationshipService либо одна точка правды
(state.AttitudeScore ↔ сервис).

### NPC-5 [P2] Смерть участника/лидера группы не обрабатывается
**Файл:** `NPCGroupService.cs` (нет подписки на NPCDeathEvent; RemoveMember 78-96 — только ручной), `GROUP_SYSTEM.md:28, 118, 129`.
**Сценарий:** NPC умирает → труп/killfeed, но состав групп не меняется:
мёртвый лидер остаётся `LeaderId`, мёртвые копятся в `MemberIds`/
`_memberToGroup`, групповой Tick ведёт живых к целям. Doc §129 «повышение
первого последователя», §28/§118 (расформирования) — не реализованы.
**Фикс:** подписка на NPCDeathEvent → RemoveMember (повышение уже готово) →
DisbandGroup при пустоте.

### NPC-6 [P2] Двойная генерация экипировки: «Матрёшка» затирает assembly-набор
**Файл:** `NPCAssemblyService.cs:236-281` (EquipHumanoid, IItemGeneratorService, все 7 слотов с L1+, вкл. не-документированный WeaponOff) + `NPCSpawnerService.cs:287-323` (EquipFromGenerator, IEquipmentGenerator, перезапись WeaponMain + 60% Head/Torso).
**Сценарий:** assembly экипирует NPC; затем спавнер ПЕРЕЗАПИСЫВАЕТ
WeaponMain/Head/Torso предметами другого генератора — assembly-предметы
молча исчезают (не в инвентаре/трупе: state.EquipmentIds уже «матрёшкины»).
`BaseDefense` включает `equipmentIds.Count*5` (`NPCAssemblyService.cs:498`)
от смешанного набора. Doc `NPC_ASSEMBLY_PIPELINE.md §6.2` (Torso L2+/Head L3+/
Feet L4+, один генератор) не соответствует коду.
**Фикс:** один источник экипировки (оставить EquipFromGenerator — реальные
Damage/Penetration), из EquipHumanoid убрать дубли; синхронизировать §6.2.

### NPC-7 [P2] Фантомная угроза "sever_unknown" → атака на несуществующую цель
**Файл:** `NPCAIService.cs:588` (`Threats["sever_unknown"]=100`), `:193-202, 277-283` (topThreat→TargetId), `NPCModule.cs:161-177` и `NPCMovementService.cs:344-352` (фолбэк «не-NPC → позиция игрока»).
**Сценарий:** ампутация → угроза с фиктивным ID. После таймаута Fleeing (4
тика) при HP>20% GetTopThreatId вернёт фантом → Attacking с
TargetId="sever_unknown": все потребители трактуют «нет такого NPC» как
ИГРОКА — дистанция/чейз/кайт до игрока, `AttackIntentEvent(npc, фантом)`.
NPC гонится и бьёт игрока «из ниоткуда». Тот же фолбэк — для любой
несуществующей/мёртвой NPC-цели.
**Фикс:** в OnBodyPartSevered писать реальный sourceId (или не писать вовсе);
валидация topThreat (NPC существует или игрок) перед назначением TargetId.

### NPC-8 [P3] Темп движения NPC: 2 тайла/тик каждый тик — без каденции
**Файл:** `NPCMovementService.cs:358-370` (speed×DeltaTime, DeltaTime=1/тик — `WorldService.cs:88`), `NPCConfig.cs:48`.
**Сценарий:** NPC-люди двигаются 2 тайла КАЖДЫЙ тик; звери — 1–2 тайла раз
в 2–6 тиков (`AnimalService.cs:631-640`). NPC в ~6–12× подвижнее зверей,
wander-цель перевыбирается мгновенно (порог 0.5 при шаге 2).
**Фикс:** каденция (MoveCooldownTicks-паттерн зверей) и/или speed≈0.3–0.5.

### NPC-9 [P3] Random.Shared в AI/движении + мёртвые NPCConfig-таймауты
**Файл:** `NPCAIService.cs:380, 459-474` (хардкод 3/5/8/2/6/10/15), `NPCMovementService.cs:392-393, 427`; мёртвое: `NPCConfig.cs:87-99` (Idle/Wander/Patrol/FleeTimeout, FollowDistance — 0 читателей).
**Сценарий:** поведение NPC недетерминировано (спавн — SeededRandom);
таймауты разошлись с конфигом (Idle 3 vs 10).
**Фикс:** SeededRandom для AI или пометка осознанности; вычистить конфиг.

### NPC-10 [P3] Кролик: «удваивает скорость убегания» не реализовано; месть зверя — всегда к игроку
**Файл:** `AnimalService.cs:534-539` (rabbit → return; продолжает обычный wander), `:411-465` (чейз к `_playerService.Position`; LastAttackerId не используется), `:508-549` (hostile от ЛЮБОГО источника); `ANIMALS.md:121, §5.1, §5.4`.
**Сценарий:** doc: кролик удваивает скорость — код не меняет speed/поведение;
doc: месть «только к атаковавшему» — код направляет на игрока; doc: «цель
умирает → wander» — PlayerDeathEvent не отслеживается (hostile остаётся
после смерти/респавна игрока).
**Фикс:** rabbit → ускорение/отход от атакующего; чейз по LastAttackerId;
подписка на PlayerDeathEvent → de-aggro.

### NPC-11 [P3] Мёртвый код: NPCSpeciesSelector, NPCVisualService-stub
**Файл:** `NPCSpeciesSelector.cs` (+`NPCModuleServices.cs:32` DI; SelectSpecies — 0 вызовов), `NPCVisualService.cs` (no-op; рендер в Adapter NPCSpriteRenderer; пустой UpdateVisualPositions() каждый тик из NPCModule.Tick).
**Фикс:** удалить селектор (или задействовать в GenerateStartup), заглушку оставить с честным комментарием (уже есть).

### NPC-12 [P3] Ghost-кэши переживают ResetWorld
**Файл:** `NPCModule.cs:108` (`_npcAttackTimers` не чистится в ResetWorld:118-123), `NPCSpriteRenderer.cs:53-58` (`_npcSwings/_npcFacingLeft/_npcLastX` копят мёртвых; `_npcWeaponIds` пересканируется по живым), `AnimalService.cs:73` (`_nextId` не сбрасывается ClearAnimals), `NPCSpawnCompositionService.cs:93-120` (`_nextEventSpawnSeq`/`EventSpawnCount` сбрасываются только фазой 7 — на LoadGame остаются).
**Сценарий:** мелкие утечки/мусорные ключи (NpcId=Guid → ключи уникальны,
словарь растёт меж мирами). На логику не влияет.
**Фикс:** очистка в ResetWorld/Reset по аналогии со спавнером.

### NPC-13 [P3] Doc-drift NPC.md §12 + устаревший комментарий RestoreState
**Файл:** `NPC.md:233-251` (заявляет PresetId в NPCSaveEntry — в коде SpeciesId; PresetId теряется, RestoreState:461 ставит ""), `NPCService.cs:443-444` (комментарий «Runtime-коллекции … не сериализуются» — ложь после Волны 3: BodyParts/Equipment/Inventory сериализуются, 397-426).
**Фикс:** обновить §12 (SpeciesId + flat-массивы) и комментарий.

### NPC-14 [P3] Недетерминизм лута камней (string.GetHashCode) + Guid NpcId
**Файл:** `CorpseService.cs:418, 350` (`SeededRandom(npcId.GetHashCode())`), `NPCAssemblyService.cs:389-392` (NpcId = Guid).
**Сценарий:** string.GetHashCode рандомизирован per-process → число камней в
трупе одного NPC меняется между запусками; Guid-ID делает инстансы
неповторимыми (состав населения детерминирован — R13 выполняется; лут
камней формально «от npcId», но сам ID хаотичен).
**Фикс:** сид камней от стабильных данных (CultivationLevel+SpeciesId) или
зафиксировать осознанность в DEATH_AND_LOOT §2.1.

### NPC-15 [P3][UNVERIFIED] Merchant-гейт сбивает месть вне боя; Ци NPC не синкается
**Файл:** `NPCAIService.cs:256-261` (торговец в Attacking вне CombatService-боя → принудительно Idle на следующем AI-тике; актуально только при вне-боевом DamageAppliedEvent — сейчас паблишер только CombatService.ExecuteAttack, в бою гейт IsInCombat выше); `NPCState.CurrentQi` после спавна никто не пишет (QiRegenService:56-74 обновляет только QiDataProvider; CaptureState сериализует state.CurrentQi) — дрейф пуст, пока Ци NPC не расходуется (QiConsumeRequestEvent обрабатывает только игрок, `QiService.cs:316-321`).
**Фикс:** перепроверить при появлении NPC-кастов/вне-боевого урона; Ци —
синк state↔провайдер при регене.

### NPC-16 [P3] AI §4.2 «Attacking → цель мертва → Patrolling» не реализован
**Файл:** `NPCAIService.cs:215-293` (нет проверки живости TargetId), `NPCModule.cs:156-196` (интент к мёртвой NPC-цели не гейтится; CombatModule тоже не проверяет — CombatEndedEvent чистит TargetId, поэтому ограничено окном до конца боя).
**Фикс:** в EvaluateAndDecide — цель-NPC мертва → сброс угроз/TargetId → Wandering.

---

## Проверено чисто

- **Lifecycle (R14 P2-1):** NpcDomainResetPhase(0) → NPCModule.ResetWorld →
  Spawner (полный путь DespawnNPC: Body/Qi/Equipment провайдеры, баффы,
  якоря движения, отношения, реестр + restored-ветка GetAllStates +
  `_spawnedIds`/`_generatedDamage` clear) + Corpse (CorpseRemovedEvent на
  каждый, seq=1) + Group (3 словаря, seq=1) + WeaponVisualCatalog.ResetCache.
  GameSession.LoadGame:143 — сброс ДО `_save.Load` (RestoreState). Исключение —
  животные (NPC-1). Double-spawn NPC устранён (ReAssemblySimDebug-ассерты).
- **NPCService:** GetNPCState null-гвард (R16); GetAllStates-снапшот (NPC-A2);
  мёртвые фильтруются во всех запросах; ghost-спрайтов нет
  (NPCSpriteRenderer._Draw:204 IsAlive-фильтр); E-диалог → OnNPCInteracted с
  RoleId (P0-1) — работает; RegisterNPC/UnregisterNPC симметричны спавнеру.
- **NPCAIService R16:** боевые переходы ДО гейта IsInCombat; бегство HP≤0.2
  работает и в бою (PublishDisengage); leash AggroRadius×3=15 для
  цели-игрока; анти-flip-flop (пере-агр гейтится healthRatio > FleeRatio);
  месть RetaliateOrFlee идемпотентна; OnCombatStarted покрывает обе стороны;
  threat-decay по снапшоту-буферу (NPC-A07); O(n)-тик + O(n²) Friendly —
  норм при ≤12 NPC (животные вне реестра — не тикаются AI).
- **NPCCombatAdapter:** единое правило тел (IsEntityAlive || HP≤0) =
  AnimalService; null-гварды Winner/Loser (Flee-окончание);
  OnCombatDisengage — двусторонний сброс; фантомный CombatStartedEvent-publish
  удалён (R16); CurrentHealth обновляется из BodyParts (Волна 2.3).
- **CorpseService (R13):** full-loot честен — экипировка (все слоты) +
  инвентарь (стаками) + камни (таблица 4.1); дедуп по NpcId; гвард «труп
  живому не создаётся»; SlotId=Guid-адресность; TTL 1440 игр.сек;
  CorpseRemovedEvent → закрытие окон; LootAll/TryTakeItem →
  ItemAddRequestEvent (EVT-02); фантомные ID фильтруются
  (material_iron_scrap зарегистрирован ClassicLootSeeder). Трупы зверей:
  TryCreateAnimalCorpse («Волк», камни 1–3, дедуп).
- **NPCSpawnCompositionService (R13/R14):** GenerateStartup детерминирован
  (SeededRandom(loc.Seed+31337)); состав по типу локации + DangerLevel;
  кап 12 с резом с конца (мирные раньше врагов); ReinforcementTick удалён —
  внутрисессионного восполнения нет; TrySpawnEventNpc — walkable +
  Чебышёв≥12 от игрока + кап спавнера (event-pipeline пока не вызывает —
  задел, R14-план соблюдён).
- **AnimalService 09-11:** волк — чейз (грид-шаги, во время кулдауна бежит),
  укус только Чебышёв≤2, кулдаун 2 тика < EnemyTurnTimeout 2.5с; de-aggro
  >5 тайлов → CombatDisengageEvent; кролик не мстит; статы SpeciesRegistry
  (волк STR8/AGI14); Quadruped; смерть → NPCDeathEvent → труп. ClearAnimals
  чистит все 3 коллекции — для NewGame.
- **NPCMovementService:** kiting (KiteMinRange=3, AttackRange оружия,
  отход/стойка/сближение×1.2); R14 P2-2 — якорь восстановленных NPC =
  текущая позиция (фолбэк GenerateRandomPointInRadius); group-overlay
  (CurrentGroupTarget приоритет); ProcessNpcAttacks — дальность из оружия
  (Ranged>2), кулдаун 1.6с, кэш позиции игрока (M2).
- **Save/Load (кроме NPC-3/4):** NPCSaveEntry полный round-trip — позиции,
  body parts (flat Red/Black/Max/vital), техники/экипировка/инвентарь
  (joined), статы, InnateElement, AIState/TargetId/IsInCombat, Age/
  MaxLifespan; RestoreState пересобирает BodyParts; per-entity регистрация
  только живым.
- **Qi:** QiChangedEvent эмиттер единственный — игрок (QiService
  config.EntityId) → CachedPlayerQi корректен; NPC QiRegen 10%/день — парная
  подписка DayChangedEvent.
- **Killfeed:** KillerId=="old_age" обработан («ушёл из мира»);
  PlayerIdResolver-паттерн в killfeed/animal-атаке.

## Соответствие docs_v2

| Doc | Вердикт | Детали |
|-----|---------|--------|
| NPC.md | drift §12 | PresetId не сериализуется (SpeciesId); список entry устарел (NPC-13) |
| NPC_AI_SYSTEM.md | ~ок | §4.2/§5.2/§8.2 соответствуют (5/1.5/10/8); «цель мертва→Patrolling» нет (NPC-16); §8.1 aggression −100..100 vs float 0..1 |
| NPC_ASSEMBLY_PIPELINE.md | drift §6.2 | уровневые гейты слотов не реализованы; WeaponOff не задокументирован; «Матрёшка» не упомянута (NPC-6) |
| ANIMALS.md | drift §5 | кролик «удваивает скорость» нет; «только к атаковавшему» — к игроку; «цель умирает→wander» нет (NPC-10) |
| DEATH_AND_LOOT.md | ок | §1–2/§5 соответствуют (TTL, full loot, дедуп, звери); сохранение трупов в сейве doc не заявляет — loss при save/load молчаливый (осознанное умолчание) |
| GROUP_SYSTEM.md | drift | §129/§28/§118 — повышения/расформирования при смерти нет (NPC-5) |

---

*Аудит NPC завершён. Приоритет фиксов: NPC-1 → NPC-3+NPC-6 (в связке) →
NPC-7 → NPC-4 → NPC-5 → NPC-2 → P3. Код не изменялся (READ-ONLY).*
