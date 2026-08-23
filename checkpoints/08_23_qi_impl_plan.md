# План внедрения системы ЦИ (техники + формации + камни + читы)

**Дата:** 2026-08-23
**Задача:** Внедрение духовной части — рабочие техники у персонажа, схематическое отображение их действия, создание формаций (генератор формаций), выдача техник персонажу, случайный набор техник для тест-режима, чит-меню для развития ядра (тест высокоуровневых техник).

**Исходное состояние:** коммит fc19e27 (после git pull 2026-08-23). Физический прототип работает: бой, NPC, экипировка, лут.

---

## 1. Что уже есть в коде (аудит перед планом)

| Компонент | Файл | Статус |
|-----------|------|--------|
| QiService (игрок: currentQi/capacity/density/conductivity/прорывы) | `Modules/Qi/QiService.cs` | ✅ работает, long |
| QiDataProvider (per-entity Ци для NPC) | `Modules/Qi/QiDataProvider.cs` | ✅ |
| QiBufferService (буфер Ци = защита) | `Modules/Qi/QiBufferService.cs` | ✅ скелет |
| TechniqueGeneratorService «Матрёшка» | `Modules/Generator/TechniqueGeneratorService.cs` | ✅ (баг: Cultivation cap=0) |
| TechniqueService (изучение/кулдауны/расход Ци) | `Modules/Combat/TechniqueService.cs` | ✅ скелет, не подключён к игроку |
| CombatService (11-слойный damage pipeline, ExecuteAttack) | `Modules/Combat/CombatService.cs` | ✅ |
| FormationService (Drawing→Filling→Active→Depleted) | `Modules/Formation/FormationService.cs` | ✅ скелет, нет генератора/визуала |
| ChargerService (буфер/тепло/слоты) | `Modules/Charger/ChargerService.cs` | ✅ скелет, не интегрирован |
| PlayerCombatAdapter (Space → AttackIntentEvent) | `Modules/Player/PlayerCombatAdapter.cs` | ✅ |

## 2. Чего НЕТ (что строим)

1. Игрок не имеет техник: Space = `basic_attack` (кулак, 10 урона). Нет слотов техник, нет панели, нет каста.
2. Нет визуализации техник (любой).
3. FormationService.FindFormationData — заглушка; нет генератора формаций, нет данных (форма/размер/стихия/эффекты).
4. Нет отображения формаций на земле.
5. Нет камней Ци как предметов (Q12: ItemCategory без QiStone — добавить категорию/предметы).
6. Нет чит-меню.
7. V (медитация) — action зарегистрирован, эффекта нет.
8. Нет Qi HUD (полоска Ци у игрока).

## 3. Канонические формулы (из документации — не изобретать)

- `qiDensity = 2^(level-1)`; `coreCapacity = 1000 × 1.1^totalSubLevels`; `effectiveQi = coreCapacity × density` (QI_SYSTEM).
- `capacity(техника) = baseCapacity(type) × 2^(level-1) × (1 + mastery×0.005)` (TECHNIQUE_SYSTEM §4.2).
- `qiCost = floor(baseCapacity × 2^(level-1))` — Grade НЕ влияет (TECHNIQUE_SYSTEM §5.2).
- `finalDamage = capacity × gradeMultiplier` (×2.0 Ultimate).
- Grade множители техник: {1.0, 1.3, 1.6, 2.0}; распределение {60,30,9,1} (в коде уже так).
- Слоты техник: Cultivation 1, Combat 3+(L−1), Curse 1, Formation 1 (TECHNIQUE_SYSTEM §12).
- Ограничение уровня: minL = max(1, L_практика − 4); maxL = L_практика (§8.1).
- Формации: `contourQi = 80 × 2^(level-1)`; `capacity = contourQi × sizeMult {small:10, medium:50, large:200, great:1000, heavy:10000}` (FORMATION_SYSTEM §6-7).
- Утечка: интервал в тиках по уровню {L1-2:60, L3-4:40, L5-6:20, L7-8:10, L9:5}; Ци за раз по размеру {1,3,10,30,100} (§8).
- Камни Ци: объём = 1024 ед/см³; размеры dust..boulder; calm/chaotic; БЕЗ стихии (GENERATORS_SYSTEM §10).
- Цвета стихий для визуала: fire красный, water синий, earth коричневый, air серый, lightning жёлтый, void фиолетовый, light золотой, neutral белый (ELEMENTS_SYSTEM §2).

## 4. Этапы (каждый — чекпоинт + коммит)

### Этап 1. Выдача техник игроку + слоты + Ци HUD + медитация
- `PlayerTechniqueGranter` (или расширение PlayerModule): при старте новой игры — тест-набор техник через TechniqueGeneratorService (роль Cultivator), заполнение слотов по правилам §12, minL/maxL по §8.1.
- Фикс бага Cultivation cap=0 (baseCapacity=0 → qiCost=0 → генерация невалидна; Cultivation — пассивная, capacity=null; в таблице оставить, но генератору не выдавать Cultivation в тест-набор).
- Слоты техник в TechniqueService: учитывать лимиты (Combat 3+(L-1) и т.д.).
- Qi HUD: полоска Ци (текущее/Max) рядом с HP-баром, цвет золотой.
- V = медитация: поглощение Ци из среды со скоростью conductivity (ед/сек, × envMult=0.5 обычная), тост статуса, движение/бой прерывает.
- **Чекпоинт:** `08_23_qi_stage1_techniques_grant.md`

### Этап 2. UI техник + каст + расход Ци + кулдауны
- `TechniquesPanel` (клавиша T): non-modal панель, слоты по типам, карточка техники (имя, стихия, грейд, L, урон, Ци, кулдаун, мастерство), LMB = выбрать/каст.
- Каст боевых техник: цель = ближайший NPC в Range техники (для melee) или направление на курсор (для ranged) → AttackIntentEvent(techniqueId) → CombatModule → damage pipeline. Расход Ци через TechniqueService.UseTechnique (QiConsumeRequestEvent).
- Healing/Support: прямой эффект (лечение по BaseDamage×0.5, бафф-тост) + кулдаун. Formation-техника: стартует создание формации (этап 5).
- Клавиши: Q = цикл выбора техники, R = каст выбранной (по направлению курсора), T = панель.
- Кулдауны: визуальная индикация (затемнение слота, сек).
- **Чекпоинт:** `08_23_qi_stage2_techniques_ui.md`

### Этап 3. Схематические визуальные эффекты техник
- `TechniqueEffectRenderer` (Node2D, _Draw — без PNG, принцип проекта):
  - Directional (снаряд): движущийся круг + шлейф от игрока к цели, цвет стихии.
  - Expanding (AoE/взрыв): растущая окружность с альфа-затуханием.
  - SelfAura (бафф/медитация): пульсирующее кольцо вокруг игрока.
  - Heal: зелёное расширяющееся кольцо.
  - Shield (Defense): дуга/круг вокруг игрока.
- Подписка на TechniqueUsedEvent/DamageAppliedEvent; pooling через Queue<Visual>; длительность ~0.6-1.0 c.
- **Чекпоинт:** `08_23_qi_stage3_technique_visuals.md`

### Этап 4. Генератор формаций
- `FormationGenerator` (Modules/Generator): SeededRandom; «Матрёшка»: тип (8: Barrier/Trap/Amplification/Suppression/Gathering/Detection/Teleportation/Summoning) × размер (5) × уровень (1-9) × стихия × форма (circle/triangle/square/pentagon/star/hexagram).
- Расширить FormationData: Shape, Element, FormationKind (8 типов), Size, contourQi, capacity, leakIntervalTicks, leakAmount, эффекты зоны.
- `FormationRegistry` (аналог TechniqueRegistry). Названия: «[Стихия] [Тип] формация [формы] L{n}».
- **Чекпоинт:** `08_23_qi_stage4_formation_generator.md`

### Этап 5. Создание формаций игроком + жизненный цикл
- Formation-техника в слоте → каст (R/клик) → FormationService.StartDrawing в точке игрока: расход contourQi (уже реализовано), стадия Drawing (схематично: 2 сек) → Filling: автонаполнение от практики = conductivity ед/сек, прогресс-события → 100% → Active.
- Утечка: тиковая обработка (интервалы по уровню формации).
- Эффекты активной формации в радиусе действия (схематично):
  - Gathering: envMult ×2 для медитации в зоне.
  - Barrier: поглощение урона игроку в зоне (пока есть Ци в пуле).
  - Amplification: +20% урона техник игрока в зоне.
  - Suppression: −30% скорость врагов в зоне.
- Depleted → исчезает (без ядра) + событие.
- **Чекпоинт:** `08_23_qi_stage5_formation_lifecycle.md`

### Этап 6. Отображение формаций на поверхности
- `FormationVisualRenderer` (Node2D, _Draw): контур по Shape (золотой пунктир при Drawing; контур+руны-узлы при Filling с заливкой по прогрессу, цвет стихии; свечение/пульсация при Active; серый тусклый при Depleted).
- Руны: точки/простые глифы на вершинах + центральный символ; прогресс наполнения — дуга вокруг центра; текст названия+% при наведении не нужен (схематично).
- **Чекпоинт:** `08_23_qi_stage6_formation_visuals.md`

### Этап 7. Камни Ци + чит-меню развития ядра
- `QiStoneData` + генерация (размер по объёму: 1024 ед/см³; calm 90%/chaotic 10%; chaotic — риск: −10% HP при использовании, по доке «опасна»).
- Камни как предметы инвентаря (ItemCategory.QiStone — новая категория, Q12 снят) + RMB инфо + использование: канал поглощения = conductivity ед/сек до исчерпания камня (V-медитация ускоряет ×1.5).
- `CheatPanel` (клавиша F1, #if DEBUG): уровень L1..L9, Ци: fill/clear/+10000, прорыв, выдать N случайных техник, очистить техники, выдать 3 камня Ци, создать тест-формацию, скорость утечки ×10 toggle.
- **Чекпоинт:** `08_23_qi_stage7_cheats_qistones.md`

### Этап 8. Финал
- Полный headless-прогон (GODOT_NEWGAME=1), сверка логов, фикс регрессий.
- Обновить SESSION_CONTEXT.md, SESSION_SUMMARY.md, worklog.md.
- Push на GitHub.

## 5. Вне рамок (отложено, документация не редактируется)

- Зарядники ЦИ как экипировка (ChargerService интеграция со слотом charger, камни в слоты, буфер в бою, тепло) — отдельное внедрение.
- Формации с физическим ядром (диски/алтари, многоразовые) — сейчас только вариант А (одноразовая без ядра).
- Помощники при наполнении формаций (multi-practitioner) — сейчас один создатель.
- Дестабилизация (переполнение техники) — требует отдельного пайплайна.
- Спрайты техник PNG (sprite-swap каталог) — схематический _Draw достаточно для цели «пока что схематическое».

## 6. Правила (НЕ нарушать)

1. Qi = long (ЗАПРЕТ 2); combat = integer permil (ЗАПРЕТ 3.9).
2. Hub-and-Spoke: кросс-модульное — только EventBus, readonly struct события.
3. Документация первична — НЕ редактировать.
4. Custom _Draw для рендера (без PNG, TileMapLayer отложен).
5. Save/load отключён (Q8).
6. Проверенные фиксы не ломать (возврат экипировки через событие, resume после диалога, modalOpen-гвард).
