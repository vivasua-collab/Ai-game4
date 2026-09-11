# АУДИТ: Adapter-слой (Scene / UI / Input / Di / Persistence)

**Дата:** 2026-09-11
**HEAD на старте:** `d0b065d`
**Режим:** READ-ONLY (код не изменялся), основной поток ИИ

**Скоуп:** Adapter целиком — карта (62 файла, ~22.5k строк):

- `Scene/` (34): GameWorldController 2515 (полностью), SceneBuilder 340
  (+BiomeTileRenderer), NPCSpriteRenderer 431, CorpseSpriteRenderer 101,
  StrikeFxRenderer 235, DamageNumberRenderer 189, WeaponVisualCatalog 157,
  GameBoot 135, ObjectLayerRenderer 387, GroundItemRenderer 270,
  ProjectileRenderer 181, ReAssemblySimDebug 175 (полностью); остальные
  рендереры + 19 *SimDebug — Grep-гейты/паттерны.
- `UI/` (23): InventoryWindow 1019, LootWindow 591, HotkeysWindow 251
  (полностью); DialogueWindow/ItemContextMenu(+SplitStackDialog)/
  CharacterDollPanel/TradeWindow/QuestWindow/EventLogWindow/CultivationWindow/
  MainMenuController/HotbarPanel/UIFactory/ParchmentTheme/CheatPanel/
  TestItemSeeder — Grep-выборка + целевые фрагменты.
- `Input/` (2): InputAdapter 201, InputMapInitializer 179 (полностью).
- `Di/` (1): ContainerAdapter 133 (полностью).
- `Persistence/` (2): GameSettings 88, SaveFileHandler 221 (полностью).
- Стыки: PlayerInputService (стики-флаги), PlayerService.MoveTo,
  BodyService.GetAllParts, InventoryService.GetCurrentWeight/TryAddItem,
  InventoryModule.OnEquipmentChanged, NpcDomainResetPhase (ResetCache).

---

## Сводка

- Прочитано полностью: 14 ключевых файлов; остальное — адресные Grep-проверки
  (подписки/диспозы, QueueRedraw-паттерны, замороженные Godot-запреты, гейты QA).
- **Находок: 10** (P2×2, P3×8). P1 нет — слой запускается и в сессии работает
  честно; оба P2 — UI-модальность (вложенные окна, конфликт цифр в диалоге).
- Главные подтверждённые регрессии-фиксы БЕЗ рецидива: D9 (QA-телепорт),
  R13 P1-1/P1-2 (LootWindow), D5 (цифры над зверями), R15 P2-2 (ResetCache),
  P1-4 ревью-1 (диспоз 16 токенов GWC в _ExitTree), HUD-легенда удалена без
  остатков (канон F1 актуален).

---

## Находки

### ADP-1 [P2] Вложенные модальные окна ломают единый флаг паузы → «залипание» паузы

**Файл:** `Scene/GameWorldController.cs:1572-1620, 1682-1702, 2179, 2232, 1993`;
`Input/InputAdapter.cs:99` («inventory»-стики не гейтится по `_isOverUI`).

**Сценарий:** диалог открыт (пауза, `_wasPausedBeforeInventory=false`). Игрок жмёт **B** — инвентарь открывается поверх диалога (гард у B только против LootWindow, GWC:1572), и строка 1583 перезаписывает общий флаг: `_wasPausedBeforeInventory = Time.IsPaused` = **true**. Закрытие инвентаря не резюмит (верно — диалог ещё открыт). Затем Esc закрывает диалог → `OnDialogueEnded` (GWC:1982): `!_wasPausedBeforeInventory` = false → **резюма нет — игра остаётся на паузе** (снимается повторным Esc, 1545-1550, но UX-деградация). Тот же механизм: C/T/F1/J/Q при открытом диалоге/лавке/обыске; E при открытом инвентаре (E не гейтится — открывает диалог/обыск, 1682-1702). R13-audit P2-2 закрыл только лут-гварды, семейство не закрыто.

**Доказательство:** трассировка кода; флаг — единственный на 10 окон, взаимного исключения модальностей нет.

**Фикс (рекомендация):** гварды «любое модальное окно открыто → B/C/T/F1/J/Q/E
игнорируются» (расширить условие R13 P2-2 с LootWindow на все окна) ИЛИ стек
пауз (каждое окно помнит свою причину паузы), ИЛИ паузу снимает только окно,
которое её ставило.

### ADP-2 [P2] Цифры 1-9 в диалоге: двойное действие (выбор ответа + каст техники/режим боя/пояс)

**Файл:** `UI/DialogueWindow.cs:320-348` (_Input, цифры-выбор, SetInputAsHandled);
`Input/InputAdapter.cs:138-160` (hotbar-цикл без `_isOverUI`-гейта);
`Scene/GameWorldController.cs:1631-1653` (1/2 — режим боя), `1656-1674`
(3-9 — каст слота), `1761-1777` (Shift+N — пояс) — все без модального гарда.

**Сценарий:** в диалоге с ≥2 вариантами игрок жмёт «2» → DialogueWindow выбирает вариант 2 (SetInputAsHandled) — НО polling `IsActionJustPressed` не уважает handled-марку: InputAdapter в тот же кадр добавляет `weapon_ranged`, GWC переключает режим боя и показывает тост поверх диалога. «3»-«9» → публикация TechniqueCastRequestedEvent прямо из диалога; Shift+цифра → BeltService.Use. В прочих окнах цифры тоже кастуют; в диалоге конфликт гарантирован (цифры — легитимный ввод).

**Доказательство:** InputAdapter.cs:116-122 гейтит по `_isOverUI` только attack/cast_technique/defend; hotbar-цикл (138-160) и GWC-ветки (1631-1777) без гейтов. `SetInputAsHandled` влияет только на `_UnhandledInput`-каскад.

**Фикс:** гейт hotbar-цикла в InputAdapter по `!_isOverUI` (симметрично
attack/cast/defend) или гейт `TechniqueSlotIndex/SelectedTechniqueSlot/
IsWeaponMeleePressed/IsWeaponRangedPressed`-веток в GWC по modalOpen.

### ADP-3 [P3] GWC не мигрирован на имена животных: killfeed-тост «Существо», атрибуция «?», нет стрелки направления

**Файл:** `Scene/GameWorldController.cs:1885` (OnNpcDied → `GetNPCState` →
fallback «Существо»), `1789-1791` (OnPlayerDamaged → «— ?» для зверя),
`1811-1812` (ShowDamageDirection → null для животного — стрелки нет).

**Сценарий:** игрок убивает волка → тост «☠ Существо повержен» (EventLogWindow
при этом пишет «Волк» — D6-фикс есть только там, EventLogWindow.cs:209-215
через IAnimalService.GetDisplayName). Укус волка → «💥 −12 HP — ?» и без
стрелки направления (источник вне экрана). ANIMALQA проверял killfeed-имя
только через EventLogWindow.Name — GWC-тост-путь пропущен.

**Фикс:** в OnNpcDied/OnPlayerDamaged/ShowDamageDirection использовать
`IAnimalService.GetDisplayName` фолбэком после NPCService (как EventLogWindow).

### ADP-4 [P3] CMB-14 (нет FX для животных) — подтверждён на Adapter-стороне, D5-фикс неполный

**Файл:** `Scene/StrikeFxRenderer.cs:204-218` (ResolveTargetPixelPos → null
для животных: «животные/неизвестные — без FX пока»); `Scene/GameWorldController.cs:2097`
(OnAttackIntentForSwing → GetNPC(e.TargetId) = null → нет замаха оружия).

**Сценарий:** удар по волку: цифра урона над зверем есть (DamageNumberRenderer
D5: TryGetAnimal, строки 173-177), но дуги-слэша/искр НЕТ (StrikeFx), замаха
оружия игрока НЕТ (GWC). Асимметрия фиксов D5 vs CMB-14.

**Фикс:** добавить IAnimalService в StrikeFxRenderer.ResolveTargetPixelPos и
GWC.OnAttackIntentForSwing (зеркально DamageNumberRenderer).

### ADP-5 [P3] Лог-спам ~30 строк/сек из _Draw трёх тайловых рендереров

**Файл:** `Scene/SceneBuilder.cs:317-318` (BiomeTileRenderer: GD.Print каждый
редроу), `Scene/SurfaceTransitionRenderer.cs:68`, `Scene/ObjectLayerRenderer.cs:64`.

**Сценарий:** GWC гонит `QueueRedrawAll` каждые 0.1с (GWC:1023-1028) → каждый
_Draw печатает «Drew N textures/sprites (culled to …)» → ~10 строк/сек на
рендерер, 30/сек суммарно — вечно, включая живую игру. Лог захламляется,
I/O-нагрузка в headless-прогонах (QA ищет VERDICT в этом же логе).

**Фикс:** демп-печать (раз в N сек / только при изменении счётчика) или
`OS.IsDebugBuild` + счётчик.

### ADP-6 [P3] Нарушения Zero-GC-паттерна (аллокации на кадр в _Draw / _PhysicsProcess)

**Файл:** `Scene/CorpseSpriteRenderer.cs:88-90` (string-интерполяция имени
трупа на каждый труп каждый физ-кадр 60 Гц), `Scene/NPCSpriteRenderer.cs:241-243`
(строка нейм-плейта на кадр на каждого раненого NPC), `Scene/GameWorldController.cs:1034,1055,1090-1092,1103-1105`
(5 Label.Text обновлений каждый физ-кадр).

**Сценарий:** паттерн проекта — «без аллокаций на кадр в _Draw» (комментарии
DamageNumberRenderer/StrikeFxRenderer/WeaponVisualCatalog). Пулевые рендереры
(StrikeFx/DamageNumber/TechniqueEffect) паттерн держат; Corpse/NPC-нейм-плейты
и HUD-лейблы — нет. Масштаб мал (десятки строк/сек), но постоянный.

**Фикс:** кэш строки в маркере трупа (перестраивать по CorpseLootedEvent);
текст лейблов — только при изменении значения (сравнение перед присвоением,
как DialogueWindow._Process:308-309).

### ADP-7 [P3] NPCSpriteRenderer: словари facing/lastX не чистятся при смерти NPC; static QA-счётчики StrikeFx не сбрасываются при пересборке

**Файл:** `Scene/NPCSpriteRenderer.cs:57-58` (`_npcFacingLeft`, `_npcLastX`
растут всю сессию — мёртвые npcId не удаляются; `_npcWeaponIds` чистится
пересканом 0.5с, `_npcSwings` — TTL ✓), `Scene/StrikeFxRenderer.cs:58-59`
(TotalSwipes/TotalStrikes — static, не сбрасываются; при повторной сборке в
одном процессе QA-числа включают прошлый мир).

**Фикс:** чистить facing/lastX в RescanNpcWeapons (там уже есть список живых);
сбрасывать счётчики в NpcDomainResetPhase (рядом с WeaponVisualCatalog.ResetCache).

### ADP-8 [P3] CultivationWindow (K): вне Esc-цепочки, без гварда модальности, мёртвое событие-тоггл

**Файл:** `Scene/GameWorldController.cs:1493-1567` (Esc-цепочка: hotkeys →
eventlog → quest → techniqueBook → cheat → trade → loot → dialogue → pause →
inventory — **K-окна нет**), `1624` (K без `_lootWindow`-гарда, в отличие от
T/F1/J/Q/B/C), `UI/CultivationWindow.cs:98` (подписка на
CultivationWindowToggleRequestedEvent — **паблишеров нет**, GWC тогглит
напрямую), GWC:928 (комментарий обещает «открывается через событие из
InputAdapter» — ложь).

**Сценарий:** K открыт → Esc → паузит игру (K не в цепочке), окно остаётся;
K при обыске — открывается поверх LootWindow (K не паузит — флаг не ломается,
но два перекрывающихся окна). Мёртвое событие + ложный комментарий — drift.

**Фикс:** добавить K в Esc-цепочку и гвард модальности; удалить событие или
мигрировать GWC на публикацию (единый путь).

### ADP-9 [P3] ПКМ-контекстное меню разрешает слот по индексу рендера (отступление от R10 SlotId-паттерна)

**Файл:** `UI/InventoryWindow.cs:512-517` (OpenContextMenu(int slotIndex) —
повторный `slots[slotIndex]` по индексу из времени рендера строки), при том что
строка-источник несёт стабильный `SlotIdForQA` (718). Плюс опечатка в UI:
`«ргкз:»` вместо «рюкзак» (676).

**Сценарий:** клик по строке после мутации инвентаря, не вызвавшей
RefreshItems (внешний ItemAddedEvent — InventoryWindow на него не подписана),
откроет меню ДРУГОЙ кучки. Основные пути обновляются (Toggle/pickup/harvest
зовут Refresh), остаточный TOCTOU узок.

**Фикс:** передавать в OpenContextMenu сам слот/SlotId из строки (как
DropSlotOnGround), исправить опечатку.

### ADP-10 [P3] Мёртвый код и недо-модальность мелочами

**Файл:** `UI/TestItemSeeder.cs` (363 строки, 0 вызовов — заменён
StartingGearPhase 2026-09-03, подтверждено Grep); `Modules/Player/PlayerInputService.cs:115`
(InputDisabled — никто не выставляет, Grep `InputDisabled\s*=` — 0 совпадений;
подтверждение PLR-аудита с Adapter-стороны); `Input/InputAdapter.cs:102`
(special_action/X — единственный из боевых стиков без `_isOverUI`-гейта:
цикл выбора техники работает поверх модальных окон, в отличие от Space/Z/G —
PLR-7 закрыт частично); GWC:2462 (комментарий «11 из _Ready + 1 lazy» —
фактически 15+1=16 токенов в DisposeSubscriptionTokens).

**Фикс:** удалить TestItemSeeder; гейт X по _isOverUI; поправить комментарии.

---

## Проверено чисто

- **D9-фикс (QA-телепорт):** `DEBUG_TeleportPlayer` (GWC:233-240) ставит
  `_visualPosition` + `Player.SetPosition` + сброс `_mouseTarget` — визуал
  больше не откатывает логику; аналогично респавн (GWC:1957-1961).
  `_PhysicsProcess`-синк: визуал — источник истины, тайл синкается по
  пересечению границы (GWC:1400-1411), клампы в границы карты ✓.
- **HUD-легенда удалена (09-11), регресса НЕТ:** комментарий-могила GWC:850-854;
  Grep по Adapter — HudHint-кода не осталось; канон F1 (HotkeysWindow)
  соответствует реестру InputMapInitializer 1:1, включая честные «не
  реализовано» (M/N/R/F5/F9) и F2-читы ✓.
- **Подписки EventBus — все в _Ready ПОСЛЕ ContainerAdapter.InjectProperties**
  (правило проекта): GWC, LootWindow, TradeWindow, TechniqueBookWindow,
  CultivationWindow, EventLogWindow, HotbarPanel, BeltSlotRow, BodyStatusPanel,
  NPCSpriteRenderer, StrikeFxRenderer, DamageNumberRenderer, GroundItemRenderer,
  ProjectileRenderer, TechniqueEffectRenderer ✓. Парный диспоз в _ExitTree у
  ВСЕХ (GWC: DisposeSubscriptionTokens — 16/16 токенов, P1-4 ревью-1 удержан;
  EventLogWindow 10/10; TradeWindow 5/5; LootWindow 2/2 …).
- **LootWindow (R13-фиксы без рецидива):** CurrentCorpseId (GWC:2023),
  Closed-единая точка резюма (GWC:2035-2039), CorpseRemovedEvent-закрытие
  (GWC:2020-2027), SlotId-адресное взятие + отказ «Предмет уже взят»
  (LootWindow:342-349), _ExitTree-диспоз ✓.
- **InputAdapter/маппинг:** программный InputMapInitializer (физические WASD/стрелки/PageUp/Down, логические буквы), полный реестр стиков (E/B/R/F/X/Esc/F5/F9/J/T/C/Q/M/N/V/Z/Space/G/K/F1/F2/hotbar_1-9); Shift-роутинг цифр без конфликтов (belt vs техника vs режим — взаимоисключающие ветки, InputAdapter:138-160); PLR-E06 — ResetFrameFlags в конце GWC._PhysicsProcess (GWC:1209) ✓.
- **Мышь:** _UnhandledInput для ЛКМ/зума (GWC:1219-1281) — UI-скроллы не зумят, modalOpen-гейт, клампы зума [1..8], средняя — сброс ×3, кламп клика в карту ✓.
- **Рендереры:** StrikeFx/DamageNumber — пулы структур + MaxConcurrent (32/48) + QueueRedraw только при активных ✓; D5-фикс цифр над зверями ✓; CorpseSpriteRenderer — саван/крест/бейдж/приглушение пустых ✓; ObjectLayer/BiomeTiles/SurfaceTransitions — viewport-culling ✓ (но лог-спам — ADP-5); GroundItemRenderer — event-driven Sprite2D ✓; NPC weapon overlay — морфология-фильтр (R15 P1-1), facing-гистерезис |dx|≥0.05, DrawSetTransform-зеркало x'=2cx−x (SPRITE_CATALOG §16) ✓.
- **WeaponVisualCatalog:** кэш `class|tier|rarity` + кламп тира 1-5,
  fallback-цепочка WeaponClassId → префикс ItemId → "sword", ResetCache
  вызывается NpcDomainResetPhase:35 (R15 P2-2 ✓), HandOffset-таблица (§1.3) ✓.
- **Adapter/Di:** Godot-ноды НЕ регистрируются в контейнере — тянут зависимости сами (ContainerAdapter.InjectProperties, partial-injection с warning); единственная Adapter-регистрация — SaveFileHandler (RegisterInstance, GameBoot:46-51) ✓.
- **Adapter/Persistence:** SaveFileHandler — санитизация путей (path-traversal), единые JsonOptions (R11 IncludeFields), честные try/catch, слоты user://saves ✓; GameSettings — идемпотентная загрузка, двойной гейт читов (#if DEBUG × CheatsEnabled) ✓.
- **QA-ноды:** 20 *SimDebug создаются в GWC._Ready ТОЛЬКО по GODOT_*-env (GWC:359-521) — живой игре не мешают; CheatPanel #if DEBUG ✓; ReAssemblySimDebug — честный harness-гейт ✓.
- **Замороженные Godot-запреты — не нарушены:** SetStylebox (не SetStyleBox; ParchmentTheme:67-78), TileMap не используется, SetAnchorsAndOffsetsPreset повсеместно, QueueRedraw вне _Draw, сигналы через `+=` ✓.
- **RespawnAfterDeath:** async void обёрнут try/catch (P0-фикс 2026-09-06 удержан) ✓. BodyService.GetAllParts — кэш (без per-frame аллокаций в GWC) ✓.

**Кросс-слойное замечание (не числится):** `InventoryModule.OnEquipmentChanged:135`
— хардкод `e.EntityId != "player"` вместо PlayerIdResolver; сейчас безопасно
(EquipmentService.Initialize("player") — единственный паблишер), но хрупко при
появлении «player_0»-паблишеров. Сам INV-2 (потеря при volume-full) —
реестр InventoryModule-аудита (INV-2 [P1]), Adapter-сторона подтверждает:
комментарий CharacterDollPanel.cs:281-282 («TryAddItem drops surplus on the
ground») — ложный, HandleUnequip (272-287) оверфлоу не проверяет.

---

## Соответствие docs_v2

- **HOTKEYS.md / F1-канон:** HotkeysWindow = канонический перечень
  InputMapInitializer; «В разработке / отключено» честно отражает M/N/R/F5-F9
  (легенда HUD удалена — регресса нет) ✓. Расхождение: X в F1 описан без
  оговорки «работает поверх окон» (ADP-10).
- **UI_DESIGN.md §4.2 (per-mille):** UIFactory — PpmToSize/PpmToPx на
  1920×1080, integer math ✓.
- **MOUSE_INPUT_SCHEME.md:** _UnhandledInput-паттерн, модальность, клампы
  зума — соответствуют (GWC:1212-1281) ✓.
- **SPRITE_CATALOG (§1.3 HandOffset, §16 зеркалирование, §19 fallback):**
  WeaponVisualCatalog/NPCSpriteRenderer/GWC-комозит — соответствуют ✓.
  K-замечание: зеркалирование «только X» выдержано везде.
- **TESTING_RULES §0.1 (33 хука):** все SimDebug-гейты env-условные,
  «новая фича = новый хук» выдержан (ANIMALQA/COMBATAI/STRIKEFX_HOLD в
  реестре и в коде) ✓. Замечание: static-счётчики StrikeFx не сбрасываются
  при пересборке (ADP-7) — влияет только на QA-числа.

---

## Журнал

READ-ONLY: код не изменялся; верификация — чтение + Grep-трассировка
(подписчики/паблишеры событий, вызыватели ResetCache/InputDisabled/
TestItemSeeder, SetStyleBox/TileMap/Connect-запреты). Приоритет фиксов:
ADP-1 → ADP-2 → ADP-3/ADP-4 (одним животным-эпизодом) → ADP-5/6 → остальное.
