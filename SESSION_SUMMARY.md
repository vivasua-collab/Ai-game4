# Сводка сессий (обновляется при завершении каждой сессии)

Обновлено: 2026-09-11 11:30 UTC (облачный агент Z.ai Code)

## Проект
Cultivation World Simulator (Ai-game4), Godot 4.7.1 .NET, C#
Репозиторий: https://github.com/vivasua-collab/Ai-game4 (публичный)
HEAD: см. `git ls-remote origin main` (полный аудит + P1-фиксы запушены)

---

## Последние сессии

### 2026-09-11 №9 (ПОЛНЫЙ АУДИТ: 18 чекпоинтов + P1-пакет 14 фиксов)
- **Метод:** 4 волны / 9 параллельных аудита-агентов (READ-ONLY), каждый
  модуль целиком; каждый верифицирован в коде перед фиксом; аудит
  закоммичен ОТДЕЛЬНО (2072fde) до фиксов. Чекпоинт:
  checkpoints/09_11_full_audit_and_p1_fixes.md (сводка+решения+очередь).
- **Итог аудита: P1×19, P2×85+, P3×137+** (~200 файлов). Чекпоинт на
  КАЖДЫЙ модуль: checkpoints/09_11_audit_{core_layer,world_tile,body,buff,
  qi,charger,inventory,combat,npc,formation,generator,player,quest,
  interaction,trade,save,entry,adapter}.md — evidence-based, file:line.
- **P1-пакет закрыт (14 дефектов + BOD-10 попутно):** статы игрока всегда
  0 (InitializeDefaults + StatChangedEvent → VIT→HP ожил); смерть игрока
  по правилу тел (Head|Heart × Disabled|Severed, IsAlive=IsEntityAlive,
  defenderDead без !isPlayerTarget, алиас-безопасный BodyService);
  хит-таблицы зверей ретаргетнуты на реальные части (паук больше не умирает
  от 2 ударов в «сердце», призрак не теряет 50% ударов); баффы: GetStat-
  ModifierPermil = аддитивный процент (слои 3a/3b урона работали в 0),
  bleed/shock/void_pierce/severed_* честные ветки, DoT-тики из potency,
  duration<0=Permanent, potency-нормализация (slow −9000% бомба);
  слой брони 6-7 оживлён для всех (coverage из предметов, грейд-
  множители, NPC-агрегаты переживают load); двойное списание Ци за щит G
  (было 50%, стало 25%); 3 точки потери предметов при volume-full
  (unequip/пояс/harvest); itemCount-кэш при мульти-кучках; Ци-техники не
  требуют стрелы; Dodge публикует событие («уклонение» в цифрах);
  респавн ресурсов оживлён (Initialize из TileModule + абсолютный день +
  RespawnDays из ObjectDefaults); техники видят зверей (D1-паттерн);
  фантомная угроза sever_unknown/dot:* (NPC бил игрока без агро); цифры
  1-9 в диалоге — одинарное действие.
- **QA: build 0 err; регрессия 15/15 PASS** (COMBAT_SIM/COMBATAI/ANIMALQA/
  LOOT/DOT/SAVELOAD/KILLFEED/QUEST/STORAGE/HOTBAR/TRASHDROP/CONTEXT/
  WEAPONVIS/REASSEMBLY/CHARGE).
- **Решение:** P1-семья «полнота сейва» (QI-2 Qi вне сейва, QST-1 квесты,
  TRD-1 валюта, INV-4 кукла/пояс → дюп, G-2 фантомные ID cold-load, NPC-1
  звери переживают Load, E-3 SkipOnLoad-контракт, WT-5 время) — ОТЛОЖЕНА
  отдельным эпизодом R17; G-1+NPC-6 (один генератор экипировки NPC) — R18.

### 2026-09-11 №8 (Бой с животными: 9 дефектов; легенда клавиш → F1)
- **Жалоба:** «бегу с посохом за волком — результата 0». **D1 (ПРИЧИНА):**
  Space-таргетинг видел только NPCService — волки живут в AnimalService,
  атака молча не находила цель.
- **D8 (СИСТЕМНЫЙ, животные И NPC): «сущности неубиваемы»** — домены смерти
  ждали «суммарный HP ≤ 0» при per-part floor (урон в мёртвую часть
  теряется) → практически недостижимо. IsFatalHit не вёл к смерти.
  Фикс: единое правило тел (vital Head/Heart RedHP ≤ 0 — IsEntityAlive —
  ИЛИ дренаж) в CombatService/NPCCombatAdapter/AnimalService.
- **Контур зверя:** IAnimalService (новый Core-интерфейс + AnimalInfo) —
  таргетинг (NPC ∪ животные), статы вида (волк STR 8/AGI 14,
  Quadruped-таблица попаданий), труп «Волк» (камни 1–3, DEATH_AND_LOOT
  §5), цифры урона над зверем, killfeed «Волк повержен».
- **Месть волка:** чейз + укус только вплотную (кулдаун 2 тика < окна
  хода 2.5с — 3-й тик опаздывал вечно), de-aggro > 5 тайлов →
  CombatDisengageEvent → AbandonCombat; кролик мирный.
- **Легенда клавиш с HUD УДАЛЕНА** — канон F1 (HotkeysWindow; легенда
  к тому же врала: M/N-карты не реализованы, F1 ≠ чит-меню).
- QA: ANIMALQA (новый хук, 7 тестов) PASS; регрессия 14/14 PASS; build 0
  err. Чекпоинт: checkpoints/09_11_animal_combat_fix.md.

### 2026-09-10 №7 (Аудит R14–R16: 9 дефектов исправлено, REASSEMBLY QA усилен)
- **R14:** P2-1 — NPC-домен переживал пересборку мира (double-spawn,
  «призраки» при тёплом LoadGame) → NpcDomainResetPhase (фаза 0) +
  GameSession.LoadGame сброс до RestoreState + ResetWorld по всему
  NPC-домену (Spawner/Corpse/Group; CorpseRemovedEvent на каждый труп);
  P2-2 — восстановленные из сейва NPC дрейфовали к (0,0) (якорь блуждания
  не восстанавливался) → блуждание вокруг текущей позиции.
- **R15:** P1-1 — звери экипировались оружием («волки с мечами», урон
  поверх BaseDamage) → фильтр морфологий с руками (генерация + рендер
  defense-in-depth); P2-2 — ResetCache кэша спрайтов оружия вызван
  при пересборке.
- **R16:** P1-1 — незавершённый каст переживал EndCombat (AbandonCombat
  посреди каста → перманентный лок боя) → гашение в EndCombat; P2-1 —
  QA-геттер LastNpcDefenseSelected (детерминированная проверка проводки
  селектора защит); P2-2 — тест двустороннего боя требует урона по NPC;
  P3 — мёртвый ApplyDamage удалён, сброс CurrentDefenseStance на
  CombatEnded, пустые подписки адаптера удалены.
- **REASSEMBLY QA усилен:** NPC-домен-ассерты (QA-смерть+QA-группа в мире 1
  → ghost/труп/группы не переживают пересборку; реестр не ×2;
  harness-гейт). 16/16 фаз.
- QA: build 0 err; 14 инструментов VERDICT: PASS (REASSEMBLY/
  COMBAT_SIM/COMBATAI/LOOT/SAVELOAD/KILLFEED/QUEST/STORAGE/HOTBAR/
  DOT/CHARGE/TRASHDROP/CONTEXT/WEAPONVIS).
- docs_v2: ARCHITECTURE §6.1 (фаза 0), FILE_TREE (16), MODULE_STRUCTURE
  §2.4/§2.7, COMBAT_SYSTEM §1.4.1, NPC_AI_SYSTEM шапка, TESTING_RULES.
  Чекпоинт: checkpoints/09_10_r14_r16_audit.md.

### 2026-09-10 №6 (Аудит R13: 3×P1 + P2 исправлены; CombatLootService удалён)
- P1-1 окно обыска не закрывалось при удалении трупа (Name-сравнение
  никогда не срабатывало) → LootWindow.CurrentCorpseId; P1-2 резюм тиков
  дублирован в 3 путях → единая точка LootWindow.Closed; P1-3
  CombatLootService УДАЛЁН — двойной лут поверх труп-контейнера;
  P2-2 гварды модальности T/F1/J/Q/B/C при обыске.
- Урок аудита: чекпоинт-verification R13 синхронизировал доки, но код не
  тронул — «удалено» относилось к документации. Аудит обязан сверять
  claims с кодом.
- QA: build 0 err, LOOT/COMBAT_SIM/KILLFEED/QUEST/SAVELOAD PASS.
  Коммит: 0843e27.

### 2026-09-10 №5 (R16: боевой ИИ NPC + рабочий бой + анимация удара)
- **Аудит D1–D8:** NPC не отвечал на атаку игрока («манекен» — гейт
  IsInCombat в EvaluateAndDecide глотал переходы; месть не доходила до
  ProcessNpcAttacks), не бежал при HP<20% в бою, вечное преследование,
  ActiveDefense=None для NPC, клавиша defend без ввода, фантомный
  CombatStartedEvent, лучники лезли в melee, нет анимации удара.
- **ИИ (NPCAIService):** переходы ДО гейта боя — месть
  (Attacking/Fleeing по личности: роли-бойцы/Aggressive дерутся,
  Pacifist/Cautious бегут), бегство HP≤20% в бою + leash AggroRadius×3
  (CombatDisengageEvent → AbandonCombat → Flee-финал), анти-flip-flop
  (раненый не пере-агрится).
- **Механики:** NPCDefenseSelector (щит→Block/силовик→Parry/прочие→
  Dodge) — слой активной защиты для NPC-защитников; стойка игрока **G**
  (цикл None→Dodge→Parry→Shield, DefenseIntentEvent); ExecuteDefense
  работает и вне боя; AbandonCombat в ICombatService.
- **Анимация удара:** StrikeFxRenderer (свип по AttackIntent, слэш+
  искры по DamageApplied, крит золотой) + замахи оружия игрока/NPC
  (sin-выпад 0.42с, зеркалирование учтено) — всё процедурно _Draw.
- **Попутно:** kiting лучников (NPCMovementService); null-гварды
  GetNPCState/OnCombatEnded (спящий баг Flee-финала валил весь AI-тик
  ArgumentNullException'ом); стек в логе исключений GameEntryPoint.
- QA: GODOT_COMBATAI_DEBUG=1 PASS (7 тестов); 12 регрессий PASS;
  build 0 err; Xvfb+VLM (слэш-дуга/искры/−27/выпады подтверждены).
  docs_v2: COMBAT_SYSTEM §1.4.1-2/§7, NPC_AI_SYSTEM шапка,
  MODULE_STRUCTURE §2.4/§2.7, TESTING_RULES (33), SPRITE_CATALOG §22.
  Чекпоинт: checkpoints/09_10_r16_combat_ai.md.

### 2026-09-10 №4 (R15: оружие в руках — фазы A+B)
- **План одобрен** (дефолты §6; статичные спрайты; PNG — отдельный план;
  анимация — фаза C). Внедрено:
- **WeaponClassId** (EquipmentData + генератор) с fallback-парсингом
  ItemId (сейвы R11) → generic sword.
- **Спрайты:** CreateWeaponIcon(32×32) + CreateWeaponHandSprite(48×48),
  7 рецептов × 5 тиров материала × редкость, Legendary+ золотая обводка.
- **WeaponVisualCatalog:** кэш (class|tier|rarity)→(icon+hand+key),
  HandOffset (1H/2H/лук), ResetCache для QA.
- **Игрок:** MainHand Sprite2D (EquipmentChangedEvent + страховка 0.5с),
  facing-wiring (клавиши/мышь) → FlipH + offset-зеркало только X.
- **Хотбар 1-2** (иконки+подпись) и **кукла** (DollSlotRow иконки).
- **NPC:** NPCSpriteRenderer overlay (кэш npcId→itemId, перескан 0.5с,
  facing-гистерезис, DrawSetTransform-зеркало).
- QA: GODOT_WEAPONVIS_DEBUG=1 PASS (7/7); 10 регрессий PASS; build
  0 err; Xvfb-скриншот+VLM (кинжал в руке + иконка хотбара подтверждены).
  docs_v2: SPRITE_CATALOG/MODULE_STRUCTURE/TESTING_RULES (31 хук).
  Чекпоинт: checkpoints/09_10_r15_weapon_visuals.md.

### 2026-09-10 №3 (R14: правило населения + R15-план)
- **R14 (c07427c):** восполнение населения НЕ работает в рамках сессии
  (ReinforcementTick удалён; выбитое — остаётся выбитым). Ивенты
  (караван/набег) — единственный внутрисессионный источник:
  TrySpawnEventNpc(Caravan/Raid/Event). Естественное восстановление —
  при (пере)сборке локации (TRANSITION_SYSTEM §5.3 «таймер памяти»).
  docs_v2 sync (DEATH_AND_LOOT §2.4 + MODULE_STRUCTURE + TESTING_RULES);
  QA 9/9 PASS.
- **R15 КОНЦЕПЦИЯ+ПЛАН** (на согласовании): «оружие в руках» игрока и NPC —
  RimWorld-слои, 7 классов × материал, icon+hand спрайты, фазы A/B/C:
  checkpoints/plans/2026-09-10_r15_weapon_visualization_plan.md

### 2026-09-10 №1-2 (R13 full-loot + верификация)
- **R13 (5f7e7e7):** NPC спаун через генерацию (NPCSpawnCompositionService:
  состав от типа локации+DangerLevel+сид, кап 12, ReinforcementTick
  восполняет потери <60%); трупы-контейнеры (CorpseService: снапшот
  экипировки+инвентаря+камней 4.1, TTL 1 день, SlotId-адресно);
  full loot (LootWindow: E-обыск, «Забрать всё», Esc-авторитет);
  ClassicLootSeeder фикс фантомных ID.
- **Верификация (запрос пользователя):** QA 9/9 хуков PASS + build
  0 errors; **docs_v2 синхронизированы**: DEATH_AND_LOOT (трупы вместо
  случайного дропа), MODULE_STRUCTURE (NPC: +Corpse/+Composition),
  DI_AND_EVENTBUS (+CorpseContracts 3), TESTING_RULES §0.1 (реестр
  25→30 хуков). SESSION_CONTEXT/SESSION_SUMMARY обновлены.
- Ответ про чекпоинты: правила — START_PROMPT §6 (файла правил в
  checkpoints/ никогда не было, git-история подтверждена).

### 2026-09-09 (R10 SlotId + R11 Save/Load)
- R10 (756fd79): SlotId-адресность инвентаря (TOCTOU-защита кучек),
  ПКМ-меню (свойства/разделение стака/выброс).
- R11 (1fd5ba9): Save/Load — типизированный round-trip (IncludeFields),
  честные события, World biome/danger; анти-тривиальный QA.

### 2026-09-08 (внешнее ревью этапы 1-7)
- 34 находки исправлены: combat authority, inventory transactions,
  qi transactions + DoT, world/tile respawn, quests semantics.
  QA 17/17 PASS, доки 1v1 (3e41927…80f4f25).

### 2026-09-10 утро (R12: восстановление после ночного сброса)
- Клон без токена (репо публичный), dotnet 9.0.318, Godot
  /home/z/godot_flat/godot (flat-структура), restore_env.sh починен
  (имя ассета с подчёркиванием), QA PASS.

---

## Текущее состояние игры

### Что работает
- ✅ Мир/биомы/harvest/ground items; инвентарь+кукла+пояс+ПКМ-меню
- ✅ Бой: melee+ranged+LOS+ammo, turn-gate, визуал (цифры/HP/kill-feed)
- ✅ **Боевой ИИ R16:** NPC отвечает на атаку (Attacking/Fleeing по
  личности), бежит при HP<20% (даже в бою), leash 15 тайлов, защищается
  (Dodge/Parry/Block), лучники кайтят; стойка игрока G; анимация удара
  (слэш/искры/замахи)
- ✅ **Full loot R13 + R14:** генерация населения, трупы, обыск;
  восполнение населения — только вне сессии (ивенты — TrySpawnEventNpc)
- ✅ **Оружие в руках R15:** hand-спрайты игрока+NPC (7 классов × 5
  тиров), иконки хотбара/куклы, facing-зеркалирование
- ✅ Save/Load; торговля; Qi/техники/формации/зарядники; квесты
- ✅ Генераторы + легендарки + верификация/дедуп

### Что НЕ работает (отложено)
- ❌ Визуал R13/R15 живьём на ПК (Xvfb подтвердил игрока; NPC — QA);
  баланс камней в трупах; тонкая настройка амплитуд R16-анимации
- ❌ Авто-замедление времени в бою (§1.2 superSuperSlow — кандидат)
- ❌ PNG-ассеты (план пользователя); броня-слои (архитектура R15)
- ❌ Лут с животных (материалы TODO — только камни); faction port
- ❌ Per-attacker pending; обыск трупов ИИ; состав для 500×500;
  спинальный AI/Brain (NPC_AI_SYSTEM §2)

---

## Замороженные решения (НЕ нарушать)

- Godot 4.7.1 .NET; Qi = long; Permil integer math
- Документация первична: docs_v2 ≠ код = баг → синхронизировать
  с обоснованием (прецедент: R13 sync 2026-09-10)
- НЕ запускать Next.js DEV сервер (остановлен по запросу 09-10)

---

## Следующие шаги
1. P0: живой QA R13+R14+R15+R16 на ПК (вечером): бой с NPC (Space →
   обмен ударами), G-стойки, бегство/leash, слэш/искры/замахи,
   НЕ-восполнение после зачистки
2. Авто-замедление времени в бою (§1.2) или PNG-ассеты (план)
3. Броня-слои (архитектура R15); лут с животных (материалы)
4. Обыск трупов ИИ-NPC; faction port; 500×500 состав

---

## Предупреждения
- Godot: **/home/z/godot_flat/godot** (flat, не старый вложенный путь)
- QA-хуки поштучно, timeout ≥90с, вердикт grep 'VERDICT' (реестр —
  TESTING_RULES §0.1, 33 хука)
- Токен: /home/z/my-project/.auth/github.token (не запрашивать, если есть)
- После сброса песочницы: recover_sandbox.sh; сверять git ls-remote
