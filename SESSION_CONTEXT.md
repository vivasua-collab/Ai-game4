# SESSION_CONTEXT — Передача контекста агенту

**Дата создания:** 2026-08-22
**Обновлено:** 2026-08-25 15:15 UTC (облачная сессия: Phase 7+8+4-5, P0-фиксы)
**Назначение:** Полный контекст для продолжения разработки
**Инструкция:** Прочитай этот файл ПЕРВЫМ при старте новой сессии

---

## 0. ЧТО СДЕЛАЛОСЬ ЗА ОБЛАЧНУЮ СЕССИЮ 2026-08-25 (3 коммита: 679f19e, f02d61d, 8a5001b)

### P0-баги найдены и исправлены — бой теперь РЕАЛЬНО работает
1. **Урон NPC→игрок не применялся ВООБЩЕ**: BodyService сравнивал
   `e.TargetId == _entityId` ("player"), а NPC AI атакует "player_0".
   Тост показывался, HP не падал. Фикс: `IsPlayerEntityId()` (BodyService).
2. **Все таймеры в 60× медленнее**: WorldService.DeltaTime был 1/60 на тик
   (V1 placeholder). Каст 0.5с = ~30 реальных секунд. Фикс: DeltaTime=1.0/тик
   по TIME_SYSTEM.md; QiRegenCalculator SECONDS_PER_DAY 86400→1440.

### Phase 8: wiring экипировки (5 TODO CombatService закрыты)
- IEquipmentDataProvider: +GetDodgeBonusPermil/GetBlockBonusPermil/
  GetParryBonusPermil/GetCritBonusPermil/GetWeaponPenetration/SetEquipmentData
- EquipmentDataProvider: резолв ID→EquipmentData через IItemDatabaseService
- EquipmentService.SyncToProvider: экипировка игрока видна бою (оба player-ID)
- CombatService: броня→уклонение, щит/оружие→блок/парирование, крит, пробитие;
  базовая атака с оружием использует урон оружия (кулак был всегда 10)

### Phase 7: Combat Visuals
- DamageNumberRenderer: «−N»/«КРИТ −N»/«уклонение»/«парирование»/«блок»
  над целями (пул структур, _Draw, ZIndex Objects+3)
- HP-бар 48×5 над раненым NPC в NPCSpriteRenderer
- Подписки EventBus в Godot-нодах — ТОЛЬКО в _Ready (_EnterTree ранf DI!)

### Phase 4-5: Торговля (модуль Trade — 17-й)
- CurrencyService (ICurrencyService, старт 50 камней, CurrencyChangedEvent)
- TradeService: сток от FNV-1a(npcId): 1-2 оружия+1-2 брони «Матрёшка» L1-3,
  3-4 расходника, 1 материал; цены Permil 1200‰/500‰
- TradeWindow «Лавка торговца» (900×600): Товары/Инвентарь, ЛКМ=1/Shift=5
- Диалог торговца «Покажи товары» → sentinel "open_trade" → TradeRequestedEvent
  (EndDialogue ДО публикации — иначе resume поверх паузы лавки)

### Headless-хуки верификации (новые)
- GODOT_COMBAT_SIM=1 — бой в обе стороны + weapon end-to-end (VERDICT: PASS)
- GODOT_TRADE_DEBUG=1 — smoke buy/sell; GODOT_TRADE_HOLD=1 — держать лавку
- GODOT_SCREENSHOT_DELAY=<сек> — задержка скриншота (для кадров боя)
- `--import --path <АБСОЛЮТНЫЙ>` генерирует .ctex — текстуры работают в облаке

---

## 1. БЫСТРЫЙ СТАРТ

### Проект
- **Название:** Cultivation World Simulator (Ai-game4)
- **Жанр:** Xianxia cultivation life-sim (Kenshi + RimWorld + cultivation)
- **Движок:** Godot 4.7.1 .NET (C#, net8.0)
- **Рендер:** 2D top-down, NEAREST, gl_compatibility/opengl3
- **Репозиторий:** https://github.com/vivasua-collab/Ai-game4

### Восстановление окружения (cloud sandbox)
```bash
# 1. .NET SDK 8.0 (8.0.424; 8.0.30 EOL — НЕ доступен, 424 работает)
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0 --install-dir /home/z/.dotnet

# 2. Godot 4.7.1 mono
mkdir -p /home/z/godot && cd /home/z/godot && \
curl -sSL https://github.com/godotengine/godot/releases/download/4.7.1-stable/Godot_v4.7.1-stable_mono_linux_x86_64.zip -o /tmp/g.zip && unzip -oq /tmp/g.zip

# 3. Репо + симлинки
cd /home/z/my-project && git clone https://github.com/vivasua-collab/Ai-game4.git
ln -sfn /home/z/my-project/Ai-game4 /home/z/my-project/aigame4
ln -sfn /home/z/godot /home/z/my-project/godot

# 4. NuGet.config (game/NuGet.config, gitignored)
cat > /home/z/my-project/Ai-game4/game/NuGet.config << 'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration><packageSources>
<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
</packageSources></configuration>
EOF

# 5. Сборка + ИМПОРТ (абсолютный путь!) + верификация
export DOTNET_ROOT=/home/z/.dotnet && export PATH="$DOTNET_ROOT:$PATH"
cd /home/z/my-project/Ai-game4/game && dotnet build
GODOT='/home/z/godot/Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64'
timeout 240 "$GODOT" --headless --import --path /home/z/my-project/Ai-game4/game
GODOT_NEWGAME=1 timeout 25 "$GODOT" --headless --path "$PWD" scenes/MainMenu.tscn
```

ВНИМАНИЕ:
- `--import` с `--path .` из чужого cwd молча берёт НЕ ТОТ проект → всегда
  абсолютный путь
- Сообщения «.NET Sdk not found. The required version is '8.0.30'» и
  «Microsoft.Build not found» при import — НЕ фатальны
- Push: `git remote set-url origin https://x-access-token:<TOKEN>@github.com/...`,
  после push вернуть публичный URL (токен не хранить в конфиге)

---

## 2. ТЕКУЩЕЕ СОСТОЯНИЕ (коммит 8a5001b)

### Что работает
- ✅ Main Menu → New Game (50×50) / Large World (500×500)
- ✅ Мир: 9 биомов + ТЕКСТУРЫ (импорт облака), переходы, объекты, harvest (F),
      ground items (E), перевес, процедурные спрайты
- ✅ Инвентарь (B) + кукла (11 слотов) + слоты пояса (хотбар 3-9)
- ✅ Character Sheet (C): силуэт тела, статы, культивация
- ✅ **Бой (P0-фиксы):** Space-атака (цель ≤2.5 тайла), урон NPC→игрок по
      частям тела (70/30 RedHP/WhiteHP), HP-бар, тосты, смерть→респавн 3с,
      лут с трупов (1-2 предмета)
- ✅ **Статы экипировки в бою:** урон оружия (вкл. базовую атаку), пробитие,
      штраф уклонения брони, блок/парирование/крит бонусы, coverage
- ✅ **Combat Visuals:** цифры урона (−N/КРИТ) + слова защиты; HP-бары над NPC
- ✅ **Торговля:** E у торговца → диалог → «Покажи товары» → лавка (Товары/
      Инвентарь, ЛКМ=1/Shift=5, Esc), духовные камни (старт 50)
- ✅ NPC: 6 на локации (2 Enemy + Guard + 2 Passerby + Merchant), ИИ
      диспозиций, диалоги, NPC-атаки раз в 1.6с
- ✅ Qi: уровни/прорывы, техники (T, слоты, Z/X каст), формации (генератор +
      lifecycle + визуал), камни Ци (RMB), медитация (V), чит-меню (F1)
- ✅ Генераторы: «Матрёшка» экипировка (7 оружий × 6 бронь × 14 материалов ×
      грейды × зачарования), техники, формации, предметы
- ✅ Скорость PageUp/Down; EventBus re-entrancy; детерминированный combat RNG

### НЕ проверено живьём (в редакторе на ПК)
- ⚠ Страж-союзник вступается (Friendly → threat врагу)
- ⚠ Слоты пояса end-to-end (надеть → drag&drop → клавиша 3)
- ⚠ Лут-дроп + подбор E (пайплайн готов, headless не проверяет подбор)

### Что НЕ работает (отложено)
- ❌ Phase 3: Faction system (порт из Ai-game3-ref, ~400 LOC)
- ❌ Phase 8 остаток: isRanged → CombatSubtype (CombatService ~:300);
      ammo/луки/стрелы (Phase 8 ч.2 — ProjectileRenderer ~300 LOC)
- ❌ Phase 9: метательное оружие + dual wield
- ❌ Tooltip + Context Menu (UI_DESIGN #21-22)
- ❌ Per-attacker pending technique (глобальный pending: NPC может ударить
      себя при смене цели в полёте атаки)
- ❌ Save/load (Q8 — отключён); TileMapLayer миграция (Q11)

---

## 3. АРХИТЕКТУРА (кратко — детали в START_PROMPT.md)

```
game/src/
├── Core/            ← engine-agnostic: Data, Interfaces (40+), Messaging/Contracts
├── Modules/         ← 17 модулей: ... Interaction, Trade (НОВЫЙ), UI, ...
├── Entry/           ← GameSession, 10+ Phases, GameLifetimeScope (порядок см. ниже)
└── Adapter/         ← Godot: Scene (GameBoot, GameWorldController, рендереры),
                        UI (окна), Input, DI, Persistence
```

- **DI-порядок** (GameLifetimeScope): World → Tile → Body → Qi → Buff →
  Inventory → Combat → Formation → NPC → Player → Quest → Interaction →
  **Trade** → UI → Charger → Save → Generator (19 startables, 18 tickables)
- **Игрок под двумя ID**: "player" (EquipmentService/BodyService) и "player_0"
  (PlayerService/Combat/NPC AI). Нормализация: BodyService.IsPlayerEntityId,
  EquipmentService.SyncToProvider пушит под обоими
- **Время:** DeltaTime=1.0/тик (1 тик = 1 игроминута = 1 сек на Normal;
  Fast ×5, Quick ×15 тиков/сек). Все таймеры — «секунды на Normal»
- **Подписки Godot-нод на EventBus — в _Ready ПОСЛЕ ContainerAdapter.InjectProperties**

---

## 4. ЗАМОРОЖЕННЫЕ РЕШЕНИЯ (НЕ нарушать)

1. Godot 4.7.1 .NET — единственный движок; чистый 2D
2. Qi = long (не float); integer math (Permil) для боя
3. config/name без пробелов ("CultivationGame")
4. Input actions программно; constructor injection поддерживается
5. **Документация первична** — НЕ редактировать без прямого указания
6. Custom _Draw рендеринг (TileMapLayer отложен); Element.Poison остаётся
7. Save/load отключён (Q8); GameTile mutable (Q14)
8. НЕ запускать Next.js DEV сервер (песочница — не для игры)

---

## 5. СЛЕДУЮЩИЕ ШАГИ (приоритет)

### P0 — живая проверка в редакторе (ПК)
1. Бандит бьёт игрока: HP падает, тосты, смерть→респавн (пайплайн исправлен
   и headless-верифицирован, но живая проверка не помешает)
2. Страж вступается; лут-подбор; пояс end-to-end
3. Цифры урона/HP-бары/лавка — глазами

### P1 — по плану NPC_COMBAT_PREP
4. Phase 3: Faction Port (Ai-game3-ref/Modules/World/FactionService.cs —
   227 LOC portable; FactionData 34 LOC; wire в NPCRelationshipService)
5. Phase 8 ч.2: Weapon Variety + Ammo (WeaponSubtype в атаках, IAmmoService,
   ProjectileRenderer ~300 LOC; генератор уже выдаёт bow/ spear/axe/dagger)
6. Phase 9: Thrown + Dual Wield

### P2 — долг
7. Per-attacker pending technique (CombatService)
8. Tooltip/ContextMenu (UI_DESIGN #21-22)
9. Консистентность единиц времени (TICK_SECONDS=3.0 в CombatConsequences)

---

## 6. КОМАНДЫ ПРОВЕРКИ

```bash
export DOTNET_ROOT=/home/z/.dotnet && export PATH="$DOTNET_ROOT:$PATH"
cd /home/z/my-project/aigame4/game
GODOT='/home/z/godot/Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64'

dotnet build                                                     # 0 errors

GODOT_NEWGAME=1 timeout 25 "$GODOT" --headless --path "$PWD" scenes/MainMenu.tscn          # полный флоу
GODOT_NEWGAME=1 GODOT_COMBAT_SIM=1 timeout 25 "$GODOT" --headless --path "$PWD" scenes/MainMenu.tscn  # бой: VERDICT PASS
GODOT_NEWGAME=1 GODOT_TRADE_DEBUG=1 timeout 25 "$GODOT" --headless --path "$PWD" scenes/MainMenu.tscn # торговля smoke
GODOT_MAP_SIZE=500 timeout 30 "$GODOT" --headless --path "$PWD" scenes/GameWorld.tscn      # большая карта
GODOT_GEN_DEBUG=1 timeout 20 "$GODOT" --headless --path "$PWD" scenes/GameWorld.tscn       # дамп генераторов

# Скриншот с боем (Xvfb + софтверный GL + VLM для анализа)
Xvfb :99 -screen 0 1920x1080x24 > /dev/null 2>&1 & sleep 2
DISPLAY=:99 LIBGL_ALWAYS_SOFTWARE=1 GODOT_NEWGAME=1 GODOT_COMBAT_SIM=1 \
  GODOT_SCREENSHOT=/tmp/shot.png GODOT_SCREENSHOT_DELAY=4.3 timeout 60 "$GODOT" \
  --path "$PWD" --rendering-driver opengl3 scenes/MainMenu.tscn
# Скриншот лавки: GODOT_TRADE_DEBUG=1 GODOT_TRADE_HOLD=1 GODOT_SCREENSHOT_DELAY=5.0
```

### Ожидаемый вывод боя (ключевые строки)
```
[CombatSim] equip[npc_...]: pen=4 dodge=... dmg=15
[CombatSim] damage: npc_... → player_0: 21 (Hit, part=Torso)
[CombatSim] player equipped 'Сталь Посох' (dmg=7, pen=1) — provider: dmg=7, pen=1
[CombatSim] armed swing: npc HP 493→479 (14 RedHP dmg)
[CombatSim] VERDICT: PASS — обе стороны боя получают урон
```

---

## 7. КЛЮЧЕВЫЕ ФАЙЛЫ

| Файл | Назначение |
|------|------------|
| `SESSION_CONTEXT.md` | ЭТОТ ФАЙЛ |
| `SESSION_SUMMARY.md` | Сводка сессий + состояние |
| `checkpoints/08_25_phase7_8_trade.md` | Чекпоинт последней сессии |
| `docs/docs_v2/09_workflow/NPC_COMBAT_PREP.md` | План (Phase 3/8ч.2/9 остались) |
| `game/src/Adapter/Scene/CombatSimDebug.cs` | Headless-верификация боя |
| `game/src/Adapter/Scene/DamageNumberRenderer.cs` | Цифры урона (Phase 7) |
| `game/src/Adapter/UI/TradeWindow.cs` | Окно лавки (Phase 5) |
| `game/src/Modules/Trade/*` | Торговля + валюта (Phase 4) |

---

## 8. ПРЕДУПРЕЖДЕНИЯ

1. Next.js DEV сервер НЕ запускать — песочница для игры
2. `--import` — ТОЛЬКО абсолютный путь
3. SDK 8.0.30 EOL → 8.0.424; ошибки GodotSharp editor при import нефатальны
4. BiomeTiles/спрайты логируют каждый кадр — спам в лог
5. Токен GitHub не коммитить; после push сбрасывать remote URL на публичный
6. Документация НЕ редактируется без прямого указания пользователя

*Конец файла. Прочитай START_PROMPT.md следующим.*
