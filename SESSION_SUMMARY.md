# Сводка сессий (обновляется при завершении каждой сессии)

Обновлено: 2026-08-25 15:10 UTC (облачный агент Z.ai Code)

## Проект
Cultivation World Simulator (Ai-game4), Godot 4.7.1 .NET, C#
Репозиторий: https://github.com/vivasua-collab/Ai-game4

---

## Последние сессии

### 2026-08-25 (облачный агент — Phase 7+8+4-5, P0-фиксы)

#### P0-баги найдены и исправлены (бой теперь реально работает!)
- **Урон NPC→игрок не применялся**: BodyService сравнивал TargetId=="player",
  а NPC AI атакует "player_0" → IsPlayerEntityId() нормализует оба ID
- **Время 60× медленнее**: WorldService.DeltaTime 1/60 → 1.0/тик
  (TIME_SYSTEM.md: 1 тик = 1 сек на Normal); QiRegen SECONDS_PER_DAY
  86400→1440 (регенерация теперь точно 10%/сутки)
- CombatSimDebug (GODOT_COMBAT_SIM=1): headless-верификация боя в обе
  стороны + weapon end-to-end — VERDICT: PASS

#### Phase 8: wiring экипировки (5 TODO закрыты)
- EquipmentDataProvider резолвит ID→EquipmentData через IItemDatabaseService;
  агрегаты dodge/block/parry/crit/penetration (промилле)
- EquipmentService пушит экипировку игрока в провайдер (оба player-ID)
- CombatService: броня влияет на уклонение, щит/оружие на блок/парирование,
  крит от экипировки, пробитие оружия; базовая атака с оружием = урон оружия
  (раньше кулак 10 всегда)

#### Phase 7: Combat Visuals
- DamageNumberRenderer: всплывающие «−N»/«КРИТ −N»/слова промахов
  (пул, _Draw, подписка в _Ready после DI)
- HP-бар над раненым NPC (зел/жёлт/красн)
- Визуально верифицировано скриншотами (Xvfb+opengl3+VLM)

#### Phase 4-5: Торговля (17-й модуль Trade)
- CurrencyService (первая реализация ICurrencyService, 50 камней старт)
- TradeService: ассортимент от seed(npcId) — «Матрёшка» L1-3 + расходники;
  Permil-цены 1200‰ покупка / 500‰ продажа
- TradeWindow «Лавка торговца»: Товары/Инвентарь, ЛКМ=1/Shift=5, пауза тиков
- Диалог торговца: «Покажи товары» → open_trade → лавка
- GODOT_TRADE_DEBUG=1 smoke-тест buy/sell; GODOT_TRADE_HOLD=1 скриншоты

#### Холодный старт улучшен
- `godot --headless --import --path <абсолютный путь>` генерирует .ctex —
  биом-текстуры рендерятся в облаке (раньше «нужен редактор»)
- Коммиты: 679f19e, f02d61d, 8a5001b

### 2026-08-23 (Qi stages 1-8)
- Техники: слоты §12, TechniquesPanel (T), каст-пайплайн по типам, Z/X
- Формации: генератор «Матрёшка» (8×5×level×element×shape), lifecycle
  Drawing→Filling→Active→Depleted, FormationVisualRenderer
- Камни Ци (10 видов, RMB), чит-меню F1 (9 действий), Qi HUD, V-медитация

### 2026-08-22 (локальный ZCode — играбельный прототип)
- Диспозиционный ИИ, NPC-атаки, экипировка NPC, лут, HP-бар, респавн
- Генератор «Матрёшка», слоты пояса (3-9), P0 NPC_COMBAT_PREP Phase 1/2/6
- Playtest-фиксы: скорость, зум, дублирование экипировки, диалог-resume

---

## Текущее состояние игры

### Что работает
- ✅ Main Menu → New Game 50×50 / Large 500×500 (+GODOT_NEWGAME=1)
- ✅ Мир: 9 биомов, текстуры рендерятся (импорт в облаке работает), объекты,
  harvest (F), ground items (E), перевес
- ✅ Инвентарь (B), кукла 11 слотов, Character Sheet (C), слоты пояса (3-9)
- ✅ **Бой РАБОТАЕТ (P0-фиксы)**: Space атака, урон NPC→игрок по телу,
      HP-бар игрока, тосты, смерть→респавн, лут с трупов
- ✅ **Боевые статы экипировки**: урон оружия, пробитие, штраф уклонения
      брони, блок/парирование/крит бонусы
- ✅ **Combat Visuals**: цифры урона над целями, HP-бары над NPC
- ✅ **Торговля**: диалог → лавка → покупка/продажа за духовные камни
- ✅ NPC: 6 на локации, диспозиции, диалоги (E), атаки, лут
- ✅ Qi: культивация, техники (T, Z/X), формации, камни Ци, медитация (V),
      чит-меню F1
- ✅ Скорость PageUp/Down; EventBus re-entrancy; детерминированный RNG

### Headless-верификация (облако)
- GODOT_COMBAT_SIM=1 → VERDICT: PASS (урон в обе стороны + оружие end-to-end)
- GODOT_TRADE_DEBUG=1 → buy/sell smoke OK
- GODOT_SCREENSHOT(+_DELAY) → скриншоты (см. docs/screenshots/)

### Что НЕ работает (отложено)
- ❌ Phase 3: Faction system (порт из Ai-game3-ref ~400 LOC)
- ❌ Phase 8 остаток: isRanged→CombatSubtype; ammo/луки (Phase 8 ч.2)
- ❌ Phase 9: метательное + двойное оружие
- ❌ Tooltip/ContextMenu (UI_DESIGN #21-22)
- ❌ Живая проверка в редакторе: страж-союзник, пояс end-to-end
- ❌ Save/load (отключён — Q8); TileMapLayer (Q11)

---

## Архитектура

### 3-layer + 17 модулей (новый Trade после Interaction, до UI)
World → Tile → Body → Qi → Buff → Inventory → Combat → Formation → NPC →
Player → Quest → Interaction → **Trade** → UI → Charger → Save → Generator

### Key patterns
- Hub-and-Spoke (EventBus, readonly struct контракты)
- DI: greediest ctor + [Inject]; Qi=long; Permil integer math
- Godot-ноды: подписки на EventBus ТОЛЬКО в _Ready (после DI-инъекции!)
- Игрок существует под двумя ID: "player" (Inventory/Body) и "player_0"
  (Combat/NPC AI) — нормализуется IsPlayerEntityId / dual-check

---

## Замороженные решения (НЕ нарушать)

- Godot 4.7.1 .NET — единственный движок; чистый 2D
- Qi = long; integer math (Permil) для боя
- config/name без пробелов; input actions программно
- **Документация первична** — НЕ редактировать без указания пользователя
- Custom _Draw рендеринг; Element.Poison остаётся; save/load отключён
- НЕ запускать Next.js DEV сервер (песочница — не для игры)

---

## Следующие шаги

1. P0: живая проверка в редакторе (страж, пояс, лут)
2. Phase 3: Faction port (Ai-game3-ref)
3. Phase 8 остаток: isRanged, ammo/луки
4. Per-attacker pending technique (NPC может ударить себя при смене цели)
5. Tooltip/ContextMenu UI

---

## Команды проверки

```bash
export DOTNET_ROOT=/home/z/.dotnet && export PATH="$DOTNET_ROOT:$PATH"
cd /home/z/my-project/aigame4/game && dotnet build

GODOT='/home/z/godot/Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64'
GODOT_NEWGAME=1 timeout 25 "$GODOT" --headless --path "$PWD" scenes/MainMenu.tscn
GODOT_NEWGAME=1 GODOT_COMBAT_SIM=1 timeout 25 "$GODOT" --headless --path "$PWD" scenes/MainMenu.tscn
GODOT_NEWGAME=1 GODOT_TRADE_DEBUG=1 timeout 25 "$GODOT" --headless --path "$PWD" scenes/MainMenu.tscn

# Импорт ресурсов (после клона; АБСОЛЮТНЫЙ путь!):
timeout 240 "$GODOT" --headless --import --path /home/z/my-project/Ai-game4/game

# Скриншот (Xvfb + софтверный GL):
Xvfb :99 -screen 0 1920x1080x24 & sleep 2
DISPLAY=:99 LIBGL_ALWAYS_SOFTWARE=1 GODOT_NEWGAME=1 GODOT_COMBAT_SIM=1 \
  GODOT_SCREENSHOT=/tmp/shot.png GODOT_SCREENSHOT_DELAY=4.3 timeout 60 "$GODOT" \
  --path "$PWD" --rendering-driver opengl3 scenes/MainMenu.tscn
```

---

## Предупреждения

- SDK 8.0.424 (8.0.30 EOL); GodotSharp editor plugin пишет «.NET Sdk not found
  8.0.30» + Microsoft.Build — НЕ фатально для import/runtime (dotnet build внешне)
- `--import` с относительным `--path .` из чужого cwd ловит не тот проект
- worklog.md (сессионный) может теряться — ключевые решения в checkpoints/
- BiomeTiles логирует каждый кадр — спам
