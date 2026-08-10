# 📂 docs_temp/ — Временная документация, черновики

**Версия:** 4.1
**Дата:** 2026-06-05
**Проект:** Cultivation World Simulator (Unity 6.3 URP 2D)

---

## ⚠️ Назначение папки

> **docs_temp/** — временное хранилище: черновики систем в глубокой разработке,
> аналитические отчёты, исследования, примеры и руководства.
>
> **Статус файлов:** 🟢 Актуальный | 🟡 В разработке / Требует переработки | ⚪ Исторический
>
> **Основная документация:** `docs/` (валидированная, утверждённая).
> **Архив (Phaser-эра):** `docs_old/`.
> **Легаси (аудиты, старый код, инструкции):** `Legacy/`
> **Все папки docs/Legacy/checkpoints — внутри UnityProject/.**

### 📐 Оценка токенов

> **Метод:** `chars ÷ 3` — приближённая оценка для русскоязычного текста с кодом.
> **Легенда стоимости:** 🔥 >15K | ⚠️ 5K–15K | ✅ <5K

---

## 📊 Сводка по категориям

| # | Категория | Файлов | Объём | Токены ≈ |
|---|-----------|--------|-------|----------|
| 1 | 📐 Черновики систем (в глубокой разработке) | 6 | 119 KB | 39.7K |
| 2 | 🧪 Примеры и спецификации | 5 | 100 KB | 33.3K |
| 3 | 🔧 Технические исследования | 2 | 59 KB | 19.7K |
| 4 | 📊 Аналитические отчёты | 2 | 40 KB | 13.3K |
| 5 | 📖 Справочники и руководства | 2 | 40 KB | 13.3K |
| | **Итого** | **17** | **~358 KB** | **~119K** |

---

## 1. 📐 Черновики систем — в глубокой разработке (6 файлов)

| Файл | Описание | Статус | Размер | Токены ≈ | |
|------|----------|--------|--------|----------|-|
| [INVENTORY_UI_DRAFT.md](./INVENTORY_UI_DRAFT.md) | Черновик UI инвентаря (grid→list переход) | 🟡 В разработке | 38 KB | 12.7K | 🔥 |
| [LONG_TERM_MEMORY_SCHEME.md](./LONG_TERM_MEMORY_SCHEME.md) | Схема долговременной памяти ИИ-агента (3 варианта) | 🟡 В разработке | 30 KB | 9.9K | ⚠️ |
| [tool_system_draft.md](./tool_system_draft.md) | Черновик системы инструментов (топор/кирка/серп) | 🟡 В разработке | 16 KB | 5.5K | ⚠️ |
| [QI_ABSORPTION_RADIUS.md](./QI_ABSORPTION_RADIUS.md) | Радиус сферы поглощения Ци (4 модели) | 🟡 В разработке | 13 KB | 4.3K | ✅ |
| [ACHIEVEMENT_SYSTEM.md](./ACHIEVEMENT_SYSTEM.md) | Система достижений — концепция и план (код удалён) | 🟡 План | 11 KB | 3.8K | ✅ |
| [STACKING_SYSTEM_DRAFT.md](./STACKING_SYSTEM_DRAFT.md) | Черновик системы стакинга предметов | 🟡 В разработке | 11 KB | 3.7K | ✅ |

---

## 2. 🧪 Примеры и спецификации (5 файлов)

| Файл | Описание | Статус | Размер | Токены ≈ | |
|------|----------|--------|--------|----------|-|
| [NPC_ASSEMBLY_EXAMPLES.md](./NPC_ASSEMBLY_EXAMPLES.md) | Тестовые сборки NPC L3/L6/L9 (VContainer+MessagePipe) | 🟢 Актуальный | 36 KB | 12.0K | 🔥 |
| [BREAKTHROUGH_MODELS_COMPARISON.md](./BREAKTHROUGH_MODELS_COMPARISON.md) | Сравнение 4 моделей прорыва (баланс) | 🟢 Актуальный | 25 KB | 8.4K | ⚠️ |
| [TechniqueEffectsSystem.md](./TechniqueEffectsSystem.md) | Система эффектов техник (спецификация) | 🟡 В разработке | 19 KB | 6.4K | ⚠️ |
| [StatThresholdSystem_Examples.md](./StatThresholdSystem_Examples.md) | Примеры порогов характеристик | ⚪ Справочный | 11 KB | 3.7K | ✅ |
| [FormationSystem_Examples.md](./FormationSystem_Examples.md) | Примеры системы формаций (паттерны актуальны) | 🟢 Актуальный | 9 KB | 3.1K | ✅ |

> `NPC_ASSEMBLY_EXAMPLES.md` — основан на текущем коде (NPCAssemblyService, SoulGenerator, VContainer+MessagePipe).

---

## 3. 🔧 Технические исследования (2 файла)

| Файл | Описание | Статус | Размер | Токены ≈ | |
|------|----------|--------|--------|----------|-|
| [CODE_REFERENCE.md](./CODE_REFERENCE.md) | Справочник кодовой базы (VContainer+MessagePipe, ~200 файлов) | 🟢 Актуальный | 43 KB | 14.4K | ⚠️ |
| [COMPUTATIONAL_RESOURCES_CALCULATION.md](./COMPUTATIONAL_RESOURCES_CALCULATION.md) | Расчёт ресурсов (обновлён под модульную архитектуру) | 🟢 Актуальный | 16 KB | 5.3K | ⚠️ |

> `CODE_REFERENCE.md` — описывает VContainer+MessagePipe модульную архитектуру.
> Включает: Namespace Map, Module Reference, Core Interfaces, MessagePipe Contracts.

---

## 4. 📊 Аналитические отчёты (2 файла)

| Файл | Описание | Статус | Размер | Токены ≈ | |
|------|----------|--------|--------|----------|-|
| [LOOT_SYSTEM_DRAFT.md](./LOOT_SYSTEM_DRAFT.md) | Черновик системы лута | 🟡 В разработке | 28 KB | 9.2K | ⚠️ |
| [TECHNIQUE_USAGE_REPORT.md](./TECHNIQUE_USAGE_REPORT.md) | Теория использования техник (пайплайн актуален) | 🟢 Актуальный | 12 KB | 4.1K | ✅ |

> `TECHNIQUE_USAGE_REPORT.md` — теория (10-слойный пайплайн, формулы) актуальна для доработки системы техник.

---

## 5. 📖 Справочники и руководства (2 файла)

| Файл | Описание | Статус | Размер | Токены ≈ | |
|------|----------|--------|--------|----------|-|
| [NameGenerator_Russian.md](./NameGenerator_Russian.md) | Генератор имён → переработка под генераторы техник | 🟡 Переработка | 23 KB | 7.2K | ⚠️ |
| [OrbitalWeaponSystem.md](./OrbitalWeaponSystem.md) | Орбитальное оружие → концепция артифактов | 🟡 Перенаправление | 17 KB | 5.7K | ⚠️ |

> 🔄 `NameGenerator_Russian.md` — концепция грамматического согласования будет переиспользована
> для генераторов техник (Пылающий Удар / Пылающая Стена / Пылающее Копьё).
> 🔄 `OrbitalWeaponSystem.md` — концепция перенаправлена на систему артифактов.

---

## 💰 Топ-5 самых дорогих файлов docs_temp/

| Файл | Токены ≈ | Категория | |
|------|----------|-----------|-|
| CODE_REFERENCE.md | 14.4K | Тех. исследования | ⚠️ |
| INVENTORY_UI_DRAFT.md | 12.7K | Черновики | 🔥 |
| NPC_ASSEMBLY_EXAMPLES.md | 12.0K | Примеры | 🔥 |
| LONG_TERM_MEMORY_SCHEME.md | 9.9K | Черновики | ⚠️ |
| LOOT_SYSTEM_DRAFT.md | 9.2K | Аналитика | ⚠️ |

---

## 🔄 Изменения версии 4.0 (2026-05-23)

### Удалено 17 устаревших файлов:

| Удалённый файл | Причина удаления |
|----------------|-----------------|
| ANALYSIS_REPORT.md | Одноразовый аудит документации, выводы учтены |
| BuffSystem_Examples.md | Legacy-код (ServiceLocator, float), не VContainer |
| CharacterSpriteMirroring.md | Legacy MonoBehaviour, концепция тривиальна |
| CODE_REVIEW_Local_Folder.md | Одноразовый аудит, папка Local уже решена |
| EQUIPPED_SPRITES_DRAFT.md | Legacy EquipmentController, будет пересоздано |
| GIT_WORKFLOW_TWO_PC.md | Устаревшая ветка main3Uniny, банальный Git |
| INVENTORY_FLAGS_AUDIT.md | Legacy + float (ЗАПРЕТ 3.9) |
| INVENTORY_IMPLEMENTATION_PLAN.md | Legacy-архитектура, этапы не начаты |
| LOST_SESSION_ANALYSIS.md | P1 задачи решены, анализ исторический |
| MIGRATION_ANALYSIS.md | Решение «новый код» принято и реализуется |
| NPC_L6_ASSEMBLY_EXAMPLE.md | Legacy-код, заменён на NPC_ASSEMBLY_EXAMPLES.md |
| PROJECT_SETUP_PLAN.md | Проект уже создан и настроен |
| RunningTests.md | Тесты для legacy-кода |
| TILE_SYSTEM_IMPLEMENTATION.md | Legacy-реализация |
| UNITY_63_RESEARCH.md | Общая информация, не специфична для проекта |
| UNITY_VERSION_COMPARISON.md | Решение Unity 6.3 уже принято |
| WORKFLOW_GITHUB_UNITY.md | Устаревшая ветка, банальный Git |

### Результат чистки:
- **Было:** 34 файла, ~564 KB, ~188K токенов
- **Стало:** 17 файлов, ~358 KB, ~119K токенов
- **Удалено:** 17 файлов, ~206 KB, ~69K токенов (−37%)

---

## 📊 Статистика по статусам

| Статус | Файлов | Доля |
|--------|--------|------|
| 🟢 Актуальный | 7 | 41% |
| 🟡 В разработке / Переработка | 8 | 47% |
| ⚪ Исторический / Справочный | 2 | 12% |

---

## 🔗 Связи с основной документацией

```
docs_temp/ → docs/ (основная документация)
    │
    ├── INVENTORY_UI_DRAFT.md
    │   └── → docs/INVENTORY_SYSTEM.md
    │
    ├── QI_ABSORPTION_RADIUS.md
    │   └── → docs/QI_SYSTEM.md, docs/ALGORITHMS.md
    │
    ├── BREAKTHROUGH_MODELS_COMPARISON.md
    │   └── → docs/ALGORITHMS.md, docs/CONFIGURATIONS.md
    │
    ├── TechniqueEffectsSystem.md
    │   └── → docs/TECHNIQUE_SYSTEM.md
    │
    ├── FormationSystem_Examples.md, StatThresholdSystem_Examples.md
    │   └── → docs/FORMATION_SYSTEM.md, docs/STAT_THRESHOLD_SYSTEM.md
    │
    ├── CODE_REFERENCE.md
    │   └── → UnityProject/Assets/Scripts/ (~200 файлов)
    │
    ├── NPC_ASSEMBLY_EXAMPLES.md
    │   └── → docs/NPC_ASSEMBLY_PIPELINE.md, Modules/NPC/NPCAssemblyService.cs
    │
    ├── NameGenerator_Russian.md
    │   └── → docs/TECHNIQUE_SYSTEM.md (генерация имён техник)
    │
    └── OrbitalWeaponSystem.md
        └── → docs/ARTIFACT_SYSTEM.md (концепция → артифакты)
```

---

*Создано: 2026-04-28*
*Редактировано: 2026-06-05 — версия 4.1: обновлены пути после переноса в UnityProject/, исправлены сломанные ссылки*
*Редактировано: 2026-05-23 — версия 4.0: удалено 17 устаревших файлов (legacy, исторические, банальные). Чистка −37% объёма.*
