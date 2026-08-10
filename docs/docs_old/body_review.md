# 🦴 Обзор системы тела и развития (Body Review)

**Версия:** 5.0
**Создано:** 2026-03-21
**Обновлено:** 2026-03-22
**Статус:** 📋 Морфология тела (Уровень 2 в иерархии)

---

## ⚠️ ВАЖНОЕ ЗАМЕЧАНИЕ

> **Этот документ — ВТОРИЧНЫЙ по отношению к [soul-system.md](./soul-system.md).**
> 
> Классификация здесь (Morphology) является Уровнем 2 в иерархии:
> - **Уровень 1:** SoulType (character, creature, spirit, construct) — см. soul-system.md
> - **Уровень 2:** Morphology (humanoid, quadruped, bird) — этот документ
> - **Уровень 3:** Species (human, elf, wolf, dragon) — см. body_monsters.md

---

## 📋 Обзор документа

Документ описывает **морфологию тела** (внешнюю форму) и механики:
- **Двойная HP** — функциональная и структурная (Kenshi-style)
- **Буфер Ци** — поглощение урона техниками Ци
- **Развитие характеристик** — виртуальная дельта и пороги

---

## 1️⃣ Иерархия системы тела

### 1.1 Место в общей архитектуре

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         ИЕРАРХИЯ ТИПОВ СУЩНОСТЕЙ                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  УРОВЕНЬ 1: SoulType (soul-system.md) — ПЕРВИЧНЫЙ                           │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │ character  │ organic body + core Qi + full mind                      │    │
│  │ creature   │ organic body + core Qi + instinct mind                  │    │
│  │ spirit     │ ethereal body + reservoir Qi + full mind                │    │
│  │ construct  │ construct body + reservoir Qi + simple mind             │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                              │                                               │
│                              ▼                                               │
│  УРОВЕНЬ 2: Morphology (ЭТОТ ДОКУМЕНТ) — ВТОРИЧНЫЙ                          │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │ humanoid   │ Двурукое двуногое                                       │    │
│  │ quadruped  │ Четвероногое                                            │    │
│  │ bird       │ Крылатое                                                │    │
│  │ serpentine │ Змееподобное                                            │    │
│  │ amorphous  │ Бесформенное (духи)                                     │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                              │                                               │
│                              ▼                                               │
│  УРОВЕНЬ 3: Species (body_monsters.md) — КОНКРЕТНЫЙ                         │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │ elf        │ character + humanoid + organic                          │    │
│  │ wolf       │ creature + quadruped + organic                          │    │
│  │ ghost      │ spirit + amorphous + ethereal                           │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Морфология тела (УРОВЕНЬ 2)

| Morphology | Описание | Части тела | Примеры Species |
|------------|----------|------------|-----------------|
| `humanoid` | Двурукое двуногое | 11 частей + сердце | Человек, Эльф, Демон, Великан |
| `quadruped` | Четвероногое | 8 частей + сердце | Волк, Тигр, Медведь, Дракон |
| `bird` | Крылатое | 6-7 частей | Орёл, Феникс |
| `serpentine` | Змееподобное | 6 частей + сегменты | Змея, Ламия |
| `arthropod` | Членистоногое | Экзоскелет | Паук, Многоножка, Скорпион |
| `amorphous` | Бесформенное | 2 части (core + essence) | Призрак, Элементаль |
| `hybrid_*` | Гибридные формы | Зависит от типа | Кентавр, Русалка, Гарпия |

### 1.3 Материалы тела

| Материал | Твёрдость | Снижение урона | SoulType |
|----------|-----------|----------------|----------|
| `organic` | 3 | 0% | character, creature |
| `scaled` | 6 | 30% | creature (рептилии) |
| `chitin` | 5 | 20% | creature (членистоногие) |
| `ethereal` | 1 | 70% физики | spirit |
| `mineral` | 8 | 50% | construct |
| `chaos` | 5 | переменно | spirit (аномалии) |

### 1.4 Таблица соответствия SoulType → Morphology → Material

| Species | SoulType (L1) | Morphology (L2) | Material |
|---------|---------------|-----------------|----------|
| Человек | `character` | `humanoid` | `organic` |
| Эльф | `character` | `humanoid` | `organic` |
| Демон | `character` | `humanoid` | `organic` |
| Великан | `character` | `humanoid` | `organic` |
| Зверолюд | `character` | `humanoid` | `organic` |
| Волк | `creature` | `quadruped` | `organic` |
| Тигр | `creature` | `quadruped` | `organic` |
| Дракон | `creature` | `quadruped` | `scaled` |
| Феникс | `creature` | `bird` | `scaled` |
| Змея | `creature` | `serpentine` | `scaled` |
| Паук | `creature` | `arthropod` | `chitin` |
| Многоножка | `creature` | `arthropod` | `chitin` |
| Скорпион | `creature` | `arthropod` | `chitin` |
| Призрак | `spirit` | `amorphous` | `ethereal` |
| Элементаль | `spirit` | `amorphous` | `ethereal` |
| Небесный дух | `spirit` | `amorphous` | `ethereal` |
| Голем | `construct` | `humanoid` | `mineral` |
| Кентавр | `character` | `hybrid_centaur` | `organic` |
| Русалка | `character` | `hybrid_mermaid` | `organic` |
| Оборотень | `character` | `humanoid` + transform | `organic` |
| Хаос | `spirit` | `amorphous` | `chaos` |

---

## 2️⃣ Система двойной HP (Kenshi-style)

### 2.1 Механика

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    HP КОНЕЧНОСТИ (Пример: Рука)                              │
├─────────────────────────────────────────────────────────────────────────────┤
│   ████████████████████░░░░░░░░░░░░░░░░░░░░  Красная HP (40)                 │
│   │←── Функциональная HP ──→│                (паралич при 0)                │
│                                                                              │
│   ████████████████████████████████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  │
│   │←────────── Структурная HP (80) ──────────────→│ (отрубание при 0)       │
│                                                                              │
│   Соотношение: Структурная HP = Функциональная HP × 2                        │
│   ИСКЛЮЧЕНИЕ: Сердце имеет только красную HP!                               │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Части тела и базовые HP

| Часть | Функциональная HP | Структурная HP | Функции |
|-------|-------------------|----------------|---------|
| **Голова** | 50 | 100 | sensory, breathing |
| **Торс** | 100 | 200 | circulation, digestion |
| **Сердце** | 80 | — | ❤️ только красная HP! |
| **Рука** | 40 | 80 | manipulation, attack |
| **Нога** | 50 | 100 | movement |

### 2.3 Формула расчёта HP

```typescript
function calculatePartHP(
  baseHP: number,
  sizeMultiplier: number,
  vitality: number,
  cultivationLevel: number,
  materialHardness: number
): { functionalHP: number; structuralHP: number } {
  // Множитель живучести
  const vitalityMultiplier = 1 + (vitality - 10) * 0.05;
  
  // Бонус культивации (независимый от тела)
  const cultivationBonus = 1 + (cultivationLevel - 1) * 0.1;
  
  // Множитель материала (твёрдость)
  const materialMultiplier = 1 + (materialHardness - 3) * 0.1;
  
  // Итоговая функциональная HP
  const functionalHP = Math.floor(
    baseHP * sizeMultiplier * vitalityMultiplier * cultivationBonus * materialMultiplier
  );
  
  // Структурная HP = Функциональная × 2
  const structuralHP = functionalHP * 2;
  
  return { functionalHP, structuralHP };
}
```

---

## 3️⃣ ⭐ НОВАЯ МЕХАНИКА: Ци как буфер урона

### 3.1 Концепция

**Проблема:** Ци не участвует в получении урона. Практик с высоким уровнем Ци уязвим так же, как смертный.

**Решение:** Ци действует как "энергетический щит", поглощая урон от техник Ци.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    БУФЕР ЦИ (Qi Buffer System)                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   Атака техникой Ци (100 урона)                                             │
│       │                                                                      │
│       ▼                                                                      │
│   ┌───────────────────────────────────────────────────────────────────┐     │
│   │ БУФЕР ЦИ (без щитовой техники) — СЫРАЯ ЦИ                         │     │
│   │                                                                    │     │
│   │ ⚠️ ВАЖНО: Сырая Ци поглощает только 90% урона!                     │     │
│   │                                                                    │     │
│   │ Поглощение: 90% от урона = 270 Ци (за 90 урона)                   │     │
│   │ Пробитие: 10% урона = 10 урона проходит в броню/HP                │     │
│   │ Соотношение: 1 поглощённый урон = 3 Ци                            │     │
│   │                                                                    │     │
│   │ Если Ци достаточно:                                               │     │
│   │   - Ци: -270 (поглощает 90 урона)                                 │     │
│   │   - HP: 10 урона (10% пробитие)                                   │     │
│   │                                                                    │     │
│   │ Если Ци недостаточно (например, 150 Ци):                          │     │
│   │   - Ци: 0                                                         │     │
│   │   - Поглощено: 150 / 3 = 50 урона                                 │     │
│   │   - Пробитие: 100 - 50 = 50 урона + 10% = 55 урона в HP          │     │
│   └───────────────────────────────────────────────────────────────────┘     │
│       │                                                                      │
│       ▼                                                                      │
│   ┌───────────────────────────────────────────────────────────────────┐     │
│   │ БРОНЯ (если есть)                                                  │     │
│   │                                                                    │     │
│   │ Снижение урона: DEF брони                                         │     │
│   │ Урон после брони = max(0, damage - armorDEF)                      │     │
│   └───────────────────────────────────────────────────────────────────┘     │
│       │                                                                      │
│       ▼                                                                      │
│   ┌───────────────────────────────────────────────────────────────────┐     │
│   │ HP ЧАСТИ ТЕЛА                                                      │     │
│   │                                                                    │     │
│   │ Урон по красной HP → структурной HP                               │     │
│   └───────────────────────────────────────────────────────────────────┘     │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Формула поглощения урона Ци

```typescript
interface QiBufferConfig {
  // Базовый множитель поглощения (без щитовой техники)
  baseQiAbsorptionRatio: 3.0;  // 1 поглощённый урон = 3 Ци
  
  // Процент поглощения сырой Ци (без щитовой техники)
  rawQiAbsorptionPercent: 0.90;  // 90% поглощается, 10% пробивает
  
  // Множитель при активной щитовой технике
  shieldTechniqueMultiplier: 1.0;  // 1 урон = 1 Ци (эффективнее!)
  
  // Щитовая техника поглощает 100%
  shieldAbsorptionPercent: 1.0;  // 100% поглощается
  
  // Минимальный порог Ци для активации буфера
  minQiForBuffer: 10;
}

function processQiDamage(
  incomingDamage: number,
  currentQi: number,
  maxQi: number,
  hasShieldTechnique: boolean,
  config: QiBufferConfig
): { qiConsumed: number; absorbedDamage: number; remainingDamage: number } {
  // Если Ци недостаточно для активации буфера
  if (currentQi < config.minQiForBuffer) {
    return { qiConsumed: 0, absorbedDamage: 0, remainingDamage: incomingDamage };
  }
  
  if (hasShieldTechnique) {
    // === ЩИТОВАЯ ТЕХНИКА: 100% поглощение ===
    const absorptionRatio = config.shieldTechniqueMultiplier;
    const requiredQi = incomingDamage * absorptionRatio;
    
    if (currentQi >= requiredQi) {
      // Полное поглощение щитом
      return { qiConsumed: requiredQi, absorbedDamage: incomingDamage, remainingDamage: 0 };
    } else {
      // Частичное поглощение (Ци кончилось)
      const absorbedDamage = currentQi / absorptionRatio;
      return { 
        qiConsumed: currentQi, 
        absorbedDamage,
        remainingDamage: incomingDamage - absorbedDamage 
      };
    }
  } else {
    // === СЫРАЯ ЦИ: 90% поглощение, 10% пробитие ===
    const absorptionPercent = config.rawQiAbsorptionPercent;  // 0.90
    const absorptionRatio = config.baseQiAbsorptionRatio;      // 3.0
    
    // Урон, который можно поглотить сырой Ци (90%)
    const absorbableDamage = incomingDamage * absorptionPercent;
    // Урон, который ВСЕГДА пробивает (10%)
    const piercingDamage = incomingDamage * (1 - absorptionPercent);
    
    // Требуемая Ци для поглощения 90%
    const requiredQi = absorbableDamage * absorptionRatio;
    
    if (currentQi >= requiredQi) {
      // Достаточно Ци для поглощения 90%
      return { 
        qiConsumed: requiredQi, 
        absorbedDamage: absorbableDamage,
        remainingDamage: piercingDamage  // 10% всегда пробивает!
      };
    } else {
      // Недостаточно Ци — поглощаем сколько можем
      const absorbedDamage = currentQi / absorptionRatio;
      const notAbsorbed = absorbableDamage - absorbedDamage;
      return { 
        qiConsumed: currentQi, 
        absorbedDamage,
        remainingDamage: notAbsorbed + piercingDamage 
      };
    }
  }
}
```

### 3.3 Сравнение сценариев (новая механика 90%)

| Сценарий | Урон | Ци до | Ци после | Поглощено | Пробитие в HP |
|----------|------|-------|----------|-----------|---------------|
| **Сырая Ци, достаточно** | 100 | 500 | 230 | 90 (270 Ци) | **10** |
| **Сырая Ци, мало** | 100 | 150 | 0 | 50 (150 Ци) | **50** |
| **Щит, достаточно** | 100 | 500 | 400 | 100 (100 Ци) | **0** |
| **Щит, мало** | 100 | 50 | 0 | 50 (50 Ци) | **50** |
| **Смертный (нет Ци)** | 100 | 0 | 0 | 0 | **100** |

⚠️ **Ключевое отличие:** Сырая Ци ВСЕГДА пропускает 10% урона!

### 3.4 Почему сырая Ци пропускает 10%

**Дизайн-решение:**
1. **Броня нужна всем** — даже L8 мастер получает урон через Ци
2. **Щит-техника ценнее** — разница между 90% и 100% критическая
3. **Реалистичность** — "сырая" Ци не идеальный щит
4. **Тактика богаче** — нельзя игнорировать броню на высоких уровнях

### 3.5 Интеграция со щитовыми техниками

```typescript
interface ShieldTechniqueBonus {
  // Множитель эффективности (меньше = лучше)
  qiCostPerDamage: number;  // 1.0 вместо 3.0
  
  // Дополнительное снижение урона
  damageReduction: number;  // % снижения урона
  
  // Поглощение элементального урона
  elementalResistance: Record<Element, number>;
  
  // Стоимость поддержания
  qiCostPerSecond: number;
}

const SHIELD_TECHNIQUES: Record<string, ShieldTechniqueBonus> = {
  'qi_barrier': {
    qiCostPerDamage: 1.5,
    damageReduction: 0,
    elementalResistance: {},
    qiCostPerSecond: 1,
  },
  'protective_dome': {
    qiCostPerDamage: 1.0,
    damageReduction: 0.2,
    elementalResistance: {},
    qiCostPerSecond: 3,
  },
  'elemental_shield': {
    qiCostPerDamage: 1.0,
    damageReduction: 0,
    elementalResistance: { fire: 0.5, water: 0.5 },
    qiCostPerSecond: 2,
  },
};
```

---

## 4️⃣ Интеграция с бронёй

### 4.1 Порядок применения защит

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    ПОРЯДОК ПРИМЕНЕНИЯ ЗАЩИТ                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   1. УКЛОНЕНИЕ (Agility)                                                    │
│      └── Успех → урон = 0                                                   │
│      └── Провал → продолжаем                                                │
│                                                                              │
│   2. ПАРИРОВАНИЕ (Weapon/Shield)                                            │
│      └── Успех → урон снижен на % блока                                     │
│      └── Провал → продолжаем                                                │
│                                                                              │
│   3. БУФЕР ЦИ (Qi Buffer) — ТОЛЬКО для техник Ци!                          │
│      └── Поглощение: урон × 3 (без щита) или × 1 (со щитом)                │
│      └── Пробитие → продолжаем                                              │
│                                                                              │
│   4. БРОНЯ (Armor)                                                          │
│      └── Снижение: урон - DEF                                               │
│      └── Минимальный урон: 1                                                │
│                                                                              │
│   5. МАТЕРИАЛ ТЕЛА (Body Material)                                          │
│      └── Твёрдость материала снижает урон                                   │
│      └── Пример: scales (6) → -30% урона                                   │
│                                                                              │
│   6. HP ЧАСТИ ТЕЛА                                                          │
│      └── Красная HP → Структурная HP                                        │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 4.2 Формула итогового урона

```typescript
function calculateFinalDamage(
  baseDamage: number,
  isQiTechnique: boolean,
  attacker: AttackerStats,
  defender: DefenderStats
): DamageResult {
  let damage = baseDamage;
  
  // 1. Уклонение
  const dodgeChance = calculateDodgeChance(defender.agility, attacker.agility);
  if (Math.random() < dodgeChance) {
    return { finalDamage: 0, qiConsumed: 0, avoided: 'dodge' };
  }
  
  // 2. Парирование (если есть оружие/щит)
  if (defender.hasWeapon || defender.hasShield) {
    const parryChance = calculateParryChance(defender);
    if (Math.random() < parryChance) {
      damage *= 1 - defender.blockPercent;
    }
  }
  
  // 3. Буфер Ци (только для техник Ци)
  let qiConsumed = 0;
  if (isQiTechnique && defender.currentQi > 0) {
    const result = processQiDamage(
      damage,
      defender.currentQi,
      defender.maxQi,
      defender.hasActiveShieldTechnique,
      QI_BUFFER_CONFIG
    );
    qiConsumed = result.qiConsumed;
    damage = result.remainingDamage;
  }
  
  // 4. Броня
  if (defender.armor) {
    damage = Math.max(1, damage - defender.armor.defense);
  }
  
  // 5. Материал тела
  const materialReduction = (defender.bodyMaterialHardness - 3) * 0.1;
  damage = Math.max(1, damage * (1 - materialReduction));
  
  return { 
    finalDamage: Math.floor(damage), 
    qiConsumed,
    avoided: damage === 0 ? 'blocked' : null 
  };
}
```

### 4.3 Типы брони и их взаимодействие с Ци

| Тип брони | DEF | Слот | Влияние на Ци |
|-----------|-----|------|---------------|
| **Ткань** | 1-3 | torso | Нет |
| **Кожа** | 3-5 | torso, legs | Нет |
| **Кольчуга** | 5-10 | torso | -10% проводимости |
| **Латы** | 10-20 | torso, legs, arms | -30% проводимости |
| **Броня практика** | 5-15 | torso | +5% проводимости |
| **Духовная броня** | 3-8 | torso | +20% проводимости |

---

## 5️⃣ Система развития характеристик

### 5.1 Виртуальная дельта и пороги развития

```typescript
interface StatDevelopment {
  current: number;        // Текущее значение
  virtualDelta: number;   // Накопленная виртуальная дельта
  threshold: number;      // Порог для повышения (вычисляемый)
}

// Формула порога: аналог с ядром культивации
function calculateStatThreshold(currentStat: number): number {
  // Чем выше стат, тем больше нужно для повышения
  return Math.max(1.0, Math.floor(currentStat / 10));
}

// Примеры:
// Стат 10 → 11: нужно 1.0 виртуальной дельты
// Стат 20 → 21: нужно 2.0 виртуальной дельты
// Стат 50 → 51: нужно 5.0 виртуальной дельты
// Стат 100 → 101: нужно 10.0 виртуальной дельты
```

### 5.2 Источники развития

| Источник | Прирост | Характеристика |
|----------|---------|----------------|
| **Удар в бою** | +0.001 | Сила |
| **Уклонение** | +0.001 | Ловкость |
| **Блок/Парирование** | +0.0005 | Сила/Ловкость |
| **Использование техники** | +0.0005 | Интеллект |
| **Получение урона + лечение** | +0.005 | Живучесть |
| **Тренировка (1 мин)** | +0.002 | По выбору |
| **Медитация (1 мин)** | +0.01 | Интеллект |

### 5.3 Закрепление при сне

```typescript
const MAX_CONSOLIDATION_PER_SLEEP = 0.20;  // За 8 часов

function calculateMaxConsolidation(sleepHours: number): number {
  if (sleepHours < 4) return 0;
  // 4 часа = +0.067, 6 часов = +0.133, 8 часов = +0.20
  const base = 0.067;
  const perHour = 0.033;
  return Math.min(MAX_CONSOLIDATION_PER_SLEEP, base + (sleepHours - 4) * perHour);
}

function processSleep(
  stats: CharacterStatsDevelopment,
  sleepHours: number
): void {
  const maxConsolidation = calculateMaxConsolidation(sleepHours);
  
  for (const stat of Object.values(stats)) {
    // Закрепление с учётом порогов
    while (stat.virtualDelta >= stat.threshold && maxConsolidation > 0) {
      stat.current += 1;
      stat.virtualDelta -= stat.threshold;
      stat.threshold = calculateStatThreshold(stat.current);
    }
  }
}
```

### 5.4 Достижимость за периоды

| Период | Достижимый стат | Время на +1 |
|--------|-----------------|-------------|
| 1000 дней | ~55 | 21-28 дней |
| 3000 дней | ~80 | 42-56 дней |
| 10000 дней | ~125 | 70+ дней |

---

## 6️⃣ ⭐ Развитие ядра Ци (Core Capacity)

### 6.1 Формула вместимости ядра

**Источник:** `src/services/cheats.service.ts` (строки 218-220)

```typescript
/**
 * Расчёт вместимости ядра культивации
 * @param level - уровень культивации (1-9)
 * @param subLevel - подуровень (0-9)
 * @returns максимальная вместимость ядра в единицах Ци
 */
function calculateCoreCapacity(level: number, subLevel: number): number {
  const BASE_CAPACITY = 1000;                    // Базовая ёмкость L1.0
  const totalLevels = (level - 1) * 10 + subLevel;  // Всего пройденных подуровней
  return Math.floor(BASE_CAPACITY * Math.pow(1.1, totalLevels));
}
```

**Принцип:** Каждый подуровень увеличивает ёмкость на 10%.

### 6.2 Таблица вместимости по уровням

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    ВМЕСТИМОСТЬ ЯДРА ПО УРОВНЯМ                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   Уровень   │ totalLevels │ 1.1^N   │ coreCapacity  │ Плотность Ци          │
│   ──────────┼─────────────┼─────────┼───────────────┼───────────────────    │
│   L1.0      │ 0           │ 1.0     │ 1,000         │ 1 ед/см³              │
│   L2.0      │ 10          │ 2.59    │ 2,594         │ 2 ед/см³              │
│   L3.0      │ 20          │ 6.73    │ 6,727         │ 4 ед/см³              │
│   L4.0      │ 30          │ 17.45   │ 17,450        │ 8 ед/см³              │
│   L5.0      │ 40          │ 45.26   │ 45,260        │ 16 ед/см³             │
│   L6.0      │ 50          │ 117.39  │ 117,390       │ 32 ед/см³             │
│   L7.0      │ 60          │ 304.48  │ 304,480       │ 64 ед/см³             │
│   L8.0      │ 70          │ 789.75  │ 789,750       │ 128 ед/см³            │
│   L9.0      │ 80          │ 2048.4  │ 2,048,400     │ 256 ед/см³            │
│                                                                              │
│   Плотность Ци = 2^(level-1) ед/см³                                         │
│   Максимальная Ци (перегрузка) = coreCapacity × 2                           │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 6.3 Почему мастера "жирные" по Ци

**Дизайн-решение:** Мастера высоких уровней должны выдерживать длительные бои.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    ПРИМЕР: БОЙ МАСТЕРА L8 (механика 90%)                     │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   Атакующий: Техника L9 (урон 16,000, без элемента)                         │
│   Защищающийся: Практик L8.0 (Ци: 789,750)                                  │
│                                                                              │
│   СЫРАЯ ЦИ (без щитовой техники):                                           │
│   ─────────────────────────────────────                                      │
│   - Поглощение 90%: 14,400 урона → 43,200 Ци                                │
│   - Пробитие 10%: 1,600 урона → в броню/HP                                  │
│   - После удара: 789,750 - 43,200 = 746,550 Ци                              │
│   - HP урон: 1,600 (броня снижает)                                          │
│   - Выдерживает: ~18 ударов до истощения Ци                                 │
│   - НО: за бой получит ~28,800 урона в HP!                                  │
│                                                                              │
│   ЩИТОВАЯ ТЕХНИКА (100% поглощение):                                        │
│   ─────────────────────────────────────                                      │
│   - Поглощение 100%: 16,000 урона → 16,000 Ци                               │
│   - Пробитие: 0                                                             │
│   - После удара: 789,750 - 16,000 = 773,750 Ци                              │
│   - HP урон: 0                                                              │
│   - Выдерживает: ~49 ударов техникой L9                                     │
│                                                                              │
│   ═══════════════════════════════════════════════════════════════════════   │
│   ВЫВОД: Мастер L8 со щитом — неуязвим. Без щита — нужен танк!              │
│   Разница: со щитом 0 урона, без щита 1,600 урона за удар.                  │
│   ═══════════════════════════════════════════════════════════════════════   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 6.4 Генерация Ци микро-ядром

```typescript
/**
 * Скорость генерации Ци микро-ядром
 * @param coreCapacity - текущая вместимость ядра
 * @returns количество Ци, генерируемое в сутки
 */
function calculateDailyQiGeneration(coreCapacity: number): number {
  return Math.floor(coreCapacity * 0.1);  // 10% от вместимости в сутки
}

// Примеры:
// L1: 1,000 × 0.1 = 100 Ци/сутки
// L5: 45,260 × 0.1 = 4,526 Ци/сутки
// L8: 789,750 × 0.1 = 78,975 Ци/сутки
// L9: 2,048,400 × 0.1 = 204,840 Ци/сутки
```

### 6.5 Сравнение буфера Ци по уровням (механика 90%)

| Уровень | coreCapacity | Ударов L9 (сырая Ци) | Пробитие/удар | Ударов L9 (щит) |
|---------|--------------|----------------------|---------------|-----------------|
| L1 | 1,000 | 0 | — | 0 |
| L3 | 6,727 | 0 | — | 0 |
| L5 | 45,260 | 0-1 | 1,600 | 2-3 |
| L7 | 304,480 | 6 | 1,600 | 19 |
| **L8** | **789,750** | **18** | **1,600** | **49** |
| L9 | 2,048,400 | 42 | 1,600 | 128 |

⚠️ **Без щитовой техники:** каждый удар L9 техники наносит 1,600 урона в HP!

---

## 7️⃣ Живучесть (Vitality) и HP

### 7.1 Влияние на HP

```typescript
function calculateVitalityMultiplier(vitality: number): number {
  return 1 + (vitality - 10) * 0.05;
}

// Примеры:
// Vitality 10: ×1.0 (базовый)
// Vitality 20: ×1.5
// Vitality 30: ×2.0
// Vitality 50: ×3.0
// Vitality 100: ×5.5
```

### 7.2 Бонусы живучести

| Vitality | HP множитель | Регенерация | Сопротивление шоку |
|----------|--------------|-------------|---------------------|
| 10 | ×1.0 | 0% | 0% |
| 20 | ×1.5 | +10% | +5% |
| 30 | ×2.0 | +20% | +10% |
| 50 | ×3.0 | +40% | +20% |
| 75 | ×4.25 | +65% | +35% |
| 100 | ×5.5 | +90% | +50% |

---

## 8️⃣ Универсальность системы (Практики и Монстры)

### 8.1 Применимость механик

| Механика | Практик | Монстр | Дух |
|----------|---------|--------|-----|
| **Части тела** | ✅ | ✅ | ⚠️ (особые) |
| **Двойная HP** | ✅ | ✅ | ❌ |
| **Буфер Ци** | ✅ | ⚠️ (врождённый) | ✅ |
| **Развитие статов** | ✅ | ⚠️ (эволюция) | ✅ |
| **Броня** | ✅ | ⚠️ (естественная) | ❌ |
| **Тренировка** | ✅ | ❌ | ✅ |

### 8.2 Особенности монстров

```typescript
interface MonsterBodyConfig {
  // Естественная броня (чешуя, шкура)
  naturalArmor: number;
  
  // Врождённый буфер Ци (менее эффективный)
  innateQiBuffer: {
    enabled: boolean;
    efficiency: number;  // Обычно 0.5 (требует 6 Ци на 1 урон)
  };
  
  // Развитие через эволюцию
  evolutionGrowth: {
    strengthPerLevel: number;
    vitalityPerLevel: number;
  };
  
  // Материал тела
  bodyMaterial: 'flesh' | 'scaled' | 'mineral' | 'chaos';
}
```

### 8.3 Особенности духов

```typescript
interface SpiritBodyConfig {
  // Эфирное тело — нет физических частей
  hasPhysicalBody: false;
  
  // HP = Ци (единый ресурс)
  hpEqualsQi: true;
  
  // Уязвимости
  vulnerabilities: ('spiritual_damage', 'elemental_opposite');
  
  // Сильные стороны
  immunities: ('physical_damage', 'poison');
}
```

---

## 9️⃣ Баланс и числовые значения

### 9.1 Сводная таблица множителей

| Источник | Формула | Пример (L5, Vit 30) |
|----------|---------|---------------------|
| **Ядро культивации** | `STAT_MULTIPLIERS_BY_LEVEL[L]` | ×2.0 |
| **Тело (развитие)** | `currentStat / 10` | ×5.5 (стат 55) |
| **Живучесть** | `1 + (vit - 10) × 0.05` | ×2.0 |
| **Материал тела** | `1 + (hardness - 3) × 0.1` | ×1.3 (scaled) |
| **Буфер Ци** | `урон × 3` или `урон × 1` | Зависит от щита |

### 9.2 Пример расчёта танка

```
Практик L5.0, Сила 50, Живучесть 50
Ци: 45,260 (coreCapacity L5)
Броня: 15 DEF
Материал: flesh (твёрдость 3)

HP Торса:
- База: 100
- Размер (medium): ×1.0
- Живучесть 50: ×3.0
- Культивация L5: ×2.0
- Итого: 100 × 1.0 × 3.0 × 2.0 = 600 HP (функциональная)
         1200 HP (структурная)

Защита от атаки техникой Ци (100 урона) — СЫРАЯ ЦИ:
1. Поглощение 90%: 90 урона × 3 = 270 Ци
2. Пробитие 10%: 10 урона в броню/HP
   - После удара: 45,260 - 270 = 44,990 Ци
   - Броня 15: 10 - 15 = 0 (блокировано бронёй!)
   - Выдерживает: ~168 ударов по 100 урона (но каждый удар = 10 урона в броню!)

Защита от техники L9 (16,000 урона) — СЫРАЯ ЦИ:
1. Поглощение 90%: 14,400 × 3 = 43,200 Ци
   - Ци недостаточно! (45,260 < 43,200 × 3)
   - Можно поглотить: 45,260 / 3 = 15,087 урона
   - Но сырая Ци ограничена 90%: макс. поглощение = 14,400
   - Требуется Ци: 14,400 × 3 = 43,200 ✓ (хватает!)
2. Результат:
   - Ци: 45,260 - 43,200 = 2,060
   - Пробитие 10%: 1,600 урона
   - Броня 15: 1,600 - 15 = 1,585 урона в HP

Вывод: L5 может пережить ОДИН удар L9 техники, но получит 1,585 урона в HP.
         Без брони — 1,600 урона. Броня критически важна!
```

---

## 🔟 Рекомендации по реализации

### 10.1 Приоритеты

1. **P0 — Буфер Ци** — новая механика, ключевая для баланса
2. **P0 — HP с Vitality** — уже частично реализовано
3. **P1 — Интеграция брони** — требует пересчёта формул
4. **P1 — Система развития** — виртуальная дельта + пороги
5. **P2 — UI отображения** — визуализация буфера Ци

### 10.2 Файлы для изменения

| Файл | Изменения |
|------|-----------|
| `src/lib/game/body-system.ts` | Буфер Ци, расчёт урона |
| `src/lib/game/combat-system.ts` | Порядок применения защит |
| `src/lib/game/stat-development.ts` | Система порогов |
| `prisma/schema.prisma` | Поля развития |
| `src/components/game/CharacterStatus.tsx` | UI буфера Ци |

---

## 🔗 Связанные документы

- **[body.md](./body.md)** — Базовая система тела
- **[body-development-analysis.md](./body-development-analysis.md)** — Анализ развития
- **[development-1000-days-calculation.md](./development-1000-days-calculation.md)** — Расчёты
- **[stat-threshold-system.md](./stat-threshold-system.md)** — Система порогов
- **[vitality-hp-system.md](./vitality-hp-system.md)** — Vitality и HP
- **[combat-system.md](./combat-system.md)** — Боевая система

---

*Документ создан: 2026-03-21*
*Версия: 5.0*
*Статус: Синтез всех концепций + буфер Ци 90% + формула ядра + ⭐ подавление уровнем*

---

## 1️⃣1️⃣ ⭐ ПОДАВЛЕНИЕ УРОВНЕМ (NEW v5.0)

### 11.1 Проблема

> **Сырая Ци поглощает только 90% урона.**
> 
> Практик L8 получает 10% урона даже от L1 — противоречит жанру сянься.

### 11.2 Решение: Подавление по уровням

**Техника уровня N пробивает до N уровней разницы.**

```
Формула:
  levelDiff = defenderLevel - attackerLevel
  breakthrough = techniqueLevel
  effectiveDiff = max(0, levelDiff - breakthrough)
  
  attackType = isUltimate ? 'ultimate' : technique ? 'technique' : 'normal'
  multiplier = SUPPRESSION_TABLE[effectiveDiff][attackType]
```

### 11.3 Таблица подавления

| effectiveDiff | normal | technique | ultimate |
|---------------|--------|-----------|----------|
| 0 | ×1.0 | ×1.0 | ×1.0 |
| +1 | ×0.5 | ×0.75 | ×1.0 |
| +2 | ×0.1 | ×0.25 | ×0.5 |
| +3 | ×0 | ×0.05 | ×0.25 |
| +4 | ×0 | ×0 | ×0.1 |
| +5+ | ×0 | ×0 | ×0 |

### 11.4 Примеры

```
Практик L5, техника L5 атакует L8:
  levelDiff = 3, breakthrough = 5
  effectiveDiff = 0
  → ×1.0 урона (нет подавления)

Практик L1, техника L1 атакует L8:
  levelDiff = 7, breakthrough = 1
  effectiveDiff = 6
  → ×0 (ИММУНИТЕТ!)

Практик L8 атакует L9 (technique L8):
  levelDiff = 1, breakthrough = 8
  effectiveDiff = 0
  → ×1.0 урона
```

### 11.5 Ultimate-техники

**Флаг `isUltimate: true`:**
- Игнорируют 1 уровень разницы (×1.0 на +1)
- Улучшенная таблица подавления
- Только Transcendent grade

### 11.6 Интеграция в пайплайн урона

```
1. rawDamage = techniqueDamage × qiDensity × gradeMult
2. ⭐ levelSuppression (NEW!)
   damage ×= suppressionMultiplier
   if (multiplier === 0) return IMMUNE
3. hitPart = rollBodyPart()
4. Активная защита
5. Буфер Ци (90%)
6. Броня
7. Материал тела
8. Итоговый урон
```

**Подробнее:** [body_armor.md](./body_armor.md) раздел 11
