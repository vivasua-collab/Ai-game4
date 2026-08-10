# 🔧 Система Материалов

**Версия:** 1.0
**Создано:** 2026-03-14
**Статус:** 📋 Готов к внедрению

---

## 📋 Обзор

Документ описывает **систему материалов** для использования в генераторах экипировки. Каждый материал имеет уникальный ID для упрощения расширения перечня ресурсов без переписывания кода генераторов.

### Ключевые принципы

1. **ID-ификация** — каждый материал имеет уникальный строковый идентификатор
2. **Тир-система** — материалы разделены на 5 тиров по мощности
3. **Категории** — материалы делятся по типу (металлы, кожа, ткани, кристаллы и др.)
4. **Свойства** — каждый материал имеет набор характеристик для расчётов

---

## 1️⃣ АРХИТЕКТУРА СИСТЕМЫ

### 1.1 Структура материала

```typescript
interface MaterialDefinition {
  // === ИДЕНТИФИКАЦИЯ ===
  id: string;                    // Уникальный ID (например, "iron", "spirit_iron")
  name: string;                  // Отображаемое имя
  nameEn: string;                // Английское имя
  
  // === КЛАССИФИКАЦИЯ ===
  category: MaterialCategory;    // Категория материала
  tier: MaterialTier;            // Тир (1-5)
  
  // === ХАРАКТЕРИСТИКИ ===
  properties: MaterialProperties;
  
  // === СОВМЕСТИМОСТЬ ===
  compatibleTypes: EquipmentType[]; // С какими типами экипировки совместим
  
  // === ДОПОЛНИТЕЛЬНО ===
  description?: string;
  icon?: string;
}

// Категории материалов
type MaterialCategory = 
  | 'metal'       // Металлы
  | 'leather'     // Кожа
  | 'cloth'       // Ткани
  | 'wood'        // Дерево
  | 'bone'        // Кости
  | 'crystal'     // Кристаллы
  | 'gem'         // Драгоценные камни
  | 'organic'     // Органические материалы
  | 'spirit'      // Духовные материалы
  | 'void';       // Материалы пустоты

// Тиры материалов
type MaterialTier = 1 | 2 | 3 | 4 | 5;

// Свойства материала
interface MaterialProperties {
  // === БАЗОВЫЕ ===
  baseDurability: number;        // Базовая прочность (30-600)
  weight: number;                // Вес единицы (кг)
  hardness: number;              // Твёрдость (1-10)
  flexibility: number;           // Гибкость (0-1)
  
  // === ЦИ И КУЛЬТИВАЦИЯ ===
  qiConductivity: number;        // Проводимость Ци (ед/сек)
  qiRetention: number;           // Сохранение Ци (% в час)
  
  // === ДЛЯ ОРУЖИЯ ===
  damageBonus?: number;          // Бонус к урону (%)
  penetrationBonus?: number;     // Бонус к пробитию (%)
  
  // === ДЛЯ БРОНИ ===
  defenseBonus?: number;         // Бонус к защите (%)
  resistanceBonus?: Partial<Record<Element, number>>; // Бонус к сопротивлениям
  
  // === ДЛЯ ЗАРЯДНИКОВ ===
  bufferCapacityMult?: number;   // Множитель ёмкости буфера
  heatResistance?: number;       // Термостойкость (0-100)
  
  // === ОСОБЫЕ СВОЙСТВА ===
  specialProperties: string[];   // Массив особых свойств
}
```

---

## 2️⃣ РЕЕСТР МАТЕРИАЛОВ

### 2.1 Тир 1: Базовые материалы

| ID | Название | Категория | Прочность | Проводимость |
|----|----------|-----------|-----------|--------------|
| `iron` | Железо | metal | 40 | 0.3 |
| `leather` | Кожа | leather | 30 | 0.4 |
| `cloth` | Ткань | cloth | 20 | 0.5 |
| `wood` | Дерево | wood | 25 | 0.6 |
| `bone` | Кость | bone | 35 | 0.4 |
| `copper` | Медь | metal | 35 | 0.8 |
| `bronze` | Бронза | metal | 45 | 0.5 |

```typescript
// Детальные определения Тира 1
const TIER_1_MATERIALS: MaterialDefinition[] = [
  {
    id: 'iron',
    name: 'Железо',
    nameEn: 'Iron',
    category: 'metal',
    tier: 1,
    properties: {
      baseDurability: 40,
      weight: 7.8,
      hardness: 5,
      flexibility: 0.1,
      qiConductivity: 0.3,
      qiRetention: 90,
      damageBonus: 0,
      defenseBonus: 0,
      specialProperties: [],
    },
    compatibleTypes: ['weapon', 'armor', 'tool'],
    description: 'Базовый металл для простого оружия и брони.',
  },
  {
    id: 'leather',
    name: 'Кожа',
    nameEn: 'Leather',
    category: 'leather',
    tier: 1,
    properties: {
      baseDurability: 30,
      weight: 0.8,
      hardness: 2,
      flexibility: 0.7,
      qiConductivity: 0.4,
      qiRetention: 85,
      defenseBonus: 0,
      specialProperties: ['flexible', 'lightweight'],
    },
    compatibleTypes: ['armor', 'accessory'],
    description: 'Обработанная кожа для лёгкой брони.',
  },
  {
    id: 'cloth',
    name: 'Ткань',
    nameEn: 'Cloth',
    category: 'cloth',
    tier: 1,
    properties: {
      baseDurability: 20,
      weight: 0.3,
      hardness: 1,
      flexibility: 0.9,
      qiConductivity: 0.5,
      qiRetention: 80,
      defenseBonus: -0.2,
      specialProperties: ['breathable', 'comfortable'],
    },
    compatibleTypes: ['clothing', 'accessory'],
    description: 'Простая ткань для одежды.',
  },
  {
    id: 'wood',
    name: 'Дерево',
    nameEn: 'Wood',
    category: 'wood',
    tier: 1,
    properties: {
      baseDurability: 25,
      weight: 0.6,
      hardness: 3,
      flexibility: 0.3,
      qiConductivity: 0.6,
      qiRetention: 75,
      specialProperties: ['natural'],
    },
    compatibleTypes: ['weapon', 'tool'],
    description: 'Обычное дерево для посохов и рукоятей.',
  },
  {
    id: 'bone',
    name: 'Кость',
    nameEn: 'Bone',
    category: 'bone',
    tier: 1,
    properties: {
      baseDurability: 35,
      weight: 1.2,
      hardness: 4,
      flexibility: 0.2,
      qiConductivity: 0.4,
      qiRetention: 88,
      specialProperties: ['organic'],
    },
    compatibleTypes: ['weapon', 'accessory'],
    description: 'Кость животных для простых орудий.',
  },
];
```

### 2.2 Тир 2: Улучшенные материалы

| ID | Название | Категория | Прочность | Проводимость |
|----|----------|-----------|-----------|--------------|
| `steel` | Сталь | metal | 70 | 0.4 |
| `treated_leather` | Выделанная кожа | leather | 50 | 0.5 |
| `silk` | Шёлк | cloth | 35 | 0.8 |
| `ironwood` | Железное дерево | wood | 45 | 0.7 |
| `silver` | Серебро | metal | 55 | 1.0 |
| `carved_bone` | Обработанная кость | bone | 55 | 0.6 |

```typescript
const TIER_2_MATERIALS: MaterialDefinition[] = [
  {
    id: 'steel',
    name: 'Сталь',
    nameEn: 'Steel',
    category: 'metal',
    tier: 2,
    properties: {
      baseDurability: 70,
      weight: 7.8,
      hardness: 7,
      flexibility: 0.15,
      qiConductivity: 0.4,
      qiRetention: 92,
      damageBonus: 10,
      defenseBonus: 15,
      specialProperties: ['tempered'],
    },
    compatibleTypes: ['weapon', 'armor', 'tool'],
    description: 'Закалённая сталь для качественного снаряжения.',
  },
  {
    id: 'silk',
    name: 'Шёлк',
    nameEn: 'Silk',
    category: 'cloth',
    tier: 2,
    properties: {
      baseDurability: 35,
      weight: 0.2,
      hardness: 1,
      flexibility: 0.95,
      qiConductivity: 0.8,
      qiRetention: 90,
      specialProperties: ['luxurious', 'conductive'],
    },
    compatibleTypes: ['clothing', 'accessory'],
    description: 'Шёлк с лучшей проводимостью Ци.',
  },
  {
    id: 'silver',
    name: 'Серебро',
    nameEn: 'Silver',
    category: 'metal',
    tier: 2,
    properties: {
      baseDurability: 55,
      weight: 10.5,
      hardness: 3,
      flexibility: 0.2,
      qiConductivity: 1.0,
      qiRetention: 95,
      specialProperties: ['purifying', 'conductive'],
    },
    compatibleTypes: ['weapon', 'accessory', 'charger'],
    description: 'Серебро хорошо проводит Ци и чистит от скверны.',
  },
];
```

### 2.3 Тир 3: Особые материалы

| ID | Название | Категория | Прочность | Проводимость |
|----|----------|-----------|-----------|--------------|
| `spirit_iron` | Духовное железо | spirit | 120 | 1.2 |
| `cold_iron` | Холодное железо | metal | 100 | 0.8 |
| `spirit_silk` | Духовный шёлк | spirit | 80 | 1.5 |
| `spirit_crystal` | Духовный кристалл | crystal | 90 | 2.0 |
| `jade` | Нефрит | crystal | 75 | 1.3 |
| `treated_monster_bone` | Кость монстра | bone | 110 | 1.0 |

```typescript
const TIER_3_MATERIALS: MaterialDefinition[] = [
  {
    id: 'spirit_iron',
    name: 'Духовное железо',
    nameEn: 'Spirit Iron',
    category: 'spirit',
    tier: 3,
    properties: {
      baseDurability: 120,
      weight: 6.0,
      hardness: 8,
      flexibility: 0.2,
      qiConductivity: 1.2,
      qiRetention: 97,
      damageBonus: 25,
      defenseBonus: 30,
      specialProperties: ['spiritual', 'self_repair'],
    },
    compatibleTypes: ['weapon', 'armor', 'charger'],
    description: 'Железо, напитанное духовной энергией.',
  },
  {
    id: 'cold_iron',
    name: 'Холодное железо',
    nameEn: 'Cold Iron',
    category: 'metal',
    tier: 3,
    properties: {
      baseDurability: 100,
      weight: 8.0,
      hardness: 8,
      flexibility: 0.1,
      qiConductivity: 0.8,
      qiRetention: 95,
      damageBonus: 20,
      penetrationBonus: 15,
      specialProperties: ['cold', 'spirit_bane'],
    },
    compatibleTypes: ['weapon', 'armor'],
    description: 'Железо из вечной мерзлоты, опасно для духов.',
  },
  {
    id: 'spirit_crystal',
    name: 'Духовный кристалл',
    nameEn: 'Spirit Crystal',
    category: 'crystal',
    tier: 3,
    properties: {
      baseDurability: 90,
      weight: 2.5,
      hardness: 6,
      flexibility: 0,
      qiConductivity: 2.0,
      qiRetention: 99,
      bufferCapacityMult: 1.5,
      heatResistance: 70,
      specialProperties: ['amplifier', 'fragile'],
    },
    compatibleTypes: ['charger', 'accessory', 'artifact'],
    description: 'Кристалл, усиливающий потоки Ци.',
  },
  {
    id: 'jade',
    name: 'Нефрит',
    nameEn: 'Jade',
    category: 'crystal',
    tier: 3,
    properties: {
      baseDurability: 75,
      weight: 3.0,
      hardness: 7,
      flexibility: 0,
      qiConductivity: 1.3,
      qiRetention: 98,
      specialProperties: ['protective', 'calming'],
    },
    compatibleTypes: ['accessory', 'charger', 'artifact'],
    description: 'Нефрит успокаивает разум и защищает дух.',
  },
];
```

### 2.4 Тир 4: Редкие материалы

| ID | Название | Категория | Прочность | Проводимость |
|----|----------|-----------|-----------|--------------|
| `star_metal` | Звёздный металл | metal | 250 | 2.5 |
| `dragon_bone` | Кость дракона | bone | 300 | 2.0 |
| `elemental_core` | Элементальное ядро | spirit | 200 | 3.0 |
| `spirit_jade` | Духовный нефрит | spirit | 180 | 2.5 |
| `phoenix_feather` | Перо феникса | organic | 150 | 4.0 |

```typescript
const TIER_4_MATERIALS: MaterialDefinition[] = [
  {
    id: 'star_metal',
    name: 'Звёздный металл',
    nameEn: 'Star Metal',
    category: 'metal',
    tier: 4,
    properties: {
      baseDurability: 250,
      weight: 5.0,
      hardness: 9,
      flexibility: 0.15,
      qiConductivity: 2.5,
      qiRetention: 99,
      damageBonus: 50,
      defenseBonus: 45,
      specialProperties: ['celestial', 'indestructible_core'],
    },
    compatibleTypes: ['weapon', 'armor', 'artifact'],
    description: 'Металл с упавших звёзд, почти не разрушается.',
  },
  {
    id: 'dragon_bone',
    name: 'Кость дракона',
    nameEn: 'Dragon Bone',
    category: 'bone',
    tier: 4,
    properties: {
      baseDurability: 300,
      weight: 1.5,
      hardness: 9,
      flexibility: 0.3,
      qiConductivity: 2.0,
      qiRetention: 99,
      damageBonus: 40,
      penetrationBonus: 30,
      specialProperties: ['draconic', 'fire_resistant'],
    },
    compatibleTypes: ['weapon', 'armor', 'charger', 'artifact'],
    description: 'Кость древнего дракона, обладает огромной силой.',
  },
  {
    id: 'elemental_core',
    name: 'Элементальное ядро',
    nameEn: 'Elemental Core',
    category: 'spirit',
    tier: 4,
    properties: {
      baseDurability: 200,
      weight: 0.5,
      hardness: 4,
      flexibility: 0.1,
      qiConductivity: 3.0,
      qiRetention: 100,
      resistanceBonus: { fire: 50, water: 50, earth: 50, air: 50 },
      specialProperties: ['elemental', 'unstable'],
    },
    compatibleTypes: ['weapon', 'artifact', 'charger'],
    description: 'Кристаллизованная суть стихии.',
  },
];
```

### 2.5 Тир 5: Божественные материалы

| ID | Название | Категория | Прочность | Проводимость |
|----|----------|-----------|-----------|--------------|
| `void_matter` | Материя пустоты | void | 500 | 4.5 |
| `chaos_matter` | Материя хаоса | void | 450 | 5.0 |
| `primordial_essence` | Первородная эссенция | spirit | 600 | 4.0 |
| `celestial_jade` | Небесный нефрит | spirit | 550 | 4.5 |

```typescript
const TIER_5_MATERIALS: MaterialDefinition[] = [
  {
    id: 'void_matter',
    name: 'Материя пустоты',
    nameEn: 'Void Matter',
    category: 'void',
    tier: 5,
    properties: {
      baseDurability: 500,
      weight: 0.0,
      hardness: 10,
      flexibility: 0.5,
      qiConductivity: 4.5,
      qiRetention: 100,
      damageBonus: 80,
      defenseBonus: 70,
      penetrationBonus: 50,
      specialProperties: ['void_touched', 'phase_shift', 'reality_warp'],
    },
    compatibleTypes: ['weapon', 'armor', 'artifact', 'implant'],
    description: 'Материал из-за пределов реальности.',
  },
  {
    id: 'chaos_matter',
    name: 'Материя хаоса',
    nameEn: 'Chaos Matter',
    category: 'void',
    tier: 5,
    properties: {
      baseDurability: 450,
      weight: 0.1,
      hardness: 10,
      flexibility: 0.4,
      qiConductivity: 5.0,
      qiRetention: 100,
      damageBonus: 100,
      defenseBonus: 60,
      specialProperties: ['chaotic', 'transforming', 'unpredictable'],
    },
    compatibleTypes: ['weapon', 'artifact'],
    description: 'Материал чистого хаоса, непредсказуем и силён.',
  },
  {
    id: 'primordial_essence',
    name: 'Первородная эссенция',
    nameEn: 'Primordial Essence',
    category: 'spirit',
    tier: 5,
    properties: {
      baseDurability: 600,
      weight: 0.0,
      hardness: 10,
      flexibility: 1.0,
      qiConductivity: 4.0,
      qiRetention: 100,
      bufferCapacityMult: 5.0,
      specialProperties: ['primordial', 'creation', 'life'],
    },
    compatibleTypes: ['artifact', 'implant', 'charger'],
    description: 'Материал времён сотворения мира.',
  },
];
```

---

## 3️⃣ СИСТЕМА ХРАНЕНИЯ И РАСШИРЕНИЯ

### 3.1 Реестр материалов

```typescript
// src/lib/data/materials-registry.ts

/**
 * Глобальный реестр материалов
 * Позволяет добавлять новые материалы без изменения генераторов
 */
class MaterialsRegistry {
  private materials: Map<string, MaterialDefinition> = new Map();
  private byCategory: Map<MaterialCategory, MaterialDefinition[]> = new Map();
  private byTier: Map<MaterialTier, MaterialDefinition[]> = new Map();
  
  constructor() {
    // Загружаем базовые материалы
    this.loadBaseMaterials();
  }
  
  /**
   * Регистрация нового материала
   */
  register(material: MaterialDefinition): void {
    if (this.materials.has(material.id)) {
      console.warn(`Material ${material.id} already registered, overwriting`);
    }
    
    this.materials.set(material.id, material);
    
    // Индексируем по категории
    const categoryList = this.byCategory.get(material.category) || [];
    categoryList.push(material);
    this.byCategory.set(material.category, categoryList);
    
    // Индексируем по тиру
    const tierList = this.byTier.get(material.tier) || [];
    tierList.push(material);
    this.byTier.set(material.tier, tierList);
  }
  
  /**
   * Получение материала по ID
   */
  get(id: string): MaterialDefinition | undefined {
    return this.materials.get(id);
  }
  
  /**
   * Получение материалов по категории
   */
  getByCategory(category: MaterialCategory): MaterialDefinition[] {
    return this.byCategory.get(category) || [];
  }
  
  /**
   * Получение материалов по тиру
   */
  getByTier(tier: MaterialTier): MaterialDefinition[] {
    return this.byTier.get(tier) || [];
  }
  
  /**
   * Получение материалов, совместимых с типом экипировки
   */
  getCompatibleWith(equipmentType: EquipmentType): MaterialDefinition[] {
    return Array.from(this.materials.values())
      .filter(m => m.compatibleTypes.includes(equipmentType));
  }
  
  /**
   * Случайный выбор материала по тиру
   */
  randomByTier(tier: MaterialTier, rng: () => number): MaterialDefinition | null {
    const materials = this.getByTier(tier);
    if (materials.length === 0) return null;
    return materials[Math.floor(rng() * materials.length)];
  }
  
  /**
   * Загрузка базовых материалов
   */
  private loadBaseMaterials(): void {
    const allMaterials = [
      ...TIER_1_MATERIALS,
      ...TIER_2_MATERIALS,
      ...TIER_3_MATERIALS,
      ...TIER_4_MATERIALS,
      ...TIER_5_MATERIALS,
    ];
    
    for (const material of allMaterials) {
      this.register(material);
    }
  }
}

// Синглтон
export const materialsRegistry = new MaterialsRegistry();
```

### 3.2 Интеграция с генераторами

```typescript
// Использование в генераторе оружия
import { materialsRegistry } from '@/lib/data/materials-registry';

function generateWeapon(options: WeaponGenerationOptions): Weapon {
  // Определяем тир материала по уровню
  const tier = getMaterialTierForLevel(options.level);
  
  // Выбираем подходящий материал
  const material = materialsRegistry.randomByTier(tier, rng);
  
  // Используем свойства материала в расчётах
  const baseDamage = calculateBaseDamage(weaponType, level, material);
  const maxDurability = material.properties.baseDurability * gradeMultiplier;
  
  // ...
}
```

### 3.3 Расширение списка материалов

```typescript
// Пример добавления нового материала через плагин или мод

// Новый материал для будущих обновлений
const newMaterial: MaterialDefinition = {
  id: 'moon_silver',
  name: 'Лунное серебро',
  nameEn: 'Moon Silver',
  category: 'spirit',
  tier: 3,
  properties: {
    baseDurability: 130,
    weight: 8.0,
    hardness: 6,
    flexibility: 0.25,
    qiConductivity: 1.8,
    qiRetention: 98,
    damageBonus: 20,
    specialProperties: ['lunar', 'night_power'],
  },
  compatibleTypes: ['weapon', 'accessory'],
  description: 'Серебро, напитанное энергией луны.',
};

// Регистрация
materialsRegistry.register(newMaterial);
```

---

## 4️⃣ МАТЕРИАЛЫ ДЛЯ ЗАРЯДНИКОВ

### 4.1 Специализированные материалы

```typescript
// Материалы с bonus к буферу и проводимости

const CHARGER_MATERIALS: MaterialDefinition[] = [
  {
    id: 'conductive_copper',
    name: 'Проводящая медь',
    nameEn: 'Conductive Copper',
    category: 'metal',
    tier: 1,
    properties: {
      baseDurability: 50,
      weight: 8.9,
      hardness: 3,
      flexibility: 0.3,
      qiConductivity: 2.0,
      qiRetention: 85,
      bufferCapacityMult: 1.2,
      heatResistance: 40,
      specialProperties: ['highly_conductive'],
    },
    compatibleTypes: ['charger'],
    description: 'Медь для простых зарядников.',
  },
  {
    id: 'spirit_jade',
    name: 'Духовный нефрит',
    nameEn: 'Spirit Jade',
    category: 'spirit',
    tier: 3,
    properties: {
      baseDurability: 180,
      weight: 3.2,
      hardness: 7,
      flexibility: 0,
      qiConductivity: 2.5,
      qiRetention: 99,
      bufferCapacityMult: 2.0,
      heatResistance: 80,
      specialProperties: ['amplifier', 'stable'],
    },
    compatibleTypes: ['charger', 'accessory', 'artifact'],
    description: 'Нефрит, идеально подходящий для зарядников.',
  },
];
```

---

## 5️⃣ БАЗА ДАННЫХ

### 5.1 Prisma Schema

```prisma
// Таблица материалов (для динамического добавления)
model Material {
  id          String   @id
  name        String
  nameEn      String
  category    String   // metal, leather, cloth, wood, bone, crystal, gem, organic, spirit, void
  tier        Int      // 1-5
  
  // Свойства (JSON)
  properties  String   // MaterialProperties JSON
  
  // Совместимость (JSON array)
  compatibleTypes String
  
  // Метаданные
  description String?
  icon        String?
  
  // Флаги
  isBaseGame  Boolean  @default(true) // Базовый или добавленный модом
  isActive    Boolean  @default(true) // Активен ли материал
  
  createdAt   DateTime @default(now())
  updatedAt   DateTime @updatedAt
  
  @@index([category])
  @@index([tier])
  @@map("materials")
}
```

### 5.2 Начальная миграция

```typescript
// Скрипт для заполнения базы материалов
async function seedMaterials() {
  const allMaterials = [
    ...TIER_1_MATERIALS,
    ...TIER_2_MATERIALS,
    ...TIER_3_MATERIALS,
    ...TIER_4_MATERIALS,
    ...TIER_5_MATERIALS,
  ];
  
  for (const material of allMaterials) {
    await prisma.material.create({
      data: {
        id: material.id,
        name: material.name,
        nameEn: material.nameEn,
        category: material.category,
        tier: material.tier,
        properties: JSON.stringify(material.properties),
        compatibleTypes: JSON.stringify(material.compatibleTypes),
        description: material.description,
        isBaseGame: true,
      },
    });
  }
}
```

---

## 6️⃣ АЛГОРИТМЫ ВЫБОРА МАТЕРИАЛА

### 6.1 По уровню предмета

```typescript
function getMaterialTierForLevel(level: number): MaterialTier {
  if (level <= 2) return 1;
  if (level <= 4) return 2;
  if (level <= 6) return 3;
  if (level <= 8) return 4;
  return 5;
}
```

### 6.2 По редкости предмета

```typescript
function getMaterialTierForRarity(rarity: EquipmentGrade): MaterialTier {
  switch (rarity) {
    case 'damaged': return 1;  // Повреждённый - базовые материалы
    case 'common': return Math.floor(Math.random() * 2) + 1; // 1-2
    case 'refined': return Math.floor(Math.random() * 2) + 2; // 2-3
    case 'perfect': return Math.floor(Math.random() * 2) + 3; // 3-4
    case 'transcendent': return Math.floor(Math.random() * 2) + 4; // 4-5
  }
}
```

### 6.3 По типу экипировки

```typescript
function selectMaterialForEquipment(
  equipmentType: EquipmentType,
  tier: MaterialTier,
  rng: () => number
): MaterialDefinition {
  const compatible = materialsRegistry
    .getCompatibleWith(equipmentType)
    .filter(m => m.tier === tier);
  
  if (compatible.length === 0) {
    // Fallback на любой материал тира
    return materialsRegistry.randomByTier(tier, rng)!;
  }
  
  return compatible[Math.floor(rng() * compatible.length)];
}
```

---

## 7️⃣ ОСОБЫЕ СВОЙСТВА МАТЕРИАЛОВ

### 7.1 Список особых свойств

| ID свойства | Название | Эффект |
|-------------|----------|--------|
| `tempered` | Закалённый | +10% к прочности |
| `flexible` | Гибкий | Снижен шанс слома |
| `lightweight` | Лёгкий | -20% к весу |
| `conductive` | Проводящий | +20% к проводимости |
| `spiritual` | Духовный | Самовосстановление 1%/день |
| `self_repair` | Самовосстановление | Медленный ремонт |
| `cold` | Холодный | Доп. урон холодом |
| `spirit_bane` | Губитель духов | +50% урона духам |
| `amplifier` | Усилитель | +15% к эффектам Ци |
| `celestial` | Небесный | Бонус ночью |
| `draconic` | Драконий | Огнеупорность |
| `elemental` | Элементальный | Бонус к стихии |
| `void_touched` | Пустотный | Игнорирует 20% брони |
| `chaotic` | Хаотичный | Случайные эффекты |
| `primordial` | Первородный | Нельзя уничтожить |

---

## 🔗 Связанные документы

- [equip.md](./equip.md) — Унифицированная система экипировки
- [weapon-armor-system.md](./weapon-armor-system.md) — Система оружия и брони
- [charger.md](./charger.md) — Зарядники Ци

---

## 📝 История изменений

| Дата | Версия | Изменение |
|------|--------|-----------|
| 2026-03-14 | 1.0 | Создан документ |

---

*Документ создан: 2026-03-14*
