# Плейтест-2 (25.10, 09:00–10:30 UTC) — анализ и фиксы

> Владелец: отчёт 7 пунктов, лог сессии НЕ сохранён (сам владелец:
> «не сохранил лог»). Артефакты: upload/1.png, 2.png, 3.png — спрайт-пак
> (загружены 08:52 UTC, VLM-анализ ниже). Лог Logs_playtest1.txt (07:51) —
> пройденный Плейтест-1, не использовался.

## Вердикты/подтверждения владельца

| № | Сигнал владельца | Вердикт/фикс |
|---|---|---|
| 1 | Прерывание медитации мышью РАБОТАЕТ | **PT-R01 passed** (вердикт владельца; PT1-001 подтверждён) |
| 0 | Формация молнии: «в описании нет ничего» | PT2-001 fixed |
| 2 | Агро-NPC на старте убил, «передвинуть дальше» | PT2-002 fixed (≥40 тайлов) |
| 3 | Tab не фокусирует атакующего (до отхода на 1 тайл) | PT2-003 fixed (тост+маркер) |
| 4 | Техники не наносят урон NPC («требует анализа») | PT2-004 fixed (2 корня) |
| 5 | Выброшенные вещи нельзя поднять | PT2-005 fixed (регрессия ST14-005) |
| 6 | «Постоянно бьёт NPC» | следствие №2, не отдельный тикет |
| 7 | Спрайты: анализ + имплантация, нужна ли помощь? | PT2-006: пак = установленные idle/walk; бег/бой НЕТ — ТЗ выдано |

## Корневые причины (все — статически доказаны)

1. **PT2-001 (книга):** 3 слоя: (а) LearnedTechnique НЕ хранил Description
   → генераторское описание терялось при изучении; (б) RefreshDetails
   рендерил только статы — для формации мёртвые («Урон: 80» никем не
   потребляется); (в) FormationType бросался кубиком в момент каста
   (PlayerTechniqueCaster:645, несидированный rng) — «что делает» не
   знал код. FormationDescriptions (канон текстов R30) к книге не
   подключался.
2. **PT2-002 (спаун):** Hostile-роли в 5 тайлах от старта ==
   AggroRadius=5 (евклид) → агро с 1-го AI-тика; банды: центры 8 +
   patrol 9 + члены ±2. Погоня 4.8 vs 2.8 тайл/с, leash 30 — бегство
   невозможно. Role-aware дистанции НЕ существовало (EventSpawn=12
   существовал, но не для стартовой сборки).
3. **PT2-003 (Tab):** ЛОГИКА работала (CycleTarget, радиус 12,
   атакующий всегда 1-й в списке). Рендер: рамка ZIndex=Objects(3) <
   Player(4) → вплотную игрок перекрывает рамку и спрайт NPC; фидбека
   Tab не имел вовсе.
4. **PT2-004 (урон×NPC):** ДВА независимых слоя: (а) GetTechniqueAttackType
   мапит подтипы в Melee*/Ranged для scaling → LevelSuppression попадал
   в «нормальную» колонку: diff 2=×0.1, diff 3+=×0.0 (культиватор L3-4 =
   иммунен); (б) RawQi-буфер R21-4: NPC CoreCapacity=1000 → 90% Qi-урона
   в Ци, фидбека не было → «урон не проходит». Олень в PT1 умирал, т.к.
   звери после R38-4 CoreCapacity=0 (RawQi OFF) — прецедент того же
   симптома. PT0-3 melee-гейт и R39 Miss — проверены, НЕ виноваты.
5. **PT2-005 (pickup):** регрессия ST14-005 (e1c5140 10-23): E молча
   съедался при ЛЮБОЙ модали, а единственный UI выброса — внутри окна
   инвентаря, которое остаётся открытым после drop. Спека
   GROUND_ITEM_SYSTEM §3.1 гейта не знала → реализация противоречила
   канону. Вторично: пустой труп в 2.5 тайлах ел E всегда.
6. **Спрайты:** все 3 листа = ОДИН пакет «player_idle 4 + player_walk 6,
   64×64, AIGAME4 SPRITE PACK 1» (VLM×4 + слайсер + пиксельное сравнение
   35%/23% отличий из-за превью-подписей/сглаживания). Установленные
   idle/walk — тот же контент ЛУЧШЕГО качества. Боевых/беговых зон в
   паке НЕТ. Замена не требуется; нужны ГЕНЕРАЦИИ.

## Фиксы (коммит этот)

- **CombatContracts:** DamageAppliedEvent + AbsorbedByQi (10-арг
  супер-конструктор, старые цепочки делегируют).
- **LevelSuppression:** перегрузка CalculateSuppressionPermil(...,
  fromTechniqueAttack) → техники (кроме Ultimate) читают колонку
  technique (750/250/50‰), scaling-тип не тронут.
- **ICombatService:** DamageRequest + FromTechnique (default=false,
  конец конструктора — обратная совместимость).
- **CombatService:** fromTechnique = techniqueId числится в реестре.
- **DamageService:** СЛОЙ-2 через перегрузку; публикация с absorbedByQi.
- **DamageNumberRenderer:** «−X (Ци −Y)» / «КРИТ −X (Ци −Y)».
- **GWC:** инвентарь-исключение E (HandlePickup при видимом инвентаре);
  тост-подсказка в модальной ветке; пустой труп → false при предмете
  рядом (HasGroundItemNearby, 1.5 тайла); подписка
  PlayerTargetChangedEvent → тост «🎯 Цель: {имя} · {N} т» (NPC:
  NPCState.DisplayName; зверь: IAnimalService.GetDisplayName);
  +IAnimalService инжект.
- **NPCSpriteRenderer:** маркер-стрелка (3×DrawLine) НАД головой
  выбранной цели — читается при перекрытии спрайтом игрока.
- **HumanNPCSpawnPhase:** HostileMinDistanceFromPlayer=40, Enemy/Monster
  → minDist 40 ТОЛЬКО на clustered (≥100k) картах; мирные/малые — прежние
  5 (rng-стром малых карт НЕ сдвинут: параметр передан, вызовы прежние).
- **GroupSpawnPhase:** HostileGroupMinDistanceFromPlayer=40 → банды
  (FindGroupCenter параметризован minDistFromPlayer, прочие группы — 8).
- **TechniqueData:** + FormationKind (int, -1=легаси; FormationType в
  Core.Data — без циклов).
- **TechniqueGeneratorService:** PickFormationKind (пул кастера
  {Barrier,Amplification,Suppression,Gathering}, детерминированно;
  ролл ПОСЛЕ Ultimate-блока — прежние rng-значения не сдвинуты);
  GenerateDescription(+formationKind): формация — «Создаёт малую
  формацию «{label}» — {action}. Каст Z …»; Support/Movement описания
  поправлены по механике (врали).
- **TechniqueService:** LearnedTechnique + Description + FormationKind;
  LearnCore копирует; DTO: FromLearned/FromData/ToLearned/ToData
  пробрасывают (сейв старых версий — default -1, совместимо).
- **PlayerTechniqueCaster:** FormationKind≥0 → использовать, иначе
  прежний рандом-пул (легаси).
- **TechniqueBookWindow:** BuildActionBlock — блок «Действие» (описание
  генератора; формация: тип+действие+способ активации; легаси-фолбэк
  по подтипу); ChipTitle формаций: вместо «⚔{мёртвый урон}» — тип.
- **HotkeysWindow:** E — «…/ подобрать предмет» (канон HOTKEYS.md).

## Верификация

- Build 0 err (740 warnings — фон, как до правок).
- **Регрессия 37/37 PASS** (чанки 1–7): критичные для правок —
  COMBAT_SIM/COMBATAI (урон), STAGE8/STAGE15 (генератор техник:
  детерминизм сохранён — FormationKind-ролл в хвосте rng-строма),
  SAVELOAD (DTO), STAGE14/MODALQA/MODAL2 (E-гейт меню сейвов не тронут,
  A-сценарий — гейт в адаптере), L500 (спаун: состав/кольцо ≥40%/банды —
  Hostile теперь в кольце 40–100, сим зелёный), STORAGE/TRASHDROP
  (drop-пути), ANIMQA (спрайт-контур).
- UIShot-пруф (Xvfb+opengl3, full render): pt2_formation.png — «Молниевая
  Формация 1» (Lightning, та же стихия, что у владельца!) с блоком
  «Действие: … Создаёт малую формацию «Сбор Ци» — ×2 к поглощению Ци…»;
  VLM подтвердил текст. Попутно PT1-001/002-пруфы продолжают зелёнеть
  (медитация-клик True/True, walkability True/True).
- Портал: bugs.json 97→105 (волна 12: PT2-001..005 fixed, PT2-006
  planned/gap, PT2-007 proposed/owner-design, PT2-008 P3 planned);
  playtest.json +категория pt1_pt2_fix_retest (PT-R01 passed — вердикт
  владельца; PT-R02..R07 — шаги ретеста для владельца; PT-R07 = PT-S02
  failed до ретеста); валидатор VERDICT OK. status.json v2.5.

## Спрайт-ТЗ для генерации (ChatGPT по референсу pack 1)

Приоритет G2 (игрок), 64×64, top-down 45°, стиль = player_idle/walk:
1. player_run — 6 кадров (loop), поза бега.
2. player_melee — 6 кадров (one-shot), замах→удар→возврат.
3. player_cast — 4 кадра, накопление Ци (руки к груди → выброс).
4. player_hit — 2 кадра, реакция на урон.
5. player_death — 4 кадра (one-shot, hold last), падение.
Формат: полоса 64N×64 на прозрачном фоне (или сырой лист для слайсера),
NEAREST, персонаж идентичен idle/walk. NPC/звери — следующей пачкой (G3).
Доставка: zip → tools/sprites/intake_batch.py (без правок кода).

## Открытые хвосты

- PT2-007 (дизайн, владелец): баланс RawQi NPC (3 варианта, однострочные).
- PT2-008 (P3): честный тост при overflow-подборе.
- PT2-006: генерации спрайтов (владелец → приёмка агентом).
- Звериный fallback-уровень в подавлении (звери получают уровень игрока,
  никогда не подавляются) — осознанно НЕ тронут (олень за 3 каста — норма
  владельца); под вопрос PT2-007.
- НОВАЯ категория в чек-листе `pt1_pt2_fix_retest` — ретест за владельцем.
