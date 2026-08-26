# Система Vitality и HP частей тела

## Статус: ЗАПЛАНИРОВАНО

## Описание

Система живучести (vitality) влияет на расчёт HP частей тела персонажей и NPC. Это ключевой элемент боевой системы в стиле Kenshi.

---

## 1. Текущее состояние

### 1.1 Характеристика Vitality

**Vitality (Живучесть)** - характеристика, определяющая выносливость тела и способность переносить повреждения.

**Текущая реализация:**
- ✅ Vitality добавлена в `PresetNPCStats` (preset-npc.ts)
- ✅ Vitality добавлена в `TempNPCStats` (temp-npc.ts)
- ✅ Vitality добавлена в схему Prisma (NPC model)
- ✅ Vitality генерируется в `npc-generator.ts` из `species.baseStats.vitality`
- ✅ Vitality отображается в NPCViewerDialog

**Диапазоны по видам (из species-presets.ts):**
| Вид | Мин | Макс |
|-----|-----|------|
| Человек | 10 | 30 |
| Эльф | 15 | 50 |
| Демон | 20 | 50 |
| Волк | 5 | 20 |
| Тигр | 8 | 25 |
| Дракон | 50 | 300 |
| Дух | 1 | 100 |

### 1.2 HP частей тела (Body Parts)

**Текущая реализация:**
- BodyState определён в `npc-generator.ts`
- HP рассчитывается как: `baseHP * sizeMultiplier * cultivationBonus`
- `cultivationBonus = 1 + (cultivationLevel - 1) * 0.1`

**Проблема:** Vitality НЕ учитывается в расчёте HP!

---

## 2. План внедрения

### 2.1 Формула расчёта HP частей тела

**Предлагаемая формула:**
```
maxFunctionalHP = baseHP * sizeMultiplier * vitalityMultiplier * cultivationBonus
```

Где:
- `baseHP` - базовое HP части тела (head: 50, torso: 100, heart: 80, arm: 40, leg: 50)
- `sizeMultiplier` - множитель размера вида (tiny: 0.5, small: 0.75, medium: 1.0, large: 1.5, huge: 2.0)
- `vitalityMultiplier` = 1 + (vitality - 10) * 0.05
  - При vitality = 10: множитель = 1.0 (базовый)
  - При vitality = 20: множитель = 1.5
  - При vitality = 30: множитель = 2.0
  - При vitality = 50: множитель = 3.0
- `cultivationBonus` = 1 + (cultivationLevel - 1) * 0.1

### 2.2 Примеры расчёта

**Человек, уровень 1, vitality 15:**
- Torso: 100 * 1.0 * 1.25 * 1.0 = 125 HP
- Head: 50 * 1.0 * 1.25 * 1.0 = 62 HP

**Тигр, уровень 3, vitality 20:**
- Torso: 100 * 1.0 * 1.5 * 1.2 = 180 HP
- Head: 50 * 1.0 * 1.5 * 1.2 = 90 HP

**Дракон, уровень 7, vitality 200:**
- Torso: 100 * 2.0 * 10.5 * 1.6 = 3360 HP
- Head: 50 * 2.0 * 10.5 * 1.6 = 1680 HP

### 2.3 Файлы для изменения

1. **`src/lib/generator/npc-generator.ts`**
   - Обновить функцию `createBodyForSpecies()`
   - Добавить vitalityMultiplier в расчёт HP

2. **`src/game/character/CharacterBody.ts`** (если существует)
   - Добавить пересчёт HP при изменении vitality

3. **`src/components/game/BodyEditorDialog.tsx`**
   - Добавить отображение влияния vitality на HP

---

## 3. Зависимости

### 3.1 Связанные системы
- Боевая система (урон по частям тела)
- Система культивации (рост характеристик)
- Система ранений (Kenshi-style)

### 3.2 Баланс
- Vitality должна быть значимой, но не доминирующей
- Высокая vitality не должна делать NPC бессмертным
- Нужны тесты для проверки баланса

---

## 4. Тестирование

### 4.1 Unit-тесты
- [ ] Проверка формулы HP при разных vitality
- [ ] Проверка граничных случаев (vitality = 1, vitality = 300)
- [ ] Проверка совместимости с существующими NPC

### 4.2 Integration-тесты
- [ ] Генерация NPC с разными vitality
- [ ] Сохранение/загрузка NPC с vitality
- [ ] Отображение HP в UI

### 4.3 Ручное тестирование
- [ ] Открыть NPCViewerDialog
- [ ] Проверить отображение vitality
- [ ] Переключиться на полигон
- [ ] Проверить спавн NPC

---

## 5. Хронология изменений

| Дата | Изменение |
|------|-----------|
| 2024-03-01 | Добавлено поле vitality в схему NPC |
| 2024-03-01 | Исправлено отображение vitality в NPCViewerDialog |
| 2024-03-01 | GameContainer стал основным режимом |
| 2024-03-01 | Добавлена кнопка переключения на полигон |
| - | Планируется: пересчёт HP по формуле с vitality |

---

## 6. Ссылки

- [Lore формулы](../src/lib/generator/lore-formulas.ts)
- [NPC Generator](../src/lib/generator/npc-generator.ts)
- [Species Presets](../src/data/presets/species-presets.ts)
- [Типы NPC](../src/types/preset-npc.ts)
