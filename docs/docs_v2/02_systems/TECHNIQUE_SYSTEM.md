# Система техник (Technique System)

> **Назначение:** Документ описывает все способности практика: боевые, защитные, лечебные, вспомогательные. Включает классификацию, систему Grade, структурную ёмкость (capacity), генерацию «Матрёшка», использование техник, мастерство, Ultimate-техники, дестабилизацию. Документ engine-agnostic: концепции, формулы, данные — без привязки к движку.
>
> **Статус (обновлено 2026-08-25, GLM-5.3):** §5 унифицирован под модель заполнения
> (charge-by-conductivity) — реализовано в `TechniqueChargeService` (Stage 0).
> Добавлена §5.4 «Аура-задержка» (вариант В) — реализовано в `AuraHoldService` (Stage 1).
> Код: HEAD соответствует этому разделу; прецедент — `checkpoints/08_25_technique_hold_analysis.md`.

**Связанные документы:** `02_systems/COMBAT_SYSTEM.md`, `02_systems/QI_SYSTEM.md`, `02_systems/ELEMENTS_SYSTEM.md`, `02_systems/TECHNIQUE_EFFECTS.md`, `09_workflow/ALGORITHMS.md`.

---

## 1. Обзор

Система техник описывает все способности практика: боевые, защитные, лечебные и вспомогательные. Техника — это **знание**, а не физический предмет: она не может быть «повреждена», в отличие от экипировки.

### Ключевые принципы

1. **Урон = Ёмкость × Grade.** `finalDamage = capacity × gradeMultiplier × ultimateMultiplier`. Это главное правило расчёта урона техник.
2. **Архитектура «Матрёшка».** Три слоя генерации техники: База → Grade → Бонусы.
3. **Система тиков.** Все эффекты измеряются в тиках (1 тик = 1 минута игрового времени). См. `03_world/TIME_SYSTEM.md`.

---

## 2. Классификация техник

### 2.1. Типы техник (9 основных + Cultivation)

| Тип | Префикс | Описание | baseCapacity |
|-----|---------|----------|--------------|
| Formation | (массивы) | Магические формации | 80 |
| Defense | DF | Защитные | 72 |
| Support | SP | Поддержка (баффы) | 56 |
| Healing | HL | Исцеление | 56 |
| Combat (melee_strike) | TC | Контактный удар | 64 |
| Combat (melee_weapon) | TC | Удар с оружием | 48 |
| Combat (ranged_*) | TC | Снаряд/луч/AoE | 32 |
| Movement | MV | Перемещение | 40 |
| Curse | CR | Проклятия | 40 |
| Poison | PN | Отравления | 40 |
| Sensory | SN | Восприятие | 32 |
| Cultivation | CU | Пассивная культивация | null |

> `Cultivation` — пассивная: `capacity = null`, `qiCost = 0`, работает во время медитации/отдыха.

### 2.2. Подтипы

- **Combat:** `melee_strike`, `melee_weapon`, `ranged_projectile`, `ranged_beam`, `ranged_aoe`.
- **Defense:** `shield`, `block`, `dodge`, `reflect`.
- **Curse:** `combat` (секунды-минуты), `ritual` (часы-месяцы).
- **Poison:** `body` (физическое тело), `qi` (меридианы и ядро).
- **Movement:** `Dash`, `Dodge`, `Teleport`, `Flight`.
- **Sensory:** `Detection`, `Analysis`, `Vision`, `Link`.

### 2.3. Уровни редкости (Basic/Advanced/Master/Legendary)

Техники делятся на 4 уровня редкости, определяющие доступность в мире:

| Уровень | Описание | Доступность |
|---------|----------|-------------|
| **Basic** | Базовые техники | Широко доступны, изучаются новичками |
| **Advanced** | Продвинутые | Доступны в сектах и у опытных практиков |
| **Master** | Мастерские | Редкие, передаются от мастера к ученику |
| **Legendary** | Легендарные | Уникальные, известны по имени |

> Уровень редкости не влияет на формулу урона напрямую — урон определяется `grade` и `capacity`. Уровень редкости влияет на доступность в мире, требования к изучению и цену.

---

## 3. Система Grade (Качество)

### 3.1. Уровни Grade (4 грейда)

| Grade | Урон | Бонусов | Шанс эффекта |
|-------|------|---------|--------------|
| Common | ×1.0 | 0 | 0% |
| Refined | ×1.3 | 1 | 20% |
| Perfect | ×1.6 | 2 | 50% |
| Transcendent | ×2.0 | 3 | 80% |

> У техник 4 грейда (без Damaged). Техника — знание, не предмет. **Стоимость Ци всегда ×1.0** — не зависит от Grade.

### 3.2. Распределение Grade (универсальное, не зависит от уровня)

| Grade | Шанс | Примечание |
|-------|------|------------|
| Common | 60% | Базовый |
| Refined | 28% | Улучшенный |
| Perfect | 10% | Редкий |
| Transcendent | 2% | Легендарный (даже на L1!) |

---

## 4. Структурная ёмкость (Capacity)

### 4.1. Принцип

Ёмкость техники — максимальное базовое Ци, которое техника может обработать.

### 4.2. Формула

```
capacity = baseCapacity(type) × 2^(level-1) × (1 + mastery/100 × 0.5)
```

Где:
- `baseCapacity(type)` — базовая ёмкость по типу техники (см. §2.1).
- `2^(level-1)` — множитель уровня техники.
- `masteryBonus = 1 + mastery/100 × 0.5` (диапазон 1.0..1.5).

### 4.3. Базовая ёмкость по типам

| Тип техники | baseCapacity |
|-------------|--------------|
| Formation | 80 |
| Defense | 72 |
| Support | 56 |
| Healing | 56 |
| Combat (melee_strike) | 64 |
| Combat (melee_weapon) | 48 |
| Combat (ranged_*) | 32 |
| Movement | 40 |
| Curse | 40 |
| Poison | 40 |
| Sensory | 32 |
| Cultivation | null (пассивная) |

### 4.4. Примеры расчёта

**melee_strike L5, mastery 0%:**

```
capacity = 64 × 2^4 × 1.0 = 1024 базового Ци
finalDamage = 1024 × gradeMultiplier × ultimateMultiplier
```

**ranged_projectile L9, mastery 100%:**

```
capacity = 32 × 256 × 1.5 = 12 288 базового Ци
```

**Combat-Melee L3, mastery 50%:**

```
capacity = 64 × 2^2 × (1 + 50 × 0.005)
         = 64 × 4 × 1.25
         = 320
```

---

## 5. Использование техники (V2 pipeline)

### 5.1. Порядок выполнения

```
1. ПРОВЕРКА УСЛОВИЙ
   ├── Кулдаун = 0?
   ├── Уровень культивации ≥ minCultivationLevel?
   │   └── minL = max(1, L(практик) - 4)
   └── Текущее Ци ≥ qiCost?
       │
       ├── FAIL → Возврат ошибки
       └── PASS ↓

2. РАСЧЁТ ПАРАМЕТРОВ
   ├── qiCost   = floor(baseCapacity × 2^(techniqueLevel - 1))
   ├── capacity = floor(baseCapacity × 2^(level-1) × (1 + mastery × 0.005))
   └── damage   = capacity × gradeMultiplier × ultimateMultiplier
       │
       ↓

3. ТРАТА ЦИ
   └── currentQi -= qiCost
       │
       ↓

4. УСТАНОВКА КУЛДАУНА
   └── cooldownRemaining = cooldown × 60 секунд
       │
       ↓

5. ПОВЫШЕНИЕ МАСТЕРСТВА
   └── mastery = min(100, mastery + 0.01)
       │
       ↓

6. ВОЗВРАТ РЕЗУЛЬТАТА
   └── TechniqueUseResult { success, damage, qiCost, ... }
```

### 5.2. Стоимость Ци

```
qiCost = floor(baseCapacity × 2^(techniqueLevel - 1))
```

| Тип техники | baseCapacity | L1 | L2 | L3 | L4 | L5 |
|-------------|--------------|----|----|----|----|----|
| Formation | 80 | 80 | 160 | 320 | 640 | 1 280 |
| Defense | 72 | 72 | 144 | 288 | 576 | 1 152 |
| Combat-Melee | 64 | 64 | 128 | 256 | 512 | 1 024 |
| Support | 56 | 56 | 112 | 224 | 448 | 896 |
| Movement | 40 | 40 | 80 | 160 | 320 | 640 |

### 5.3. Время каста

```
castTime = qiCost / effectiveSpeed

effectiveSpeed = conductivity × (1 + cultivationBonus) × (1 + masteryBonus)
```

**Пример:**

- `qiCost = 50`, `conductivity = 2.0`, `cultivationLevel = 3` (+10%), `mastery = 50%` (+50%).
- `effectiveSpeed = 2.0 × 1.10 × 1.50 = 3.3`
- `castTime = 50 / 3.3 = 15.15` секунд.

> **⚠️ Примечание (GLM-5.3, 2026-08-25):** Литеральный пример даёт 15 с — это
> **медитативный масштаб** (проводимость = coreCapacity/360 — полный цикл
> поглощения). Для боевого канала введён множитель `K = COMBAT_CHANNEL_MULT = 12`
> («боевой прогон меридиан» против медитативного поглощения):
>
> ```
> chargeRate = finalConductivity × K × (1 + mastery × 0.005)   [Ци/тик]
> fillTicks  = qiCost / chargeRate
> ```
>
> | K | L1 (64 qi, cond 2.8) | L3 (256, 18.7) | L5 (1024, 125.7) | L9 (16384, 5690) |
> |---|---|---|---|---|
> | ×12 (выбран) | 2.0 тика | 1.1 | 0.68 | 0.24 |
>
> Реализация: `Constants.COMBAT_CHANNEL_MULT`, `TechniqueChargeService.ComputeChargeRate`.
> Лёгкие реакции (Dodge/Block, малый qiCost) заполняются <0.3 тика — мгновенно.

---

## 5.4. Аура-задержка (вариант В, Stage 1 — реализовано 2026-08-25, GLM-5.3)

> **Источник:** `checkpoints/08_25_technique_hold_analysis.md` §4 (вариант В рекомендован).
> Реализация: `Modules/Player/AuraHoldService.cs`, `PlayerTechniqueCaster.OnChargeCompleted`.

### Принцип

ВСЕ техники (кроме Cultivation) могут «зависать» в ауре игрока после зарядки,
но **аура держит только ОДНУ**. Остальные срабатывают сразу по завершении зарядки.

### Поток (с Stage 0)

```
1. Z (TechniqueCastRequestedEvent):
   ├── если аура удерживает технику → Release + FireTechnique (второе нажатие)
   └── иначе → TechniqueChargeService.StartCharge (первое нажатие)

2. CombatModule.Tick → TechniqueChargeService.UpdateCharges
   └── drain chargeRate Ци/тик через QiConsumeRequestEvent

3. ChargedQi ≥ QiCost → TechniqueChargeCompletedEvent (potency=1000 на Stage 0)
   └── PlayerTechniqueCaster.OnChargeCompleted:
       ├── аура свободна → AuraHoldService.Hold (park, ждёт второго нажатия)
       └── аура занята → FireTechnique немедленно («остальные срабатывают сразу»)

4. Z повторно (аура удерживает) → Release → FireTechnique
   └── AttackIntentEvent(isCharged=true) → CombatService: пропуск pending-таймера
```

### Декей удержания

- `AURA_HOLD_DECAY_PERMIL = 10` (1% QiCost/тик) — удержание требует концентрации.
- При `ChargedQi < QiCost/2` → авто-рассеивание (возврат 50% остаточного Ци).
- Принудительное рассеивание: стюн/смерть/медитация (через `AuraHoldService.Dissipate`).

### Потency (окно перезарядки)

На Stage 1 potency всегда 1000‰ (базовая мощность). Окно перезарядки
[qiCost..capacity] → potency 1000→2000‰ + дестабилизация §7 — **Stage 2**
(опциональный план, см. `checkpoints/08_25_technique_hold_analysis.md` §8).

### NPC-паритет

NPC используют псевдо-технику `"npc_strike"` (NPCModule:132) без данных техники →
зарядка не применяется; NPC атакует через `CombatService.ExecuteAttack` с
pending-таймером (castTime по умолчанию 0.5 с). **Осознанная временная
асимметрия** — паритет NPC = Stage 2 (отдельный план).

### Связанные константы (`Constants.cs`)

- `COMBAT_CHANNEL_MULT = 12` — K-множитель боевого прогона
- `MIN_CHARGE_RATE = 1.0` — минимальная проводимость для зарядки
- `AURA_HOLD_DECAY_PERMIL = 10` — декей удержания (1%/тик)
- `POTENCY_BASE_PERMIL = 1000`, `POTENCY_MAX_PERMIL = 2000` — окно мощности

### Верификация

`GODOT_CHARGE_SIM=1` (headless): зарядка → hold → release → урон по NPC.
Ожидаемый вывод: `[ChargeSim] VERDICT: PASS — fill model + aura hold + release all wired`.

---

## 6. Архитектура «Матрёшка» (генерация техник)

Три слоя генерации техники:

### 6.1. Слой 1: База

- `qiCost` (по типу и уровню).
- `capacity` (по типу и уровню).
- `baseDamage = capacity` (V2: `baseDamage = qiCost`).

### 6.2. Слой 2: Grade

- Множитель урона (×1.0..×2.0).
- Количество бонусов (0..3).
- Шанс эффекта (0%..80%).

### 6.3. Слой 3: Бонусы

- Бонус 1: сила эффекта от Grade (0%..150%).
- Бонус 2: эффект от стихии (см. `02_systems/ELEMENTS_SYSTEM.md`).
- Бонус 3: Transcendent-эффект (только для Transcendent Grade) — уникальное свойство стихии.

> Генерация детерминирована через `SeededRandom` (см. `05_data/GENERATORS_SYSTEM.md`).

---

## 7. Дестабилизация (переполнение)

### 7.1. Принцип неделимости Ци

**Кратность выпуска Ци практиком = 1.** Практик выпускает Ци минимальными единицами своего уровня.

| Уровень практика | 1 единица Ци практика = базового Ци |
|------------------|-------------------------------------|
| L1 | 1 |
| L5 | 16 |
| L9 | 256 |

### 7.2. Переполнение

При `вливаемое Ци > ёмкость техники` → **ПЕРЕПОЛНЕНИЕ**.

**Пример:**

- Практик L9 использует технику L2 (`capacity = 128`).
- 1 единица Ци L9 = 256 базового Ци.
- Переполнение: 256 − 128 = 128 единиц.

**Результат:**

1. Излишки Ци (128) рассеиваются.
2. Урон практику = 128 × 0.5 = 64.
3. Урон по цели (только melee!) = 256 × 0.5 = 128.

### 7.3. Правило

> Переполнение ВОЗМОЖНО, но с последствиями. Для ranged атак Ци разлетается во все стороны — урона по цели НЕТ.

---

## 8. Ограничения по уровню

### 8.1. Лор: Резонанс Ци

Техника требует определённой плотности Ци для поддержания структуры канала.

**Минимальный уровень техники:** Практик может использовать техники с `L(min) = max(1, L(практик) − 4)`.

| Уровень практика | qiDensity | Мин. L техники | Макс. L техники |
|------------------|-----------|----------------|-----------------|
| L1 | 1 | L1 | L1 |
| L3 | 4 | L1 | L3 |
| L5 | 16 | L2 | L5 |
| L7 | 64 | L3 | L7 |
| L9 | 256 | L4 | L9 |

---

## 9. Ultimate-техники

### 9.1. Условия

- 5% шанс для Transcendent техник.
- Множитель урона: ×2.0 (`ultimateMultiplier = isUltimate ? 2.0 : 1.0`).
- Множитель стоимости Ци: ×2.0.
- Маркер в названии: ⚡.

### 9.2. Сочетание с level suppression

Ultimate-техники имеют усиленный множитель подавления уровнем (см. `02_systems/COMBAT_SYSTEM.md` §6).

---

## 10. Система мастерства

### 10.1. Прирост

```
masteryGained = max(0.1, baseGain × (1 - currentMastery / 100))
```

### 10.2. Влияние

| Мастерство | Бонус ёмкости |
|------------|---------------|
| 0% | +0% |
| 50% | +25% |
| 100% | +50% |

---

## 11. Подробное описание типов техник

### 11.1. Combat (Боевые)

| Подтип | Бонус от STR | Бонус от AGI | Бонус от INT |
|--------|--------------|--------------|--------------|
| melee_strike | 5% | 2.5% | — |
| melee_weapon | 2.5% | 5% | — |
| ranged_projectile | — | 2.5% | 5% |
| ranged_beam | — | 2.5% | 5% |
| ranged_aoe | — | 2.5% | 5% |

### 11.2. Defense (Защитные)

| Подтип | Механика |
|--------|----------|
| shield | Поглощение урона за счёт Ци |
| block | Снижение урона на % |
| dodge | Шанс уклонения |
| reflect | Отражение урона |

### 11.3. Support (Поддержка)

> **Важно:** Баффы НЕ увеличивают телесные характеристики (STR/AGI/VIT/INT)!

| Тип | Что баффает |
|-----|-------------|
| Damage | +% урона техник |
| Defense | +% сопротивления |
| Speed | +% передвижения |
| Crit | +% крит шанс |

### 11.4. Healing (Исцеление)

- `element = neutral` ВСЕГДА.
- БЕЗ стихийных бонусов.
- Эффективность зависит только от Grade и уровня.

### 11.5. Cultivation (Культивация)

- `element = neutral` ВСЕГДА.
- `capacity = null` (пассивная).
- `qiCost = 0`.
- Работает во время медитации/отдыха.

### 11.6. Curse (Проклятия)

- Длительность: секунды–минуты (боевые) или часы–месяцы (ритуальные).
- Типы: `weakness`, `slowness`, `silence`, `exhaustion`, `soul_burn`.

### 11.7. Poison (Яды)

- `element = poison` ТОЛЬКО.
- Несколько дебаффов по Grade (см. `02_systems/ELEMENTS_SYSTEM.md` §3).
- Способы доставки: `ingestion`, `contact`, `injection`, `inhalation`, `technique`.

### 11.8. Movement (Перемещение)

| Тип | Описание |
|-----|----------|
| Dash | Быстрый рывок |
| Dodge | Уклонение + неуязвимость |
| Teleport | Мгновенное перемещение |
| Flight | Поддержание полёта |

### 11.9. Sensory (Восприятие)

| Тип | Описание |
|-----|----------|
| Detection | Обнаружение живых существ |
| Analysis | Информация о цели |
| Vision | Видение сквозь препятствия |
| Link | Ментальная связь |

---

## 12. Слоты техник

| Слот | Количество | Примечание |
|------|------------|------------|
| Cultivation slot | 1 | Активная техника культивации |
| Combat slots | 3 + (level − 1) | Растёт с уровнем |
| Curse slot | 1 | Отдельный слот (баланс: нельзя загрузить все слоты проклятиями) |
| Formation slot | 1 | Активная формация |

> Проклятия в отдельном слоте — тактический выбор между проклятием и защитой.

---

## 13. Бонусы техник (3 уровня)

### 13.1. Бонус 1: Сила эффекта от Grade

| Grade | Бонус |
|-------|-------|
| Common | 0% |
| Refined | 50% |
| Perfect | 100% |
| Transcendent | 150% |

### 13.2. Бонус 2: Эффект от стихии

Определяется стихией и типом техники. См. `02_systems/ELEMENTS_SYSTEM.md`.

### 13.3. Бонус 3: Transcendent-эффект

Только для Transcendent Grade — уникальное свойство стихии.

---

## 14. Структура данных

```
TechniqueData (статичный ресурс данных):
  id:                  string
  displayName:         string
  type:                TechniqueType         // combat/defense/support/...
  subtype:             TechniqueSubtype      // melee_strike/ranged_beam/...
  element:             Element               // fire/water/...
  level:               int                   // 1..9
  baseCapacity:        int                   // из таблицы §4.3
  cooldown:            int                   // секунды
  minCultivationLevel: int                   // требование к уровню практика
  isUltimate:          bool

LearnedTechnique (инстанс для конкретного практика):
  data:                TechniqueData         // ссылка на ресурс
  grade:               TechniqueGrade        // common/refined/perfect/transcendent
  mastery:             float                 // 0..100 (%)
  cooldownRemaining:   float                 // секунды
  isUltimate:          bool                  // флаг, выпадает при transcendent
```

> Техники хранятся как ресурсы данных (библиотека техник) + экземпляры изученных техник у практика. См. `05_data/CONFIGURATIONS.md`.

---

## 15. Производительность

- Расчёт `capacity`, `qiCost`, `damage` выполняется один раз при использовании техники (кэшируется).
- Pooling `TechniqueUseResult` (zero-GC).
- Эффекты техник (визуальные) — через систему эффектов, см. `02_systems/TECHNIQUE_EFFECTS.md`.
- При `MaxActiveNPCs = 100` и типичной плотности боя: ~1 ms / tick на техники.

---

## 16. Источники

- `docs/TECHNIQUE_SYSTEM.md` (Unity-итерация, свёрено с кодом 2026-07-14)
- `docs/TECHNIQUE_USAGE_REPORT.md` (пайплайн использования техник)
- `docs_old/technique-system-v2.md` (Phaser-итерация, V2 концепт)
- `docs_old/combat-system.md` (Phaser-итерация, типы техник)
- `docs/ALGORITHMS.md` (формулы, источник истины)

---

*Документ engine-agnostic. Сохранены: 4 грейда техник, формула capacity, V2 pipeline (baseDamage=qiCost), «Матрёшка»-генерация, Ultimate-техники, дестабилизация, слоты техник.*
