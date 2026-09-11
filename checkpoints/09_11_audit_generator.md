# АУДИТ: Generator (модуль Generator + сидеры + ItemDatabase + стыки NPC/Save)

**Дата:** 2026-09-11, **HEAD:** `d0b065d`
**Скоуп (READ-ONLY):** Modules/Generator — все 13 файлов (EquipmentGenerator 471,
ItemGeneratorService 530, TechniqueGeneratorService 677, FormationGeneratorService
250, DeduplicationService, VerificationService, ItemDatabaseService, оба сидера,
GeneratorModule/Services, TechniqueRegistry, IFormationGeneratorService);
Core/Data: GeneratorTables, EquipmentGenerationTables, ItemData/EquipmentData/
QiStoneData, Constants (482-519), LevelBoundaries; стыки: NPCAssemblyService,
StartingGearPhase, GeneratorModule.Start, SceneOrchestrator/SkipOnLoad,
GameSession.LoadGame, InventoryService, CheatPanel; docs_v2: PRE_GENERATION,
GENERATORS_SYSTEM §9-10, 06_player/EQUIPMENT_SYSTEM §4-8, чекпоинт
2026-08-26_epic_legendary_overcap.
**Заморожено (не репортится):** промо 20%/оверкап 18% — канон; Qi=long; Permil;
Element.Poison.

---

## Сводка

- Прочитано полностью 13 файлов + таблицы + стыки (grep-карта потребителей
  генераторов/сеятелей; вероятностная оценка коллизий ID).
- **Находок: 13 (P1×2, P2×3, P3×8).** Канон «матрёшки»/легендарок в коде
  честный; SeededRandom-детерминизм устойчив; Interlocked-уникальность в
  рамках процесса есть. Оба P1 — жизненный цикл ID между сессиями и коллизии
  легаси-пути. Код НЕ менялся.

---

## Находки

### G-1 [P1] Легаси-генератор: ItemId с modulo-1000 → коллизии, подмена/дюп предметов
`ItemGeneratorService.cs:77/139/216/278` — ID вида
`{weapon|armor|charger|consumable}_{level}_{seed % 1000:D3}`. Основной
потребитель — `NPCAssemblyService.cs:241-278` (экипировка NPC) и :327-343
(пилюли): seeds = `rng.Next(0, int.MaxValue)` равновероятны → для одинаковых
(префикс, уровень) P(коллизия) ≈ birthday/1000: 20 предметов → ~18%, 60 →
~83%. `ItemDatabaseService.Register` (:75-80) при совпадении ItemId молча
ЗАМЕНЯЕТ запись. Следствия: слоты экипировки двух NPC ведут на один ItemId →
резолв по ID (лут/UI/EquipmentDataProvider) отдаёт последний объект обоим;
лут с двух трупов = два «предмета» с одним ID → фактический дюп экипировки
+ кросс-загрязнение статов. EquipmentGenerator.NextId (:348-352) чинил это
(«фикс modulo-1000»), но NPC-сборка НЕ мигрирована на «Матрёшку».
**Фикс:** NPCAssemblyService.EquipHumanoid/FillInventory → IEquipmentGenerator
(заодно G-5), либо убрать `% 1000` + счётчик.

### G-2 [P1] Cold-load (новый процесс → LoadGame): каталог предметов не восстанавливается
`QiStoneSeeder.Seed` вызывается ТОЛЬКО из `StartingGearPhase.cs:174`, а фаза
генеративная: `SkipOnLoad=true` (`AbstractSceneAssemblyPhase.cs:53`;
оркестратор пропускает — `SceneOrchestrator.cs:108-115`): 10 камней Ци,
материалы (:65-93), расходники (:96-124), стрелы (:130-148), слиток
(:154-171), генерированная экипировка (:189-235) — НЕ регистрируются при
загрузке сейва. ClassicLootSeeder — ОК (`GeneratorModule.cs:42`, процесс-старт).
`InventoryService` сериализует ТОЛЬКО itemId+count (:538-559), RestoreState
восстанавливает слоты без резолва (:561-587) → после перезапуска и LoadGame
все предметы кроме 4 ClassicLoot — фантомы (`TryGetItem=null` → вес fallback
0.5 кг :461-465; использование/экипировка невозможны). R13-паттерн на уровне
каталога; QA SaveLoadSimDebug гоняет save/load в ОДНОМ процессе — не ловит.
Параллельно ломается экипировка NPC из сейва (equipmentIds → DB).
**Фикс:** ItemDatabaseService → ISaveable (снимок предметов) или сид-фаза
канона с SkipOnLoad=false + DTO-снимок сгенерированных (паттерн
TechniqueSnapshotDto, TechniqueService.cs:636-652) + NEXT-ID в сейв (G-3).

### G-3 [P2] Счётчики ItemId не персистентны между сессиями
`EquipmentGenerator._idCounter` — static (:40), `ItemGeneratorService.
_generationCounter` — instance (:35): сбрасываются при старте процесса. При
seed=0 сид = counter×1000003+level (:345-346) — ID между сессиями не
воспроизводимы (компонент G-2). В рамках процесса уникальность есть
(Interlocked ✓).
**Фикс:** NEXT-ID в save_meta + восстановление при LoadGame.

### G-4 [P2] DeduplicationService.Clean(FormationRegistry) — no-op, имитирует очистку
`DeduplicationService.cs:174-189`: собирает duplicates, но НЕ удаляет (в
отличие от Clean(TechniqueRegistry):151-169 → registry.Remove).
`FormationRegistry` вообще не имеет Remove (`Modules/Formation/Data/
FormationRegistry.cs:15-37`). Лог «duplicates found», возврат count как будто
удалено. Живых вызовов нет (потребитель — только CountDuplicates техник в
CheatPanel:613-616) — API-ловушка.
**Фикс:** FormationRegistry.Remove(id) + цикл удаления (паритет с Technique).

### G-5 [P2] NPC-экипировка: WeaponClassId не пишется (R15 покрывает только «Матрёшку»)
NPCAssemblyService на легаси-генераторе (см. G-1) → `WeaponClassId` пуст →
WeaponVisualCatalog fallback парсит `eq_wep_{subtype}_` (`EquipmentData.cs:
31-38`), легаси-ID `weapon_{L}_{seed}` не матчится → всё NPC-оружие рендерится
generic «sword». Морфофильтр R15 на месте: EquipHumanoid только
Humanoid/HybridHarpy/HybridLamia (`NPCAssemblyService.cs:104-113`) — звери без
экипировки ✓. 7 классов ✓ (WeaponVisSimDebug 7/7).
**Фикс:** закрывается миграцией на IEquipmentGenerator (G-1).

### G-6 [P3] Double-register стартового набора
`StartingGearPhase.cs:192/201/211/221/226/233` — повторный Register после
внутреннего Register генератора (`EquipmentGenerator.cs:146/238`):
идемпотентно, но ложные логи «уже зарегистрирован — замена».

### G-7 [P3] Дедуп — точный fingerprint, без «окна/порога»
`DeduplicationService.cs:41-73`: разные роллы ≠ дубли (by-design Phase D);
PRE_GENERATION.md описывает только техники — fingerprints экипировки/формаций
в доке не задокументированы.

### G-8 [P3] FormationGeneratorService: Heavy отсутствует в SizePool
`:93-99` — Small×4, Medium×3, Large×2, Great×1: Heavy НЕ выпадает через
`Generate()`, даунгрейд Heavy→Medium (:121-122) — мёртвая ветка (жива только
через GenerateSpecified). Комментарий «генератор честно их выдаёт» неточен.
**Фикс:** добавить Heavy в пул — даунгрейд начнёт работать.

### G-9 [P3] 16-бит хеш сида в ID формаций/техник
`FormationGeneratorService.cs:141`, `TechniqueGeneratorService.cs:621` —
пространство 65536 (seed=0 и 65537 → одинаковый хеш) → при совпадении
остальных компонент — молчаливая замена в реестре. Маловероятно.

### G-10 [P3] VerificationService не верифицирует легаси-предметы
`:117-148`: матчинг по ItemType==w.Id (легаси "Weapon") или NameEn
"_id_" (легаси "Sword Level 3") → «Unknown base class — bounds skipped»:
валидатор работает только для «Матрёшки».

### G-11 [P3] Легаси-мелочи: Coverage без клампа, QiFlowPenalty-двойная семантика
`ItemGeneratorService.cs:172` — `80f + level*2f` → L11+ >100% (связи с
MAX_CULTIVATION_LEVEL нет; «Матрёшка» клампит DamageReduction 80 ✓
`:220`). `:496-501` — положительное QiFlowPenalty = «бонус проводимости»
(кламп −30..+30) в поле с именем Penalty — шум для потребителей.

### G-12 [P3] Doc-drift: легендарки не описаны в docs_v2
`06_player/EQUIPMENT_SYSTEM.md §4.1` — таблица грейдов до Transcendent;
Legendary-тир/промо 20%/оверкап 18%/value ×3 в доке отсутствуют (канон живёт
в `Constants.cs:506/516/519` + чекпоинт 2026-08-26). Также §4.1 «Эффектов
20/50/80%» не реализовано в основном пути (гарант-энчант — только у
легендарок, `EquipmentGenerator.cs:331-343`); GENERATORS_SYSTEM §9.1
таксономия vs FormationType×8; §9.2 ядра не реализованы (вариант А).

### G-13 [P3] Недетерминизм мира между запусками (признано докой); реестры не сбрасываются
`TechniqueGrantPhase.cs:59` и PreGenTechniquePhase — `seed = Environment.
TickCount`; PRE_GENERATION.md:126-131 признаёт («В будущем: seed в сейве»).
Формации игрока — `_formationRng.NextInt64()` (PlayerTechniqueCaster:267-269)
— невоспроизводимы (следствие — F-5 Formation-чекпоинта). Плюс
`WorldInitPhase.cs:35` сбрасывает только TechniqueRegistry —
FormationRegistry/ItemDatabase копят записи прошлого мира при warm-NewGame.

---

## Проверено чисто

- **Промо 20% / оверкап 18% / value ×3 (замороженный канон):** `Constants.cs:
  506/516/519` = 0.20/0.18/3.0 ✓. Промо только при ролле Transcendent, rng НЕ
  потребляется для прочих грейдов (`EquipmentGenerator.cs:304-306`) —
  детерминизм обычных предметов не нарушен ✓. Итоговые шансы: L9 →
  Legendary ≈4%, оверкап 18% = контрольный дамп (`GeneratorModule.cs:135-167`)
  ✓. Легендарка = Transcendent + гарант-энчант + макс. бонусы + value ×3 +
  суффикс ✓ (:77-83/101/108-110/143-145); оверкап = статы по L+1 с клампом
  MAX_CULTIVATION_LEVEL=10 ✓ (:86-88/178-180).
- **«Матрёшка» §2/§4.1/§4.2 дословно** (:127-130/:213-215; GradeProfiles;
  EquipmentGradeWeightsByLevel — чётные уровни интерполированы);
  **стат-границы:** ScaleInt(max(1,round)), Coverage ∈ [Min..Max], DR-кламп
  80, Volume 1..4 — int не переполняется при L≤10 ✓.
- **WeaponClassId — 7 классов (R15)** ✓ (dagger/sword/axe/spear/greatsword/
  bow/staff; EquipmentGenerator:122; WeaponVisSimDebug 7/7; морфофильтр
  зверей NPCAssemblyService:104-113 ✓).
- **Тирание материалов × редкость** ✓ — tier=clamp((level+1)/2,1,5)
  (PickMaterial:379 = DurabilityFor:321 = легаси:424); T5 void доступен
  оружию (фикс «L9-оружие из железа»:373-391) ✓.
- **Interlocked-уникальность NextId** ✓ (static counter, потокобезопасно,
  в рамках процесса коллизий нет). **SeededRandom-детерминизм** ✓ (явный
  seed; GenerateRandom — производные seed+1/+2; BuildSpecified — грейд из
  отдельного потока seed^0x5A5A5A5A, ревью-1 P1-1).
- **PreGenTechniquePhase:** BuildSpecified без регистрации → FilterValid →
  дедуп → Register только валидных ✓ (= PRE_GENERATION.md).
- **Сеятели:** QiStoneSeeder — канон 1024 ед/см³, объёмы {1,8,27,64,125} ✓
  (QiStoneData.cs:79-91 = GENERATORS_SYSTEM §10), 10 ID до выдачи ✓ (новая
  игра); ClassicLootSeeder — 4 ID из GeneratorModule.Start раньше NPC-спавна
  ✓ — R13-фикс держится для своих ID (регресс остального каталога — G-2).
- **ItemDatabaseService:** кэш-флаг + перестройка ✓; копия списка категорий
  ✓; замена при дубле с чисткой индекса ✓. **VerificationService:**
  "_id_"-матчинг (фикс greatsword ⊃ sword) ✓; Ultimate ×2 ✓; пассив = 0 ✓.
- **TechniqueGeneratorService:** capacity = base×2^(L−1)×(1+mastery×0.005),
  qiCost = floor(base×2^(L−1)) БЕЗ грейда, damage = capacity×gradeMult,
  MathF.Round-согласование с LevelBoundaries ✓; множители {1.0,1.3,1.6,2.0}
  из доки ✓.

---

## Соответствие docs_v2

| Пункт доки | Код | Статус |
|---|---|---|
| PRE_GENERATION.md (алгоритм/логи/фаза 13) | PreGenTechniquePhase | ✓ точно |
| PRE_GENERATION «Детерминизм: sessionSeed» | Environment.TickCount | признано докой; TODO seed-in-save (G-13) |
| EQUIPMENT_SYSTEM §2 «Матрёшка» | EquipmentGenerator | ✓ |
| §4.1 грейды (мульт./бонусы) | GradeProfiles | ✓ точно |
| §4.2 веса по уровню | GeneratorTables | ✓ точно |
| §4.1 «Эффектов 20/50/80%» | не реализовано (кроме гарант-энчанта легендарок) | ✗ (G-12) |
| §5 материалы/тиры | Materials + PickMaterial | ✓ (+T5 void фикс) |
| §8 зачарования | TryApplyEnchant (MinGrade, §8.4 eff) | ✓ |
| Легендарный тир/промо/оверкап | Constants 0.20/0.18/×3 | ✓ канон; доки нет (G-12) |
| GENERATORS_SYSTEM §9 формации | FormationType×8, вариант А | ✓ (таксономия/ядра — drift, G-12) |
| GENERATORS_SYSTEM §10 камни Ци | QiStoneSeeder/QiStoneData | ✓ точно |

---

*Аудит READ-ONLY. Код не менялся. Приоритет фиксов: G-1 (миграция NPC на
Матрёшку — закрывает G-5 и дюп-вектор) → G-2 (персистентность каталога/счётчика
ID — закрывает G-3) → G-4 → G-8 → P3.*