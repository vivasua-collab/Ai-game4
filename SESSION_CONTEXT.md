# SESSION_CONTEXT — Передача контекста агенту

**Дата создания:** 2026-08-22
**Обновлено:** 2026-09-10 10:20 UTC (R15 внедрена: оружие в руках игрока+NPC; QA 11/11 PASS)
**Назначение:** Полный контекст для продолжения разработки
**Инструкция:** Прочитай START_PROMPT.md ПЕРВЫМ, затем этот файл.

---

## 0. ЧТО СДЕЛАЛОСЬ ЗА СЕССИИ 2026-09-08…10 (коммиты 3e41927…HEAD)

### Сессия 2026-09-10 №4: R15 — «оружие в руках» (фазы A+B внедрены)
1. **План одобрен пользователем** (дефолтные ответы §6; статичные
   спрайты; PNG-ассеты — отдельный будущий план; анимация — фаза C).
2. **WeaponClassId** в EquipmentData + EquipmentGenerator пишет
   (7 классов); fallback: парсинг ItemId `eq_wep_*` → generic sword.
3. **Спрайты:** ProceduralSpriteGenerator.CreateWeaponIcon (32×32) +
   CreateWeaponHandSprite (48×48) — 7 рецептов × 5 тиров материала ×
   редкость; Legendary+ — золотая обводка (ApplyGoldenOutline).
4. **WeaponVisualCatalog** (Adapter/Scene): кэш (class|tier|rarity) →
   (icon+hand+key), Resolve с fallback, HandOffset по классам
   (1H (+14,-6); 2H (+2,-2); лук (-2,+4)).
5. **Композит игрока:** GameWorldController MainHand Sprite2D +
   EquipmentChangedEvent + страховка 0.5с; facing-wiring (клавиши +
   мышь) → FlipH + offset-зеркалирование ТОЛЬКО по X.
6. **Хотбар 1-2:** иконки 32×32 + короткая подпись; **кукла:** иконки
   в DollSlotRow (WeaponMain/Off).
7. **NPC:** NPCSpriteRenderer overlay + кэш npcId→itemId (перескан
   0.5с) + facing-гистерезис + DrawSetTransform-зеркалирование.
8. QA: GODOT_WEAPONVIS_DEBUG=1 PASS; 10 регрессий PASS; build 0 err;
   Xvfb-скриншот + VLM: кинжал в руке игрока и иконка в хотбаре видны.
9. docs_v2: SPRITE_CATALOG §7/§18/§20/§21, MODULE_STRUCTURE §2.7/§4,
   TESTING_RULES §0.1 (31 хук). Чекпоинт: 09_10_r15_weapon_visuals.md.

### Сессия 2026-09-10 №3: R14 — правило восполнения населения (коммит c07427c)
1. **§2.4 DEATH_AND_LOOT исправлен по запросу пользователя:** пока
   персонаж в локации, новые NPC НЕ генерируются (ReinforcementTick
   удалён из NPCModule.Tick); выбитое население остаётся выбитым.
2. **Ивент-точка входа:** TrySpawnEventNpc(Caravan/Raid/Event) —
   единственный внутрисессионный источник NPC (будущий event-pipeline/
   GROUP_SYSTEM: караваны/набеги). QA: EventSpawnCount.
3. **Естественное восстановление** — при (пере)сборке локации
   (GenerateStartup; «таймер памяти» TRANSITION_SYSTEM §5.3: NPC —
   1 игровой день после ухода; с будущим travel-pipeline).
4. docs_v2 sync: DEATH_AND_LOOT §2.4, MODULE_STRUCTURE §2.7,
   TESTING_RULES §0.1. QA 9/9 PASS (LOOT step7 перевёрнут: население
   ниже floor НЕ восполняется; ивент-спаун работает).

### Сессия 2026-09-10 №2: верификация R13 + синхронизация docs_v2
1. **R13 full-loot верифицирован** по правилам docs_v2 (запрос пользователя):
   build 0 errors; QA 9/9 хуков PASS (LOOT/COMBAT/SAVELOAD/CONTEXT/
   REASSEMBLY/KILLFEED/TRASHDROP/STORAGE/QUEST).
2. **docs_v2 синхронизированы с R13** (workflow §3.3: расхождение код↔доки = баг):
   DEATH_AND_LOOT.md (трупы-контейнеры вместо случайного дропа, таблица
   событий с CorpseContracts, TTL, ReinforcementTick, камни 4.1);
   MODULE_STRUCTURE.md §1/§2.7 (NPCSpawnCompositionService, CorpseService,
   ICorpseService, CorpseContracts в таблицах); DI_AND_EVENTBUS.md
   (CorpseContracts: 3 события); TESTING_RULES.md §0.1 (реестр хуков
   25→30: +LOOT/SAVELOAD/CONTEXT_HOLD/SCREENSHOT_MENU/SCREENSHOT_SPLIT).
3. Ответ на вопрос пользователя о правилах чекпоинтов: правила живут в
   START_PROMPT.md §6 (по дизайну — START_PROMPT читается первым на каждой
   сессии); отдельного файла правил в checkpoints/ никогда не было
   (подтверждено git-историей всех имён файлов).

### Сессия 2026-09-10 №1: R13 FULL-LOOT (коммиты 5f7e7e7 + 7347bc1)
1. **NPC спаун через генерацию:** NPCSpawnCompositionService — состав
   населения локации от (тип локации + DangerLevel + сид), детерминизм,
   кап 12; ReinforcementTick (45 игр. сек) восполняет потери <60%.
   Хардкод-массив ролей в HumanNPCSpawnPhase удалён.
2. **Доступ к инвентарю мёртвого NPC:** CorpseService + CorpseData +
   CorpseContracts + ICorpseService — смерть → труп-контейнер (снапшот
   экипировки + инвентаря + камней по таблице 4.1), TTL 1 игровой день,
   SlotId-адресность (паттерн R10), защита от фантомных ID (P1-5).
   Старый «случайный дроп 1-2 предмета» удалён.
3. **Full loot:** LootWindow (E — обыск, ЛКМ — по одному, «Забрать всё»,
   Esc/CorpseRemovedEvent — авторитетное закрытие, пауза тиков) +
   CorpseSpriteRenderer (саван/крест/лут-бейдж) + EventLog-подсказки.
4. Фикс: ClassicLootSeeder регистрирует 4 «фантомных» ID материалов
   (material_iron_scrap и др.) в ItemDatabase.
5. QA: GODOT_LOOT_DEBUG=1 → PASS; 8 регрессий PASS; build 0 errors.

### Сессия 2026-09-09: R10 + R11 (коммиты 756fd79, 1fd5ba9)
1. **R10:** SlotId-адресность инвентаря (Guid на кучку) — TOCTOU-защита:
   stale drop/split отклоняются при мутации в полёте. ПКМ-меню
   (свойства, разделение стака слайдером, слот-адресный выброс).
2. **R11:** Save/Load persistence — типизированный round-trip
   (IncludeFields, честный success/error), World biome/danger в сейве.
   SaveLoadSimDebug v2 с анти-тривиальными мутациями и integrity.

### Сессия 2026-09-08: внешнее ревью этапы 1-7 (коммиты 3e41927…80f4f25)
34 находки внешнего ревью: combat authority (turn-gate, фантомный enemy,
ammo-транзакция), inventory transactions (spirit retrieve/stacking, ring Qi,
craft overflow, pickup integrity), qi transactions + DoT, world/tile
(respawn, честный travel, time delta), quests (semantic targets, real
rewards, dialogue-quest link, gates). QA 17/17 PASS. Доки 1v1.

---

## 1. БЫСТРЫЙ СТАРТ

### Проект
- **Название:** Cultivation World Simulator (Ai-game4)
- **Жанр:** Xianxia cultivation life-sim (Kenshi + RimWorld + cultivation)
- **Движок:** Godot 4.7.1 .NET (C#, net8.0)
- **Репозиторий:** https://github.com/vivasua-collab/Ai-game4 (публичный)

### Окружение (после ночного сброса песочницы)
```bash
# Полное восстановление одной командой (idempotent, ~1 мин):
bash /home/z/my-project/Ai-game4/recover_sandbox.sh
# или вручную:
bash /home/z/my-project/automation/restore_env.sh
```

**Проверенное окружение 2026-09-10:**
- .NET SDK 9.0.318 → /home/z/.dotnet (DOTNET_ROOT)
- Godot 4.7.1 mono → **/home/z/godot_flat/godot** (flat-структура! не старый путь)
- Репо: /home/z/my-project/Ai-game4 (HEAD == origin/main)
- Токен: /home/z/my-project/.auth/github.token (вне git, переживает сбросы)
- Симлинк: /home/z/my-project/Ai-game4-repo → Ai-game4 (в git окружения)

### Сборка и QA
```bash
export DOTNET_ROOT=/home/z/.dotnet && export PATH="$DOTNET_ROOT:$PATH"
cd /home/z/my-project/Ai-game4/game && dotnet build        # 0 errors (348 warnings — базовый уровень)

GODOT='/home/z/godot_flat/godot'
GODOT_NEWGAME=1 timeout 90 "$GODOT" --headless --path "$PWD" scenes/MainMenu.tscn   # полный флоу
env GODOT_NEWGAME=1 GODOT_LOOT_DEBUG=1 timeout 90 "$GODOT" --headless --path "$PWD" scenes/MainMenu.tscn    # R13 full-loot
env GODOT_NEWGAME=1 GODOT_COMBAT_SIM=1 timeout 90 "$GODOT" --headless --path "$PWD" scenes/MainMenu.tscn     # бой
env GODOT_NEWGAME=1 GODOT_SAVELOAD_DEBUG=1 timeout 90 "$GODOT" --headless --path "$PWD" scenes/MainMenu.tscn # сейвы
# Полный реестр 30 хуков: docs/docs_v2/09_workflow/TESTING_RULES.md §0.1
```

ВНИМАНИЕ:
- Хуки запускать поштучно (не в bash-цикле с общим пайпом) — некоторые
  сцены держат процесс; вердикт искать grep 'VERDICT'.
- `--import` — только абсолютный путь; сообщения GodotSharp при import
  нефатальны. После клона импорт мог НЕ выполняться (.ctex отсутствует,
  ERROR в логе) — headless QA работает, скриншоты требуют Xvfb+opengl3.
- Push: `git push origin main` через credential store; токен в
  /home/z/my-project/.auth/github.token. НЕ запрашивать у пользователя,
  если файл есть. Пушить только с полным QA-прогоном.

---

## 2. ТЕКУЩЕЕ СОСТОЯНИЕ (R15 внедрена — см. git log origin/main)

### Что работает
- ✅ Main Menu → New Game (50×50) / Large World (500×500)
- ✅ Мир: 9 биомов, переходы, объекты, harvest (F), ground items (E),
      перевес, процедурные спрайты
- ✅ Инвентарь (B) + кукла (11 слотов) + пояс (3-9) + ПКМ-контекстное
      меню (свойства/разделение стака/выброс, SlotId-адресно — R10)
- ✅ Бой: Space-атака, урон по частям тела, HP-бары, цифры урона,
      kill-feed, стрелка атакующего, ranged+LOS+ammo, turn-gate,
      смерть→респавн
- ✅ **Full loot (R13) + правило населения (R14):** спаун NPC через
      генерацию состава, трупы-контейнеры (снапшот экипировки/инвентаря/
      камней), обыск E, «Забрать всё», TTL трупов; восполнение населения
      — ТОЛЬКО вне сессии (R14), ивенты (караван/набег) — через
      TrySpawnEventNpc
- ✅ **Оружие в руках (R15):** hand-спрайты на теле игрока и NPC
      (7 классов × 5 тиров, Legendary-обводка), иконки в хотбаре 1-2 и
      кукле, facing-зеркалирование игрока и NPC
- ✅ Save/Load (R11): типизированный round-trip, 8 ISaveable-блоков,
      IncludeFields, честные события
- ✅ Торговля: диалог → лавка → buy/sell за духовные камни
- ✅ Qi: уровни/прорывы, техники (T, Z/X), формации, камни Ци (RMB),
      медитация (V), зарядники
- ✅ Генераторы: «Матрёшка» + легендарки (промо 20%/оверкап 18%) +
      верификация/дедуп
- ✅ Квесты: полный цикл через реальный диалог, гейты, награды

### НЕ проверено живьём (в редакторе на ПК)
- ⚠ Визуал R13: маркеры трупов/LootWindow в реальном рендере
      (headless не рисует; нужен Xvfb-скриншот или ПК)
- ⚠ Визуал R15: позиционирование/размеры оружия у NPC (композит игрока
      подтверждён VLM-скриншотом; NPC — только QA-ключи, камера не видела
      NPC в кадре) — вечером у пользователя
- ⚡ Баланс: камни в трупах по таблице 4.1 могут быть щедрыми

### Что НЕ работает (отложено)
- ❌ Faction system (порт из Ai-game3-ref)
- ❌ Лут с животных: материалы (шкура/мясо) TODO — звери дают только камни
- ❌ Per-attacker pending technique
- ❌ Обыск трупов ИИ-NPC (только игрок)
- ❌ Генерация состава для large_world (500×500) — не проверено

---

## 3. АРХИТЕКТУРА (кратко — детали в START_PROMPT.md / MODULE_STRUCTURE.md)

```
game/src/
├── Core/            ← engine-agnostic: Data, Interfaces (45+), Messaging/Contracts (~133)
├── Modules/         ← 17 модулей: ... CorpseService в NPC, Trade, Generator ...
├── Entry/           ← GameSession, 16 фаз, GameLifetimeScope
└── Adapter/         ← Godot: Scene (рендереры, *SimDebug QA), UI (окна), Input, DI
```

- **DI-порядок** (GameLifetimeScope): World → Tile → Body → Qi → Buff →
  Inventory → Combat → Formation → NPC → Player → Quest → Interaction →
  Trade → UI → Charger → Save → Generator
- **Игрок под двумя ID**: "player" (Equipment/Body) и "player_0"
  (PlayerService/Combat/NPC AI) — IsPlayerEntityId нормализует
- **Время:** 1 тик = 1 игроминута = 1 сек на Normal
- **Подписки Godot-нод на EventBus — в _Ready ПОСЛЕ DI-инъекции**

---

## 4. ЗАМОРОЖЕННЫЕ РЕШЕНИЯ (НЕ нарушать)

1. Godot 4.7.1 .NET — единственный движок; чистый 2D
2. Qi = long (не float); integer math (Permil) для боя
3. config/name без пробелов ("CultivationGame")
4. Input actions программно
5. **Документация первична** — редактирование docs_v2 только с
   обоснованием и синхронизацией (workflow §3.3), без «тайных» знаний
6. Custom _Draw рендеринг; Element.Poison остаётся
7. Save/load включён (R11 закрыл Q8)
8. **НЕ запускать Next.js DEV сервер** (песочница — не для игры;
   сейчас он остановлен по запросу пользователя от 09-10)

---

## 5. СЛЕДУЮЩИЕ ШАГИ (приоритет)

### P0 — живая проверка в редакторе (ПК)
1. Визуал R13 + правило R14: маркеры трупов, LootWindow, «Забрать всё",
   НЕ-восполнение населения после зачистки (вечером у пользователя)
2. Визуал R15: оружие в руке игрока/на разных NPC (кинжал/меч/копьё/
   двуручник/лук/посох), хотбар-иконки, зеркалирование при движении
   влево/вправо; вылеты за тайл 2H-оружия
3. Баланс камней в трупах (таблица 4.1 vs ощущение)

### P1 — кандидаты следующей сессии
4. R15 фаза C: замах-анимация (sprite-swap §17), off-hand щит, иконки
   в лут-окне трупов / ground items
5. PNG-ассеты: генерация и привязка к объектам (отдельный план
   пользователя; сейчас процедурные рецепты)
6. Броня-слои на теле (R16-кандидат — та же архитектура R15)
7. Лут с животных: материалы по видам (шкура/клыки/мясо) в CorpseService
8. Обыск трупов ИИ-NPC (приоритет лута для AI)
9. Faction port (Ai-game3-ref, ~400 LOC)
10. Генерация состава для 500×500 (мульти-локации)

### P2 — долг
7. Per-attacker pending technique
8. Tooltip-система (UI_DESIGN)
9. Классические dotnet test-наборы (TESTING_RULES §1-§10 — плановая работа)

---

## 6. КОМАНДЫ ПРОВЕРКИ (полный реестр — TESTING_RULES.md §0.1)

```bash
export DOTNET_ROOT=/home/z/.dotnet && export PATH="$DOTNET_ROOT:$PATH"
cd /home/z/my-project/Ai-game4/game
GODOT='/home/z/godot_flat/godot'

dotnet build                                                     # 0 errors

# Ключевые хуки (полный список 31 шт — TESTING_RULES §0.1):
env GODOT_NEWGAME=1 timeout 90 "$GODOT" --headless --path "$PWD" scenes/MainMenu.tscn
env GODOT_NEWGAME=1 GODOT_LOOT_DEBUG=1 timeout 90 "$GODOT" --headless --path "$PWD" scenes/MainMenu.tscn
env GODOT_NEWGAME=1 GODOT_WEAPONVIS_DEBUG=1 timeout 90 "$GODOT" --headless --path "$PWD" scenes/MainMenu.tscn
env GODOT_NEWGAME=1 GODOT_COMBAT_SIM=1 timeout 90 "$GODOT" --headless --path "$PWD" scenes/MainMenu.tscn
env GODOT_NEWGAME=1 GODOT_SAVELOAD_DEBUG=1 timeout 90 "$GODOT" --headless --path "$PWD" scenes/MainMenu.tscn

# Скриншот (Xvfb + софтверный GL + VLM):
Xvfb :99 -screen 0 1920x1080x24 > /dev/null 2>&1 & sleep 2
DISPLAY=:99 LIBGL_ALWAYS_SOFTWARE=1 GODOT_NEWGAME=1 \
  GODOT_SCREENSHOT=/tmp/shot.png GODOT_SCREENSHOT_DELAY=4.3 timeout 60 "$GODOT" \
  --path "$PWD" --rendering-driver opengl3 scenes/MainMenu.tscn
```

---

## 7. КЛЮЧЕВЫЕ ФАЙЛЫ

| Файл | Назначение |
|------|------------|
| `SESSION_CONTEXT.md` | ЭТОТ ФАЙЛ |
| `SESSION_SUMMARY.md` | Сводка сессий + состояние |
| `checkpoints/09_10_r14_population_rule.md` | Чекпоинт R14 (правило населения) |
| `checkpoints/09_10_r15_weapon_visuals.md` | Чекпоинт R15 (оружие в руках) |
| `checkpoints/plans/2026-09-10_r15_weapon_visualization_plan.md` | R15 план (фазы A+B реализованы) |
| `checkpoints/09_10_r13_full_loot.md` | Чекпоинт R13 (фичи) |
| `docs/docs_v2/04_entities/DEATH_AND_LOOT.md` | Спецификация смерти/лута (§2.4 R14) |
| `docs/docs_v2/07_ui/SPRITE_CATALOG.md` | Спрайты: §7 equipped-оружие (R15) |
| `docs/docs_v2/09_workflow/TESTING_RULES.md` | Реестр 31 QA-хуков |
| `game/src/Adapter/Scene/WeaponVisualCatalog.cs` | R15: кэш/резолв спрайтов оружия |
| `game/src/Adapter/Scene/WeaponVisSimDebug.cs` | QA оружия (GODOT_WEAPONVIS_DEBUG) |
| `game/src/Modules/NPC/CorpseService.cs` | Трупы-контейнеры |
| `game/src/Modules/NPC/NPCSpawnCompositionService.cs` | Генерация населения + TrySpawnEventNpc |
| `game/src/Adapter/UI/LootWindow.cs` | Окно обыска |
| `game/src/Adapter/Scene/LootSimDebug.cs` | QA full-loot + R14 |

---

## 8. ПРЕДУПРЕЖДЕНИЯ

1. Next.js DEV сервер НЕ запускать (сейчас остановлен по запросу)
2. `--import` — ТОЛЬКО абсолютный путь; после клона .ctex может
   отсутствовать (headless QA работает без него)
3. QA-хуки запускать поштучно с таймаутом ≥90 сек
4. BiomeTiles/спрайты логируют каждый кадр — спам в лог
5. Токен GitHub: /home/z/my-project/.auth/github.token (вне git).
   НЕ запрашивать у пользователя, если файл есть. Репозиторий публичный
   (pull без токена работает).
6. **Сброс песочницы:** Godot/dotnet могут пропасть →
   `bash /home/z/my-project/Ai-game4/recover_sandbox.sh`. Локальный git
   может отставать от GitHub → сверять `git ls-remote origin` → fetch.
   После сброса Godot живёт в **/home/z/godot_flat/godot** (flat).
7. Документация редактируется только с обоснованием и коммитом-ссылкой
   на причину (workflow §3.3) — прецедент: синхронизация R13 2026-09-10.
8. Правила чекпоинтов — в START_PROMPT.md §6 (не отдельный файл в
   checkpoints/; подтверждено git-историей 2026-09-10).

*Конец файла. Полная хроника — worklog.md (репо) + checkpoints/.*
