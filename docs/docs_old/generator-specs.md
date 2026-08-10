# 📋 Характеристики генераторов

**Версия:** 5.0  
**Создано:** 2026-03-19  
**Обновлено:** 2026-03-22 15:40 UTC  
**Статус:** ✅ Все генераторы работают на V2

---

## 📊 Сводная таблица

| Генератор | Объекты | Архитектура | Версия | Статус |
|-----------|--------|-------------|--------|--------|
| technique-generator-v2.ts | Техники | ✅ Матрёшка V2 | 5.0.0 | ✅ Основной |
| technique-generator.ts | Техники | ⚠️ Устаревшая | 3.0 | ⛔ @deprecated |
| technique-compat.ts | V1↔V2 конвертер | ✅ Совместимость | 1.0 | ✅ Новый |
| equipment-generator-v2.ts | Экипировка | ✅ Матрёшка | 2.0 | ✅ Основной |
| equipment-generator.ts | Экипировка/Инвентарь | ✅ Bridge | 2.0 | ✅ Мост к V2 |
| consumable-generator.ts | Расходники | ✅ Матрёшка | 1.0 | ✅ Основной |
| npc-generator.ts | NPC | ✅ Оркестратор | 2.1 | ✅ Основной |
| npc-full-generator.ts | Полный NPC | ✅ Оркестратор V2 | 1.1 | ✅ V2 Migrated |
| formation-generator.ts | Боевые формации | ✅ Матрёшка | 1.0 | ✅ Основной |
| formation-core-generator.ts | Медитативные формации | ✅ Матрёшка | 1.0 | ✅ Основной |
| qi-stone-generator.ts | Камни Ци | ✅ Упрощённая | 1.0 | ✅ Основной |

---

## 1️⃣ technique-generator-v2.ts

### Назначение
Генерация техник культивации: combat, defense, cultivation, support, movement, sensory, healing, curse, poison.

### Версия
**5.0.0** — исправление формул согласно `docs/technique-system-v2.md`

### ⚠️ ФОРМУЛА УРОНА (КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ)

> **Урон = Ёмкость × Grade**

```
finalDamage = capacity × gradeMult

где:
  capacity = baseCapacity(type) × 2^(level-1) × masteryBonus
  gradeMult = множитель Grade (×1.0 ~ ×1.6)
```

### Архитектура V2

```
┌─────────────────────────────────────────────────────────────────┐
│                     ГЕНЕРАЦИЯ ТЕХНИКИ V5.0                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  1. ЁМКОСТЬ ТЕХНИКИ (ОСНОВА УРОНА!)                              │
│     - capacity = baseCapacity × 2^(level-1) × masteryBonus       │
│     - baseCapacity зависит от типа техники                       │
│                                                                  │
│  2. GRADE МНОЖИТЕЛЬ (НЕ зависит от уровня!)                     │
│     - common:      ×1.0 урона, qiCost ×1.0                       │
│     - refined:     ×1.2 урона, qiCost ×1.0                       │
│     - perfect:     ×1.4 урона, qiCost ×1.0                       │
│     - transcendent: ×1.6 урона, qiCost ×1.0                      │
│                                                                  │
│  3. СТИХИЙНЫЕ ЭФФЕКТЫ (Бонус 2)                                  │
│     - Определяются element + type                                │
│     - Длительность в ТИКАХ (1 тик = 1 минута)                    │
│                                                                  │
│  4. TRANSCENDENT-ЭФФЕКТЫ (Бонус 3, только transcendent)          │
│     - Уникальные свойства по стихии                              │
│                                                                  │
│  ИТОГ: finalDamage = capacity × gradeMult                        │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Множители Grade (КОРРЕКТНЫЕ)

```typescript
// Урон — основа формулы
export const GRADE_DAMAGE_MULTIPLIERS: Record<TechniqueGrade, number> = {
  common: 1.0,
  refined: 1.2,
  perfect: 1.4,
  transcendent: 1.6,
};

// Стоимость Ци — ВСЕГДА ×1.0!
export const GRADE_QI_COST_MULTIPLIERS: Record<TechniqueGrade, number> = {
  common: 1.0,
  refined: 1.0,
  perfect: 1.0,
  transcendent: 1.0,
};
```

### Соответствие Матрёшке V2
✅ **Полное** — урон = capacity × gradeMult

---

## 2️⃣ technique-generator.ts (V1 — deprecated)

### ⚠️ СТАТУС: УСТАРЕВШИЙ!

Используйте `technique-generator-v2.ts` вместо этого файла.

### Проблемы V1

| # | Проблема | Критичность |
|---|----------|-------------|
| 1 | Неправильная формула урона | 🔴 КРИТИЧНО |
| 2 | 7+ хардкод таблиц | 🔴 КРИТИЧНО |
| 3 | ~19 000 техник (избыточно) | 🟠 ВЫСОКО |
| 4 | Несоответствие "Матрёшка" | 🟠 ВЫСОКО |

---

## 3️⃣ equipment-generator-v2.ts

### Назначение
Оркестратор генерации экипировки: weapon, armor, charger, accessory, artifact.

### Архитектура

```
Base → Material → Grade → Final
EffectiveStats = Base × MaterialProperties × GradeMultipliers
```

### Соответствие Матрёшке
✅ **Полное**

---

## 4️⃣ equipment-generator.ts (Bridge)

### Назначение
Мост между старой системой и V2 генераторами. Также генерирует инвентарь NPC.

### Использует
- `equipment-generator-v2.ts` — для экипировки
- `consumable-generator.ts` — для расходников

### Ключевые функции

```typescript
// Генерация экипировки NPC (V2)
generateEquipmentForNPC(context) → TempEquipment

// Генерация инвентаря NPC
generateInventoryForNPC(context) → TempItem[]

// Полная генерация (экипировка + инвентарь)
generateFullEquipmentForNPC(npc, rng) → void  // изменяет npc.equipment и npc.quickSlots
```

### Интеграция с NPC

```typescript
// npc-full-generator.ts:233
generateFullEquipmentForNPC(tempNPC, rng);
  ↓
// equipment-generator.ts:309-321
const equipment = generateEquipmentForNPC(context);
equipItemsToNPC(npc, equipment);

const inventory = generateInventoryForNPC(context);
addInventoryToNPC(npc, inventory);
```

---

## 5️⃣ consumable-generator.ts ✅

### Назначение
Процедурная генерация расходников: таблетки, эликсиры, еда, свитки.

### Префикс ID
`CS` (CS_000001, CS_000002, ...)

### ⚠️ ВАЖНО: Расходники НЕ добавляют Ци — это задача зарядников!

### Типы расходников

| Тип | Название | Стек | Эффекты |
|-----|----------|------|---------|
| pill | Таблетки | 20 | heal_hp, heal_stamina, buff_stat, buff_resistance |
| elixir | Эликсиры | 10 | buff_stat, buff_resistance, cure, special |
| food | Еда | 50 | heal_hp, heal_stamina |
| scroll | Свитки | 5 | special, cure, buff_stat |

### Типы эффектов

| Тип | Описание | Длительность |
|-----|----------|--------------|
| heal_hp | Восстановление HP | — |
| heal_stamina | Восстановление сил | — |
| buff_stat | Усиление характеристики | 60 сек × grade |
| buff_resistance | Усиление сопротивления | 120 сек × grade |
| cure | Лечение статуса | — |
| special | Особый эффект | 30 сек × grade |

### Система Grade

```typescript
const CONSUMABLE_GRADE_CONFIGS = {
  common:    { effectMultiplier: 1.0,  durationMultiplier: 1.0 },
  refined:   { effectMultiplier: 1.2,  durationMultiplier: 1.2 },
  perfect:   { effectMultiplier: 1.5,  durationMultiplier: 1.5 },
  transcendent: { effectMultiplier: 2.0, durationMultiplier: 2.0 },
};
```

### Базовые значения по уровню (heal_hp)

```
L1: 10 → L5: 80 → L9: 300
```

### Пояс (Belt System)

```typescript
const BELT_INFO = {
  quickAccessSlots: 4,
  hotkeys: ['CTRL+1', 'CTRL+2', 'CTRL+3', 'CTRL+4'],
};
```

### Использование в NPC

```typescript
// equipment-generator.ts

function generateHealingPill(level, rng): TempItem {
  return convertConsumableToTempItem(
    generateConsumable({ type: 'pill', effectType: 'heal_hp', level })
  );
}

function generateElixir(level, rng): TempItem {
  return convertConsumableToTempItem(
    generateConsumable({ type: 'elixir', level })
  );
}

function generateFood(level, rng): TempItem {
  return convertConsumableToTempItem(
    generateConsumable({ type: 'food', level })
  );
}
```

### API

```typescript
// Генерация одного расходника
generateConsumable(options: ConsumableGenerationOptions): Consumable

// Генерация нескольких
generateConsumables(count, options): ConsumableGenerationResult

// Утилиты для UI
getConsumableTypes()      // pill, elixir, food, scroll
getEffectTypes()          // heal_hp, buff_stat, ...
getPossibleEffects(type)  // возможные эффекты для типа
```

---

## 6️⃣ npc-generator.ts

### Назначение
Оркестратор генерации NPC с учётом вида, роли, культивации.

### Формулы культивации

```typescript
// Плотность Ци = 2^(level - 1)
qiDensity = Math.pow(2, cultivationLevel - 1);

// Объём ядра
coreVolume = baseVolume * qiDensity;

// Качество ядра
coreQuality = Math.floor(meridianConductivity * 10) / 10;
```

---

## 7️⃣ npc-full-generator.ts (V2 Migrated) ✅

### Назначение
Полная генерация NPC со всеми компонентами: техники, формации, экипировка, инвентарь.

### Миграция на V2 ✅ ЗАВЕРШЕНА (2026-03-22)

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
| Расходники | `generateConsumable()` | ✅ Автогенерация |

---

## 8️⃣ technique-compat.ts (NEW!)

### Назначение
Совместимость между V1 и V2 форматами техник.

### Ключевые функции

```typescript
// Конвертация V2 → V1
export function v2ToV1(technique: GeneratedTechniqueV2): GeneratedTechnique

// Маппинги Grade ↔ Rarity
export const GRADE_TO_RARITY: Record<TechniqueGrade, string>
export const RARITY_TO_GRADE: Record<string, TechniqueGrade>
```

### Использование

```typescript
import { v2ToV1, GRADE_TO_RARITY } from './technique-compat';

// Генерация V2 техники
const techniqueV2 = generateTechniqueV2({ level: 5, type: 'combat' });

// Конвертация для TempNPC (использует V1 формат)
const techniqueV1 = v2ToV1(techniqueV2);
tempNPC.techniqueData = techniqueV1;
```

### Маппинг Grade ↔ Rarity

| Grade (V2) | Rarity (V1) |
|------------|-------------|
| common | common |
| refined | uncommon |
| perfect | rare |
| transcendent | legendary |

---

## 📁 Файлы эффектов

### Структура

```
src/lib/generator/effects/
├── index.ts              — Экспорт всех эффектов
├── tier-1-combat.ts      — Combat (НЕТ эффектов, только множители)
├── tier-2-defense-healing.ts — Defense & Healing
├── tier-3-curse-poison.ts    — Curse & Poison (DoT)
├── tier-4-support-utility.ts — Support, Movement, Sensory
└── tier-5-cultivation.ts     — Cultivation
```

### Стихийные эффекты (атакующие)

| Стихия | Эффект | Длительность |
|--------|--------|--------------|
| 🔥 Огонь | Горение 5% урона/тик | 3 тика |
| 💧 Вода | Замедление -20% скорости | 2 тика |
| 🪨 Земля | Стан 15% шанс | 1 тик |
| 💨 Воздух | Отброс 3 клетки | — |
| ⚡ Молния | Цепной урон 50% по 2 целям | — |
| 🌑 Пустота | +30% пробития брони | — |

---

## 📊 Интеграция генераторов

```
┌─────────────────────────────────────────────────────────────────────┐
│                    NPC GENERATION PIPELINE                           │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  npc-full-generator.ts (ОРКЕСТРАТОР)                                 │
│  │                                                                   │
│  ├── 1. generateNPC() → GeneratedNPC (базовый NPC)                  │
│  │                                                                   │
│  ├── 2. generateTechniqueV2() × slots → techniques[] (V2!)          │
│  │      └── technique-generator-v2.ts                               │
│  │                                                                   │
│  ├── 3. generateFormation() × formationSlots → formations[]        │
│  │      └── formation-generator.ts                                  │
│  │                                                                   │
│  ├── 4. convertToTempNPC(base, techniques, formations) → TempNPC   │
│  │                                                                   │
│  └── 5. generateFullEquipmentForNPC(tempNPC, rng)                  │
│         │                                                            │
│         ├── generateEquipmentForNPC() → TempEquipment              │
│         │   └── equipment-generator-v2.ts                           │
│         │                                                            │
│         └── generateInventoryForNPC() → TempItem[]                  │
│             ├── generateHealingPill() → consumable-generator.ts     │
│             ├── generateElixir() → consumable-generator.ts          │
│             ├── generateFood() → consumable-generator.ts            │
│             └── generateJunkItem() → хлам                            │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 📚 Связанные документы

- [generators.md](./generators.md) — Документация генераторов V5
- [technique-system-v2.md](./technique-system-v2.md) — Система техник V2.8
- [matryoshka-architecture.md](./matryoshka-architecture.md) — Архитектура "Матрёшка"
- [checkpoints/checkpoint_03_22_Generator_Migration.md](./checkpoints/checkpoint_03_22_Generator_Migration.md) — Миграция V1→V2

---

## 📝 API Endpoints

| Endpoint | Назначение |
|----------|-----------|
| `/api/generator/techniques` | Техники (V2 по умолчанию) |
| `/api/generator/npc` | Базовые NPC |
| `/api/generator/npc-full` | Полные NPC |
| `/api/generator/formations` | Боевые формации |
| `/api/generator/equipment` | Экипировка V2 |
| `/api/generator/items` | Расходники (сохранение/загрузка) |

---

*Документ создан: 2026-03-19*
*Обновлён: 2026-03-22 15:40 UTC*
*Добавлен technique-compat.ts, обновлён npc-full-generator (V2 Migrated)*
*Миграция V1→V2: ✅ ЗАВЕРШЕНА*
