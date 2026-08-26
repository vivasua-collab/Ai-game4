# ⚔️ Система Оружия и Брони — Теоретические изыскания

**Версия:** 1.0
**Создано:** 2026-03-14
**Статус:** 📋 Теоретические изыскания

---

## 📋 Обзор

Документ содержит теоретический анализ системы оружия и брони для мира культивации. Рассматриваются несколько вариантов реализации каждой механики.

---

## 1️⃣ СИСТЕМА ПРОЧНОСТИ (DURABILITY)

### 1.1 Параметры прочности

```typescript
interface DurabilityProps {
  current: number;      // Текущая прочность (0-max)
  max: number;          // Максимальная прочность
  hardness: number;     // Твёрдость материала (1-10)
  flexibility: number;  // Гибкость (0-1), влияет на шанс сломаться
}
```

### 1.2 Варианты потери прочности

**ВАРИАНТ A: При каждом использовании**
```
durabilityLoss = baseLoss × (1 - flexibility) × hardnessFactor
```
- Каждая атака/блок теряет 0.1-1.0 прочности
- Зависит от материала
- Простая реализация

**ВАРИАНТ B: При получении урона выше порога**
```
if (damageAbsorbed > hardness × 10) {
  durabilityLoss = damageAbsorbed / hardness / 10
}
```
- Прочность теряется только при сильных ударах
- Твёрдые материалы теряют меньше
- Более реалистично

**ВАРИАНТ C: Комбинированный**
```
// Базовый износ
baseLoss = 0.01 × actionCount

// Ударный износ
if (damageAbsorbed > threshold) {
  impactLoss = (damageAbsorbed - threshold) / hardness
}

totalLoss = baseLoss + impactLoss
```

### 1.3 Условия поломки

| Состояние | Прочность | Эффект |
|-----------|-----------|--------|
| Pristine | 100% | Полная эффективность |
| Good | 75-99% | Эффективность × 0.95 |
| Worn | 50-74% | Эффективность × 0.85 |
| Damaged | 25-49% | Эффективность × 0.70 |
| Critical | 1-24% | Эффективность × 0.50, шанс сломаться |
| Broken | 0% | Невозможно использовать |

**Шанс сломаться при Critical:**
```
breakChance = (1 - durability/maxDurability) × 0.1 × hitCount
// При 0% durability: 10% за каждый удар
```

### 1.4 Ремонт

**ВАРИАНТ A: NPC-кузнец**
- Фиксированная стоимость
- Восстановление до 100%
- Требует материалов

**ВАРИАНТ B: Самостоятельный ремонт**
```
repairAmount = skill × materials × time
maxRepair = originalMaxDurability × 0.9 // Теряется 10% макс.
```

**ВАРИАНТ C: Ци-ремонт (для духовного оружия)**
```
qiCost = (maxDurability - currentDurability) × qiDensity
// Высокие уровни культивации могут чинить духовным железом
```

---

## 2️⃣ ОРУЖИЕ И УРОН

### 2.1 Классификация оружия

```typescript
type WeaponClass = 
  | 'unarmed'      // Без оружия (кулаки, когти)
  | 'light'        // Лёгкое (кинжалы, короткие мечи)
  | 'medium'       // Среднее (мечи, топоры, копья)
  | 'heavy'        // Тяжёлое (двуручники, молоты)
  | 'ranged'       // Дальнобойное (луки, арбалеты)
  | 'magic';       // Магическое (посохи, жезлы)
```

### 2.2 Параметры оружия

```typescript
interface WeaponStats {
  // Базовый урон
  baseDamage: number;
  damageType: 'slashing' | 'piercing' | 'blunt' | 'elemental';
  
  // Модификаторы атаки
  attackSpeed: number;      // 0.5-2.0 (количество атак в секунду)
  range: number;            // 0.5-3.0 метра
  penetration: number;      // 0-100 (пробитие брони)
  
  // Требования
  strengthReq: number;      // Минимальная сила
  agilityReq: number;       // Минимальная ловкость
  
  // Прочность
  durability: DurabilityProps;
  
  // Слот 1 интеграция
  techniqueBonus?: {
    damageMultiplier: number;  // Бонус к урону техники
    qiCostReduction: number;   // Снижение стоимости Ци
  };
}
```

### 2.3 Расчёт урона оружия

**ВАРИАНТ A: Аддитивный**
```
totalDamage = handDamage + weaponDamage
handDamage = 3 + (STR-10) × 0.3
weaponDamage = baseWeaponDamage × conditionMultiplier × statScaling
```

**ВАРИАНТ B: Мультипликативный**
```
totalDamage = handDamage × weaponMultiplier
weaponMultiplier = 1 + (weaponDamage / 10)
// Оружие — множитель к руке, не добавка
```

**ВАРИАНТ C: Гибридный**
```
baseDamage = max(handDamage, weaponDamage × 0.5)
bonusDamage = weaponDamage × statScaling
totalDamage = baseDamage + bonusDamage

// Оружие всегда даёт минимум половину своего урона
// Плюс бонус от характеристик
```

### 2.4 Влияние на слот 1 (melee_strike техники)

```typescript
// Оружие с melee_strike техникой
function calculateWeaponTechniqueDamage(
  weapon: Weapon,
  technique: Technique,
  character: Character
): number {
  // Базовый урон техники
  const techDamage = calculateTechniqueDamage(technique, character);
  
  // Бонус от оружия
  const weaponBonus = weapon.baseDamage * (weapon.techniqueBonus?.damageMultiplier || 1);
  
  // Итог
  return techDamage + weaponBonus;
}
```

---

## 3️⃣ БРОНЯ И ЗАЩИТА

### 3.1 Части брони и защищаемые области

| Часть брони | Защищаемые части тела | Слот экипировки |
|-------------|----------------------|-----------------|
| Шлем (armor_head) | head | head |
| Нагрудник (armor_torso) | torso, heart | torso |
| Наручи (armor_arms) | left_arm, right_arm | arms |
| Перчатки (armor_hands) | left_hand, right_hand | hands |
| Поножи (armor_legs) | left_leg, right_leg | legs |
| Сапоги (armor_feet) | left_foot, right_foot | feet |
| Полный доспех (armor_full) | Все части | torso + conflicts |

### 3.2 Параметры брони

```typescript
interface ArmorStats {
  // Защита
  armor: number;           // Базовая броня (1-100)
  damageReduction: number; // Снижение урона % (0-80)
  
  // Покрытие
  coverage: number;        // % площади защищаемой части (50-100)
  
  // Типы урона
  resistances: {
    slashing: number;      // Сопротивление рубящему
    piercing: number;      // Сопротивление колющему
    blunt: number;         // Сопротивление дробящему
    elemental: number;     // Сопротивление стихиям
  };
  
  // Штрафы
  moveSpeedPenalty: number;  // Снижение скорости
  dodgePenalty: number;      // Штраф к уклонению
  qiFlowPenalty: number;     // Штраф к проводимости Ци
  
  // Прочность
  durability: DurabilityProps;
}
```

### 3.3 Порядок расчёта урона (8 слоёв)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         ПОРЯДОК РАСЧЁТА УРОНА                           │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  1. ИСХОДНЫЙ УРОН                                                        │
│     rawDamage = handDamage + weaponDamage + techniqueDamage              │
│                                                                          │
│  2. ОПРЕДЕЛЕНИЕ ЧАСТИ ТЕЛА                                               │
│     hitPart = rollBodyPartHit(attacker, target)                         │
│                                                                          │
│  3. ПРОВЕРКА УКЛОНЕНИЯ                                                   │
│     if (random() < dodgeChance) → damage = 0, END                       │
│                                                                          │
│  4. ПРОВЕРКА БЛОКА                                                       │
│     if (isBlocking && random() < blockChance) {                         │
│       damage ×= (1 - blockEffectiveness)                                │
│       durabilityLoss++                                                   │
│     }                                                                    │
│                                                                          │
│  5. БРОНЯ ЧАСТИ ТЕЛА                                                     │
│     armor = getEquippedArmorForPart(hitPart)                            │
│     if (random() < armor.coverage) {                                    │
│       damageReduction = calculateArmorReduction(armor, damageType)      │
│       damage ×= (1 - damageReduction)                                   │
│       armor.durability.current -= damage × 0.1                          │
│     }                                                                    │
│                                                                          │
│  6. ПРОБИТИЕ БРОНИ                                                       │
│     penetration = weapon.penetration + attackerSTR × 0.5                │
│     effectiveArmor = max(0, armor.armor - penetration)                  │
│     damage -= effectiveArmor × 0.5                                      │
│                                                                          │
│  7. МАТЕРИАЛ ТЕЛА                                                        │
│     materialReduction = getMaterialReduction(target.bodyMaterial)       │
│     damage ×= (1 - materialReduction)                                   │
│                                                                          │
│  8. ФИНАЛЬНЫЙ УРОН                                                       │
│     finalDamage = max(1, floor(damage))                                 │
│     applyDamageToBodyPart(target, hitPart, finalDamage)                 │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 4️⃣ ШАНСЫ ПОПАДАНИЯ ПО ЧАСТЯМ ТЕЛА

### 4.1 Базовые шансы (гуманоид)

```typescript
const BASE_BODY_PART_CHANCES: Record<BodyPartType, number> = {
  head: 5,        // 5% — маленькая мишень
  torso: 40,      // 40% — большая мишень
  heart: 2,       // 2% — только при открытой груди
  left_arm: 10,   // 10%
  right_arm: 10,  // 10%
  left_leg: 12,   // 12%
  right_leg: 12,  // 12%
  left_hand: 4,   // 4%
  right_hand: 4,  // 4%
  left_foot: 0.5, // 0.5%
  right_foot: 0.5 // 0.5%
};
```

### 4.2 Модификаторы от позиции

```typescript
// Атака сверху (прыжок, полёт)
positionModifier = {
  head: +10,
  torso: -5,
  legs: -10
};

// Атака снизу (подземный монстр)
positionModifier = {
  head: -10,
  legs: +15
};

// Атака сбоку (фланговая)
positionModifier = {
  left_arm: +10,  // если атака слева
  right_arm: +10, // если атака справа
};
```

### 4.3 Модификаторы от размера цели

```typescript
// Маленькая цель (tiny)
sizeModifier = { head: +5, torso: -10 };

// Огромная цель (huge)
sizeModifier = { legs: +10, torso: -10 };
```

### 4.4 Модификаторы от оружия

```typescript
// Кинжал — точные удары
weaponModifier = { head: +3, heart: +2, hands: +3 };

// Двуручный меч — размашистые удары
weaponModifier = { torso: +10, arms: +5, head: -5 };

// Копьё — колющие удары
weaponModifier = { torso: +5, heart: +3, head: -3 };
```

### 4.5 Прицельные удары

```typescript
interface AimedAttack {
  targetPart: BodyPartType;
  accuracyPenalty: number;  // -10% to -50% шанс попадания
  damageBonus: number;      // +10% to +50% урон при успехе
}

// Прицеливание в голову
aimedHead: {
  targetPart: 'head',
  accuracyPenalty: -30,
  damageBonus: +50
};
```

---

## 5️⃣ ЭКИП, НЕ УЧАСТВУЮЩИЙ В БОЮ

### 5.1 Полный список не-боевого эквипа

| Тип | Влияние | Почему не участвует |
|-----|---------|---------------------|
| jewelry_ring | +статы, +Ци | Не закрывает тело |
| jewelry_necklace | +статы, +резисты | Не закрывает тело |
| jewelry_earring | +восприятие | Не закрывает тело |
| jewelry_bracelet | +статы | Не закрывает тело |
| clothing_cloak | +скрытность | Ткань не защищает |
| clothing_belt | +инвентарь | Не закрывает тело |
| tool_crafting | — | Инструмент, не оружие |
| tool_gathering | — | Инструмент, не оружие |
| tool_medical | — | Инструмент, не оружие |
| consumable_* | — | Расходник |
| artifact_passive | +баффы | Артефакт, не броня |

### 5.2 Условное участие

| Тип | Условие участия |
|-----|-----------------|
| artifact_active | Участвует если активирован (дает щит) |
| implant_* | Участвует только: implant_limbs (протез руки/ноги) |
| shield_* | Участвует только при блоке |

---

## 6️⃣ ПЕРВИЧНЫЙ УРОН И ЕГО УМЕНЬШЕНИЕ

### 6.1 Слои защиты (5 слоёв)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        СЛОИ ЗАЩИТЫ (порядок)                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  СЛОЙ 1: Активная защита (выбор игрока)                                  │
│  ├── Уклонение (dodge) → 0 урона при успехе                             │
│  ├── Блок щитом (shield block) → absorbedDamage, durability--          │
│  └── Парирование (parry) → counter-attack chance                        │
│                                                                          │
│  СЛОЙ 2: Пассивная защита Ци                                             │
│  ├── meridianBuffer → до 30% урона поглощается Ци                       │
│  └── qiShield (активная техника) → поглощает до shieldHP                │
│                                                                          │
│  СЛОЙ 3: Физическая броня                                                │
│  ├── coverage check → броня работает только при попадании              │
│  ├── damageReduction → % снижения урона                                 │
│  └── armor value → плоское вычитание урона                              │
│                                                                          │
│  СЛОЙ 4: Тело цели                                                       │
│  ├── materialReduction → кожа/чешуя/призрак                             │
│  ├── vitalityMultiplier → высокое vitality = больше HP                 │
│  └── cultivationBonus → культивация усиливает тело                      │
│                                                                          │
│  СЛОЙ 5: Внутренние механики                                             │
│  ├── redHP → функциональность части тела                                │
│  └── blackHP → структурная целостность                                   │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 6.2 Что получает первичный урон

**ВАРИАНТ A: redHP сначала**
```
// Урон сначала идёт в функциональную HP
// При достижении 0 redHP — часть тела парализована
// Избыток урона идёт в blackHP
if (damage > redHP) {
  overflowDamage = damage - redHP;
  redHP = 0;
  blackHP -= overflowDamage;
} else {
  redHP -= damage;
}
```

**ВАРИАНТ B: blackHP сначала (реалистичный)**
```
// Урон сначала идёт в структурную HP
// При достижении 0 blackHP — часть отрублена
// redHP снижается пропорционально blackHP
blackHP -= damage;
redHP = min(redHP, blackHP × 0.5);
```

**ВАРИАНТ C: Распределённый (Kenshi-style)**
```
// Урон распределяется: 70% в redHP, 30% в blackHP
redHP -= damage × 0.7;
blackHP -= damage × 0.3;

// При критических повреждениях
if (blackHP < maxBlackHP × 0.3) {
  // Ускоренная потеря redHP (шок)
  redHP -= damage × 0.2;
}
```

---

## 7️⃣ ПРИМЕРЫ РАСЧЁТА

### Пример 1: Удар мечом по бронированному человеку

```
Исходные данные:
- Атакующий: STR 20, меч (baseDamage 15, penetration 10)
- Цель: torso, железная броня (armor 30, coverage 80%, DR 20%)
- Урон: handDamage(6) + weaponDamage(15) = 21

Расчёт:
1. Roll bodyPart → torso (40% шанс)
2. Roll coverage → броня работает (80% шанс)
3. damageReduction = 21 × 0.2 = 4.2
4. damage = 21 - 4.2 = 16.8
5. penetration = 10 + (20-10) × 0.5 = 15
6. effectiveArmor = max(0, 30 - 15) = 15
7. damage = 16.8 - 15 × 0.5 = 9.3
8. finalDamage = max(1, floor(9.3)) = 9

Броня теряет: 9 × 0.1 = 0.9 прочности
```

### Пример 2: Удар кулаком по дракону

```
Исходные данные:
- Атакующий: STR 15, без оружия
- Цель: scales (materialReduction 30%), vitality 200
- Урон: handDamage = 3 + (15-10) × 0.3 = 4.5

Расчёт:
1. damage = 4.5
2. materialReduction = 4.5 × 0.3 = 1.35
3. damage = 4.5 - 1.35 = 3.15
4. finalDamage = max(1, floor(3.15)) = 3

Дракон почти не чувствует удара!
```

### Пример 3: Техника с оружием

```
Исходные данные:
- Атакующий: L5 культиватор, STR 25
- Оружие: Меч Дракона (baseDamage 50, techniqueBonus ×1.5)
- Техника: "Рубящий вихрь" L5 (baseDamage 75, qiCost 100)

Расчёт:
1. Техника: qiDensity = 16 (L5)
   effectiveness = 100 × 16 = 1600
   
2. Урон техники: 75 × (1 + 1600/1000) = 195

3. Бонус оружия: 50 × 1.5 = 75

4. Итоговый урон: 195 + 75 = 270
```

---

## 8️⃣ РЕКОМЕНДУЕМЫЕ ВАРИАНТЫ

На основе анализа рекомендуется:

| Механика | Рекомендуемый вариант | Обоснование |
|----------|----------------------|-------------|
| Потеря прочности | **ВАРИАНТ C** (Комбинированный) | Реалистичный + не слишком быстрый износ |
| Ремонт | **ВАРИАНТ B + C** | Самостоятельный + Ци-ремонт для высоких уровней |
| Урон оружия | **ВАРИАНТ C** (Гибридный) | Баланс между рукой и оружием |
| Распределение HP | **ВАРИАНТ C** (Kenshi-style) | Проверенная механика |

---

## 9️⃣ АНАЛИЗ ТЕКУЩЕЙ РЕАЛИЗАЦИИ ГЕНЕРАТОРОВ

### 9.1 Текущее состояние (2026-03-14)

**Файлы:**
- `src/lib/generator/weapon-generator.ts` — генератор оружия
- `src/lib/generator/armor-generator.ts` — генератор брони
- `src/lib/generator/base-item-generator.ts` — базовый генератор (редкость, элементы)

**Текущая система редкости:**
```typescript
// base-item-generator.ts
export const RARITY_MULTIPLIERS: Record<Rarity, {...}> = {
  common:     { statMult: 0.8,  weight: 50 },
  uncommon:   { statMult: 1.0,  weight: 30 },
  rare:       { statMult: 1.25, weight: 15 },
  legendary:  { statMult: 1.6,  weight: 5 },
};
```

**Проблема:** Редкость «запекается» в параметры предмета при генерации:
```typescript
// weapon-generator.ts:202
let baseDamage = baseValues.damage * weaponConfig.baseDamage / 15;
baseDamage *= rarityMult.statMult; // ← Редкость необратимо влияет на урон
```

### 9.2 Ограничения текущей архитектуры

| Ограничение | Причина |
|-------------|---------|
| **Нет прочности** | `DurabilityProps` не реализована в генераторах |
| **Редкость неизменна** | Редкость умножает статы при генерации |
| **Нельзя понизить редкость** | Нет разделения базы и надстройки |
| **Сложно хранить** | Нет шаблонов базового оружия |

---

## 🔟 ПРЕДЛОЖЕНИЕ: РАЗДЕЛЕНИЕ БАЗЫ И РЕДКОСТИ

### 10.1 Концепция

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    ПРЕДЛОЖЕННАЯ АРХИТЕКТУРА                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   ТЕКУЩАЯ СХЕМА:                                                         │
│   ┌────────────────────────────────────┐                                │
│   │ Weapon {                           │                                │
│   │   baseDamage: 25 (уже с редкостью) │ ← Нельзя изменить              │
│   │   rarity: "rare" (только метка)    │                                │
│   │ }                                  │                                │
│   └────────────────────────────────────┘                                │
│                                                                          │
│   ПРЕДЛОЖЕННАЯ СХЕМА:                                                    │
│   ┌────────────────────────────────────┐                                │
│   │ Weapon {                           │                                │
│   │   // Базовые статы (неизменны)     │                                │
│   │   baseStats: {                     │                                │
│   │     damage: 20,                    │ ← Только уровень + тип         │
│   │     range: 1.2,                    │                                │
│   │     attackSpeed: 1.0               │                                │
│   │   },                               │                                │
│   │   // Надстройка редкости (изменяема)│                               │
│   │   rarityOverlay: {                 │                                │
│   │     rarity: "rare",                │ ← Можно понизить при ремонте   │
│   │     durabilityMult: 2.0,           │                                │
│   │     bonusStats: [...],             │ ← Теряются при понижении       │
│   │     specialEffects: [...]          │                                │
│   │   },                               │                                │
│   │   // Прочность                     │                                │
│   │   durability: {                    │                                │
│   │     current: 100,                  │                                │
│   │     max: 100, // = base × rarityMult│                               │
│   │   }                                │                                │
│   │ }                                  │                                │
│   └────────────────────────────────────┘                                │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 10.2 Формула максимальной прочности по редкости

```typescript
const DURABILITY_BY_RARITY: Record<Rarity, number> = {
  common:    50,   // Базовая прочность
  uncommon:  80,   // ×1.6
  rare:      120,  // ×2.4
  legendary: 200,  // ×4.0
};

// Расчёт макс. прочности
function calculateMaxDurability(baseWeapon: WeaponBase, rarity: Rarity): number {
  const materialBonus = baseWeapon.material === 'spirit_iron' ? 1.5 : 1.0;
  return Math.floor(DURABILITY_BY_RARITY[rarity] * materialBonus);
}
```

### 10.3 Механика понижения редкости при ремонте

```typescript
interface RepairResult {
  success: boolean;
  newDurability: number;
  rarityChanged: boolean;
  oldRarity?: Rarity;
  newRarity?: Rarity;
  lostBonusStats: EquipmentBonus[];
}

function repairWeapon(
  weapon: Weapon,
  repairSkill: number,
  materials: Material[]
): RepairResult {
  // Базовый ремонт
  const repairAmount = repairSkill * 10;
  let newDurability = Math.min(weapon.durability.max, weapon.durability.current + repairAmount);
  
  // Проверка качества ремонта
  const quality = calculateRepairQuality(repairSkill, materials);
  
  // Некачественный ремонт → шанс понижения редкости
  if (quality < 0.5 && weapon.rarityOverlay.rarity !== 'common') {
    const downgradeChance = (1 - quality) * 0.5; // до 25%
    
    if (Math.random() < downgradeChance) {
      const oldRarity = weapon.rarityOverlay.rarity;
      const newRarity = downgradeRarity(oldRarity);
      
      // Сохраняем потерянные бонусы для отображения игроку
      const lostBonusStats = weapon.rarityOverlay.bonusStats;
      
      // Понижаем редкость
      weapon.rarityOverlay.rarity = newRarity;
      weapon.rarityOverlay.bonusStats = generateBonusStatsForRarity(newRarity);
      weapon.durability.max = calculateMaxDurability(weapon.baseStats, newRarity);
      
      return {
        success: true,
        newDurability,
        rarityChanged: true,
        oldRarity,
        newRarity,
        lostBonusStats,
      };
    }
  }
  
  return {
    success: true,
    newDurability,
    rarityChanged: false,
    lostBonusStats: [],
  };
}

function downgradeRarity(rarity: Rarity): Rarity {
  const order: Rarity[] = ['common', 'uncommon', 'rare', 'legendary'];
  const index = order.indexOf(rarity);
  return index > 0 ? order[index - 1] : rarity;
}
```

### 10.4 Анализ утверждения пользователя

> *"Предложение по разделению характеристик базового оружия, а редкость как надстройка, этим мы сможем реализовать потерю редкости при некачественном ремонте (понижение грейда) и упростить систему хранения и генерации оружия."*

**ПРОВЕРКА УТВЕРЖДЕНИЯ:**

| Аспект | Верность | Комментарий |
|--------|----------|-------------|
| Потеря редкости при ремонте | ✅ ВЕРНО | Архитектура позволяет понижать rarityOverlay без пересоздания предмета |
| Упрощение хранения | ⚠️ ЧАСТИЧНО | Хранение становится сложнее (два уровня), НО появляется возможность шаблонов |
| Упрощение генерации | ⚠️ ЧАСТИЧНО | Генерация становится двухэтапной, НО более гибкой |

**СЛОЖНОСТЬ РЕАЛИЗАЦИИ:**

| Этап | Сложность | Объём работы |
|------|-----------|--------------|
| 1. Рефакторинг интерфейсов | Средняя | 2-3 часа |
| 2. Изменение weapon-generator.ts | Средняя | 3-4 часа |
| 3. Изменение armor-generator.ts | Средняя | 2-3 часа |
| 4. Реализация DurabilityProps | Средняя | 2-3 часа |
| 5. Система ремонта | Высокая | 4-6 часов |
| 6. Миграция данных | Высокая | 2-3 часа |
| **ИТОГО** | | **15-22 часа** |

### 10.5 Альтернативный вариант (более простой)

```typescript
// Без разделения базы и редкости
// Но с возможностью потери макс. прочности

interface DurabilityProps {
  current: number;
  max: number;
  originalMax: number;  // Изначальный максимум
  degradationCount: number; // Сколько раз теряли макс. прочность
}

function poorRepair(weapon: Weapon): void {
  // Теряем 10% макс. прочности при плохом ремонте
  weapon.durability.max = Math.floor(weapon.durability.max * 0.9);
  weapon.durability.degradationCount++;
  
  // После 3 плохих ремонтов предмет сломан окончательно
  if (weapon.durability.degradationCount >= 3) {
    weapon.durability.max = 0;
    weapon.durability.current = 0;
  }
}
```

**Преимущества альтернативы:**
- Меньше кода (5-8 часов работы)
- Совместимость с текущей архитектурой
- Миграция данных не требуется

**Недостатки:**
- Нет визуального понижения редкости (игрок не видит, что предмет "ухудшился")
- Бонусы от редкости остаются (дисбаланс)

### 10.6 Рекомендация

**РЕКОМЕНДУЕТСЯ:** Полный вариант с разделением базы и редкости

**Обоснование:**
1. Игрок видит изменение цвета предмета (потеря редкости)
2. Баланс сохраняется (бонусы теряются вместе с редкостью)
3. Возможность создания системы шаблонов для генерации
4. Соответствует концепции «мира культивации» (духовное оружие теряет силу)

---

## 1️⃣1️⃣ ЗАДАЧИ ДОРАБОТКИ ГЕНЕРАТОРА ЭКИПИРОВКИ

### Фаза 1: Интерфейсы и типы

**Файл:** `src/types/equipment-v2.ts`

```typescript
// === БАЗОВЫЕ ХАРАКТЕРИСТИКИ (неизменны) ===
interface WeaponBaseStats {
  damage: number;        // Только уровень + тип оружия
  range: number;
  attackSpeed: number;
  damageType: 'slashing' | 'piercing' | 'blunt' | 'elemental';
  material: MaterialType;
}

// === НАДСТРОЙКА РЕДКОСТИ (изменяема) ===
interface RarityOverlay {
  rarity: Rarity;
  durabilityMultiplier: number;
  bonusStats: EquipmentBonus[];
  specialEffects: SpecialEffect[];
  grantedTechniques?: GrantedTechnique[];
}

// === ПРОЧНОСТЬ ===
interface DurabilityProps {
  current: number;
  max: number;
  condition: EquipmentCondition;
}

// === ИТОГОВОЕ ОРУЖИЕ ===
interface WeaponV2 {
  id: string;
  name: string;
  baseStats: WeaponBaseStats;      // Неизменны
  rarityOverlay: RarityOverlay;     // Можно понижать
  durability: DurabilityProps;
  level: number;
  weaponType: WeaponType;
}
```

**Задачи:**
1. [ ] Создать `src/types/equipment-v2.ts`
2. [ ] Определить `WeaponBaseStats`, `ArmorBaseStats`
3. [ ] Определить `RarityOverlay` с возможностью модификации
4. [ ] Определить `DurabilityProps` с condition
5. [ ] Создать типы для системы ремонта

---

### Фаза 2: Рефакторинг генераторов

**Файл:** `src/lib/generator/weapon-generator-v2.ts`

```typescript
// Двухэтапная генерация
function generateWeaponV2(options: WeaponGenerationOptions): WeaponV2 {
  // Этап 1: Базовые характеристики (без редкости)
  const baseStats = generateBaseStats(options.level, options.weaponType);
  
  // Этап 2: Надстройка редкости
  const rarity = selectRarity(options);
  const rarityOverlay = generateRarityOverlay(rarity, options.level);
  
  // Этап 3: Расчёт прочности
  const maxDurability = calculateMaxDurability(baseStats, rarityOverlay);
  
  return {
    id: generateItemId('WP'),
    name: generateName(baseStats, rarityOverlay),
    baseStats,
    rarityOverlay,
    durability: {
      current: maxDurability,
      max: maxDurability,
      condition: 'pristine',
    },
    level: options.level,
    weaponType: options.weaponType,
  };
}
```

**Задачи:**
1. [ ] Создать `weapon-generator-v2.ts` с двухэтапной генерацией
2. [ ] Вынести расчёт базового урона без редкости
3. [ ] Создать `generateRarityOverlay()` функцию
4. [ ] Интегрировать `calculateMaxDurability()`
5. [ ] Обновить `armor-generator.ts` по аналогии

---

### Фаза 3: Система прочности

**Файл:** `src/lib/game/durability-system.ts`

```typescript
// Константы прочности по редкости
export const DURABILITY_BY_RARITY: Record<Rarity, number> = {
  common: 50,
  uncommon: 80,
  rare: 120,
  legendary: 200,
};

// Потеря прочности (ВАРИАНТ C — комбинированный)
export function loseDurability(
  equipment: Equipment,
  action: 'attack' | 'block' | 'absorb',
  damageAbsorbed?: number
): void {
  const baseLoss = DURABILITY_LOSS_BY_ACTION[action];
  const hardnessFactor = 1 / equipment.baseStats.material.hardness;
  
  let totalLoss = baseLoss * hardnessFactor;
  
  // Ударный износ
  if (damageAbsorbed && damageAbsorbed > equipment.baseStats.material.hardness * 10) {
    totalLoss += (damageAbsorbed - equipment.baseStats.material.hardness * 10) / 10;
  }
  
  equipment.durability.current = Math.max(0, equipment.durability.current - totalLoss);
  equipment.durability.condition = calculateCondition(equipment.durability);
}
```

**Задачи:**
1. [ ] Создать `durability-system.ts`
2. [ ] Реализовать `loseDurability()` с комбинированным вариантом
3. [ ] Реализовать `calculateCondition()` по таблице состояний
4. [ ] Реализовать `getConditionMultiplier()` для эффективности
5. [ ] Добавить breakpoint для слома при 0% прочности

---

### Фаза 4: Система ремонта

**Файл:** `src/lib/game/repair-system.ts`

```typescript
export interface RepairOptions {
  skill: number;          // Навык кузнеца
  materials: Material[];  // Используемые материалы
  method: 'npc' | 'self' | 'qi';
}

export function repairEquipment(
  equipment: Equipment,
  options: RepairOptions
): RepairResult {
  const quality = calculateRepairQuality(options);
  
  // Некачественный ремонт → шанс понижения редкости
  if (quality < 0.5 && equipment.rarityOverlay.rarity !== 'common') {
    if (shouldDowngrade(quality)) {
      return downgradeAndRepair(equipment, quality);
    }
  }
  
  // Обычный ремонт
  equipment.durability.current = equipment.durability.max;
  return { success: true, rarityChanged: false };
}

function downgradeAndRepair(equipment: Equipment, quality: number): RepairResult {
  const oldRarity = equipment.rarityOverlay.rarity;
  const newRarity = downgradeRarity(oldRarity);
  
  // Сохраняем потерянные бонусы для UI
  const lostBonuses = [...equipment.rarityOverlay.bonusStats];
  
  // Понижаем редкость
  equipment.rarityOverlay.rarity = newRarity;
  equipment.rarityOverlay.bonusStats = generateBonusStatsForRarity(newRarity);
  equipment.rarityOverlay.durabilityMultiplier = DURABILITY_MULT_BY_RARITY[newRarity];
  
  // Пересчитываем макс. прочность
  equipment.durability.max = calculateMaxDurability(equipment.baseStats, equipment.rarityOverlay);
  equipment.durability.current = equipment.durability.max;
  
  return {
    success: true,
    rarityChanged: true,
    oldRarity,
    newRarity,
    lostBonuses,
  };
}
```

**Задачи:**
1. [ ] Создать `repair-system.ts`
2. [ ] Реализовать `calculateRepairQuality()` на основе навыка и материалов
3. [ ] Реализовать `shouldDowngrade()` с формулой шанса
4. [ ] Реализовать `downgradeRarity()` с сохранением потерянных бонусов
5. [ ] Реализовать Ци-ремонт для духовного оружия
6. [ ] Добавить UI предупреждения о риске понижения редкости

---

### Фаза 5: Интеграция с боевой системой

**Файлы:**
- `src/lib/game/combat-system.ts`
- `src/game/scenes/LocationScene.ts`

**Задачи:**
1. [ ] Добавить вызов `loseDurability()` при атаке
2. [ ] Добавить вызов `loseDurability()` при блоке
3. [ ] Добавить проверку `condition` для эффективности оружия
4. [ ] Добавить визуальный индикатор состояния оружия
5. [ ] Добавить звук/эффект при поломке оружия

---

## 1️⃣2️⃣ СИСТЕМА ГРЕЙДОВ (КАЧЕСТВА ЭКИПИРОВКИ)

### 12.1 Концепция

**Грейд** — это уровень качества экипировки, надстройка над базовыми характеристиками.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    АРХИТЕКТУРА СИСТЕМЫ ГРЕЙДОВ                           │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                    ЭКИПИРОВКА (Equipment)                        │   │
│   │                                                                   │   │
│   │   ┌─────────────────────────────────────────────────────────┐   │   │
│   │   │              БАЗОВЫЙ КЛАСС (Base Class)                  │   │   │
│   │   │  ┌─────────────────────────────────────────────────────┐│   │   │
│   │   │  │ • Тип экипировки (weapon_sword, armor_torso, ...)  ││   │   │
│   │   │  │ • Уровень предмета (1-9)                           ││   │   │
│   │   │  │ • Материал (iron, steel, spirit_iron, ...)         ││   │   │
│   │   │  │ • Базовые параметры (урон/защита, скорость, ...)   ││   │   │
│   │   │  │ • Требования (сила, ловкость, уровень культивации) ││   │   │
│   │   │  │                     ↓                               ││   │   │
│   │   │  │         НЕИЗМЕННЫЕ ХАРАКТЕРИСТИКИ                   ││   │   │
│   │   │  └─────────────────────────────────────────────────────┘│   │   │
│   │   └─────────────────────────────────────────────────────────┘   │   │
│   │                              +                                    │   │
│   │   ┌─────────────────────────────────────────────────────────┐   │   │
│   │   │               ГРЕЙД (Grade Overlay)                      │   │   │
│   │   │  ┌─────────────────────────────────────────────────────┐│   │   │
│   │   │  │ • Качество: Damaged → Common → Refined → Perfect   ││   │   │
│   │   │  │ • Множитель прочности (×0.5 ... ×4.0)              ││   │   │
│   │   │  │ • Дополнительные характеристики                    ││   │   │
│   │   │  │ • Специальные эффекты                              ││   │   │
│   │   │  │ • Даруемые техники (для высокого качества)         ││   │   │
│   │   │  │                     ↓                               ││   │   │
│   │   │  │         ИЗМЕНЯЕМЫЕ ХАРАКТЕРИСТИКИ                   ││   │   │
│   │   │  │   (можно понижать при плохом ремонте)              ││   │   │
│   │   │  │   (можно повышать при апгрейде)                    ││   │   │
│   │   │  └─────────────────────────────────────────────────────┘│   │   │
│   │   └─────────────────────────────────────────────────────────┘   │   │
│   │                              =                                    │   │
│   │   ┌─────────────────────────────────────────────────────────┐   │   │
│   │   │              ИТОГОВЫЙ ПРЕДМЕТ                           │   │   │
│   │   │  Эффективность = BaseStats × GradeMultiplier            │   │   │
│   │   │  Прочность = MaterialBase × GradeMultiplier             │   │   │
│   │   └─────────────────────────────────────────────────────────┘   │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

### 12.2 Уровни Грейда

```typescript
type EquipmentGrade = 
  | 'damaged'    // Повреждённый (ниже базового)
  | 'common'     // Обычный (базовый)
  | 'refined'    // Улучшенный
  | 'perfect'    // Совершенный
  | 'transcendent'; // Превосходящий

interface GradeProperties {
  name: string;
  color: string;
  durabilityMultiplier: number;    // Множитель прочности
  bonusStatsMin: number;           // Мин. доп. характеристик
  bonusStatsMax: number;           // Макс. доп. характеристик
  specialEffectChance: number;     // Шанс спецэффекта (%)
  grantedTechniqueChance: number;  // Шанс даруемой техники (%)
  upgradeCost: number;             // Стоимость апгрейда (камни)
  downgradeChance: number;         // Шанс понижения при плохом ремонте
}

const GRADE_PROPERTIES: Record<EquipmentGrade, GradeProperties> = {
  damaged: {
    name: 'Повреждённый',
    color: 'text-red-400',
    durabilityMultiplier: 0.5,
    bonusStatsMin: 0,
    bonusStatsMax: 0,
    specialEffectChance: 0,
    grantedTechniqueChance: 0,
    upgradeCost: 0,      // Нельзя апгрейдить напрямую
    downgradeChance: 0,  // Уже最低
  },
  common: {
    name: 'Обычный',
    color: 'text-gray-400',
    durabilityMultiplier: 1.0,
    bonusStatsMin: 0,
    bonusStatsMax: 1,
    specialEffectChance: 0,
    grantedTechniqueChance: 0,
    upgradeCost: 50,     // 50 духовных камней
    downgradeChance: 0,  // Минимальный грейд
  },
  refined: {
    name: 'Улучшенный',
    color: 'text-green-400',
    durabilityMultiplier: 1.5,
    bonusStatsMin: 1,
    bonusStatsMax: 2,
    specialEffectChance: 20,
    grantedTechniqueChance: 0,
    upgradeCost: 200,
    downgradeChance: 0.25,  // 25% при плохом ремонте
  },
  perfect: {
    name: 'Совершенный',
    color: 'text-blue-400',
    durabilityMultiplier: 2.5,
    bonusStatsMin: 2,
    bonusStatsMax: 4,
    specialEffectChance: 50,
    grantedTechniqueChance: 10,
    upgradeCost: 1000,
    downgradeChance: 0.30,  // 30% при плохом ремонте
  },
  transcendent: {
    name: 'Превосходящий',
    color: 'text-amber-400',
    durabilityMultiplier: 4.0,
    bonusStatsMin: 4,
    bonusStatsMax: 6,
    specialEffectChance: 80,
    grantedTechniqueChance: 30,
    upgradeCost: 5000,
    downgradeChance: 0.40,  // 40% при плохом ремонте
  },
};
```

---

### 12.3 Влияние материала на базовые параметры

```typescript
interface MaterialBaseStats {
  name: string;
  tier: number;              // Уровень материала (1-5)
  
  // Для оружия
  baseDamage: number;        // Базовый урон
  basePenetration: number;   // Пробитие
  
  // Для брони
  baseDefense: number;       // Базовая защита
  baseResistances: Partial<Record<Element, number>>;  // Сопротивления
  
  // Общее
  baseDurability: number;    // Базовая прочность
  weightMultiplier: number;  // Множитель веса
  qiConductivity: number;    // Проводимость Ци
}

const MATERIAL_STATS: Record<MaterialType, MaterialBaseStats> = {
  // === ТИР 1: Базовые материалы ===
  iron: {
    name: 'Железо',
    tier: 1,
    baseDamage: 10,
    basePenetration: 5,
    baseDefense: 8,
    baseResistances: {},
    baseDurability: 50,
    weightMultiplier: 1.0,
    qiConductivity: 0.3,
  },
  leather: {
    name: 'Кожа',
    tier: 1,
    baseDamage: 5,
    basePenetration: 2,
    baseDefense: 4,
    baseResistances: {},
    baseDurability: 30,
    weightMultiplier: 0.5,
    qiConductivity: 0.5,
  },
  
  // === ТИР 2: Улучшенные материалы ===
  steel: {
    name: 'Сталь',
    tier: 2,
    baseDamage: 15,
    basePenetration: 8,
    baseDefense: 12,
    baseResistances: {},
    baseDurability: 80,
    weightMultiplier: 1.0,
    qiConductivity: 0.4,
  },
  bronze: {
    name: 'Бронза',
    tier: 2,
    baseDamage: 12,
    basePenetration: 6,
    baseDefense: 15,
    baseResistances: { fire: 10 },
    baseDurability: 70,
    weightMultiplier: 1.2,
    qiConductivity: 0.3,
  },
  
  // === ТИР 3: Особые материалы ===
  spirit_iron: {
    name: 'Духовное железо',
    tier: 3,
    baseDamage: 25,
    basePenetration: 15,
    baseDefense: 20,
    baseResistances: { void: 20 },
    baseDurability: 150,
    weightMultiplier: 0.7,
    qiConductivity: 1.5,
  },
  cold_iron: {
    name: 'Холодное железо',
    tier: 3,
    baseDamage: 22,
    basePenetration: 20,
    baseDefense: 18,
    baseResistances: { fire: 30, lightning: -20 },
    baseDurability: 120,
    weightMultiplier: 0.9,
    qiConductivity: 1.0,
  },
  
  // === ТИР 4: Редкие материалы ===
  star_metal: {
    name: 'Звёздный металл',
    tier: 4,
    baseDamage: 40,
    basePenetration: 25,
    baseDefense: 35,
    baseResistances: { fire: 20, water: 20, earth: 20, air: 20 },
    baseDurability: 250,
    weightMultiplier: 0.5,
    qiConductivity: 2.5,
  },
  dragon_bone: {
    name: 'Кость дракона',
    tier: 4,
    baseDamage: 50,
    basePenetration: 30,
    baseDefense: 45,
    baseResistances: { fire: 50 },
    baseDurability: 400,
    weightMultiplier: 0.4,
    qiConductivity: 3.0,
  },
  
  // === ТИР 5: Божественные материалы ===
  void_matter: {
    name: 'Материя пустоты',
    tier: 5,
    baseDamage: 80,
    basePenetration: 50,
    baseDefense: 60,
    baseResistances: { void: 80 },
    baseDurability: 500,
    weightMultiplier: 0.1,
    qiConductivity: 5.0,
  },
  chaos_matter: {
    name: 'Материя хаоса',
    tier: 5,
    baseDamage: 100,
    basePenetration: 40,
    baseDefense: 50,
    baseResistances: { fire: 30, water: 30, earth: 30, air: 30, void: 30 },
    baseDurability: 600,
    weightMultiplier: 0.2,
    qiConductivity: 4.0,
  },
};
```

---

### 12.4 Формулы расчёта базовых параметров

```typescript
/**
 * Расчёт базовых параметров оружия
 */
function calculateWeaponBaseStats(
  level: number,
  weaponType: WeaponType,
  material: MaterialType
): WeaponBaseStats {
  const materialStats = MATERIAL_STATS[material];
  const typeConfig = WEAPON_TYPE_CONFIGS[weaponType];
  
  // Уровневый множитель (геометрический рост)
  const levelMult = Math.pow(1.5, level - 1);
  
  // Базовый урон
  const baseDamage = Math.floor(
    materialStats.baseDamage * typeConfig.damageMultiplier * levelMult
  );
  
  // Базовое пробитие
  const basePenetration = Math.floor(
    materialStats.basePenetration * (1 + level * 0.1)
  );
  
  // Дальность
  const range = typeConfig.baseRange * (1 + level * 0.02);
  
  // Скорость атаки
  const attackSpeed = typeConfig.attackSpeed * (1 - materialStats.weightMultiplier * 0.1);
  
  return {
    damage: baseDamage,
    penetration: basePenetration,
    range: Math.round(range * 100) / 100,
    attackSpeed: Math.round(attackSpeed * 100) / 100,
    damageType: typeConfig.damageType,
    material,
  };
}

/**
 * Расчёт базовых параметров брони
 */
function calculateArmorBaseStats(
  level: number,
  armorSlot: EquipmentSlot,
  material: MaterialType
): ArmorBaseStats {
  const materialStats = MATERIAL_STATS[material];
  const slotConfig = ARMOR_SLOT_CONFIGS[armorSlot];
  
  // Уровневый множитель
  const levelMult = Math.pow(1.4, level - 1);
  
  // Базовая защита
  const baseDefense = Math.floor(
    materialStats.baseDefense * slotConfig.defenseMultiplier * levelMult
  );
  
  // Базовые сопротивления
  const baseResistances: Partial<Record<Element, number>> = {};
  for (const [element, value] of Object.entries(materialStats.baseResistances)) {
    baseResistances[element as Element] = Math.floor(value * levelMult * 0.5);
  }
  
  // Штрафы
  const moveSpeedPenalty = Math.floor(materialStats.weightMultiplier * slotConfig.weightFactor * 5);
  const dodgePenalty = Math.floor(materialStats.weightMultiplier * slotConfig.weightFactor * 3);
  
  return {
    defense: baseDefense,
    resistances: baseResistances,
    moveSpeedPenalty,
    dodgePenalty,
    material,
  };
}

/**
 * Расчёт максимальной прочности
 */
function calculateMaxDurability(
  material: MaterialType,
  grade: EquipmentGrade
): number {
  const materialStats = MATERIAL_STATS[material];
  const gradeProps = GRADE_PROPERTIES[grade];
  
  return Math.floor(materialStats.baseDurability * gradeProps.durabilityMultiplier);
}
```

---

### 12.5 Полная структура экипировки с Грейдом

```typescript
/**
 * Базовые характеристики оружия (неизменны)
 */
interface WeaponBaseStats {
  damage: number;
  penetration: number;
  range: number;
  attackSpeed: number;
  damageType: DamageType;
  material: MaterialType;
}

/**
 * Базовые характеристики брони (неизменны)
 */
interface ArmorBaseStats {
  defense: number;
  resistances: Partial<Record<Element, number>>;
  moveSpeedPenalty: number;
  dodgePenalty: number;
  material: MaterialType;
}

/**
 * Базовые характеристики украшения (неизменны)
 */
interface JewelryBaseStats {
  qiConductivity: number;
  material: MaterialType;
}

/**
 * Грейд-надстройка (изменяема)
 */
interface GradeOverlay {
  grade: EquipmentGrade;
  
  // Множители от грейда
  durabilityMultiplier: number;
  
  // Дополнительные характеристики
  bonusStats: EquipmentBonus[];
  
  // Специальные эффекты
  specialEffects: SpecialEffect[];
  
  // Даруемые техники (для high-grade)
  grantedTechniques?: GrantedTechnique[];
  
  // История изменений грейда
  gradeHistory: GradeChangeEvent[];
}

interface GradeChangeEvent {
  fromGrade: EquipmentGrade;
  toGrade: EquipmentGrade;
  reason: 'upgrade' | 'downgrade_repair' | 'downgrade_combat';
  timestamp: number;
}

/**
 * Прочность
 */
interface DurabilityProps {
  current: number;
  max: number;
  condition: EquipmentCondition;
  totalDamageReceived: number;  // Всего получено урона
  repairCount: number;          // Количество ремонтов
  downgradeCount: number;       // Количество понижений грейда
}

/**
 * Итоговое оружие
 */
interface WeaponEquipment {
  id: string;
  name: string;
  equipmentType: 'weapon';
  weaponType: WeaponType;
  level: number;
  
  // База (неизменна)
  baseStats: WeaponBaseStats;
  
  // Грейд (изменяем)
  gradeOverlay: GradeOverlay;
  
  // Прочность
  durability: DurabilityProps;
  
  // Требования (зависят от базы)
  requirements: EquipmentRequirements;
}

/**
 * Итоговая броня
 */
interface ArmorEquipment {
  id: string;
  name: string;
  equipmentType: 'armor';
  slot: EquipmentSlot;
  level: number;
  
  // База (неизменна)
  baseStats: ArmorBaseStats;
  
  // Грейд (изменяем)
  gradeOverlay: GradeOverlay;
  
  // Прочность
  durability: DurabilityProps;
  
  // Покрытие частей тела
  bodyPartCoverage: BodyPartCoverage[];
}

/**
 * Итоговое украшение
 */
interface JewelryEquipment {
  id: string;
  name: string;
  equipmentType: 'jewelry';
  jewelryType: JewelryType;
  level: number;
  
  // База (неизменна)
  baseStats: JewelryBaseStats;
  
  // Грейд (изменяем)
  gradeOverlay: GradeOverlay;
  
  // Прочность (для украшений минимальная)
  durability: DurabilityProps;
}
```

---

## 1️⃣3️⃣ СИСТЕМА АПГРЕЙДА ГРЕЙДА

### 13.1 Механика повышения качества

```typescript
interface UpgradeResult {
  success: boolean;
  previousGrade: EquipmentGrade;
  newGrade: EquipmentGrade;
  newBonusStats: EquipmentBonus[];
  lostMaterials: Material[];
  cost: number;
  error?: string;
}

/**
 * Попытка повышения грейда
 */
function attemptGradeUpgrade(
  equipment: Equipment,
  materials: Material[],
  spiritStones: number,
  upgradeSkill: number
): UpgradeResult {
  const currentGrade = equipment.gradeOverlay.grade;
  const gradeOrder: EquipmentGrade[] = ['damaged', 'common', 'refined', 'perfect', 'transcendent'];
  const currentIndex = gradeOrder.indexOf(currentGrade);
  
  // Проверка: уже максимальный грейд
  if (currentIndex >= gradeOrder.length - 1) {
    return {
      success: false,
      previousGrade: currentGrade,
      newGrade: currentGrade,
      newBonusStats: [],
      lostMaterials: [],
      cost: 0,
      error: 'Максимальный грейд достигнут',
    };
  }
  
  const targetGrade = gradeOrder[currentIndex + 1];
  const targetProps = GRADE_PROPERTIES[targetGrade];
  
  // Проверка: недостаточно камней
  if (spiritStones < targetProps.upgradeCost) {
    return {
      success: false,
      previousGrade: currentGrade,
      newGrade: currentGrade,
      newBonusStats: [],
      lostMaterials: [],
      cost: 0,
      error: `Требуется ${targetProps.upgradeCost} духовных камней`,
    };
  }
  
  // Проверка: требуемые материалы
  const requiredMaterials = getRequiredMaterialsForUpgrade(equipment, targetGrade);
  const hasMaterials = checkMaterials(materials, requiredMaterials);
  
  if (!hasMaterials) {
    return {
      success: false,
      previousGrade: currentGrade,
      newGrade: currentGrade,
      newBonusStats: [],
      lostMaterials: [],
      cost: 0,
      error: 'Недостаточно материалов',
    };
  }
  
  // Расчёт шанса успеха
  const baseSuccessChance = 0.5;  // 50% базовый шанс
  const skillBonus = upgradeSkill * 0.02;  // +2% за каждый уровень навыка
  const materialBonus = calculateMaterialQualityBonus(materials);
  
  const successChance = Math.min(0.95, baseSuccessChance + skillBonus + materialBonus);
  
  // Бросок
  const roll = Math.random();
  
  // Материалы теряются в любом случае
  const lostMaterials = consumeMaterials(materials, requiredMaterials);
  const cost = targetProps.upgradeCost;
  
  if (roll < successChance) {
    // Успех!
    const newBonusStats = generateBonusStatsForGrade(targetGrade, equipment);
    const newSpecialEffects = generateSpecialEffects(targetGrade, equipment);
    const newGrantedTechniques = generateGrantedTechniques(targetGrade, equipment);
    
    // Обновляем грейд
    equipment.gradeOverlay.grade = targetGrade;
    equipment.gradeOverlay.durabilityMultiplier = targetProps.durabilityMultiplier;
    equipment.gradeOverlay.bonusStats = newBonusStats;
    equipment.gradeOverlay.specialEffects = newSpecialEffects;
    equipment.gradeOverlay.grantedTechniques = newGrantedTechniques;
    
    // Пересчитываем прочность
    equipment.durability.max = calculateMaxDurability(equipment.baseStats.material, targetGrade);
    equipment.durability.current = equipment.durability.max;
    
    // Добавляем в историю
    equipment.gradeOverlay.gradeHistory.push({
      fromGrade: currentGrade,
      toGrade: targetGrade,
      reason: 'upgrade',
      timestamp: Date.now(),
    });
    
    return {
      success: true,
      previousGrade: currentGrade,
      newGrade: targetGrade,
      newBonusStats,
      lostMaterials,
      cost,
    };
  } else {
    // Провал — материалы потеряны, грейд не изменился
    return {
      success: false,
      previousGrade: currentGrade,
      newGrade: currentGrade,
      newBonusStats: [],
      lostMaterials,
      cost,
      error: 'Не удалось повысить качество',
    };
  }
}

/**
 * Требуемые материалы для апгрейда
 */
function getRequiredMaterialsForUpgrade(
  equipment: Equipment,
  targetGrade: EquipmentGrade
): MaterialRequirement[] {
  const material = equipment.baseStats.material;
  const materialTier = MATERIAL_STATS[material].tier;
  
  // Базовые требования
  const baseRequirements: Record<EquipmentGrade, MaterialRequirement[]> = {
    damaged: [],  // Нельзя апгрейдить
    common: [
      { material: 'iron', count: 5 },
    ],
    refined: [
      { material: 'steel', count: 3 },
      { material: 'spirit_iron', count: 1 },
    ],
    perfect: [
      { material: 'spirit_iron', count: 5 },
      { material: 'star_metal', count: 1 },
    ],
    transcendent: [
      { material: 'star_metal', count: 3 },
      { material: 'dragon_bone', count: 1 },
      { material: 'void_matter', count: 1 },
    ],
  };
  
  return baseRequirements[targetGrade] || [];
}
```

### 13.2 Шансы успеха апгрейда

| Текущий → Целевой | Базовый | С навыком 10 | С навыком 50 |
|-------------------|---------|--------------|--------------|
| Common → Refined | 50% | 70% | 100%* |
| Refined → Perfect | 40% | 60% | 90% |
| Perfect → Transcendent | 30% | 50% | 80% |

*\* Максимум 95%*

### 13.3 Условия для апгрейда

```typescript
interface UpgradeConditions {
  // Прочность должна быть 100%
  minDurabilityPercent: 100;
  
  // Нельзя апгрейдить повреждённый предмет
  excludeGrade: ['damaged'];
  
  // Требуется кузнец (или сам игрок с навыком)
  requiresNPC: boolean;  // false если навык игрока >= 20
  
  // Требуется материал того же типа
  materialMatch: boolean;  // true для оружия из дух. железа
}
```

---

## 1️⃣4️⃣ ПОНИЖЕНИЕ ГРЕЙДА ПРИ РЕМОНТЕ

### 14.1 Механика понижения

```typescript
interface RepairResult {
  success: boolean;
  durabilityRestored: number;
  gradeChanged: boolean;
  previousGrade?: EquipmentGrade;
  newGrade?: EquipmentGrade;
  lostBonusStats: EquipmentBonus[];
  warning?: string;
}

/**
 * Ремонт с риском понижения грейда
 */
function repairWithGradeRisk(
  equipment: Equipment,
  repairSkill: number,
  materials: Material[],
  method: 'npc' | 'self' | 'qi'
): RepairResult {
  const currentGrade = equipment.gradeOverlay.grade;
  const gradeProps = GRADE_PROPERTIES[currentGrade];
  
  // Расчёт качества ремонта
  let quality = 0;
  
  switch (method) {
    case 'npc':
      quality = 0.9;  // NPC всегда чинит хорошо
      break;
    case 'self':
      quality = 0.3 + repairSkill * 0.02;  // 30-80%
      break;
    case 'qi':
      quality = 0.7 + (repairSkill * 0.01);  // 70-100%
      break;
  }
  
  // Восстановление прочности
  const repairAmount = Math.floor(equipment.durability.max * quality);
  equipment.durability.current = Math.min(
    equipment.durability.max,
    equipment.durability.current + repairAmount
  );
  equipment.durability.repairCount++;
  
  // Проверка понижения грейда
  if (quality < 0.5 && currentGrade !== 'damaged' && currentGrade !== 'common') {
    const downgradeChance = gradeProps.downgradeChance * (1 - quality);
    
    if (Math.random() < downgradeChance) {
      return downgradeGrade(equipment, quality);
    }
  }
  
  return {
    success: true,
    durabilityRestored: repairAmount,
    gradeChanged: false,
    lostBonusStats: [],
  };
}

/**
 * Понижение грейда
 */
function downgradeGrade(
  equipment: Equipment,
  repairQuality: number
): RepairResult {
  const gradeOrder: EquipmentGrade[] = ['damaged', 'common', 'refined', 'perfect', 'transcendent'];
  const currentIndex = gradeOrder.indexOf(equipment.gradeOverlay.grade);
  const newGrade = gradeOrder[Math.max(0, currentIndex - 1)];
  
  // Сохраняем потерянные бонусы для UI
  const lostBonusStats = [...equipment.gradeOverlay.bonusStats];
  
  // Обновляем грейд
  equipment.gradeOverlay.grade = newGrade;
  equipment.gradeOverlay.durabilityMultiplier = GRADE_PROPERTIES[newGrade].durabilityMultiplier;
  
  // Перегенерируем бонусы для нового грейда
  equipment.gradeOverlay.bonusStats = generateBonusStatsForGrade(newGrade, equipment);
  
  // Спецэффекты и техники теряются
  if (newGrade === 'damaged' || newGrade === 'common') {
    equipment.gradeOverlay.specialEffects = [];
    equipment.gradeOverlay.grantedTechniques = undefined;
  }
  
  // Пересчитываем прочность
  equipment.durability.max = calculateMaxDurability(equipment.baseStats.material, newGrade);
  equipment.durability.current = equipment.durability.max;
  equipment.durability.downgradeCount++;
  
  // Добавляем в историю
  equipment.gradeOverlay.gradeHistory.push({
    fromGrade: gradeOrder[currentIndex],
    toGrade: newGrade,
    reason: 'downgrade_repair',
    timestamp: Date.now(),
  });
  
  return {
    success: true,
    durabilityRestored: equipment.durability.max,
    gradeChanged: true,
    previousGrade: gradeOrder[currentIndex],
    newGrade,
    lostBonusStats,
    warning: `Качество предмета понизилось до "${GRADE_PROPERTIES[newGrade].name}"`,
  };
}
```

---

## 1️⃣5️⃣ ПОЧЕМУ НЕ ПРОКАЧКА УРОВНЯ ЭКИПИРОВКИ

### 15.1 Аргументация против повышения уровня

> *"Прокачка уровня эквипа, теоретически можно, но практически, зачем, пропадет смысл в поиске новых образцов."*

**Это верное утверждение. Вот почему:**

```
┌─────────────────────────────────────────────────────────────────────────┐
│          ПОЧЕМУ УРОВЕНЬ ЭКИПИРОВКИ НЕ ДОЛЖЕН ПОВЫШАТЬСЯ                  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ❌ ЕСЛИ УРОВЕНЬ МОЖНО ПРОКАЧАТЬ:                                        │
│                                                                          │
│   Игрок находит Железный Меч Ур.1                                       │
│           ↓                                                              │
│   Прокачивает до Ур.5, Ур.7, Ур.9...                                    │
│           ↓                                                              │
│   Зачем искать новые мечи?                                              │
│           ↓                                                              │
│   😢 ИГРА ТЕРЯЕТ СМЫСЛ ИССЛЕДОВАНИЯ                                     │
│                                                                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ✅ ЕСЛИ УРОВЕНЬ НЕИЗМЕНЕН:                                             │
│                                                                          │
│   Игрок находит Железный Меч Ур.1 (common)                              │
│           ↓                                                              │
│   Может улучшить ГРЕЙД: common → refined → perfect                      │
│           ↓                                                              │
│   Меч становится лучше, НО база остаётся Ур.1                           │
│           ↓                                                              │
│   Игрок ищет Меч Ур.3 из Стали → более мощная база                      │
│           ↓                                                              │
│   😊 МОТИВАЦИЯ ИСКАТЬ НОВОЕ СНАРЯЖЕНИЕ СОХРАНЯЕТСЯ                      │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 15.2 Что можно улучшать, а что нет

| Параметр | Можно улучшать? | Способ |
|----------|-----------------|--------|
| **Уровень предмета** | ❌ **НЕТ** | Неизменен |
| **Базовый урон/защита** | ❌ **НЕТ** | Зависит от уровня и материала |
| **Материал** | ❌ **НЕТ** | Определён при создании |
| **Грейд (качество)** | ✅ **ДА** | Апгрейд у кузнеца |
| **Доп. характеристики** | ✅ **ДА** | При повышении грейда |
| **Прочность** | ✅ **ДА** | При повышении грейда |
| **Спецэффекты** | ✅ **ДА** | При высоком грейде |

### 15.3 Пример прогрессии игрока

```
Уровень 1-2 культивации:
  → Железный Меч Ур.1 (common)
  → Кожаная Броня Ур.1 (common)

Уровень 3-4 культивации:
  → Стальной Меч Ур.3 (refined) ← Новая база!
  → Стальная Броня Ур.3 (common)

Уровень 5-6 культивации:
  → Дух. Железный Меч Ур.5 (perfect) ← Новый материал!
  → Дух. Железная Броня Ур.5 (refined)

Уровень 7-8 культивации:
  → Звёздный Клинок Ур.7 (transcendent) ← Легендарный материал!
  → Костя Дракона Броня Ур.7 (perfect)

Уровень 9 культивации:
  → Меч Пустоты Ур.9 (transcendent) ← Божественный материал!
  → Материя Хаоса Броня Ур.9 (transcendent)
```

---

## 1️⃣6️⃣ ОБНОВЛЁННЫЕ ЗАДАЧИ ДОРАБОТКИ

### Фаза 1: Интерфейсы системы Грейдов

**Файл:** `src/types/equipment-grades.ts`

**Задачи:**
1. [ ] Создать `EquipmentGrade` type
2. [ ] Создать `GRADE_PROPERTIES` константы
3. [ ] Создать `GradeOverlay` interface
4. [ ] Создать `WeaponBaseStats`, `ArmorBaseStats`, `JewelryBaseStats`
5. [ ] Создать `WeaponEquipment`, `ArmorEquipment`, `JewelryEquipment`
6. [ ] Создать `GradeChangeEvent` interface

---

### Фаза 2: Система материалов

**Файл:** `src/data/material-stats.ts`

**Задачи:**
1. [ ] Создать `MATERIAL_STATS` константы (5 тиров)
2. [ ] Создать функцию `getMaterialTier(material)`
3. [ ] Создать функцию `getMaterialByTier(tier)`
4. [ ] Создать функцию `calculateMaterialBonus(material, stat)`

---

### Фаза 3: Расчёт базовых параметров

**Файл:** `src/lib/game/equipment-calc.ts`

**Задачи:**
1. [ ] Создать `calculateWeaponBaseStats(level, type, material)`
2. [ ] Создать `calculateArmorBaseStats(level, slot, material)`
3. [ ] Создать `calculateMaxDurability(material, grade)`
4. [ ] Создать `calculateRequirements(baseStats)`

---

### Фаза 4: Система грейд-надстройки

**Файл:** `src/lib/game/grade-system.ts`

**Задачи:**
1. [ ] Создать `generateGradeOverlay(grade, equipment)`
2. [ ] Создать `generateBonusStatsForGrade(grade, equipment)`
3. [ ] Создать `generateSpecialEffects(grade, equipment)`
4. [ ] Создать `generateGrantedTechniques(grade, equipment)`

---

### Фаза 5: Система апгрейда

**Файл:** `src/lib/game/grade-upgrade.ts`

**Задачи:**
1. [ ] Создать `attemptGradeUpgrade(equipment, materials, stones, skill)`
2. [ ] Создать `getRequiredMaterialsForUpgrade(equipment, targetGrade)`
3. [ ] Создать `calculateUpgradeSuccessChance(skill, materials)`
4. [ ] Создать UI для апгрейда

---

### Фаза 6: Система понижения грейда

**Файл:** `src/lib/game/grade-downgrade.ts`

**Задачи:**
1. [ ] Создать `repairWithGradeRisk(equipment, skill, materials, method)`
2. [ ] Создать `downgradeGrade(equipment, repairQuality)`
3. [ ] Создать `calculateDowngradeChance(grade, quality)`
4. [ ] Создать UI предупреждения о риске

---

### Фаза 7: Генератор экипировки V2

**Файл:** `src/lib/generator/equipment-generator-v2.ts`

**Задачи:**
1. [ ] Создать двухэтапную генерацию (база + грейд)
2. [ ] Интегрировать `calculateWeaponBaseStats()`
3. [ ] Интегрировать `generateGradeOverlay()`
4. [ ] Обновить конвертацию в TempItem

---

### Фаза 8: Интеграция с боевой системой

**Задачи:**
1. [ ] Добавить расчёт урона с учётом грейда
2. [ ] Добавить потерю прочности
3. [ ] Добавить проверку состояния (condition)
4. [ ] Добавить визуальные эффекты грейда

---

## 🔗 Связанные документы

- [equip.md](./equip.md) — Типы экипировки, слоты, материалы
- [body.md](./body.md) — Система тела, части тела, HP
- [combat-system.md](./combat-system.md) — Боевая система
- [DAMAGE_FORMULAS_PROPOSAL.md](./DAMAGE_FORMULAS_PROPOSAL.md) — Формулы урона
- [technique-system.md](./technique-system.md) — Техники, мастерство
- [vitality-hp-system.md](./vitality-hp-system.md) — Vitality и HP частей тела
- [implementation-plan-body-development.md](./implementation-plan-body-development.md) — План развития тела

---

*Документ создан: 2026-03-14*
*Обновлён: 2026-03-14*
*Статус: Теоретические изыскания + Система Грейдов + Задачи доработки*
