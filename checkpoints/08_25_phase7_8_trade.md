# Чекпоинт: Phase 7+8 + P0-фиксы + Phase 4-5 Торговля

**Дата:** 2026-08-25 15:00 UTC
**Сессия:** Облачный агент (Z.ai Code sandbox), cold start с нуля
**Тип:** implementation + fix

---

## Контекст

Сессия начата с чистой песочницы (передан только токен + URL репо).
Холодный старт выполнен по cold_start.sh. Плановая работа — P0-проверка
прототипа боя и P1-фазы NPC_COMBAT_PREP. При проверке найдены 2 критических
P0-бага, исправлены; закрыты Phase 7, 8, 4-5.

## Что сделано

### Холодный старт (улучшен)
- .NET SDK 8.0.424 (8.0.30 недоступен — EOL; GodotSharp editor plugin
  ругается «required 8.0.30», но это НЕ фатально: import и runtime работают)
- **ОТКРЫТИЕ:** `godot --headless --import --path <АБСОЛЮТНЫЙ_ПУТЬ>` генерирует
  .godot/imported/*.ctex (150 файлов) — биом-текстуры теперь рендерятся в
  cloud sandbox; ранее считалось «нужен Godot Editor». Главный бонус: uid_cache.bin
  → main scene резолвится при обычном запуске.
- ВАЖНО: `--import` с `--path .` из чужого cwd молча берёт не тот проект.
  Всегда абсолютный путь.

### P0-БАГ №1: урон NPC→игрок никогда не применялся
- BodyService.OnDamageApplied сравнивал `e.TargetId == _entityId` ("player"),
  а NPC AI атакует игрока как "player_0" (NPCAIService.PlayerId).
  Тост «💥 −X HP» показывался, HP не падал, смерть недостижима.
- **Фикс:** IsPlayerEntityId() нормализует оба ID (прецедент — PlayerService:178).

### P0-БАГ №2: V1-плейсхолдер времени (все таймеры в 60× медленнее)
- WorldService.DeltaTime = 1/60 на тик. Каст 0.5с разрешался ~30 реальных
  секунд → урон «не проходил», NPC почти не двигались, медитация копейки.
- **Фикс:** DeltaTime = 1.0/тик (TIME_SYSTEM.md §5-6: 1 тик = 1 игроминута =
  1 реальная секунда на Normal). QiRegenCalculator SECONDS_PER_DAY
  86400→1440 (пассивная регенерация теперь точно 10%/сутки по QI_SYSTEM.md §4).

### Phase 8: wiring боевых статов экипировки (5 TODO закрыты)
- IEquipmentDataProvider: +GetDodgeBonusPermil/GetBlockBonusPermil/
  GetParryBonusPermil/GetCritBonusPermil/GetWeaponPenetration + SetEquipmentData
- EquipmentDataProvider: резолв ID→EquipmentData через IItemDatabaseService
  (старый TODO из комментария класса), прямой кэш игрока
- EquipmentService: SyncToProvider после каждого equip/unequip (оба player-ID)
- CombatService: armorDodgePenalty ← броня защитника; shieldBlock/weaponParryBonus
  ← StatBonus blockChance/parryChance; techniqueCritBonus ← critChance;
  penetration += weapon.Penetration; базовая атака с оружием = урон оружия
  (раньше кулак всегда 10); isPlayerAttacker вместо строки "player"
- «Баг» Cultivator cap=0 — НЕ баг (TECHNIQUE_SYSTEM.md §11.5: Cultivation
  пассивна) — закрыт дизайном ещё в Qi-этапах

### Phase 7: Combat Visuals
- DamageNumberRenderer (Node2D + _Draw, пул структур): «−N» красный /
  «КРИТ −N» золотой / слова уклон-парир-блок серые; всплытие 26px + затухание
  0.9с; max 48. Подписка строго в _Ready ПОСЛЕ DI (_EnterTree раньше инъекции!)
- NPCSpriteRenderer: HP-бар 48×5 над раненым NPC (зел/жёлт/красн)
- GameBoot: GODOT_SCREENSHOT_DELAY env (для кадров боя)

### Phase 4-5: Торговля
- Core: TradeContracts (5 событий), ITradeService, MerchantStockEntry
- Modules/Trade (17-й модуль): CurrencyService (первая реализация
  ICurrencyService, старт 50 камней), TradeService (ассортимент от
  FNV-1a(npcId) seed: оружие/броня «Матрёшка» L1-3 + расходники + материал;
  Permil-цены 1200‰/500‰), TradeModule (+GODOT_TRADE_DEBUG smoke-хук,
  GODOT_TRADE_HOLD для скриншотов)
- Adapter/UI/TradeWindow: «Лавка торговца» 900×600, Товары/Инвентарь,
  ЛКМ=1 (Shift=5), пауза тиков, modalOpen-гарды
- Диалог-хук: выбор «Покажи товары» → sentinel "open_trade" →
  TradeRequestedEvent; EndDialogue ДО публикации (иначе resume поверх паузы)

### Визуальная верификация (Xvfb + opengl3 + VLM-анализ)
- Биом-текстуры рендерятся: «Drew 144 textures, 0 missing»
- HP-бар над NPC + красное «−10» над целью: ПОДТВЕРЖДЕНО
- TradeWindow: баланс 46, «Товары (9 поз. / 22 шт.)» + «Инвентарь (22 поз.)»:
  ПОДТВЕРЖДЕНО
- Скриншоты: docs/screenshots/{gameworld_v0.6_combat_visuals,
  gameworld_v0.6_world_hud, trade_window_v0.6}.png

## Решения

- DeltaTime=1.0/тик, а не «реальные секунды» — время как ресурс: на Fast/Quick
  реальная длительность сжимается, количество на игро-сутки не меняется (док)
- EquipmentDataProvider с IItemDatabaseService — Core-интерфейс, цикла
  модулей нет; порядок DI-регистраций не важен (резолв после билда контейнера)
- Подписки Godot-нод на EventBus — ТОЛЬКО в _Ready (после ContainerAdapter)
- Диалог торговца: sentinel-узел "open_trade" вместо расширения модели
  DialogueNode экшенами — минимальное вторжение

## Найденные проблемы

- Pending technique глобален на бой: при смене цели в полёте NPC может ударить
  себя (npc→npc: 21 в логе) — нужен per-attacker pending (не трогал)
- CombatConsequencesService TICK_SECONDS=3.0 — ещё одна единица времени
  (длительности баффов теперь «9 тиков» — приемлемо, но консистентность хромает)
- BiomeTiles/рундуки логируют каждый кадр — спам в лог (не трогал)
- StatProvider возвращает статы игрока для ЛЮБОГО неизвестного ID (fallback)

## Следующие шаги

1. **P0 (осталось проверить живьём в редакторе):** страж-союзник вступается;
   слоты пояса end-to-end; лут-дроп + подбор E
2. **Phase 3:** Faction port (Ai-game3-ref → Modules/World, ~400 LOC)
3. **Phase 8 остаток:** isRanged → CombatSubtype (CombatService:300 TODO);
   ammo/bow логика (Phase 8 weapon variety часть 2)
4. **Refactor:** per-attacker pending technique; консистентность единиц времени
   (TICK_SECONDS против DeltaTime)
5. UI_DESIGN #22: Tooltip/ContextMenu для предметов — не начаты

## Файлы

- Новые: CombatSimDebug, DamageNumberRenderer, TradeWindow, TradeContracts,
  ITradeService, MerchantStockEntry, TradeModule/Service/Config,
  TradeModuleServices, CurrencyService
- Изменённые: IEquipmentDataProvider, EquipmentDataProvider, EquipmentService,
  CombatService, BodyService, WorldService, QiRegenCalculator,
  NPCSpriteRenderer, SceneBuilder, GameWorldController, GameBoot,
  DialogueService, GameLifetimeScope

## Коммиты

- 679f19e — Phase 8 + P0-фиксы + CombatSimDebug
- f02d61d — Phase 7 Combat Visuals
- 8a5001b — Phase 4-5 Торговля
