# Чекпоинт: Этап 4 — Генератор формаций

**Дата:** 2026-08-23
**План:** `checkpoints/08_23_qi_impl_plan.md` (этап 4)
**Статус:** ✅ Завершён, сборка чистая, headless-прогон без ошибок.

## Что сделано

### 1. FormationShape (Core/Data/FormationEnums.cs)
Новый enum: Circle, Triangle, Square, Pentagon, Star, Hexagram —
геометрия контура для визуализатора (этап 6).

### 2. FormationData расширен (Modules/Formation/Data/FormationData.cs)
- `Shape` — форма контура.
- `EffectRadiusMeters` — радиус действия по размеру (50/200/600/1000/5000 м, FORMATION_SYSTEM §4).

### 3. FormationRegistry (Modules/Formation/Data/FormationRegistry.cs)
Реестр id → FormationData (аналог TechniqueRegistry).
FormationService.FindFormationData теперь смотрит в реестр ПЕРЕД legacy-хардкодом
(basic_barrier/dao_blade/shadow_bindings остаются как fallback).

### 4. FormationGeneratorService (Modules/Generator/FormationGeneratorService.cs)
«Матрёшка»: Тип (8) × Размер (5, взвешенно к малым) × Уровень (1-9) × Стихия × Форма.
- `Generate(level, seed)` — случайная формация; Heavy только L6+ (иначе → Medium).
- `GenerateSpecified(type, size, level, seed)` — заданный тип/размер.
- Gathering/Teleportation → Element.Neutral (чистое Ци).
- Имена без родовых конфликтов (родительный падеж): «Барьер Огня · Звезда · L3 (Малая)».
- Эффекты по типу (значения масштабируются уровнем, капы):
  - Barrier: Shield ally 0.2+0.05L (кап 0.6)
  - Trap: Control Freeze/Slow enemy 0.3+0.03L (кап 0.8)
  - Amplification: Buff Damage ally 0.2+0.02L (кап 0.6)
  - Suppression: Debuff Speed enemy 0.3+0.03L (кап 0.8)
  - Gathering: Buff Conductivity ×2 (envMult-прокси для медитации, этап 5)
  - Detection/Teleportation/Summoning: схематичные записи-заглушки
- Детерминизм: SeededRandom, id = form_{type}_{size}_{element}_{shape}_L{n}_{seedhash}.
- Регистрирует результат в FormationRegistry.

### 5. DI
- FormationModuleServices: +FormationRegistry.
- GeneratorModuleServices: +IFormationGeneratorService → FormationGeneratorService.
- FormationService: ctor +optional FormationRegistry (контейнер поддерживает default-параметры).

### Замечание по архитектуре
IFormationGeneratorService размещён в модуле Generator (НЕ Core/Interfaces):
возвращает FormationData из Modules.Formation.Data — Core не может ссылаться
на типы модулей (layering). Аналогично TechniqueRegistry — модульный сервис.

## Проверка
Сборка 0 ошибок, GODOT_NEWGAME=1 — все фазы проходят.
Работа генератора в рантайме проверяется на этапе 5 (создание формации игроком).
