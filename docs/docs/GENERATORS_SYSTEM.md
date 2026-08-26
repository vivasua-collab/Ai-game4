<!-- Редактировано: 2026-05-20 — Фаза 3: обновлены пути к новым модулям -->
# 🎲 Система генерации: Unity Migration

**Версия:** 1.2  
**Дата:** 2026-05-20  
**Статус:** ⚠️ Частично реализовано (ItemGeneratorService — Common grade только; TechniqueGeneratorService — не существует)  
**Источники:** generators.md

---

## ⚠️ Важно

> **Система РЕАЛИЗОВАНА.**  
> **Код доступен в `Modules/Generator/`, `Modules/NPC/`, `Core/Random/`**

---

## 📋 Обзор

Система генерации обеспечивает процедурное создание игровых объектов: техник, экипировки, NPC, расходников.

---

## 🏗️ Архитектура генерации

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        ГЕНЕРАТОРЫ                                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                    ОСНОВНЫЕ ГЕНЕРАТОРЫ                               │   │
│   │                                                                      │   │
│   │   TechniqueGenerator                                                 │   │
│   │   ├── Генерация техник                                              │   │
│   │   └── Архитектура "Матрёшка"                                        │   │
│   │                                                                      │   │
│   │   EquipmentGenerator                                                 │   │
│   │   ├── Оружие                                                        │   │
│   │   ├── Броня                                                         │   │
│   │   ├── Аксессуары                                                    │   │
│   │   └── Зарядники Ци                                                  │   │
│   │                                                                      │   │
│   │   NPCGenerator                                                       │   │
│   │   ├── Базовые NPC                                                   │   │
│   │   └── Полная генерация с экипировкой                                │   │
│   │                                                                      │   │
│   │   ConsumableGenerator                                                │   │
│   │   └── Таблетки, эликсиры, еда                                       │   │
│   │                                                                      │   │
│   │   FormationGenerator                                                 │   │
│   │   └── Боевые формации                                               │   │
│   │                                                                      │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                    ПОДДЕРЖИВАЮЩИЕ МОДУЛИ                            │   │
│   │                                                                      │   │
│   │   ├── SeededRandom — Детерминированный RNG                          │   │
│   │   ├── GradeSelector — Выбор Grade                                   │   │
│   │   ├── NameGenerator — Имена предметов                               │   │
│   │   └── MaterialRegistry — Реестр материалов                          │   │
│   │                                                                      │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 🎲 Seeded Random

### Принцип

Детерминированная генерация на основе seed.

```
seed = 12345
rng = SeededRandom(seed)
item1 = generateItem(rng)  // Всегда одинаковый для данного seed
item2 = generateItem(rng)  // Всегда одинаковый для данного seed
```

---

## ⚔️ Генератор техник

### Параметры

| Параметр | Описание |
|----------|----------|
| type | Тип техники |
| level | Уровень (1-9) |
| grade | Фиксированный Grade |
| element | Стихия |
| count | Количество |
| seed | Seed генерации |

### Архитектура "Матрёшка"

1. **База:** qiCost, capacity, baseDamage
2. **Grade:** множители качества
3. **Бонусы:** эффекты от Grade и стихии

> **Множители грейдов техник:** [TECHNIQUE_SYSTEM.md](./TECHNIQUE_SYSTEM.md) §«Система Grade»
> **Множители грейдов экипировки:** [EQUIPMENT_SYSTEM.md](./EQUIPMENT_SYSTEM.md) §2.1

---

## 🛡️ Генератор экипировки

### Параметры

| Параметр | Описание |
|----------|----------|
| type | Тип (weapon, armor, charger) |
| level | Уровень (1-9) |
| grade | Grade |
| materialId | ID материала |
| count | Количество |

### Поток генерации

```
1. selectMaterial(options)
2. selectGrade(options)
3. getBaseStats(level, rng)
4. applyMaterialToStats(base, material)
5. applyGradeToStats(stats, grade)
6. createDurabilityState(material, grade, level)
7. generateBonuses(grade, level, type, rng)
8. generateName(material, grade, level, rng)
9. generateRequirements(level, stats)
```

---

## 👥 Генератор NPC

### Параметры

| Параметр | Описание |
|----------|----------|
| species | Вид |
| level | Уровень культивации |
| role | Роль (monster, guard, passerby) |
| count | Количество |

### Генерация компонентов

| Компонент | Генератор |
|-----------|-----------|
| Техники | TechniqueGenerator |
| Экипировка | EquipmentGenerator |
| Инвентарь | Встроенный |
| Расходники | ConsumableGenerator |

---

## 📁 Файловая структура Unity

| Файл | Назначение |
|------|------------|
| `Modules/Generator/ItemGeneratorService.cs` | Генератор предметов (оружие, броня, расходники) |
| `Modules/Generator/TechniqueGeneratorService.cs` | Генератор техник |
| `Modules/Generator/ItemDatabaseService.cs` | База данных предметов |
| `Modules/Generator/TechniqueRegistry.cs` | Реестр техник |
| `Modules/NPC/SoulGenerator.cs` | Генератор души NPC |
| `Modules/NPC/NPCAssemblyService.cs` | Оркестратор пайплайна сборки NPC |
| `Modules/NPC/NPCSpawnerService.cs` | Спавн NPC |
| `Modules/NPC/NPCNameGenerator.cs` | Генератор имён NPC |
| `Modules/Body/SpeciesRegistry.cs` | Реестр видов |
| `Modules/Body/BodyFactory.cs` | Фабрика тел |
| `Core/Random/SeededRandom.cs` | Детерминированный RNG |

---

## 📚 Связанные документы

- [TECHNIQUE_SYSTEM.md](./TECHNIQUE_SYSTEM.md) — Система техник
- [INVENTORY_SYSTEM.md](./INVENTORY_SYSTEM.md) — Система инвентаря
- [NPC_AI_SYSTEM.md](./NPC_AI_SYSTEM.md) — Система NPC AI

---

*Документ создан: 2026-03-30*  
*Статус: Черновик для доработки*
