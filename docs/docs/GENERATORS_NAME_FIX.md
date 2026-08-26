# Правка генераторов: Грамматическое согласование названий

**Создано:** 2026-04-03 09:14:44 UTC
**Статус:** Требует реализации
**Источник:** docs/examples/NameGenerator_Russian.md

---

## 🚨 Проблема

Все существующие генераторы не учитывают грамматический род русских существительных:

```
❌ "Улучшенный секира" (мужской + женский)
✅ "Улучшенная секира" (женский + женский)

❌ "Лёгкий мантия" (мужской + женский)  
✅ "Лёгкая мантия" (женский + женский)

❌ "Огненный копьё" (мужской + средний)
✅ "Огненное копьё" (средний + средний)
```

---

## 📁 Анализ существующих генераторов

### 1. WeaponGenerator.cs

**Расположение:** `Scripts/Generators/WeaponGenerator.cs`
**Проблемная функция:** `GenerateName()` (строки 543-564)

```csharp
// ТЕКУЩИЙ КОД (неправильно):
string gradePrefix = weapon.grade switch
{
    EquipmentGrade.Damaged => "Сломанный ",      // ❌ Всегда мужской род
    EquipmentGrade.Refined => "Улучшенный ",     // ❌ Всегда мужской род
    EquipmentGrade.Perfect => "Совершенный ",    // ❌ Всегда мужской род
    EquipmentGrade.Transcendent => "Трансцендентный ", // ❌ Всегда мужской род
    _ => ""
};
```

**Проблемные данные:**
```csharp
// WeaponNamesBySubtype (строки 257-271)
// Не указан грамматический род для названий!

{ WeaponSubtype.Sword, new[] { "Меч", "Клинок", "Катана" } },
// "Меч" - мужской ✓, "Катана" - женский ❌ будет "Улучшенный катана"!
```

### 2. ArmorGenerator.cs

**Расположение:** `Scripts/Generators/ArmorGenerator.cs`
**Проблемная функция:** `GenerateName()` (строки 500-529)

```csharp
// ТЕКУЩИЙ КОД (неправильно):
string weightPrefix = armor.weightClass switch
{
    ArmorWeightClass.Light => "Лёгкий ",   // ❌ Всегда мужской род
    ArmorWeightClass.Heavy => "Тяжёлый ",  // ❌ Всегда мужской род
    _ => ""
};
```

**Проблемные данные:**
```csharp
// ArmorNamesBySubtype (строки 213-222)
{ ArmorSubtype.Torso, new[] { "Нагрудник", "Кираса", "Кольчуга", "Мантия", "Доспех" } },
// "Нагрудник" - мужской, "Кираса" - женский, "Мантия" - женский!

{ ArmorSubtype.Head, new[] { "Шлем", "Корона", "Капюшон", "Диадема", "Маска" } },
// "Корона" - женский, "Диадема" - женский!
```

### 3. TechniqueGenerator.cs

**Расположение:** `Scripts/Generators/TechniqueGenerator.cs`
**Проблемная функция:** `Generate()` строки 297-308

```csharp
// ТЕКУЩИЙ КОД (неправильно):
private static readonly Dictionary<Element, string> ElementPrefixes = new Dictionary<Element, string>
{
    { Element.Fire, "Огненный" },      // ❌ Всегда мужской род
    { Element.Water, "Водяной" },      // ❌ Всегда мужской род
    { Element.Earth, "Земляной" },     // ❌ Всегда мужской род
    { Element.Air, "Воздушный" },      // ❌ Всегда мужской род
    { Element.Lightning, "Громовой" }, // ❌ Всегда мужской род
    { Element.Void, "Пустотный" },     // ❌ Всегда мужской род
};
```

**Проблемные данные:**
```csharp
// TechniqueNames (строки 182-194)
{ TechniqueType.Defense, new[] { "Защита", "Блок", "Щит", "Стена", "Барьер" } },
// "Защита" - женский, "Стена" - женский - будет "Огненный защита" ❌

{ TechniqueType.Healing, new[] { "Исцеление", "Восстановление", "Лечение" } },
// "Исцеление" - средний род - будет "Огненный исцеление" ❌
```

---

## ✅ Решение

### Шаг 1: Создать общую систему родов

**Новый файл:** `Scripts/Generators/Naming/NounWithGender.cs`

```csharp
namespace CultivationGame.Generators
{
    /// <summary>
    /// Грамматический род в русском языке
    /// </summary>
    public enum GrammaticalGender
    {
        Masculine,   // Мужской (меч, топор, посох)
        Feminine,    // Женский (секира, катана, мантия)
        Neuter,      // Средний (копьё, кольцо, ожерелье)
        Plural       // Множественное (перчатки, сапоги)
    }

    /// <summary>
    /// Существительное с указанием рода
    /// </summary>
    [System.Serializable]
    public struct NounWithGender
    {
        public string noun;
        public GrammaticalGender gender;

        public NounWithGender(string noun, GrammaticalGender gender)
        {
            this.noun = noun;
            this.gender = gender;
        }
    }
}
```

### Шаг 2: Создать систему прилагательных

**Новый файл:** `Scripts/Generators/Naming/AdjectiveForms.cs`

```csharp
namespace CultivationGame.Generators
{
    /// <summary>
    /// Прилагательное с формами для всех родов
    /// </summary>
    [System.Serializable]
    public struct AdjectiveForms
    {
        public string masculine;   // Пылающий
        public string feminine;    // Пылающая
        public string neuter;      // Пылающее
        public string plural;      // Пылающие

        public string GetForm(GrammaticalGender gender)
        {
            return gender switch
            {
                GrammaticalGender.Masculine => masculine,
                GrammaticalGender.Feminine => feminine,
                GrammaticalGender.Neuter => neuter,
                GrammaticalGender.Plural => plural,
                _ => masculine
            };
        }
    }
}
```

### Шаг 3: Создать базу данных названий

**Новый файл:** `Scripts/Generators/Naming/NamingDatabase.cs`

```csharp
using System.Collections.Generic;

namespace CultivationGame.Generators
{
    /// <summary>
    /// База данных названий с грамматическим согласованием
    /// </summary>
    public static class NamingDatabase
    {
        // === ОРУЖИЕ (с указанием рода) ===
        public static readonly Dictionary<WeaponSubtype, NounWithGender[]> WeaponNames = new()
        {
            { WeaponSubtype.Sword, new[] {
                new NounWithGender("меч", GrammaticalGender.Masculine),
                new NounWithGender("клинок", GrammaticalGender.Masculine),
                new NounWithGender("катана", GrammaticalGender.Feminine)
            }},
            { WeaponSubtype.Axe, new[] {
                new NounWithGender("топор", GrammaticalGender.Masculine),
                new NounWithGender("секира", GrammaticalGender.Feminine),
                new NounWithGender("боевой топор", GrammaticalGender.Masculine)
            }},
            { WeaponSubtype.Spear, new[] {
                new NounWithGender("копьё", GrammaticalGender.Neuter),
                new NounWithGender("алебарда", GrammaticalGender.Feminine),
                new NounWithGender("глефа", GrammaticalGender.Feminine)
            }},
            { WeaponSubtype.Dagger, new[] {
                new NounWithGender("кинжал", GrammaticalGender.Masculine),
                new NounWithGender("стилет", GrammaticalGender.Masculine)
            }},
            { WeaponSubtype.Staff, new[] {
                new NounWithGender("посох", GrammaticalGender.Masculine),
                new NounWithGender("жезл", GrammaticalGender.Masculine)
            }},
            // ... остальные
        };

        // === БРОНЯ (с указанием рода) ===
        public static readonly Dictionary<ArmorSubtype, NounWithGender[]> ArmorNames = new()
        {
            { ArmorSubtype.Head, new[] {
                new NounWithGender("шлем", GrammaticalGender.Masculine),
                new NounWithGender("корона", GrammaticalGender.Feminine),
                new NounWithGender("капюшон", GrammaticalGender.Masculine),
                new NounWithGender("диадема", GrammaticalGender.Feminine)
            }},
            { ArmorSubtype.Torso, new[] {
                new NounWithGender("нагрудник", GrammaticalGender.Masculine),
                new NounWithGender("кираса", GrammaticalGender.Feminine),
                new NounWithGender("кольчуга", GrammaticalGender.Feminine),
                new NounWithGender("мантия", GrammaticalGender.Feminine),
                new NounWithGender("доспех", GrammaticalGender.Masculine)
            }},
            { ArmorSubtype.Hands, new[] {
                new NounWithGender("перчатки", GrammaticalGender.Plural),
                new NounWithGender("рукавицы", GrammaticalGender.Plural)
            }},
            { ArmorSubtype.Feet, new[] {
                new NounWithGender("сапоги", GrammaticalGender.Plural),
                new NounWithGender("ботинки", GrammaticalGender.Plural)
            }},
            // ... остальные
        };

        // === ПРИЛАГАТЕЛЬНЫЕ ===
        
        // Грейды оружия/брони
        public static readonly Dictionary<EquipmentGrade, AdjectiveForms> GradeAdjectives = new()
        {
            { EquipmentGrade.Damaged, new AdjectiveForms {
                masculine = "Сломанный",
                feminine = "Сломанная",
                neuter = "Сломанное",
                plural = "Сломанные"
            }},
            { EquipmentGrade.Refined, new AdjectiveForms {
                masculine = "Улучшенный",
                feminine = "Улучшенная",
                neuter = "Улучшенное",
                plural = "Улучшенные"
            }},
            { EquipmentGrade.Perfect, new AdjectiveForms {
                masculine = "Совершенный",
                feminine = "Совершенная",
                neuter = "Совершенное",
                plural = "Совершенные"
            }},
            { EquipmentGrade.Transcendent, new AdjectiveForms {
                masculine = "Трансцендентный",
                feminine = "Трансцендентная",
                neuter = "Трансцендентное",
                plural = "Трансцендентные"
            }}
        };

        // Весовые классы брони
        public static readonly Dictionary<ArmorWeightClass, AdjectiveForms> WeightAdjectives = new()
        {
            { ArmorWeightClass.Light, new AdjectiveForms {
                masculine = "Лёгкий",
                feminine = "Лёгкая",
                neuter = "Лёгкое",
                plural = "Лёгкие"
            }},
            { ArmorWeightClass.Heavy, new AdjectiveForms {
                masculine = "Тяжёлый",
                feminine = "Тяжёлая",
                neuter = "Тяжёлое",
                plural = "Тяжёлые"
            }}
        };

        // Элементы для техник
        public static readonly Dictionary<Element, AdjectiveForms> ElementAdjectives = new()
        {
            { Element.Fire, new AdjectiveForms {
                masculine = "Огненный",
                feminine = "Огненная",
                neuter = "Огненное",
                plural = "Огненные"
            }},
            { Element.Water, new AdjectiveForms {
                masculine = "Водяной",
                feminine = "Водяная",
                neuter = "Водяное",
                plural = "Водяные"
            }},
            { Element.Earth, new AdjectiveForms {
                masculine = "Земляной",
                feminine = "Земляная",
                neuter = "Земляное",
                plural = "Земляные"
            }},
            { Element.Air, new AdjectiveForms {
                masculine = "Воздушный",
                feminine = "Воздушная",
                neuter = "Воздушное",
                plural = "Воздушные"
            }},
            { Element.Lightning, new AdjectiveForms {
                masculine = "Громовой",
                feminine = "Громовая",
                neuter = "Громовое",
                plural = "Громовые"
            }},
            { Element.Void, new AdjectiveForms {
                masculine = "Пустотный",
                feminine = "Пустотная",
                neuter = "Пустотное",
                plural = "Пустотные"
            }}
        };
    }
}
```

---

## 📋 Чеклист изменений

### Новые файлы (создать)

| Файл | Описание |
|------|----------|
| `Scripts/Generators/Naming/GrammaticalGender.cs` | Enum родов |
| `Scripts/Generators/Naming/AdjectiveForms.cs` | Формы прилагательных |
| `Scripts/Generators/Naming/NamingDatabase.cs` | База данных названий |
| `Scripts/Generators/Naming/NameBuilder.cs` | Утилита построения имён |

### Изменяемые файлы

| Файл | Изменение |
|------|-----------|
| `WeaponGenerator.cs` | Заменить `GenerateName()` |
| `ArmorGenerator.cs` | Заменить `GenerateName()` |
| `TechniqueGenerator.cs` | Заменить ElementPrefixes и генерацию имени |

---

## 📊 Примеры до/после

### Оружие

| Было | Стало |
|------|-------|
| Улучшенный катана | Улучшенная катана |
| Совершенный секира | Совершенная секира |
| Трансцендентный копьё | Трансцендентное копьё |

### Броня

| Было | Стало |
|------|-------|
| Лёгкий мантия | Лёгкая мантия |
| Тяжёлый кираса | Тяжёлая кираса |
| Улучшенный перчатки | Улучшенные перчатки |

### Техники

| Было | Стало |
|------|-------|
| Огненный защита | Огненная защита |
| Громовой стена | Громовая стена |
| Водяной исцеление | Водяное исцеление |

---

## 🔗 Связанные документы

- [docs/examples/NameGenerator_Russian.md](../docs_temp/NameGenerator_Russian.md) — полная теория
- [docs/GENERATORS_SYSTEM.md](./GENERATORS_SYSTEM.md) — система генераторов

---

*Документ создан: 2026-04-03 09:14:44 UTC*
