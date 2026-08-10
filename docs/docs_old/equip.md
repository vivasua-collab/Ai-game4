# ⚔️ Унифицированная система экипировки

**Версия:** 3.0
**Обновлено:** 2026-03-14
**Статус:** 📋 Готов к внедрению

---

## 📋 Обзор

Документ описывает **унифицированную систему экипировки** для всех типов снаряжения с полной интеграцией системы Грейдов, материалов и прочности.

### Ключевая концепция: БАЗА + ГРЕЙД + МАТЕРИАЛ

> **Важно:** Экипировка состоит из трёх независимых слоёв:
> 1. **Базовый класс** (неизменен) — определяется уровнем, типом
> 2. **Материал** (неизменен) — определяет базовую прочность и свойства
> 3. **Грейд** (изменяем) — качество предмета, можно повышать или понижать

---

## 🏗️ Архитектура системы v3.0

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        ЭКИПИРОВКА (Equipment) v3.0                           │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ┌──────────────────────────────────────────────────────────────────────┐   │
│   │              БАЗОВЫЙ КЛАСС (Base Class) — НЕИЗМЕНЕН                   │   │
│   │  ├── Тип          — weapon, armor, jewelry, charger, tool, artifact   │   │
│   │  ├── Подтип       — sword, torso, ring, belt, ...                    │   │
│   │  ├── Уровень      — 1-9 (определяет базовые параметры)               │   │
│   │  └── Требования   — сила, ловкость, уровень культивации              │   │
│   └──────────────────────────────────────────────────────────────────────┘   │
│                              +                                               │
│   ┌──────────────────────────────────────────────────────────────────────┐   │
│   │              МАТЕРИАЛ (Material) — НЕИЗМЕНЕН                          │   │
│   │  ├── ID           — "iron", "spirit_iron", "dragon_bone", ...        │   │
│   │  ├── Тир          — 1-5 (определяет базовую прочность)               │   │
│   │  ├── Категория    — metal, leather, cloth, crystal, spirit, void     │   │
│   │  └── Свойства     — проводимость, твёрдость, особые эффекты         │   │
│   └──────────────────────────────────────────────────────────────────────┘   │
│                              +                                               │
│   ┌──────────────────────────────────────────────────────────────────────┐   │
│   │              ГРЕЙД (Grade) — ИЗМЕНЯЕМОЕ КАЧЕСТВО                      │   │
│   │  ├── Damaged     — Повреждённый (×0.5 параметров)                    │   │
│   │  ├── Common      — Обычный (×1.0, базовый)                           │   │
│   │  ├── Refined     — Улучшенный (×1.3-1.5)                             │   │
│   │  ├── Perfect     — Совершенный (×1.7-2.5)                            │   │
│   │  └── Transcendent— Превосходящий (×2.5-4.0)                          │   │
│   └──────────────────────────────────────────────────────────────────────┘   │
│                              =                                               │
│   ┌──────────────────────────────────────────────────────────────────────┐   │
│   │                    ИТОГОВЫЕ ПАРАМЕТРЫ                                 │   │
│   │  Эффективность = BaseStats × GradeMultiplier                         │   │
│   │  Прочность = MaterialBaseDurability × GradeMultiplier                │   │
│   │  Проводимость = MaterialConductivity × GradeMultiplier               │   │
│   │  Бонусы = генерируются при изменении грейда                          │   │
│   └──────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 0️⃣ ТИПЫ ЭКИПИРОВКИ

### 0.1 Категории и применимость грейдов

| Категория | Грейды | Главный параметр | Множитель грейда |
|-----------|--------|------------------|------------------|
| **Weapon** | ✅ 5 | Damage | ×0.5 - ×4.0 |
| **Armor** | ✅ 5 | Defense | ×0.5 - ×4.0 |
| **Jewelry** | ✅ 5 | Qi Conductivity | ×0.5 - ×4.0 |
| **Charger** | ✅ 5 | Conductivity | ×0.5 - ×2.5 |
| **Tool** | ⚠️ 3 | Effectiveness | ×0.5 - ×1.5 |
| **Consumable** | ❌ 0 | — | — |
| **Artifact** | ✅ 5 | Effect Power | ×0.5 - ×4.0 |
| **Implant** | ✅ 5 | Compatibility | ×0.5 - ×4.0 |

### 0.2 Подтипы экипировки

```typescript
// === ОРУЖИЕ ===
type WeaponSubtype =
  | 'weapon_unarmed'     // Кастеты, когти
  | 'weapon_dagger'      // Кинжал
  | 'weapon_sword'       // Меч
  | 'weapon_greatsword'  // Двуручный меч
  | 'weapon_axe'         // Топор
  | 'weapon_mace'        // Булава
  | 'weapon_spear'       // Копьё
  | 'weapon_staff'       // Посох
  | 'weapon_polearm'     // Древковое
  | 'weapon_bow'         // Лук
  | 'weapon_crossbow'    // Арбалет
  | 'weapon_focus'       // Магический фокус
  | 'weapon_talisman';   // Талисман

// === БРОНЯ ===
type ArmorSubtype =
  | 'armor_head'         // Шлем
  | 'armor_torso'        // Нагрудник
  | 'armor_arms'         // Наручи
  | 'armor_hands'        // Перчатки
  | 'armor_legs'         // Поножи
  | 'armor_feet'         // Сапоги
  | 'armor_full';        // Полный доспех

// === УКРАШЕНИЯ ===
type JewelrySubtype =
  | 'jewelry_ring'       // Кольцо
  | 'jewelry_necklace'   // Ожерелье
  | 'jewelry_earring'    // Серьга
  | 'jewelry_bracelet';  // Браслет

// === ЗАРЯДНИКИ ЦИ ===
type ChargerSubtype =
  | 'charger_belt'       // Пояс-накопитель
  | 'charger_bracelet'   // Браслет-накопитель
  | 'charger_necklace'   // Ожерелье-накопитель
  | 'charger_ring'       // Кольцо-накопитель
  | 'charger_backpack';  // Ранец-накопитель

// === ИНСТРУМЕНТЫ ===
type ToolSubtype =
  | 'tool_crafting'      // Ремесленный
  | 'tool_gathering'     // Сбора
  | 'tool_exploration'   // Исследования
  | 'tool_medical';      // Медицинский

// === РАСХОДНИКИ ===
type ConsumableSubtype =
  | 'consumable_pill'      // Таблетка
  | 'consumable_potion'    // Зелье
  | 'consumable_scroll'    // Свиток
  | 'consumable_talisman'  // Талисман
  | 'consumable_food'      // Еда
  | 'consumable_drink';    // Напиток

// === АРТЕФАКТЫ ===
type ArtifactSubtype =
  | 'artifact_passive'     // Пассивный
  | 'artifact_active'      // Активируемый
  | 'artifact_soulbound'   // Связанный с душой
  | 'artifact_cursed';     // Проклятый

// === ИМПЛАНТЫ ===
type ImplantSubtype =
  | 'implant_eye'        // Глазной
  | 'implant_limbs'      // Протез
  | 'implant_internal'   // Внутренний орган
  | 'implant_neural'     // Нейронный
  | 'implant_core';      // Ядра
```

---

## 1️⃣ СИСТЕМА ГРЕЙДОВ (КАЧЕСТВА)

### 1.1 Уровни грейда

```typescript
type EquipmentGrade = 
  | 'damaged'       // Повреждённый (ниже базового)
  | 'common'        // Обычный (базовый)
  | 'refined'       // Улучшенный
  | 'perfect'       // Совершенный
  | 'transcendent'; // Превосходящий
```

### 1.2 Свойства грейдов

| Грейд | Цвет | Прочность | Эффективность | Бонусы | Эффекты | Техники |
|-------|------|-----------|---------------|--------|---------|---------|
| **Damaged** | 🔴 Красный | ×0.5 | ×0.5 | 0 | 0% | — |
| **Common** | ⚪ Серый | ×1.0 | ×1.0 | 0-1 | 0% | — |
| **Refined** | 🟢 Зелёный | ×1.5 | ×1.3 | 1-2 | 20% | — |
| **Perfect** | 🔵 Синий | ×2.5 | ×1.7 | 2-4 | 50% | 10% |
| **Transcendent** | 🟡 Золотой | ×4.0 | ×2.5 | 4-6 | 80% | 30% |

### 1.3 Конфигурация грейдов

```typescript
const GRADE_CONFIG: Record<EquipmentGrade, GradeProperties> = {
  damaged: {
    name: 'Повреждённый',
    color: 'red',
    durabilityMultiplier: 0.5,
    effectivenessMultiplier: 0.5,
    bonusSlots: 0,
    effectChance: 0,
    techniqueChance: 0,
    canUpgradeTo: 'common',
    upgradeCost: null, // Нельзя улучшить напрямую
  },
  common: {
    name: 'Обычный',
    color: 'gray',
    durabilityMultiplier: 1.0,
    effectivenessMultiplier: 1.0,
    bonusSlots: 1,
    effectChance: 0,
    techniqueChance: 0,
    canUpgradeTo: 'refined',
    upgradeCost: { materials: 1, spiritStones: 100 },
  },
  refined: {
    name: 'Улучшенный',
    color: 'green',
    durabilityMultiplier: 1.5,
    effectivenessMultiplier: 1.3,
    bonusSlots: 2,
    effectChance: 0.2,
    techniqueChance: 0,
    canUpgradeTo: 'perfect',
    upgradeCost: { materials: 3, spiritStones: 500 },
  },
  perfect: {
    name: 'Совершенный',
    color: 'blue',
    durabilityMultiplier: 2.5,
    effectivenessMultiplier: 1.7,
    bonusSlots: 4,
    effectChance: 0.5,
    techniqueChance: 0.1,
    canUpgradeTo: 'transcendent',
    upgradeCost: { materials: 10, spiritStones: 2000 },
  },
  transcendent: {
    name: 'Превосходящий',
    color: 'gold',
    durabilityMultiplier: 4.0,
    effectivenessMultiplier: 2.5,
    bonusSlots: 6,
    effectChance: 0.8,
    techniqueChance: 0.3,
    canUpgradeTo: null,
    upgradeCost: null,
  },
};
```

### 1.4 Универсальная структура грейд-надстройки

```typescript
interface GradeOverlay {
  grade: EquipmentGrade;
  
  // Множители
  durabilityMultiplier: number;
  effectivenessMultiplier: number;
  
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
  timestamp: Date;
  fromGrade: EquipmentGrade;
  toGrade: EquipmentGrade;
  reason: 'upgrade' | 'downgrade_repair' | 'downgrade_damage' | 'initial';
  quality?: number; // Качество операции
}
```

### 1.5 Изменение грейда

**Повышение (апгрейд):**
- Требуются материалы + духовные камни
- Шанс успеха: 50-95% (зависит от материала и уровня кузнеца)
- При успехе: новые бонусы, выше прочность
- При провале: материалы потеряны, грейд не изменён

**Понижение:**
- При плохом ремонте (качество < 50%)
- Шанс: 25-40%
- Теряются бонусы и эффекты
- Максимальная прочность пересчитывается

---

## 2️⃣ СИСТЕМА МАТЕРИАЛОВ

> **Подробнее:** См. [materials.md](./materials.md)

### 2.1 Уровни материалов (тиры)

| Тир | Материалы | Прочность | Проводимость Ци | Уровень предметов |
|-----|-----------|-----------|-----------------|-------------------|
| **1** | Iron, Leather, Cloth, Wood, Bone | 20-50 | 0.3-0.8 | 1-2 |
| **2** | Steel, Silk, Silver, Treated Leather | 50-80 | 0.4-1.2 | 3-4 |
| **3** | Spirit Iron, Cold Iron, Jade, Spirit Crystal | 80-150 | 1.0-2.0 | 5-6 |
| **4** | Star Metal, Dragon Bone, Elemental Core | 150-400 | 2.0-3.5 | 7-8 |
| **5** | Void Matter, Chaos Matter, Primordial Essence | 400-600 | 4.0-5.0 | 9 |

### 2.2 Выбор материала

```typescript
interface MaterialSelection {
  // По уровню предмета
  byLevel: (level: number) => MaterialTier;
  
  // По редкости
  byRarity: (grade: EquipmentGrade) => MaterialTier;
  
  // По типу экипировки
  byType: (type: EquipmentType, tier: MaterialTier) => MaterialDefinition;
}
```

### 2.3 Свойства материалов

```typescript
interface MaterialProperties {
  // Базовые
  baseDurability: number;        // Базовая прочность
  weight: number;                // Вес
  hardness: number;              // Твёрдость (1-10)
  flexibility: number;           // Гибкость (0-1)
  
  // Ци
  qiConductivity: number;        // Проводимость
  qiRetention: number;           // Сохранение (%/час)
  
  // Бонусы
  damageBonus?: number;          // Бонус к урону
  defenseBonus?: number;         // Бонус к защите
  penetrationBonus?: number;     // Бонус к пробитию
  
  // Особые
  specialProperties: string[];   // Массив особых свойств
}
```

---

## 3️⃣ СИСТЕМА ПРОЧНОСТИ

### 3.1 Состояния экипировки

| Состояние | Прочность | Эффективность | Цвет |
|-----------|-----------|---------------|------|
| Pristine | 100% | 100% | 🟢 Зелёный |
| Excellent | 80-99% | 95% | 🟢 Светло-зелёный |
| Good | 60-79% | 85% | 🟡 Жёлтый |
| Worn | 40-59% | 70% | 🟠 Оранжевый |
| Damaged | 20-39% | 50% | 🔴 Красный |
| Broken | < 20% | 20% | ⚫ Чёрный |

### 3.2 Расчёт максимальной прочности

```typescript
function calculateMaxDurability(
  material: MaterialDefinition,
  grade: EquipmentGrade,
  level: number
): number {
  const baseDurability = material.properties.baseDurability;
  const gradeMultiplier = GRADE_CONFIG[grade].durabilityMultiplier;
  const levelBonus = 1 + (level - 1) * 0.05; // +5% за уровень
  
  return Math.floor(baseDurability * gradeMultiplier * levelBonus);
}
```

### 3.3 Потеря прочности (комбинированный метод)

```typescript
function loseDurability(
  equipment: Equipment,
  action: 'attack' | 'block' | 'absorb' | 'use',
  damageAbsorbed?: number
): void {
  const material = equipment.material;
  const baseLoss = DURABILITY_LOSS_BY_ACTION[action];
  const hardnessFactor = 1 / material.properties.hardness;
  
  // Базовый износ
  let totalLoss = baseLoss * hardnessFactor;
  
  // Ударный износ (при сильных ударах)
  const threshold = material.properties.hardness * 10;
  if (damageAbsorbed && damageAbsorbed > threshold) {
    totalLoss += (damageAbsorbed - threshold) / 10;
  }
  
  // Применяем
  equipment.durability.current = Math.max(0, equipment.durability.current - totalLoss);
  
  // Обновляем состояние
  equipment.durability.condition = calculateCondition(equipment.durability);
}
```

### 3.4 Ремонт экипировки

```typescript
interface RepairResult {
  success: boolean;
  newDurability: number;
  gradeChanged: boolean;
  oldGrade?: EquipmentGrade;
  newGrade?: EquipmentGrade;
  lostBonusStats: EquipmentBonus[];
  message: string;
}

function repairEquipment(
  equipment: Equipment,
  options: RepairOptions
): RepairResult {
  const quality = calculateRepairQuality(options);
  
  // Базовый ремонт
  const repairAmount = options.skill * 10;
  let newDurability = Math.min(
    equipment.durability.max,
    equipment.durability.current + repairAmount
  );
  
  // Проверка понижения грейда
  if (quality < 0.5 && equipment.gradeOverlay.grade !== 'damaged') {
    const downgradeChance = (1 - quality) * 0.4;
    
    if (Math.random() < downgradeChance) {
      return performDowngrade(equipment, newDurability);
    }
  }
  
  return {
    success: true,
    newDurability,
    gradeChanged: false,
    message: `Прочность восстановлена до ${newDurability}`,
  };
}
```

---

## 4️⃣ БОНУСЫ ЭКИПИРОВКИ

### 4.1 Типы бонусов

```typescript
type EquipmentBonusType = 
  // === ХАРАКТЕРИСТИКИ ===
  | 'strength'           // Сила
  | 'agility'            // Ловкость
  | 'intelligence'       // Интеллект
  | 'vitality'           // Жизненная сила
  
  // === ЦИ И КУЛЬТИВАЦИЯ ===
  | 'qiMax'              // Максимальная Ци
  | 'qiRegen'            // Регенерация Ци
  | 'conductivity'       // Проводимость меридиан
  | 'coreCapacity'       // Ёмкость ядра
  
  // === БОЕВЫЕ ===
  | 'damage'             // Урон
  | 'armor'              // Броня
  | 'armorPenetration'   // Пробитие
  | 'critChance'         // Шанс крита
  | 'critDamage'         // Крит. урон
  | 'dodgeChance'        // Уклонение
  | 'blockChance'        // Блок
  
  // === ЗАЩИТНЫЕ ===
  | 'healthMax'          // Макс. HP
  | 'resistance_fire'    // Сопротивление огню
  | 'resistance_water'   // Сопротивление воде
  | 'resistance_earth'   // Сопротивление земле
  | 'resistance_air'     // Сопротивление воздуху
  | 'resistance_void';   // Сопротивление пустоте
```

### 4.2 Структура бонуса

```typescript
interface EquipmentBonus {
  type: EquipmentBonusType;
  value: number;
  isPercent: boolean;         // true = %, false = абсолютный
  source: 'base' | 'grade' | 'material' | 'set';
}
```

### 4.3 Генерация бонусов по грейду

```typescript
function generateBonusStatsForGrade(
  grade: EquipmentGrade,
  level: number,
  equipmentType: EquipmentType,
  rng: () => number
): EquipmentBonus[] {
  const config = GRADE_CONFIG[grade];
  const bonuses: EquipmentBonus[] = [];
  
  // Количество бонусов
  const numBonuses = config.bonusSlots;
  
  // Доступные типы бонусов по типу экипировки
  const availableTypes = getAvailableBonusTypes(equipmentType);
  
  for (let i = 0; i < numBonuses && availableTypes.length > 0; i++) {
    const typeIndex = Math.floor(rng() * availableTypes.length);
    const bonusType = availableTypes.splice(typeIndex, 1)[0];
    
    // Величина бонуса
    const baseValue = calculateBaseBonusValue(bonusType, level);
    const gradeMultiplier = config.effectivenessMultiplier;
    
    bonuses.push({
      type: bonusType,
      value: Math.floor(baseValue * gradeMultiplier),
      isPercent: PERCENT_BONUS_TYPES.includes(bonusType),
      source: 'grade',
    });
  }
  
  return bonuses;
}
```

---

## 5️⃣ ДАРУЕМЫЕ ТЕХНИКИ

### 5.1 Структура

```typescript
interface GrantedTechnique {
  techniqueId: string;
  
  // Заряды
  charges: {
    current: number;
    max: number;
    recharge: RechargeType;
  };
  
  // Требования
  requirements?: {
    cultivationLevel?: number;
    qiCost?: number;
  };
  
  // Модификаторы техники
  modifiers?: {
    damageMultiplier?: number;
    costMultiplier?: number;
  };
}
```

### 5.2 Типы перезарядки

```typescript
type RechargeType = 
  | { type: 'none' }                    // Не перезаряжается
  | { type: 'time'; hours: number }     // Временем
  | { type: 'qi'; amount: number }      // Ци
  | { type: 'spirit_stone'; amount: number }  // Камнями
  | { type: 'combat'; perKill: number } // За убийства
  | { type: 'meditation'; hours: number }; // Медитацией
```

---

## 6️⃣ СЛОТЫ ЭКИПИРОВКИ

### 6.1 Основные слоты

```typescript
type EquipmentSlot = 
  // === БРОНЯ ===
  | 'head'              // Голова
  | 'torso'             // Торс
  | 'arms'              // Руки (наручи)
  | 'hands'             // Кисти (перчатки)
  | 'legs'              // Ноги
  | 'feet'              // Ступни
  
  // === ОДЕЖДА ===
  | 'body'              // Тело (роба)
  | 'cloak'             // Плащ
  | 'belt'              // Пояс
  
  // === УКРАШЕНИЯ ===
  | 'ring_left'         // Кольцо левое
  | 'ring_right'        // Кольцо правое
  | 'necklace'          // Ожерелье
  | 'earring'           // Серьга
  | 'bracelet'          // Браслет
  
  // === ОРУЖИЕ ===
  | 'weapon_main'       // Основная рука
  | 'weapon_off'        // Вторичная рука
  | 'weapon_twohanded'  // Двуручное
  
  // === ИНВЕНТАРЬ ===
  | 'backpack'          // Рюкзак
  | 'pouch';            // Кошель
```

### 6.2 Конфликты слотов

```typescript
const SLOT_CONFLICTS: Record<EquipmentSlot, EquipmentSlot[]> = {
  weapon_main: ['weapon_twohanded'],
  weapon_off: ['weapon_twohanded'],
  weapon_twohanded: ['weapon_main', 'weapon_off'],
  torso: ['body'],
  body: ['torso'],
  ring_left: [],
  ring_right: [],
  // ...
};
```

---

## 7️⃣ ТРЕБОВАНИЯ К ЭКИПИРОВКЕ

### 7.1 Структура требований

```typescript
interface EquipmentRequirements {
  // Уровень культивации
  cultivationLevel?: number | { min: number; max?: number };
  
  // Характеристики
  stats?: {
    strength?: number;
    agility?: number;
    intelligence?: number;
    vitality?: number;
    conductivity?: number;
  };
  
  // Вид
  species?: SpeciesType[] | 'any';
  
  // Части тела
  requiredBodyParts?: string[];
  intactBodyParts?: string[];
  
  // Прочее
  quests?: string[];
  achievements?: string[];
}
```

---

## 8️⃣ СЕТОВЫЕ БОНУСЫ

### 8.1 Структура сета

```typescript
interface EquipmentSet {
  id: string;
  name: string;
  pieces: string[];
  
  setBonuses: {
    pieces: number;
    bonuses: EquipmentBonus[];
    grantedTechniques?: GrantedTechnique[];
  }[];
}
```

### 8.2 Пример сета

```typescript
const fireLordSet: EquipmentSet = {
  id: 'fire_lord',
  name: 'Сет Огненного Владыки',
  pieces: ['fire_lord_helmet', 'fire_lord_armor', 'fire_lord_gauntlets', 'fire_lord_boots', 'fire_lord_ring'],
  setBonuses: [
    {
      pieces: 2,
      bonuses: [
        { type: 'resistance_fire', value: 20, isPercent: true, source: 'set' },
      ],
    },
    {
      pieces: 3,
      bonuses: [
        { type: 'resistance_fire', value: 40, isPercent: true, source: 'set' },
        { type: 'intelligence', value: 5, isPercent: false, source: 'set' },
      ],
      grantedTechniques: [{
        techniqueId: 'fire_aura',
        charges: { current: -1, max: -1, recharge: { type: 'none' } },
      }],
    },
    {
      pieces: 5,
      bonuses: [
        { type: 'resistance_fire', value: 80, isPercent: true, source: 'set' },
        { type: 'intelligence', value: 15, isPercent: false, source: 'set' },
      ],
    },
  ],
};
```

---

## 9️⃣ СПЕЦИФИКА ПО ТИПАМ

### 9.1 Оружие

**Базовые параметры:**
- damage — урон
- penetration — пробитие
- range — дальность
- attackSpeed — скорость атаки

**Влияние грейда:**
| Грейд | Множитель урона | Бонусы |
|-------|-----------------|--------|
| Damaged | ×0.5 | 0 |
| Common | ×1.0 | 0-1 |
| Refined | ×1.3 | 1-2 |
| Perfect | ×1.7 | 2-4 |
| Transcendent | ×2.5 | 4-6 |

### 9.2 Броня

**Базовые параметры:**
- defense — защита
- resistances — сопротивления
- moveSpeedPenalty — штраф скорости
- dodgePenalty — штраф уклонения

**Влияние грейда:**
| Грейд | Множитель защиты | Бонусы |
|-------|------------------|--------|
| Damaged | ×0.5 | 0 |
| Common | ×1.0 | 0-1 |
| Refined | ×1.3 | 1-2 |
| Perfect | ×1.7 | 2-4 |
| Transcendent | ×2.5 | 4-6 |

### 9.3 Зарядники Ци

**Базовые параметры:**
- conductivity — проводимость (ед/сек)
- bufferCapacity — ёмкость буфера
- slotCount — количество слотов для камней
- heatResistance — термостойкость

**Влияние грейда:**
| Грейд | Проводимость | Буфер | Слоты | Эффективность |
|-------|-------------|-------|-------|---------------|
| Damaged | ×0.5 | ×0.5 | +0 | -10% |
| Common | ×1.0 | ×1.0 | +0 | 0% |
| Refined | ×1.3 | ×1.5 | +1 | +5% |
| Perfect | ×1.7 | ×2.5 | +2 | +15% |
| Transcendent | ×2.5 | ×4.0 | +2 | +30% |

---

## 🔟 ПОЛНАЯ СТРУКТУРА ЭКИПИРОВКИ

### 10.1 Интерфейс TypeScript

```typescript
/**
 * Полная структура экипировки v3.0
 */
interface Equipment {
  // === ИДЕНТИФИКАЦИЯ ===
  id: string;
  name: string;
  nameEn: string;
  description: string;
  
  // === КЛАССИФИКАЦИЯ ===
  type: EquipmentType;          // weapon, armor, jewelry, charger, tool, artifact, implant
  subtype: string;              // sword, torso, ring, belt, ...
  
  // === БАЗОВЫЙ КЛАСС (неизменен) ===
  baseStats: EquipmentBaseStats;
  level: number;                // 1-9
  requirements: EquipmentRequirements;
  
  // === МАТЕРИАЛ (неизменен) ===
  materialId: string;
  material: MaterialDefinition;
  
  // === ГРЕЙД (изменяем) ===
  gradeOverlay: GradeOverlay;
  
  // === ПРОЧНОСТЬ ===
  durability: DurabilityState;
  
  // === БОНУСЫ (вычисляемые) ===
  computedStats: ComputedStats;
  
  // === СЕТ ===
  setId?: string;
  isSetItem: boolean;
  
  // === СТОИМОСТЬ ===
  baseValue: number;
  
  // === МЕТАДАННЫЕ ===
  icon?: string;
  createdAt: Date;
  updatedAt: Date;
}

interface DurabilityState {
  current: number;
  max: number;
  condition: EquipmentCondition;
  repairCount: number;
  lastRepairQuality?: number;
}

type EquipmentCondition = 
  | 'pristine'    // 100%
  | 'excellent'   // 80-99%
  | 'good'        // 60-79%
  | 'worn'        // 40-59%
  | 'damaged'     // 20-39%
  | 'broken';     // <20%

interface ComputedStats {
  // Суммарные бонусы от всех источников
  total: Record<string, number>;
  
  // По источникам
  bySource: {
    base: EquipmentBonus[];
    material: EquipmentBonus[];
    grade: EquipmentBonus[];
    set?: EquipmentBonus[];
  };
  
  // Эффективность (учитывая состояние)
  effectiveness: number;
}
```

---

## 1️⃣1️⃣ ИНТЕГРАЦИЯ С БАЗОЙ ДАННЫХ

### 11.1 Prisma Schema

```prisma
// Расширенная модель InventoryItem
model InventoryItem {
  id        String   @id @default(cuid())
  createdAt DateTime @default(now())

  characterId String
  character   Character @relation(fields: [characterId], references: [id], onDelete: Cascade)

  // === ИДЕНТИФИКАЦИЯ ===
  name        String
  nameId      String?     // Для поиска пресета
  description String?
  type        String      // weapon, armor, jewelry, charger, tool, consumable, artifact, implant
  subtype     String?     // sword, torso, ring, belt, ...
  category    String      // weapon, armor, accessory, consumable, material, technique, quest, misc
  icon        String      // Эмодзи или путь к иконке

  // === УРОВЕНЬ ===
  level       Int     @default(1)

  // === МАТЕРИАЛ ===
  materialId  String?     // ID материала из реестра
  
  // === ГРЕЙД ===
  grade       String  @default("common")  // damaged, common, refined, perfect, transcendent
  gradeHistory String? @default("[]")     // JSON: GradeChangeEvent[]
  
  // === ПРОЧНОСТЬ ===
  durability      Int?    // Текущая прочность
  maxDurability   Int?    // Максимальная прочность
  condition       String? @default("pristine")  // pristine, excellent, good, worn, damaged, broken
  repairCount     Int     @default(0)
  
  // === БОНУСЫ ===
  bonusStats      String? @default("[]")  // JSON: EquipmentBonus[]
  specialEffects  String? @default("[]")  // JSON: SpecialEffect[]
  grantedTechniques String? @default("[]") // JSON: GrantedTechnique[]
  
  // === СЕТ ===
  setId           String?
  isSetItem       Boolean @default(false)
  
  // === ЗАРЯД ЦИ (для артефактов) ===
  qiCharge    Int?
  maxQiCharge Int?

  // === ТРЕБОВАНИЯ ===
  requirements String? // JSON: EquipmentRequirements

  // === СТОИМОСТЬ ===
  value       Int     @default(0)
  currency    String  @default("spirit_stones")

  // === ФЛАГИ ===
  isEquipped  Boolean @default(false)
  isBound     Boolean @default(false)
  isQuestItem Boolean @default(false)
  
  // === ПОЗИЦИЯ В ИНВЕНТАРЕ ===
  location    String  @default("inventory")
  equipmentSlot String?
  posX        Int?
  posY        Int?
  
  // === СТЕКИ ===
  quantity    Int     @default(1)
  maxStack    Int     @default(1)
  stackable   Boolean @default(false)
  
  // === ВЕС И РАЗМЕР ===
  weight      Float   @default(0.0)
  sizeWidth   Int     @default(1)
  sizeHeight  Int     @default(1)

  // Индексы
  @@index([characterId])
  @@index([category])
  @@index([type])
  @@index([grade])
  @@index([materialId])
  @@index([location])
}

// Материалы (для динамического расширения)
model Material {
  id          String   @id
  name        String
  nameEn      String
  category    String
  tier        Int
  
  properties  String   // JSON: MaterialProperties
  compatibleTypes String // JSON: string[]
  
  description String?
  icon        String?
  
  isBaseGame  Boolean  @default(true)
  isActive    Boolean  @default(true)
  
  createdAt   DateTime @default(now())
  updatedAt   DateTime @updatedAt
  
  @@index([category])
  @@index([tier])
  @@map("materials")
}
```

---

## 🔗 Связанные документы

- [materials.md](./materials.md) — Система материалов с ID
- [weapon-armor-system.md](./weapon-armor-system.md) — Оружие, броня, формулы
- [charger.md](./charger.md) — Зарядники Ци
- [body.md](./body.md) — Система тела
- [combat-system.md](./combat-system.md) — Боевая система

---

## 📝 История изменений

| Дата | Версия | Изменение |
|------|--------|-----------|
| 2026-02-28 | 1.0 | Создан документ |
| 2026-03-14 | 2.0 | Архитектура "База + Грейд" |
| 2026-03-14 | 3.0 | Интеграция материалов, полная структура БД |

---

*Документ обновлён: 2026-03-14*
