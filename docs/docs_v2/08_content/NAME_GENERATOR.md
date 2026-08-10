# Генератор имён (Name Generator)

> **Назначение:** Движко-независимая спецификация генератора имён на русском языке. Описывает правила грамматического согласования прилагательных с существительными по роду, словарную базу, алгоритм генерации. Применяется для генерации названий предметов, имён NPC, названий техник.
>
> **Связанные документы:** `06_player/EQUIPMENT_SYSTEM.md`, `02_systems/TECHNIQUE_SYSTEM.md`, `04_entities/NPC.md`, `05_data/GENERATORS_SYSTEM.md`.

---

## 1. Постановка задачи

При процедурной генерации предметов, NPC и техник на русском языке необходимо согласовывать модификаторы (прилагательные) с базовыми существительными по **грамматическому роду**:

```
❌ Пылающий секира     (мужской + женский = ошибка)
✅ Пылающая секира     (женский + женский = корректно)
```

Та же проблема для названий техник:
- Пылающий Удар (мужской)
- Пылающая Стена (женский)
- Пылающее Копьё (средний)
- Пылающие Перчатки (множественное)

---

## 2. Грамматическая система

### 2.1. Роды в русском языке (4)

| Род | Окончания | Примеры |
|-----|-----------|---------|
| Мужской | -∅, -й, -ь | меч, посох, кинжал, топор |
| Женский | -а, -я, -ь | секира, катана, мантия, броня |
| Средний | -о, -е, -ё | копьё, кольцо, ожерелье |
| Множественное | -ы, -и, -а | перчатки, сапоги, наручи |

### 2.2. Согласование прилагательных

| Род | Основа | Пример (Пыла-) |
|-----|--------|----------------|
| Мужской | -ый, -ий | Пылающий |
| Женский | -ая, -яя | Пылающая |
| Средний | -ое, -ее | Пылающее |
| Множественное | -ые, -ие | Пылающие |

---

## 3. Архитектура решения

### 3.1. Компоненты

```
NameGenerator:
├── GrammaticalGender (enum)       # 4 значения: Masculine, Feminine, Neuter, Plural
├── RussianWord (class)            # Слово: baseForm + gender
├── AdjectiveForms (class)         # 4 формы прилагательного
├── NounDatabase (data)            # Словарь существительных
├── ModifierDatabase (data)        # Словарь модификаторов (префиксы/суффиксы)
└── NameGenerator (service)        # Главный генератор
```

### 3.2. Структуры данных

#### GrammaticalGender

```
enum GrammaticalGender:
    Masculine    # Мужской (меч, топор, посох, кинжал)
    Feminine     # Женский (секира, катана, мантия, броня)
    Neuter       # Средний (копьё, кольцо, ожерелье)
    Plural       # Множественное (перчатки, сапоги)
```

#### RussianWord

```
RussianWord:
    baseForm: string               # Базовая форма (именительный падеж)
    gender: GrammaticalGender      # Грамматический род

    GetAgreedAdjective(adjective: AdjectiveForms) → string
        # Возвращает форму прилагательного, согласованную с родом слова
```

#### AdjectiveForms

```
AdjectiveForms:
    formMasculine: string          # "Пылающий"
    formFeminine: string           # "Пылающая"
    formNeuter: string             # "Пылающее"
    formPlural: string             # "Пылающие"

    GetForm(gender: GrammaticalGender) → string
```

### 3.3. Модификаторы

```
enum ModifierType:
    Prefix    # Перед существительным: "Пылающий меч"
    Suffix    # После существительного: "Меч Дракона"
```

#### ModifierEntry

```
ModifierEntry:
    id: string                     # Уникальный идентификатор
    formMasculine: string          # Мужской род (для префиксов)
    formFeminine: string           # Женский род
    formNeuter: string             # Средний род
    formPlural: string             # Множественное число
    modifierType: ModifierType     # Prefix или Suffix
    minRank: ItemRank              # Минимальный ранг для появления
    weight: float                  # Вес при случайном выборе (0.1–10.0)
    statModifiers: List<StatModifier>  # Модификаторы характеристик
```

> **Суффиксы** обычно не склоняются (существительное в родительном падеже: «Дракона», «Бури», «Монаха»). Поле `formMasculine` хранит каноническую форму суффикса.

---

## 4. Словарь существительных (NounDatabase)

| ID | Слово | Род | Тип экипировки | Базовый тир | Вес выбора |
|----|-------|-----|----------------|-------------|------------|
| sword | меч | Masculine | Weapon | 1 | 2.0 |
| axe | топор | Masculine | Weapon | 1 | 1.5 |
| staff | посох | Masculine | Weapon | 1 | 1.2 |
| spear | копьё | Neuter | Weapon | 1 | 1.0 |
| seax | секира | Feminine | Weapon | 1 | 1.0 |
| katana | катана | Feminine | Weapon | 1 | 0.8 |
| dagger | кинжал | Masculine | Weapon | 1 | 1.5 |
| ring | кольцо | Neuter | Accessory | 1 | 1.0 |
| robe | мантия | Feminine | Armor | 1 | 1.2 |
| gloves | перчатки | Plural | Armor | 1 | 1.0 |
| boots | сапоги | Plural | Armor | 1 | 1.0 |
| amulet | амулет | Masculine | Accessory | 1 | 0.8 |
| helmet | шлем | Masculine | Armor | 1 | 1.0 |
| belt | пояс | Masculine | Armor | 1 | 0.8 |

### 4.1. Методы NounDatabase

- `GetRandom(equipmentType) → NounEntry` — взвешенный случайный выбор по типу экипировки.
- `GetById(id) → NounEntry` — поиск по ID.

Взвешенный выбор:
```
totalWeight = Σ(weight) для отфильтрованных записей
roll = Random(0, totalWeight)
cumulative = 0
for each entry:
    cumulative += entry.weight
    if roll ≤ cumulative: return entry
```

---

## 5. Словарь модификаторов (ModifierDatabase)

### 5.1. Префиксы

| ID | Мужской | Женский | Средний | Мн.ч. | Мин.ранг |
|----|---------|---------|---------|-------|----------|
| burning | Пылающий | Пылающая | Пылающее | Пылающие | Earth |
| frozen | Ледяной | Ледяная | Ледяное | Ледяные | Mortal |
| thunder | Громовой | Громовая | Громовое | Громовые | Heaven |
| ancient | Древний | Древняя | Древнее | Древние | Earth |
| celestial | Небесный | Небесная | Небесное | Небесные | Immortal |
| cursed | Проклятый | Проклятая | Проклятое | Проклятые | Heaven |
| blessed | Благословенный | Благословенная | Благословенное | Благословенные | Heaven |
| shadow | Теневой | Теневая | Теневое | Теневые | Earth |
| spirit | Духовный | Духовная | Духовное | Духовные | Earth |
| golden | Золотой | Золотая | Золотое | Золотые | Heaven |

### 5.2. Суффиксы

| ID | Форма | Мин.ранг | Значение |
|----|-------|----------|----------|
| dragon | Дракона | Heaven | +Урон дракона |
| storm | Бури | Earth | +Скорость атаки |
| monk | Монаха | Mortal | +Реген Ци |
| emperor | Императора | Immortal | +Все статы |
| void | Пустоты | Divine | +Крит.урон |
| phoenix | Феникса | Immortal | +Возрождение |
| tiger | Тигра | Earth | +Сила |
| serpent | Змея | Heaven | +Ядовитость |

---

## 6. Ранги предметов

```
enum ItemRank:
    Mortal      # 1 — Смертный (базовый)
    Earth       # 2 — Земной
    Heaven      # 3 — Небесный
    Immortal    # 4 — Бессмертный
    Divine      # 5 — Божественный
```

Шанс модификатора по рангу:

| Ранг | Шанс префикса | Шанс суффикса |
|------|---------------|---------------|
| Mortal | 0.10 | 0.10 |
| Earth | 0.25 | 0.25 |
| Heaven | 0.50 | 0.50 |
| Immortal | 0.75 | 0.75 |
| Divine | 1.00 | 1.00 |

---

## 7. Алгоритм генерации имени

### 7.1. Шаги

```
GenerateName(nounId, prefixId?, suffixId?, rank):
  1. noun = NounDatabase.GetById(nounId)
     if noun == null: return "Неизвестный предмет" (isValid=false)

  2. prefix = null
     if prefixId != null:
         prefix = ModifierDatabase.GetById(prefixId)
     else if ShouldRollModifier(rank, prefixChances):
         prefix = ModifierDatabase.GetWeightedRandom(Prefix, rank)

  3. suffix = null
     if suffixId != null:
         suffix = ModifierDatabase.GetById(suffixId)
     else if ShouldRollModifier(rank, suffixChances):
         suffix = ModifierDatabase.GetWeightedRandom(Suffix, rank)

  4. fullName = BuildFullName(noun, prefix, suffix)

  5. return GeneratedNameResult(fullName, baseNoun, prefix, suffix, isValid=true)
```

### 7.2. Построение полного имени

```
BuildFullName(noun, prefix, suffix):
  result = ""

  # Префикс (прилагательное, согласуется по роду)
  if prefix != null:
      result += prefix.GetForm(noun.gender) + " "

  # Базовое существительное
  result += noun.nominative

  # Суффикс (существительное в родительном падеже)
  # Примеры: "Меч Дракона", "Секира Бури"
  if suffix != null:
      result += " " + suffix.formMasculine  # каноническая форма

  return result
```

### 7.3. ShouldRollModifier

```
ShouldRollModifier(rank, chances):
  rankIndex = (int)rank - 1   # ItemRank.Mortal=1 → index 0
  if rankIndex < 0 or rankIndex >= chances.Length: return false
  return Random.value < chances[rankIndex]
```

---

## 8. Примеры генерации

| Ранг | База | Префикс | Суффикс | Результат |
|------|------|---------|---------|-----------|
| Mortal | меч (м) | — | — | меч |
| Mortal | секира (ж) | Ледяная | — | Ледяная секира |
| Earth | копьё (ср) | Пылающее | Бури | Пылающее копьё Бури |
| Heaven | катана (ж) | Громовая | Дракона | Громовая катана Дракона |
| Immortal | посох (м) | Небесный | Императора | Небесный посох Императора |
| Divine | перчатки (мн) | Благословенные | Пустоты | Благословенные перчатки Пустоты |
| Heaven | амулет (м) | Духовный | Монаха | Духовный амулет Монаха |
| Immortal | кольцо (ср) | Золотое | Феникса | Золотое кольцо Феникса |

---

## 9. Расширение для названий техник

> Та же архитектура применяется для генерации названий техник.

### 9.1. Словарь базовых названий техник

| ID | Название | Род | Тип техники |
|----|----------|-----|-------------|
| strike | Удар | Masculine | melee |
| slash | Разруб | Masculine | melee |
| wall | Стена | Feminine | defense |
| spear_tech | Копьё | Neuter | ranged |
| wave | Волна | Feminine | ranged |
| barrier | Барьер | Masculine | defense |
| aura | Аура | Feminine | support |
| step | Шаг | Masculine | movement |
| gaze | Взгляд | Masculine | sensory |
| array | Массив | Masculine | formation |

### 9.2. Примеры

| Ранг | База | Префикс | Результат |
|------|------|---------|-----------|
| Mortal | Удар (м) | — | Удар |
| Earth | Удар (м) | Пылающий | Пылающий Удар |
| Earth | Стена (ж) | Пылающая | Пылающая Стена |
| Earth | Копьё (ср) | Пылающее | Пылающее Копьё |
| Heaven | Перчатки (мн) | Громовые | Громовые Перчатки (название техники-усиления) |

---

## 10. Расширение для имён NPC

### 10.1. Структура имени NPC

Имя NPC состоит из:
- **Фамилия/Прозвище** (1 слово или комбинация).
- **Имя** (1–2 слога).
- (Опционально) **Титул**.

### 10.2. Слоговая система

Имена генерируются из слогов:

| Категория | Слоги |
|-----------|-------|
| Начальные | Чжан, Ли, Ван, Чэнь, Сун, Лю, Ян, Хуан, Чжао, У |
| Средние | мин, тян, хай, лун, фэн, юй, син, бо, цзя, юань |
| Конечные | рен, фу, чжэ, лин, хуа, син, юй, лун, мин |

### 10.3. Алгоритм

```
GenerateNPCName(seed):
  rng = SeededRandom(seed)
  surname = PickRandom(initialSyllables, rng)
  givenName = ""
  syllableCount = 1 + rng.Next(0, 2)   # 1–2 слога
  for i in 0..syllableCount:
      if i == 0:
          givenName += PickRandom(middleSyllables, rng)
      else:
          givenName += PickRandom(finalSyllables, rng)
  return surname + " " + givenName
```

### 10.4. Примеры

- Чжан Минь
- Ли Тянлун
- Ван Хайюй
- Чэнь Бофэн
- Сун Юйсин

---

## 11. Детерминизм

### 11.1. Принцип

Все генераторы используют **SeededRandom** — детерминированный генератор псевдослучайных чисел с заданным seed.

```
seed → SeededRandom → последовательность "случайных" чисел
```

Один и тот же seed всегда даёт один и тот же результат. Это позволяет:
- Воспроизводимость в тестах.
- Сохранение сгенерированных имён в сейве (по seed, без хранения полного имени).
- Мультиплеер (все игроки видят одно и то же имя NPC).

> См. `05_data/GENERATORS_SYSTEM.md` для деталей SeededRandom.

---

## 12. Будущие улучшения

1. **Падежные формы** — полная система склонений для диалогов (Именительный, Родительный, Дательный, Винительный, Творительный, Предложный).
2. **Сложные прилагательные** — «иссиня-чёрный», «тёмно-красный», «светло-зелёный».
3. **Культурные стили** — китайские/японские названия для определённых стилей (секты, фракции).
4. **Уникальные имена** — генерация имён для легендарных предметов (с уникальными суффиксами).
5. **Лорные имена** — имена, привязанные к лору мира (имена исторических личностей, героев).

---

## 13. Открытые вопросы

1. **Склонение имён NPC** — нужно ли склонять имена в диалогах? (TBD — пока только именительный падеж.)
2. **Китайские vs русские имена** — в каком соотношении? (Текущее: китайские слоги, русская транслитерация.)
3. **Имена монстров** — отдельная система или общая? (Пока: общая, с расширенным словарём.)
4. **Имена техник** — сохранять ли оригинальные китайские названия или только русские? (Текущее: только русские.)

---

## 14. Связанные документы

- `06_player/EQUIPMENT_SYSTEM.md` — генерация экипировки с именами.
- `02_systems/TECHNIQUE_SYSTEM.md` — генерация техник с именами.
- `04_entities/NPC.md` — генерация NPC с именами.
- `05_data/GENERATORS_SYSTEM.md` — общая система генераторов, SeededRandom.
- `08_content/LORE_SYSTEM.md` — лорные основы имён.
