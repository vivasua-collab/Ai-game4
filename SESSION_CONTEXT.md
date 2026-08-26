# SESSION_CONTEXT — Передача контекста агенту

**Дата создания:** 2026-08-22
**Обновлено:** 2026-08-26 14:50 UTC (облачная сессия: Epic→Legendary оверкап + 3 прохода аудита)
**Назначение:** Полный контекст для продолжения разработки
**Инструкция:** Прочитай этот файл ПЕРВЫМ при старте новой сессии

---

## 0. ЧТО СДЕЛАЛОСЬ ЗА ОБЛАЧНЫЕ СЕССИИ 2026-08-25…26 (коммиты 679f19e…1c3e041)

### Сессия 2026-08-26 №1: генераторы + верификация (коммиты 29f8d50…31d679b)
1. **LevelBoundaries.cs** — границы уровней для техник/экипировки/формаций
   (L1-10 × грейды × подтипы), OvershootPolicy (None/DamageAndQi/All).
2. **VerificationService** — валидация генерируемых объектов по границам
   (+WithOvershootApplied для легендарных +1lvl).
3. **DeduplicationService** — fingerprints, очистка дублей по статам.
4. **PreGenTechniquePhase** — предгенерация техник при создании мира
   (100 техник, дедуп, верификация, регистрация в реестре).
5. **CheatPanel** — секции генерации: экипировка/расходники/техника+формация/
   cycle-формация/верификация (F1 в игре).
6. docs_v2: CHEAT_PANEL, LEVEL_BOUNDARIES, VERIFICATION_SYSTEM,
   PRE_GENERATION.

### Сессия 2026-08-26 №2: Epic→Legendary оверкап + 3 аудита (f0d11a6, b8ddda1, e7f2008, 1c3e041)
1. **Epic→Legendary промоушен:** при ролле Transcendent — 20% шанс
   промо в Legendary (итог: L9+ ≈4% легендарок). **Оверкап** 18%
   (диапазон ТЗ 10-25%): только Damage/Defense + Durability по формулам
   L+1, RequiredCultivationLevel остаётся L. Легендарка ВСЕГДА получает:
   энчант, макс стат-бонусы (5), value×3. API:
   GenerateLegendaryWeapon/Armor(level, subtype?, seed, forceOvercap?).
   Константы в GameConstants: EPIC_TO_LEGENDARY_PROMOTE_CHANCE,
   LEGENDARY_OVERCAP_CHANCE, LEGENDARY_VALUE_MULTIPLIER.
2. **3 баг-фикса генераторов:** VerificationService матчинг «_id_» (sword⊂
   greatsword ложные out-of-bounds); оружие категории Void на T5 (иначе всё
   L9-оружие — ЖЕЛЕЗО T1); BaseDamage MathF.Round (отбои на дробных гранях).
3. **Аудит-1 (архитектура):** 6 находок, 4 фикса — порядок фаз
   перенумерован 1-14 (Finalize последняя), FormationData перенесён в
   Core.Data (Core→Modules нарушение), стабильная сортировка фаз, дубль
   SceneReadyEvent удалён.
4. **Аудит-2 (мир+NPC):** 7 находок, 4 фикса — травы chance 1→100
   (двойной ролл давал 0.01%!), ResourceHarvestedEvent с исходным
   ResourceId, сброс _placedGroupCentres между сборками, dead
   NPCSpawnPhase удалён.
5. **Аудит-3 (боевой контур):** 6 находок, 1 CRITICAL-фикс — инверсия
   ролей игрока при NPC-инстагаторе (5 мест в CombatService через
   PlayerIdResolver: qi-щит игрока, защита, fatal-ветка — лут/квесты
   дропались НА игрока при ЕГО гибели, EndCombat winner, ExecuteDefense).
6. **Итог регресса (Phase H):** все 4 headless-теста PASS — NEWGAME,
   COMBAT_SIM (обе стороны получают урон; qi-щит отражает), TRADE_DEBUG
   (buy/sell), GEN_DEBUG (промо 16.8%/4.0%, оверкап 2/16, верификация
   40/40 легендарок).

### Сессия 2026-08-25: P0-фиксы боя + торговля + визуал (679f19e, f02d61d, 8a5001b)
- **P0-баги:** урон NPC→игрок не применялся ("player" vs "player_0"); все
  таймеры в 60× медленнее (DeltaTime 1/60→1.0). Исправлены.

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

## 2. ТЕКУЩЕЕ СОСТОЯНИЕ (коммит 1c3e041 — synced с origin/main)

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
- ✅ **Legendary-предметы:** промо Epic→Legendary 20%, оверкап 18%
      (статы L+1 в Damage/Defense/Durability), верификация по границам
      (все 40/40 принудительных легендарок L9 валидны)
- ✅ **Верификация/дедуп генераторов:** LevelBoundaries + VerificationService
      + DeduplicationService + PreGenTechniquePhase (100 техник при старте)
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

### P0 — живая проверка в редакторе (ПК, после 19:00)
1. **Чит-кнопки легендарок (F1):** «Легендарки (промо 20% / оверкап 18%)» —
   оружие/броня/батч ×20 со статистикой + верификацией. Сравнить семплы
   с/без оверкапа (dmg 115 vs 104 на L9 — эталон из дампа).
2. Бандит бьёт игрока: HP падает, тосты, смерть→респавн; qi-щит отражает
   урон атакующему (новое после фикса C-1)
3. Страж вступается; лут-подбор; пояс end-to-end
4. Цифры урона/HP-бары/лавка — глазами

### P1 — по плану NPC_COMBAT_PREP / кандидаты аудита-4+
4. **Аудит-4+ (по схеме этой сессии — по модулю за проход):** Inventory/UI/
   Save, Interaction/Trade (глубже), Body/Enhancement, NPC AI/Movement.
   Мелочь от аудита-3: событие отклонения атаки при _isCasting (C-5),
   удалить CombatConfig.PlayerEntityId (C-6).
5. Phase 3: Faction Port (Ai-game3-ref/Modules/World/FactionService.cs —
   227 LOC portable; FactionData 34 LOC; wire в NPCRelationshipService)
6. Phase 8 ч.2: Weapon Variety + Ammo (WeaponSubtype в атаках, IAmmoService,
   ProjectileRenderer ~300 LOC; генератор уже выдаёт bow/spear/axe/dagger)
7. Phase 9: Thrown + Dual Wield

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
| `checkpoints/plans/2026-08-26_epic_legendary_overcap_and_audit_plan.md` | План сессии 08-26 №2 |
| `checkpoints/2026-08-26_epic_legendary_overcap_checkpoint.md` | Чекпоинт сессии 08-26 №2 (фазы A-H) |
| `checkpoints/2026-08-26_audit_pass1_architecture.md` | Аудит-1: архитектура (6 находок) |
| `checkpoints/2026-08-26_audit_pass2_worldgen.md` | Аудит-2: мир+NPC (7 находок) |
| `checkpoints/2026-08-26_audit_pass3_combat_qi.md` | Аудит-3: боевой контур (6 находок) |
| `checkpoints/2026-08-27_generators_verification_checkpoint.md` | Чекпоинт сессии 08-26 №1 (генераторы) |
| `docs/docs_v2/02_systems/LEVEL_BOUNDARIES.md` | Границы уровней + промо/оверкап |
| `docs/docs_v2/07_ui/CHEAT_PANEL.md` | Чит-меню (вкл. секция «Легендарки») |
| `game/src/Adapter/Scene/CombatSimDebug.cs` | Headless-верификация боя |
| `game/src/Modules/Combat/CombatService.cs` | Боевой контур (фикс C-1 аудита-3) |

---

## 8. ПРЕДУПРЕЖДЕНИЯ

1. Next.js DEV сервер НЕ запускать — песочница для игры
2. `--import` — ТОЛЬКО абсолютный путь
3. SDK 8.0.30 EOL → 8.0.424; ошибки GodotSharp editor при import нефатальны
4. BiomeTiles/спрайты логируют каждый кадр — спам в лог
5. Токен GitHub не коммитить; после push сбрасывать remote URL на публичный
6. Документация НЕ редактируется без прямого указания пользователя
7. **Сброс песочницы между сессиями:** если Godot/dotnet пропали — запускать
   `bash /home/z/my-project/Ai-game4/cold_start.sh` (idempotent, ~15 сек).
   Аномалия ФС 08-26: бинарь Godot виден через readdir, но ENOENT при
   прямом lookup — обход: python os.walk + shutil.copyfile в /tmp/godot471,
   запускать оттуда.

*Конец файла. Прочитай START_PROMPT.md следующим.*
