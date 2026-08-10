# ⚔️ Унифицированная система экипировки v2.0

**Версия:** 2.0
**Обновлено:** 2026-03-14
**Статус:** ✅ Реализовано

---

## ✅ Статус реализации

| Компонент | Файл | Статус |
|-----------|------|--------|
| Оркестратор | `equipment-generator-v2.ts` | ✅ DONE |
| Grade System | `grade-system.ts` | ✅ DONE |
| Materials Registry | `materials-registry.ts` | ✅ DONE |
| Durability System | `durability-system.ts` | ✅ DONE |
| Bonus Registry | `bonus-registry-runtime.ts` | ✅ DONE |
| API endpoint | `api/generator/equipment/route.ts` | ✅ DONE |

---

## 📋 Обзор

Архитектура "Матрёшка" для ВСЕХ типов снаряжения.

### Принцип разделения

```
┌─────────────────────────────────────────────────────────────────────────┐
│                   АРХИТЕКТУРА ЭКИПИРОВКИ v2.0                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   БАЗОВЫЙ КЛАСС (Base Class) — НЕИЗМЕНЕН                                 │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │ • Тип экипировки (weapon, armor, jewelry, charger, tool, ...)  │   │
│   │ • Уровень предмета (1-9)                                        │   │
│   │ • Материал (определяет базовые параметры)                      │   │
│   │ • Требования (сила, ловкость, уровень культивации)             │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                              +                                            │
│   ГРЕЙД (Grade Overlay) — ИЗМЕНЯЕМ                                        │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │ • Качество: Damaged → Common → Refined → Perfect → Transcendent │   │
│   │ • Множители параметров (×0.5 ... ×4.0)                          │   │
│   │ • Дополнительные характеристики                                 │   │
│   │ • Специальные эффекты / Даруемые техники                        │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                              =                                            │
│   ИТОГОВЫЙ ПРЕДМЕТ                                                       │
│   Эффективность = BaseStats × GradeMultiplier                            │
│   Прочность = MaterialBase × GradeMultiplier                             │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 1️⃣ ПОТОК ГЕНЕРАЦИИ

```
generateEquipmentV2(options)
    │
    ├── 1. createRNG(seed)           → Функция случайных чисел
    ├── 2. selectMaterial(options)   → MaterialDefinition из реестра
    ├── 3. selectGrade(options)      → EquipmentGrade по распределению
    │
    └── 4. switch(type):
          ├── weapon   → generateWeaponV2(ctx)
          ├── armor    → generateArmorV2(ctx)
          ├── charger  → generateChargerV2(ctx)
          ├── accessory → generateAccessoryV2(ctx)
          └── artifact → generateArtifactV2(ctx)

Каждый генератор:
    1. getBaseStats(level, rng)           → { damage, defense, qiConductivity, weight }
    2. applyMaterialTo*(base, material)   → добавляет бонусы материала
    3. applyGradeToStats(stats, grade)    → применяет множители грейда
    4. createDurabilityState(material, grade, level) → прочность
    5. generateEquipmentBonuses(grade, level, type, rng) → бонусы
    6. generateName(material, grade, level, rng) → название
    7. generateRequirements(level, stats) → требования
    8. calculateEquipmentValue(stats, material, grade, level) → стоимость
```

---

## 2️⃣ СИСТЕМА ГРЕЙДОВ

### Реализовано в `grade-system.ts`

```typescript
type EquipmentGrade = 
  | 'damaged'      // Повреждённый (×0.5 прочности, ×0.8 урона)
  | 'common'       // Обычный (×1.0)
  | 'refined'      // Улучшенный (×1.5 прочности, ×1.3 урона)
  | 'perfect'      // Совершенный (×2.5 прочности, ×1.7 урона)
  | 'transcendent'; // Превосходящий (×4.0 прочности, ×2.5 урона)

const GRADE_CONFIGS: Record<EquipmentGrade, GradeConfig> = {
  damaged:     { durabilityMultiplier: 0.5, damageMultiplier: 0.8, bonusCount: [0, 0] },
  common:      { durabilityMultiplier: 1.0, damageMultiplier: 1.0, bonusCount: [0, 1] },
  refined:     { durabilityMultiplier: 1.5, damageMultiplier: 1.3, bonusCount: [1, 2] },
  perfect:     { durabilityMultiplier: 2.5, damageMultiplier: 1.7, bonusCount: [2, 4] },
  transcendent: { durabilityMultiplier: 4.0, damageMultiplier: 2.5, bonusCount: [4, 6] },
};
```

### Распределение грейдов по уровню

```typescript
// Уровень 1: 30% damaged, 60% common, 10% refined
// Уровень 3: 10% damaged, 50% common, 35% refined, 5% perfect
// Уровень 5: 5% damaged, 30% common, 45% refined, 20% perfect
// Уровень 7: 20% common, 40% refined, 35% perfect, 5% transcendent
// Уровень 9: 10% common, 30% refined, 40% perfect, 20% transcendent
```

---

## 3️⃣ МАТЕРИАЛЫ

### Реализовано в `materials-registry.ts`

```typescript
type MaterialTier = 1 | 2 | 3 | 4 | 5;

// T1 (уровень 1-2): Железо, Кожа, Ткань, Кость, Дерево, Камень
// T2 (уровень 3-4): Сталь, Бронза, Шёлк, Слоновая кость, Твёрдое дерево, Мрамор
// T3 (уровень 5-6): Духовное железо, Хладное железо, Духовный шёлк, Нефрит
// T4 (уровень 7-8): Звёздный металл, Кость дракона, Небесный шёлк, Духовный кристалл
// T5 (уровень 9): Пустотная материя, Хаотичная материя, Первородная эссенция
```

### Свойства материала

```typescript
interface MaterialProperties {
  durability: number;       // Базовая прочность
  qiConductivity: number;   // Проводимость Ци (%)
  weight: number;           // Вес
  hardness: number;         // Твёрдость (1-10)
  flexibility: number;      // Гибкость (1-10)
}

interface MaterialDefinition {
  id: string;
  name: string;
  tier: MaterialTier;
  category: 'metal' | 'organic' | 'mineral' | 'wood' | 'crystal';
  properties: MaterialProperties;
  bonuses: MaterialBonus[];
  description: string;
  rarity: number;           // Вес для случайного выбора
  source: string;           // Источник в мире
  requiredLevel?: number;   // Требуемый уровень культивации
}
```

---

## 4️⃣ ПРОЧНОСТЬ

### Реализовано в `durability-system.ts`

```typescript
type DurabilityCondition = 
  | 'pristine'   // 100-90% → 100% эффективности
  | 'good'       // 89-70% → 95% эффективности
  | 'worn'       // 69-50% → 85% эффективности
  | 'damaged'    // 49-20% → 70% эффективности
  | 'broken';    // <20% → 50% эффективности, нельзя экипировать

// Формула: maxDurability = baseDurability × gradeMultiplier + level × 2
```

---

## 5️⃣ БОНУСЫ

### Реализовано в `bonus-registry-runtime.ts`

```typescript
type BonusCategory = 
  | 'combat'     // Урон, крит, пробитие, скорость атаки
  | 'defense'    // Броня, уклонение, здоровье, блок
  | 'qi'         // Регенерация Ци, снижение стоимости, проводимость
  | 'elemental'  // Огонь, холод, молния, земля, ветер
  | 'special'    // Вампиризм, шипы, удача, бонус опыта
  | 'utility';   // Скорость, радиус подбора, вместимость, скрытность

// Генерация бонусов по категориям:
// weapon → combat, elemental, special
// armor → defense, elemental, special
// charger → qi, elemental, special
// accessory → utility, special, defense
```

---

## 6️⃣ ИСПОЛЬЗОВАНИЕ

### API Endpoint

```
POST /api/generator/equipment
{
  "action": "generate",
  "type": "weapon",       // weapon, armor, accessory, charger
  "level": 5,             // 1-9 или null для всех уровней
  "grade": "refined",     // опционально
  "materialId": "iron",   // опционально
  "count": 50,
  "mode": "append"        // replace или append
}
```

### Программно

```typescript
import { generateEquipmentV2, generateEquipmentBatch } from '@/lib/generator/equipment-generator-v2';

// Одиночная генерация
const weapon = generateEquipmentV2({
  type: 'weapon',
  level: 5,
  grade: 'refined',
  materialId: 'spirit_iron',
  seed: 12345,
});

// Массовая генерация
const weapons = generateEquipmentBatch(50, { type: 'weapon', level: 5 });
```

---

## 7️⃣ СТРУКТУРА СГЕНЕРИРОВАННОГО ОБЪЕКТА

```typescript
interface GeneratedEquipmentV2 {
  id: string;
  type: EquipmentType;
  name: string;
  level: number;
  
  // Материал
  materialId: string;
  material: MaterialDefinition;
  
  // Грейд
  grade: EquipmentGrade;
  gradeConfig: GradeConfig;
  
  // Эффективные параметры
  effectiveStats: {
    damage: number;
    defense: number;
    qiConductivity: number;
    weight: number;
  };
  
  // Прочность
  durability: DurabilityState;
  
  // Бонусы
  bonuses: GeneratedBonus[];
  
  // Специальные эффекты
  specialEffects: string[];
  
  // Требования
  requirements: EquipmentRequirements;
  
  // Стоимость
  value: number;
}
```

---

## 🔗 Связанные документы

- [weapon-armor-system.md](./weapon-armor-system.md) — Система Грейдов (детали)
- [charger.md](./charger.md) — Зарядники Ци
- [body.md](./body.md) — Система тела
- [combat-system.md](./combat-system.md) — Боевая система

---

*Документ обновлён: 2026-03-14*
*Статус: Реализовано и протестировано*
