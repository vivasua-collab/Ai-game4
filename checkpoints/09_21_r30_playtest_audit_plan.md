# R30 «Плейтест-фиксы + внешний аудит» (2026-09-21, вечер)

Контекст: R29 (VFX) закрыт и запушен (cb7b565). Пользователь провёл ЛОКАЛЬНЫЙ плейтест
(8 пунктов фидбека) и принёс внешний ИИ-аудит `upload/audit_09_21.txt`.
R30+ (мультибой-B, войны фракций) — ОТЛОЖЕНЫ по решению пользователя.

Верификация аудита (коды: ✅ подтверждено / ❌ опровергнуто / ⚠️ частично):

## Эпизод 1 — Аудит (коммит r30-1)

| # | Приоритет | Вердикт | Суть | Файл |
|---|-----------|---------|------|------|
| A1 | P1 | ❌ ОПРОВЕРГНУТ | «_lastAttackPotencyPermil протекает между участниками» — НЕТ: ExecuteAttack ставит глобальные поля из ТЕКУЩЕГО интента (:742) ДО мгновенного вызова (:789); pending/хоуминг/заряженный/AoE пути передают explicit. Утечки нет. Харденинг: мгновенный путь тоже передаёт explicit (страховка от будущих путей) | CombatService.cs |
| A2 | P1 | ✅ | CompleteUse (кулдаун+мастерство) ДО проверок эффекта: Healing «Тело не ранено», Combat «Цель исчезла», Formation отказы → техника потрачена впустую; рефанд есть ТОЛЬКО у OnAttackRejected | PlayerTechniqueCaster.cs:202 |
| A3 | P1 | ✅ | AoE из слотов 3–9: GetViewport().GetMousePosition() = ЭКРАННЫЕ коорд. (Z-путь использует GetGlobalMousePosition) → при движении камеры эпицентр AoE уезжает | GameWorldController.cs:1852 |
| A4 | P1 | ✅ | TryUseFromInventory списывает предмет ДО ApplyEffects; нереализованный эффект (teleport/vitality_boost/material) → предмет исчезает, эффекта нет, return true | ItemUseService.cs:124 |
| A5 | P1 | ✅ | CorpseService не ISaveable: сейв хранит смерть NPC (IsAlive=false), но труп+full-loot стираются ResetWorld при Load → лут теряется навсегда | CorpseService.cs |
| A6 | — | ✅ (заметка пользователя) | «Запретить жрать камни напрямую»: QiStoneData поглощается ПКМ-Использовать; оставить только зарядник (H) | ItemUseService.cs |
| B1 | P1 | ✅ | 6 stateful-сервисов НЕ IWorldResettable → New Game наследует бой/техники/слоты/баффы/зарядки/ауры прошлой сессии: CombatService, TechniqueService, TechniqueSlotService, BuffService, TechniqueChargeService, AuraHoldService | WorldDomainResetPhase |
| B2 | P1 | ✅ | TryDischarge: ExtractQi ДО проверки свободного места буфера → остаток теряется | ChargerService.cs:163 |
| B3 | P1/P0 | ✅ | Накопление: буфер получает Ци по СКОРОСТИ (без учёта остатка камней), DepleteStones снимает доли с клампом → Ци из воздуха при полупустых камнях | ChargerService.cs:306/369 |
| B4 | P1 | ✅ | NPC бьёт мёртвую цель вечно (AIState=Attacking, TargetId=труп); GetTargetPosition: неизвестный targetId = позиция ИГРОКА (фоллбек «любой не-NPC = игрок») | NPCAIService/NPCModule/NPCMovementService:348 |
| B5 | P2 | ✅ | Любая другая техника (3–9) выпускает удержанную ауру — спецификация §5.4: «повторное нажатие ТОЙ ЖЕ клавиши» | PlayerTechniqueCaster.cs:106 |
| B6 | P2 | ✅ латентный | Stack-ветка: Value += Potency (форс 1f) → 2 стака = 1.2 вместо 0.4; CreateBuffFromId ставит Refresh → не срабатывает сейчас | BuffService.cs:110 |

План фиксов эпизода 1:
1. A2: пре-валидация (Combat цель, Healing раны, Formation стадия) ДО CompleteUse;
   пост-отказы → RefundUse.
2. A3: GetGlobalMousePosition() в слот-пути.
3. A4: гейт реализуемости до списания (GetUseInfo); A6: камни → NotUsable
   («только через зарядник H»), ветка QiStone в ApplyEffects закрыта.
4. A5: CorpseService : ISaveable (StateType "corpses"; снапшот CorpseData+Items+seq;
   restore + CorpseCreatedEvent для рендера).
5. B1: IWorldResettable на 6 сервисах (wipe-семантика, LoadGame не задет — фаза
   SkipOnLoad=true).
6. B2: TryDischarge — кламп запроса по свободному месту буфера.
7. B3: extract-first: желаемое = min(скорость×dt, freeSpace); извлечение с клампом
   по остаткам камней + перераспределение дефицита; буфер получает подтверждённое.
8. B4: (a) ProcessNpcAttacks: цель-NPC мёртва → сброс TargetId+AIState;
   (b) EvaluateAndDecide: гейт живости NPC-цели до IsInCombat-return;
   (c) GetTargetPosition: не-NPC-не-игрок → позиция самого NPC (стоим), НЕ игрок.
9. B5: спуск удержанной ТОЛЬКО при совпадении techniqueId; иная клавиша — обычный путь.
10. B6: ActiveBuff.PerStackValue (время создания) → Value += PerStackValue.
11. A1-харденинг: мгновенный путь передаёт explicit potency/isRanged.

## Эпизод 2 — Плейтест (коммит r30-2)

| # | Суть | Где |
|---|------|-----|
| П1 | Формация почти не видна в пустынном биоме → контраст: тёмная подложка-обводка (outline) под светлыми линиями, адаптация к яркости биома | FormationVisualRenderer |
| П2 | Активная формация жрёт Ци на дистанции экрана → радиус зарядки: пауза потребления когда игрок вне радиуса (effectRadius+margin, min 8т), резюм при возврате; визуальное затухание в паузе | FormationService |
| П3 | «Светлая формация непонятно что делает» → детализация: тип действия, радиус, эффект, расход — в окно формации + тост завершения | FormationWindow/FormationService |
| П4 | K-окно: без паузы + полупрозрачный фон → (a) непрозрачный тёмный стильбокс; (b) схема-тумблер «Пауза при открытых окнах» (настройки+persist, default ON) | CultivationWindow/Settings |
| П5 | Медитация занимает слот хотбара → вывести из слотов 1–9: отдельная клавиша M (базовое действие, не техника слота) | PlayerInput/HotbarPanel |
| П6 | Подлагивания → FPS-счётчик: HUD-угол, F3 тумбл + persist | HUDPanel |
| П7 | Ошибка триангуляции DrawFill (indices.is_empty) → гейт вырожденных полигонов (<3 вершин, нулевая площадь/коллинеарность) | FormationVisualRenderer:305 |

Верификация эпизодов: build 0 err; COMBAT_SIM (headless) PASS; регрессия 18/18;
формации — FORMQA-скриншот цикл (Xvfb+opengl3+VLM) для П1/П3/П4/П6.

Состояние: [DONE] эпизод 1 (коммит 98fdc28) + эпизод 2 (см. git log) — все П1-П7
и A1-B6 закрыты; регрессия 18/18 PASS; формации — FormationShotSimDebug PASS
(headless + opengl3 + VLM). Скриншоты: game/screenshots/r30_form_*.png.
