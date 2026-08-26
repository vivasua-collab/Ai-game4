# 🎲 Генераторы системы

**Дата обновления:** 2026-03-22 17:30 UTC
**Версия:** 6.1
**Статус:** ✅ Все генераторы работают на V2

---

## 📊 Статус генераторов

| Генератор | Версия | Статус | Описание |
|-----------|--------|--------|----------|
| `technique-generator.ts` | V1 | ⚠️ @deprecated | Использовать technique-generator-v2.ts |
| `technique-generator-v2.ts` | V2 | ✅ Актуальный | Основной генератор техник (5.0.0) |
| `formation-generator.ts` | V1 | ✅ Актуальный | Боевые формации (defensive, offensive, support, special) |
| `formation-core-generator.ts` | V1 | ✅ Актуальный | Медитативные формации (диски, алтари) |
| `equipment-generator.ts` | V2 | ✅ Актуальный | Bridge к equipment-generator-v2.ts |
| `equipment-generator-v2.ts` | V2 | ✅ Актуальный | Основной генератор экипировки |
| `consumable-generator.ts` | V1 | ✅ Актуальный | Расходники (таблетки, эликсиры) |
| `npc-generator.ts` | V2.1 | ✅ Актуальный | Генерация базовых NPC |
| `npc-full-generator.ts` | V2 | ✅ Актуальный | Полная генерация NPC (оркестратор) |
| `technique-compat.ts` | V1↔V2 | ✅ Актуальный | Совместимость V1↔V2 |

---

## 📊 Обзор генераторов

Все генераторы работают через API endpoints и используют единую архитектуру генерации.

### Архитектура

```
┌─────────────────────────────────────────────────────────────────┐
│                     API ENDPOINTS                                │
├─────────────────────────────────────────────────────────────────┤
│ /api/generator/techniques  → technique-generator-v2.ts          │
│ /api/generator/equipment   → equipment-generator-v2.ts          │
│ /api/generator/npc         → npc-generator.ts                   │
│ /api/generator/npc-full    → npc-full-generator.ts              │
│ /api/generator/formations  → formation-generator.ts             │
│ /api/generator/items       → preset-storage.ts                  │
│ /api/formations/cores      → formation-core-generator.ts        │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                     CORE GENERATORS                              │
├─────────────────────────────────────────────────────────────────┤
│ technique-generator-v2.ts ───┐                                   │
│ equipment-generator-v2.ts ───┼──→ base-item-generator.ts        │
│ npc-generator.ts ────────────┤    (seededRandom, Rarity)        │
│ formation-generator.ts ──────┘                                   │
│ consumable-generator.ts ────────────────────────────────────────│
│ formation-core-generator.ts ────────────────────────────────────│
│ qi-stone-generator.ts ──────────────────────────────────────────│
│ technique-compat.ts ──────── V1↔V2 совместимость                │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                     SUPPORT MODULES                              │
├─────────────────────────────────────────────────────────────────┤
│ id-config.ts, id-counters.ts ─── ID генерация                   │
│ lore-formulas.ts ─────────────── Формулы культивации            │
│ grade-selector.ts ───────────── Выбор грейда                    │
│ grade-validator.ts ───────────── Валидация грейда                │
│ name-generator.ts ───────────── Имена предметов                 │
│ preset-storage.ts ───────────── Сохранение пресетов             │
│ generated-objects-loader.ts ─── Загрузка из файлов              │
│ soul-mapping.ts ─────────────── Маппинг душ                     │
│ technique-generator-config-v2.ts ─ Конфигурация техник V2       │
│ technique-capacity.ts ───────── Константы ёмкости               │
│ effects/ ────────────────────── Эффекты по Tier                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## ⚔️ Генератор техник (V5.0 — "Матрёшка" v2)

**Endpoint:** `/api/generator/techniques`
**Файл:** `src/lib/generator/technique-generator-v2.ts`
**Конфиг:** `src/lib/generator/technique-generator-config-v2.ts`
**Версия:** 5.0.0

### Ключевые принципы V2

1. **Урон = Ёмкость × Grade** (НЕ qiCost!)
2. **Формулы вместо хардкод таблиц**
3. **Архитектура "Матрёшка"** — три слоя генерации
4. **Система тиков** — 1 тик = 1 минута игрового времени

### Три слоя генерации

```
┌─────────────────────────────────────────────────────────────────┐
│                 АРХИТЕКТУРА "МАТРЁШКА" V2                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  СЛОЙ 1: БАЗА                                                    │
│  ├── qiCost = 10 × 1.5^(level-1)                                │
│  ├── capacity = baseCapacity × 2^(level-1) × masteryBonus       │
│  └── baseDamage = qiCost (для справки, НЕ для урона!)           │
│                                                                  │
│  СЛОЙ 2: GRADE (НЕ зависит от уровня!)                          │
│  ├── common:      ×1.0 урона, qiCost ×1.0                       │
│  ├── refined:     ×1.2 урона, qiCost ×1.0                       │
│  ├── perfect:     ×1.4 урона, qiCost ×1.0                       │
│  └── transcendent: ×1.6 урона, qiCost ×1.0                      │
│                                                                  │
│  СЛОЙ 3: БОНУСЫ                                                  │
│  ├── Сила эффекта от Grade (0% ~ 150%)                          │
│  ├── Эффект от стихии (по типу техники)                         │
│  ├── isUltimate (5% шанс для transcendent)                      │
│  └── Transcendent-эффект (только для Transcendent)              │
│                                                                  │
│  ИТОГ: finalDamage = capacity × gradeMult                       │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### ⚠️ КРИТИЧЕСКИЕ ИСПРАВЛЕНИЯ (V5.0)

| Было (V4.0) | Стало (V5.0) |
|-------------|--------------|
| `finalDamage = qiSpent × gradeMult` | `finalDamage = capacity × gradeMult` |
| `GRADE_QI_COST_MULTIPLIERS` разные | Все = 1.0 |
| `specDamageMult` для подтипов | УБРАТЬ из формулы урона |
| Эффекты в ходах | Эффекты в тиках |

### Ultimate-техники (NEW!)

```typescript
// 5% шанс для transcendent техник
const ULTIMATE_CHANCE_BY_GRADE = {
  common: 0,
  refined: 0,
  perfect: 0,
  transcendent: 0.05,
};

// Множители
const ULTIMATE_DAMAGE_MULTIPLIER = 1.3;
const ULTIMATE_QI_COST_MULTIPLIER = 1.5;

// Маркер в названии
if (isUltimate) {
  name = `⚡ ${name}`;
}
```

### Входные параметры

| Параметр | Тип | По умолчанию | Описание |
|----------|-----|--------------|----------|
| `types` | `TechniqueType[]` | все типы | Типы техник |
| `level` | `number` | — | Уровень (1-9) |
| `minLevel` | `number` | 1 | Минимальный уровень |
| `maxLevel` | `number` | 9 | Максимальный уровень |
| `grade` | `TechniqueGrade` | — | Фиксированный Grade |
| `count` | `number` | 10 | Количество |
| `mode` | `'replace' \| 'append'` | replace | Режим |
| `combatSubtype` | `CombatSubtype` | — | Подтип боя |
| `elements` | `TechniqueElement[]` | все | Элементы (7 стихий) |
| `seed` | `number` | Date.now() | Seed генерации |

### Формула урона (ПРАВИЛЬНАЯ)

> **Урон = Ёмкость × Grade**

```
finalDamage = capacity × gradeMult

где:
  capacity = baseCapacity(type) × 2^(level-1) × masteryBonus
  gradeMult = множитель Grade (×1.0 ~ ×1.6)
```

### Пример расчёта

**melee_strike L5, Grade Perfect, mastery 0%:**

```
baseCapacity = 64 (melee_strike)
levelMultiplier = 2^4 = 16
masteryBonus = 1.0

capacity = 64 × 16 × 1.0 = 1024
gradeMult = 1.4 (Perfect)
finalDamage = 1024 × 1.4 = 1433 урона
```

### Дестабилизация

```
При переполнении (qiInput > capacity):
  - Излишки Ци рассеиваются
  - Урон практику = excessQi × 0.5
  - Урон по цели (только melee!) = inputQi × 0.5
  - Для ranged_*: урона по цели НЕТ
```

### Типы техник по Tier

| Tier | Типы | Особенности |
|------|------|-------------|
| 1 | combat | Только множители урона, эффекты от стихий |
| 2 | defense, healing | Событийные эффекты (shield, heal) |
| 3 | curse, poison | DoT и дебаффы |
| 4 | support, movement, sensory | Баффы и утилити |
| 5 | cultivation | Специальные эффекты |

### Система стихий (7 elements)

| Стихия | Emoji | Характер |
|--------|-------|----------|
| Огонь | 🔥 | Горение, DoT |
| Вода | 💧 | Замедление, контроль |
| Земля | 🪨 | Оглушение, стун |
| Воздух | 💨 | Отталкивание |
| Молния | ⚡ | Цепной урон |
| Пустота | 🌑 | Пробитие, антимагия |
| Нейтральный | ⚪ | Чистый Ци |

> **Камни Ци НЕ имеют стихийного окраса.**

---

## 🔄 Совместимость V1 ↔ V2

**Файл:** `src/lib/generator/technique-compat.ts`

### Конвертер V2 → V1

```typescript
import { v2ToV1, GRADE_TO_RARITY, RARITY_TO_GRADE } from './technique-compat';

// Конвертация V2 техники в V1 формат для TempNPC.techniqueData
const techniqueV2 = generateTechniqueV2({ ... });
const techniqueV1 = v2ToV1(techniqueV2);
```

### Маппинг Grade ↔ Rarity

| Grade (V2) | Rarity (V1) |
|------------|-------------|
| common | common |
| refined | uncommon |
| perfect | rare |
| transcendent | legendary |

---

## 🛡️ Генератор экипировки (V2 - "Матрёшка")

**Endpoint:** `/api/generator/equipment`
**Файл:** `src/lib/generator/equipment-generator-v2.ts`

### Архитектура "Матрёшка"

```
Base → Material → Grade → Final
EffectiveStats = Base × MaterialProperties × GradeMultipliers
```

### Поддерживаемые типы

| Тип | Функция | Описание |
|-----|---------|----------|
| `weapon` | `generateWeaponV2()` | Оружие всех видов |
| `armor` | `generateArmorV2()` | Броня для всех слотов |
| `charger` | `generateChargerV2()` | Зарядники Ци |
| `accessory` | `generateAccessoryV2()` | Аксессуары |
| `artifact` | `generateArtifactV2()` | Артефакты |

---

## 🧪 Генератор расходников

**Файл:** `src/lib/generator/consumable-generator.ts`
**Версия:** 1.0

### Назначение
Процедурная генерация расходников: таблетки, эликсиры, еда, свитки.

### Префикс ID
`CS` (CS_000001, CS_000002, ...)

### Типы расходников

| Тип | Название | Стек | Эффекты |
|-----|----------|------|---------|
| pill | Таблетки | 20 | heal_hp, heal_stamina, buff_stat |
| elixir | Эликсиры | 10 | buff_stat, buff_resistance, cure |
| food | Еда | 50 | heal_hp, heal_stamina |
| scroll | Свитки | 5 | special, cure, buff_stat |

### Интеграция с NPC

```typescript
// equipment-generator.ts использует consumable-generator
function generateHealingPill(level, rng): TempItem {
  return convertConsumableToTempItem(
    generateConsumable({ type: 'pill', effectType: 'heal_hp', level })
  );
}
```

---

## 👥 Генератор NPC (V2.1)

**Endpoint:** `/api/generator/npc`
**Файл:** `src/lib/generator/npc-generator.ts`

### Формулы культивации (Lore)

```typescript
// Плотность Ци = 2^(level - 1)
qiDensity = Math.pow(2, cultivationLevel - 1);

// Объём ядра
coreVolume = baseVolume * qiDensity;

// Качество ядра
coreQuality = Math.floor(meridianConductivity * 10) / 10;
```

### Материалы тела (NEW!)

| Материал | Снижение урона | Примеры |
|----------|----------------|---------|
| organic | 0% | Люди, эльфы |
| scaled | 10% | Драконы, змеи |
| chitin | 20% | Пауки, скорпионы |
| ethereal | 70% | Призраки, духи |
| mineral | 50% | Големы |
| chaos | 30% | Хаотические существа |

---

## 🎭 Генератор полных NPC (V2 Migrated)

**Endpoint:** `/api/generator/npc-full`
**Файл:** `src/lib/generator/npc-full-generator.ts`
**Версия:** 1.1 (Migrated to V2)

### Миграция на V2 ✅

```typescript
// Было (V1):
import { generateTechnique } from './technique-generator';

// Стало (V2):
import { generateTechniqueV2 } from './technique-generator-v2';
import { v2ToV1 } from './technique-compat';

// Генерация
const techniqueV2 = generateTechniqueV2({
  id, type, element, level, seed, combatSubtype
});
const technique = v2ToV1(techniqueV2); // для TempNPC.techniqueData
```

### Генерация компонентов

| Компонент | Функция | Статус |
|-----------|---------|--------|
| Техники | `generateTechniqueV2()` | ✅ V2 |
| Формации | `generateFormation()` | ✅ V1 (боевые) |
| Экипировка | `generateFullEquipmentForNPC()` | ✅ V2 |
| Инвентарь | `generateInventoryForNPC()` | ✅ Работает |
| Расходники | `generateConsumable()` | ✅ Работает |

---

## 🏛️ Генераторы формаций

### Боевые формации

**Файл:** `src/lib/generator/formation-generator.ts`

Типы: defensive, offensive, support, special

### Медитативные формации (ядра)

**Файл:** `src/lib/formations/formation-core-generator.ts`

Типы ядер:
- **Диски** (L1-L6): stone, jade, iron, spirit_iron
- **Алтари** (L5-L9): jade, crystal, spirit_crystal, dragon_bone

---

## 📁 Хранилище пресетов

**Расположение:** `presets/`

```
presets/
├── items/
│   ├── weapon.json
│   ├── armor.json
│   ├── accessory.json
│   └── charger.json
├── techniques/
│   ├── combat/
│   │   ├── melee-strike/level-*.json
│   │   ├── melee-weapon/level-*.json
│   │   └── ranged/level-*.json
│   ├── defense/level-*.json
│   ├── cultivation/level-*.json
│   └── ... (все типы)
├── npcs/
│   ├── human.json
│   └── ... (29 species)
├── formations/
│   ├── defensive.json
│   ├── offensive.json
│   ├── support.json
│   └── special.json
├── counters.json
└── manifest.json
```

---

## 🔧 API Endpoints

### Техники
- `GET /api/generator/techniques?action=stats` — статистика
- `GET /api/generator/techniques?action=list` — список
- `GET /api/generator/techniques?action=manifest` — манифест
- `POST /api/generator/techniques` — генерация

### Экипировка
- `GET /api/generator/equipment?action=stats` — статистика
- `POST /api/generator/equipment` — генерация

### NPC
- `GET /api/generator/npc?action=stats` — статистика
- `POST /api/generator/npc` — генерация
- `POST /api/generator/npc-full` — полная генерация

### Формации
- `GET /api/formations` — список формаций сессии
- `POST /api/formations` — создание формации
- `GET /api/formations/cores` — список ядер
- `POST /api/formations/cores` — генерация ядра

---

## 📈 Статистика генерации V2

| Тип | Лимит тест | Лимит прод | Формула |
|-----|------------|------------|---------|
| Техники (combat) | 125 | 405 | 5 подтипов × 5 уровней |
| Техники (другие) | 50-100 | 150-225 | по типу |
| Формации | ~500 | ~2000 | по типу |
| NPC | 100 | 500 | по species |

---

## 📚 Связанные документы

- [technique-system-v2.md](./technique-system-v2.md) — система техник V2.8
- [matryoshka-architecture.md](./matryoshka-architecture.md) — архитектура "Матрёшка"
- [equip-v2.md](./equip-v2.md) — экипировка с Grade System
- [generator-specs.md](./generator-specs.md) — спецификации генераторов
- [checkpoints/checkpoint_03_22_Generator_Migration.md](./checkpoints/checkpoint_03_22_Generator_Migration.md) — Миграция V1→V2

---

*Обновлено: 2026-03-22 15:40 UTC*
*Версия генератора техник: 5.0.0*
*Ключевая формула: finalDamage = capacity × gradeMult*
*Миграция V1→V2: ✅ Завершена*
