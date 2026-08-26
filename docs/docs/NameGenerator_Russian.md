# Генератор названий на русском языке

> **Дата создания:** 2026-04-03 09:14:44 UTC
> **Редактировано:** 2026-05-23 06:30:00 UTC — концепция актуальна, требуется переделка под генераторы техник
> **Статус:** 🟡 Требует переработки
> **Проблема:** Согласование прилагательных с существительными по роду
> 
> ⚠️ **ПЕРЕРАБОТКА:** Концепция грамматического согласования будет использована для генераторов техник.
> Проблема имён техник аналогична: прилагательные-модификаторы должны согласовываться
> с базовым названием техники по роду (Пылающий Удар / Пылающая Стена / Пылающее Копьё).
> Код в этом документе — legacy (namespace CultivationWorld.Generation). В новой архитектуре
> используйте CultivationGame.Modules.Generator + CultivationGame.Core.Random.SeededRandom.

---

## 🎯 Постановка задачи

При процедурной генерации предметов на русском языке необходимо согласовывать модификаторы (прилагательные) с базовыми существительными по грамматическому роду:

```
❌ Пылающий секира  (мужской + женский = ошибка)
✅ Пылающая секира  (женский + женний = корректно)
```

---

## 📚 Грамматическая система

### Роды в русском языке

| Род | Окончания | Примеры |
|-----|-----------|---------|
| Мужской | -∅, -й, -ь | меч, посох, кинжаль |
| Женский | -а, -я, -ь | секира, катана, мантия |
| Средний | -о, -е, -ё | копьё, кольцо, ожерелье |
| Множественное | -ы, -и, -а | перчатки, сапоги, наручи |

### Согласование прилагательных

| Род | Основа | Пример (Пыла-) |
|-----|--------|----------------|
| Мужской | -ый, -ий | Пылающий |
| Женский | -ая, -яя | Пылающая |
| Средний | -ое, -ее | Пылающее |
| Множественное | -ые, -ие | Пылающие |

---

## 🏗️ Архитектура решения

### Структура файлов

```
Scripts/Generation/
├── Core/
│   ├── GrammaticalGender.cs      # Перечисление родов
│   ├── RussianWord.cs            # Класс слова с формами
│   └── NameGenerator.cs          # Основной генератор
├── Data/
│   ├── NounDatabase.cs           # SO для существительных
│   └── ModifierDatabase.cs       # SO для модификаторов
└── Editor/
    └── GeneratorEditor.cs        # Кастомный инспектор
```

---

## 💻 Реализация

### 1. GrammaticalGender.cs

```csharp
// Scripts/Generation/Core/GrammaticalGender.cs
// Дата: 2025-01-12

namespace CultivationWorld.Generation
{
    /// <summary>
    /// Грамматический род в русском языке
    /// </summary>
    public enum GrammaticalGender
    {
        Masculine,   // Мужской род (меч, топор, посох, кинжаль)
        Feminine,    // Женский род (секира, катана, мантия, броня)
        Neuter,      // Средний род (копьё, кольцо, ожерелье)
        Plural       // Множественное число (перчатки, сапоги)
    }

    /// <summary>
    /// Падежи русского языка (для будущего расширения)
    /// </summary>
    public enum RussianCase
    {
        Nominative,      // Именительный (кто? что?) - меч
        Genitive,        // Родительный (кого? чего?) - меча
        Dative,          // Дательный (кому? чему?) - мечу
        Accusative,      // Винительный (кого? что?) - меч
        Instrumental,    // Творительный (кем? чем?) - мечом
        Prepositional    // Предложный (о ком? о чём?) - мече
    }
}
```

### 2. RussianWord.cs

```csharp
// Scripts/Generation/Core/RussianWord.cs
// Дата: 2025-01-12

using System;

namespace CultivationWorld.Generation
{
    /// <summary>
    /// Слово с грамматическими формами для согласования
    /// </summary>
    [Serializable]
    public class RussianWord
    {
        [Tooltip("Базовая форма (Именительный падеж)")]
        public string baseForm;

        [Tooltip("Грамматический род")]
        public GrammaticalGender gender;

        /// <summary>
        /// Получить форму прилагательного, согласованную с данным словом
        /// </summary>
        public string GetAgreedAdjective(AdjectiveForms adjective)
        {
            return gender switch
            {
                GrammaticalGender.Masculine => adjective.formMasculine,
                GrammaticalGender.Feminine => adjective.formFeminine,
                GrammaticalGender.Neuter => adjective.formNeuter,
                GrammaticalGender.Plural => adjective.formPlural,
                _ => adjective.formMasculine
            };
        }
    }

    /// <summary>
    /// Формы прилагательного по родам
    /// </summary>
    [Serializable]
    public class AdjectiveForms
    {
        public string formMasculine;   // Пылающий
        public string formFeminine;    // Пылающая
        public string formNeuter;      // Пылающее
        public string formPlural;      // Пылающие

        public string GetForm(GrammaticalGender gender)
        {
            return gender switch
            {
                GrammaticalGender.Masculine => formMasculine,
                GrammaticalGender.Feminine => formFeminine,
                GrammaticalGender.Neuter => formNeuter,
                GrammaticalGender.Plural => formPlural,
                _ => formMasculine
            };
        }
    }
}
```

### 3. NounDatabase.cs

```csharp
// Scripts/Generation/Data/NounDatabase.cs
// Дата: 2025-01-12

using UnityEngine;
using System.Collections.Generic;

namespace CultivationWorld.Generation
{
    /// <summary>
    /// База данных существительных (базовые названия предметов)
    /// </summary>
    [CreateAssetMenu(
        fileName = "NounDatabase",
        menuName = "Cultivation/Generation/Noun Database")]
    public class NounDatabase : ScriptableObject
    {
        [System.Serializable]
        public class NounEntry
        {
            [Tooltip("Уникальный идентификатор")]
            public string id;

            [Tooltip("Слово в именительном падеже")]
            public string nominative;

            [Tooltip("Грамматический род")]
            public GrammaticalGender gender;

            [Tooltip("Тип экипировки")]
            public EquipmentType equipmentType;

            [Tooltip("Базовый уровень предмета")]
            [Range(1, 5)]
            public int baseTier = 1;

            [Tooltip("Вес при случайном выборе")]
            [Range(0.1f, 10f)]
            public float weight = 1f;
        }

        public List<NounEntry> nouns = new List<NounEntry>();

        /// <summary>
        /// Получить случайное существительное по типу экипировки
        /// </summary>
        public NounEntry GetRandom(EquipmentType type)
        {
            var filtered = nouns.FindAll(n => n.equipmentType == type);
            if (filtered.Count == 0) return null;

            // Взвешенный случайный выбор
            float totalWeight = 0f;
            foreach (var n in filtered) totalWeight += n.weight;

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var n in filtered)
            {
                cumulative += n.weight;
                if (roll <= cumulative) return n;
            }

            return filtered[0];
        }

        /// <summary>
        /// Получить существительное по ID
        /// </summary>
        public NounEntry GetById(string id)
        {
            return nouns.Find(n => n.id == id);
        }
    }
}
```

### 4. ModifierDatabase.cs

```csharp
// Scripts/Generation/Data/ModifierDatabase.cs
// Дата: 2025-01-12

using UnityEngine;
using System.Collections.Generic;

namespace CultivationWorld.Generation
{
    public enum ModifierType
    {
        Prefix,   // Перед существительным: "Пылающий меч"
        Suffix    // После существительного: "Меч Дракона"
    }

    /// <summary>
    /// База данных модификаторов (префиксы и суффиксы)
    /// </summary>
    [CreateAssetMenu(
        fileName = "ModifierDatabase",
        menuName = "Cultivation/Generation/Modifier Database")]
    public class ModifierDatabase : ScriptableObject
    {
        [System.Serializable]
        public class ModifierEntry
        {
            [Tooltip("Уникальный идентификатор")]
            public string id;

            [Header("Формы прилагательного по родам")]
            [Tooltip("Мужской род: Пылающий")]
            public string formMasculine;

            [Tooltip("Женский род: Пылающая")]
            public string formFeminine;

            [Tooltip("Средний род: Пылающее")]
            public string formNeuter;

            [Tooltip("Множественное число: Пылающие")]
            public string formPlural;

            [Header("Характеристики модификатора")]
            [Tooltip("Тип: префикс или суффикс")]
            public ModifierType modifierType;

            [Tooltip("Минимальный ранг для появления")]
            public ItemRank minRank = ItemRank.Mortal;

            [Tooltip("Вес при случайном выборе")]
            [Range(0.1f, 10f)]
            public float weight = 1f;

            [Tooltip("Модификаторы характеристик")]
            public StatModifier[] statModifiers;

            /// <summary>
            /// Получить форму, согласованную с родом
            /// </summary>
            public string GetForm(GrammaticalGender gender)
            {
                return gender switch
                {
                    GrammaticalGender.Masculine => formMasculine,
                    GrammaticalGender.Feminine => formFeminine,
                    GrammaticalGender.Neuter => formNeuter,
                    GrammaticalGender.Plural => formPlural,
                    _ => formMasculine
                };
            }
        }

        public List<ModifierEntry> modifiers = new List<ModifierEntry>();

        /// <summary>
        /// Получить случайный модификатор
        /// </summary>
        public ModifierEntry GetWeightedRandom(ModifierType type, ItemRank rank)
        {
            var filtered = modifiers.FindAll(m =>
                m.modifierType == type && m.minRank <= rank);

            if (filtered.Count == 0) return null;

            float totalWeight = 0f;
            foreach (var m in filtered) totalWeight += m.weight;

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var m in filtered)
            {
                cumulative += m.weight;
                if (roll <= cumulative) return m;
            }

            return filtered[0];
        }

        /// <summary>
        /// Получить модификатор по ID
        /// </summary>
        public ModifierEntry GetById(string id)
        {
            return modifiers.Find(m => m.id == id);
        }
    }
}
```

### 5. NameGenerator.cs

```csharp
// Scripts/Generation/Core/NameGenerator.cs
// Дата: 2025-01-12

using UnityEngine;

namespace CultivationWorld.Generation
{
    /// <summary>
    /// Генератор названий предметов на русском языке
    /// с учётом грамматического согласования
    /// </summary>
    public class NameGenerator : MonoBehaviour
    {
        [Header("Базы данных")]
        [SerializeField] private NounDatabase nounDatabase;
        [SerializeField] private ModifierDatabase modifierDatabase;

        [Header("Настройки генерации")]
        [Tooltip("Шанс появления префикса по рангам (Mortal..Divine)")]
        [Range(0f, 1f)]
        public float[] prefixChances = { 0.1f, 0.25f, 0.5f, 0.75f, 1f };

        [Tooltip("Шанс появления суффикса по рангам")]
        [Range(0f, 1f)]
        public float[] suffixChances = { 0.1f, 0.25f, 0.5f, 0.75f, 1f };

        /// <summary>
        /// Генерация полного названия предмета
        /// </summary>
        /// <param name="nounId">ID базового существительного</param>
        /// <param name="prefixId">ID префикса (null для случайного)</param>
        /// <param name="suffixId">ID суффикса (null для случайного)</param>
        /// <param name="rank">Ранг предмета (влияет на шанс модификаторов)</param>
        public GeneratedNameResult GenerateName(
            string nounId,
            string prefixId = null,
            string suffixId = null,
            ItemRank rank = ItemRank.Mortal)
        {
            // 1. Получаем существительное
            var noun = nounDatabase.GetById(nounId);
            if (noun == null)
            {
                return new GeneratedNameResult
                {
                    fullName = "Неизвестный предмет",
                    isValid = false
                };
            }

            // 2. Определяем префикс
            ModifierDatabase.ModifierEntry prefix = null;
            if (!string.IsNullOrEmpty(prefixId))
            {
                prefix = modifierDatabase.GetById(prefixId);
            }
            else if (ShouldRollModifier(rank, prefixChances))
            {
                prefix = modifierDatabase.GetWeightedRandom(ModifierType.Prefix, rank);
            }

            // 3. Определяем суффикс
            ModifierDatabase.ModifierEntry suffix = null;
            if (!string.IsNullOrEmpty(suffixId))
            {
                suffix = modifierDatabase.GetById(suffixId);
            }
            else if (ShouldRollModifier(rank, suffixChances))
            {
                suffix = modifierDatabase.GetWeightedRandom(ModifierType.Suffix, rank);
            }

            // 4. Строим название
            string fullName = BuildFullName(noun, prefix, suffix);

            return new GeneratedNameResult
            {
                fullName = fullName,
                baseNoun = noun.nominative,
                prefix = prefix?.GetForm(noun.gender),
                suffix = suffix?.formMasculine, // Суффиксы в родительном падеже
                isValid = true
            };
        }

        /// <summary>
        /// Генерация случайного названия по типу экипировки
        /// </summary>
        public GeneratedNameResult GenerateRandom(
            EquipmentType equipmentType,
            ItemRank rank = ItemRank.Mortal)
        {
            var noun = nounDatabase.GetRandom(equipmentType);
            if (noun == null)
            {
                return new GeneratedNameResult
                {
                    fullName = "Ошибка генерации",
                    isValid = false
                };
            }

            return GenerateName(noun.id, null, null, rank);
        }

        /// <summary>
        /// Построение полного названия
        /// </summary>
        private string BuildFullName(
            NounDatabase.NounEntry noun,
            ModifierDatabase.ModifierEntry prefix,
            ModifierDatabase.ModifierEntry suffix)
        {
            string result = "";

            // Префикс (прилагательное, согласовывается по роду)
            if (prefix != null)
            {
                result += prefix.GetForm(noun.gender) + " ";
            }

            // Базовое существительное
            result += noun.nominative;

            // Суффикс (обычно существительное в родительном падеже)
            // Примеры: "Меч Дракона", "Секира Бури"
            if (suffix != null)
            {
                result += " " + suffix.formMasculine;
            }

            return result;
        }

        /// <summary>
        /// Проверка, нужно ли бросать кубик на модификатор
        /// </summary>
        private bool ShouldRollModifier(ItemRank rank, float[] chances)
        {
            int rankIndex = (int)rank - 1; // ItemRank.Mortal = 1 -> index 0
            if (rankIndex < 0 || rankIndex >= chances.Length) return false;

            return Random.value < chances[rankIndex];
        }
    }

    /// <summary>
    /// Результат генерации названия
    /// </summary>
    [System.Serializable]
    public class GeneratedNameResult
    {
        public string fullName;    // "Пылающая секира Дракона"
        public string baseNoun;    // "секира"
        public string prefix;      // "Пылающая"
        public string suffix;      // "Дракона"
        public bool isValid;

        public override string ToString() => fullName;
    }
}
```

---

## 📊 Примеры данных

### NounDatabase.asset (существительные)

| ID | Слово | Род | Тип | Вес |
|----|-------|-----|-----|-----|
| sword | меч | Masculine | Weapon | 2.0 |
| axe | топор | Masculine | Weapon | 1.5 |
| staff | посох | Masculine | Weapon | 1.2 |
| spear | копьё | Neuter | Weapon | 1.0 |
| seax | секира | Feminine | Weapon | 1.0 |
| katana | катана | Feminine | Weapon | 0.8 |
| dagger | кинжал | Masculine | Weapon | 1.5 |
| ring | кольцо | Neuter | Accessory | 1.0 |
| robe | мантия | Feminine | Armor | 1.2 |
| gloves | перчатки | Plural | Armor | 1.0 |
| boots | сапоги | Plural | Armor | 1.0 |

### ModifierDatabase.asset (префиксы)

| ID | Мужской | Женский | Средний | Мн.ч. | Мин.ранг |
|----|---------|---------|---------|-------|----------|
| burning | Пылающий | Пылающая | Пылающее | Пылающие | Earth |
| frozen | Ледяной | Ледяная | Ледяное | Ледяные | Mortal |
| thunder | Громовой | Громовая | Громовое | Громовые | Heaven |
| ancient | Древний | Древняя | Древнее | Древние | Earth |
| celestial | Небесный | Небесная | Небесное | Небесные | Immortal |
| cursed | Проклятый | Проклятая | Проклятое | Проклятые | Heaven |
| blessed | Благословенный | Благословенная | Благословенное | Благословенные | Heaven |

### ModifierDatabase.asset (суффиксы)

| ID | Форма | Мин.ранг | Значение |
|----|-------|----------|----------|
| dragon | Дракона | Heaven | +Урон дракона |
| storm | Бури | Earth | +Скорость атаки |
| monk | Монаха | Mortal | +Реген Ци |
| emperor | Императора | Immortal | +Все статы |
| void | Пустоты | Divine | +Крит.урон |

---

## ✅ Примеры генерации

| Ранг | База | Префикс | Суффикс | Результат |
|------|------|---------|---------|-----------|
| Mortal | меч (м) | - | - | меч |
| Mortal | секира (ж) | Ледяная | - | Ледяная секира |
| Earth | копьё (ср) | Пылающее | Бури | Пылающее копьё Бури |
| Heaven | катана (ж) | Громовая | Дракона | Громовая катана Дракона |
| Immortal | посох (м) | Небесный | Императора | Небесный посох Императора |
| Divine | перчатки (мн) | Благословенные | Пустоты | Благословенные перчатки Пустоты |

---

## 🚀 Будущие улучшения

1. **Падежные формы** - полная система склонений для диалогов
2. **Сложные прилагательные** - "иссиня-чёрный", "тёмно-красный"
3. **Культурные стили** - китайские/японские названия для определённых стилей
4. **Уникальные имена** - генерация имён для легендарных предметов

---

*Документ подготовлен для ЭТАПА 4 - Генерация экипировки и техник*
