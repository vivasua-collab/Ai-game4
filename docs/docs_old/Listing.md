# 📂 docs_old/ — Архив Phaser-эры — листинг

**Версия:** 2.0
**Дата:** 2026-04-30
**Проект:** Cultivation World Simulator (Unity 6.3 URP 2D)

---

## ⚠️ Назначение папки

> **docs_old/** — архив документации из предыдущей реализации на Phaser 3.
> **НЕ актуально** для текущего Unity-проекта. Используется только как справочник при миграции лора/механик.
>
> **Основная документация:** `docs/` → [!LISTING.md](../docs/!LISTING.md).
> **Инструкции для Unity Editor:** `docs_asset_setup/` → [!LISTING.md](../docs_asset_setup/!LISTING.md).
> **Черновики, аудиты:** `docs_temp/` → [!listing.md](../docs_temp/!listing.md).

### 📐 Оценка токенов

> **Метод:** `chars ÷ 3`. **Легенда стоимости:** 🔥 >15K | ⚠️ 5K–15K | ✅ <5K

---

## 📊 Сводка

| Показатель | Значение |
|---|---|
| Файлов | 69 |
| Объём | 2.0 MB |
| Токенов ≈ | 668K |

---

## Архитектура (8 файлов)

| Файл | Описание | Размер | Токены ≈ | |
|---|---|---|---|---|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | Общая архитектура v19, Phaser 3 | 64 KB | 21.4K | 🔥 |
| [ARCHITECTURE_cloud.md](./ARCHITECTURE_cloud.md) | Облачная архитектура | 71 KB | 23.5K | 🔥 |
| [ARCHITECTURE_future.md](./ARCHITECTURE_future.md) | Будущая архитектура | 44 KB | 14.8K | ⚠️ |
| [ARCHITECTURE_refact.md](./ARCHITECTURE_refact.md) | Рефакторинг архитектуры | 41 KB | 13.6K | ⚠️ |
| [ARCHITECTURE_code_base.md](./ARCHITECTURE_code_base.md) | Примеры кода архитектуры | 11 KB | 3.6K | ✅ |
| [matryoshka-architecture.md](./matryoshka-architecture.md) | Матрёшка-архитектура | 13 KB | 4.2K | ✅ |
| [architecture-analysis.md](./architecture-analysis.md) | Анализ архитектуры | 28 KB | 9.4K | ⚠️ |
| [PHASER_STACK.md](./PHASER_STACK.md) | Стек Phaser 3 | 5 KB | 1.7K | ✅ |

---

## Сущности и тело (10 файлов)

| Файл | Описание | Размер | Токены ≈ | |
|---|---|---|---|---|
| [body.md](./body.md) | Система тела (Kenshi-style) | 9 KB | 3.1K | ✅ |
| [body_monsters.md](./body_monsters.md) | Тела монстров | 133 KB | 44.2K | 🔥 |
| [body_armor.md](./body_armor.md) | Броня и части тела | 124 KB | 41.4K | 🔥 |
| [body_review.md](./body_review.md) | Ревью системы тела | 49 KB | 16.5K | 🔥 |
| [body-development-analysis.md](./body-development-analysis.md) | Анализ развития тела | 35 KB | 11.6K | 🔥 |
| [equip.md](./equip.md) | Экипировка v1 | 34 KB | 11.4K | 🔥 |
| [equip-v2.md](./equip-v2.md) | Экипировка v2 (Grade System) | 12 KB | 4.1K | ✅ |
| [soul-system.md](./soul-system.md) | SoulEntity + PhysicalObject | 22 KB | 7.2K | ⚠️ |
| [vitality-hp-system.md](./vitality-hp-system.md) | Система HP | 5 KB | 1.6K | ✅ |
| [condition-system.md](./condition-system.md) | Система состояний | 7 KB | 2.2K | ✅ |

---

## Оружие и броня (3 файла)

| Файл | Описание | Размер | Токены ≈ | |
|---|---|---|---|---|
| [weapon-armor-system.md](./weapon-armor-system.md) | Теория: оружие и броня | 80 KB | 26.8K | 🔥 |
| [DAMAGE_FORMULAS_PROPOSAL.md](./DAMAGE_FORMULAS_PROPOSAL.md) | Предложение по формулам урона | 10 KB | 3.4K | ✅ |
| [bonuses.md](./bonuses.md) | Единая система бонусов | 34 KB | 11.4K | 🔥 |

---

## NPC и AI (6 файлов)

| Файл | Описание | Размер | Токены ≈ | |
|---|---|---|---|---|
| [NPC_AI_NEUROTHEORY.md](./NPC_AI_NEUROTHEORY.md) | Нейротеория AI NPC | 105 KB | 35.1K | 🔥 |
| [NPC_AI_THEORY.md](./NPC_AI_THEORY.md) | Теория AI NPC | 101 KB | 33.7K | 🔥 |
| [NPC_COMBAT_INTERACTIONS.md](./NPC_COMBAT_INTERACTIONS.md) | NPC боевое взаимодействие | 51 KB | 16.8K | 🔥 |
| [random_npc.md](./random_npc.md) | Генерация NPC | 28 KB | 9.4K | ⚠️ |
| [npc-session-integration.md](./npc-session-integration.md) | Интеграция NPC с сессиями | 22 KB | 7.4K | ⚠️ |
| [relations-system.md](./relations-system.md) | Система отношений | 21 KB | 7.1K | ⚠️ |

---

## Игровые системы (16 файлов)

| Файл | Описание | Размер | Токены ≈ | |
|---|---|---|---|---|
| [charger.md](./charger.md) | Зарядник Ци | 40 KB | 13.3K | ⚠️ |
| [technique-system-v2.md](./technique-system-v2.md) | Техники v2.1 (Grade + Capacity) | 54 KB | 18.0K | 🔥 |
| [technique-system-archive.md](./technique-system-archive.md) | Архив системы техник | 17 KB | 5.6K | ⚠️ |
| [BUFF_SYSTEM.md](./BUFF_SYSTEM.md) | Система баффов | 32 KB | 10.8K | ⚠️ |
| [MODIFIERS_SYSTEM.md](./MODIFIERS_SYSTEM.md) | Модификаторы | 24 KB | 7.9K | ⚠️ |
| [formation_unified.md](./formation_unified.md) | Унифицированные формации | 60 KB | 20.0K | 🔥 |
| [formation_analysis.md](./formation_analysis.md) | Анализ формаций | 42 KB | 14.0K | ⚠️ |
| [formation_visualization.md](./formation_visualization.md) | Визуализация формаций | 37 KB | 12.5K | ⚠️ |
| [formation_drain_system.md](./formation_drain_system.md) | Система утечки формаций | 15 KB | 5.1K | ⚠️ |
| [stat-development-system.md](./stat-development-system.md) | Развитие характеристик | 7 KB | 2.3K | ✅ |
| [stat-threshold-system.md](./stat-threshold-system.md) | Пороги развития | 20 KB | 6.8K | ⚠️ |
| [development-1000-days-calculation.md](./development-1000-days-calculation.md) | Расчёт на 1000 дней | 19 KB | 6.2K | ⚠️ |
| [elements-system.md](./elements-system.md) | Система стихий | 13 KB | 4.5K | ✅ |
| [materials.md](./materials.md) | Система материалов | 27 KB | 9.1K | ⚠️ |
| [qi_stone.md](./qi_stone.md) | Камни Ци | 30 KB | 10.0K | ⚠️ |
| [generator-specs.md](./generator-specs.md) | Спецификации генераторов | 18 KB | 5.9K | ⚠️ |

---

## Мир и данные (9 файлов)

| Файл | Описание | Размер | Токены ≈ | |
|---|---|---|---|---|
| [sector-architecture.md](./sector-architecture.md) | Архитектура секторов | 21 KB | 6.9K | ⚠️ |
| [ENVIRONMENT_SYSTEM_PLAN.md](./ENVIRONMENT_SYSTEM_PLAN.md) | План системы окружения | 24 KB | 8.1K | ⚠️ |
| [inventory-system.md](./inventory-system.md) | Система инвентаря | 13 KB | 4.2K | ✅ |
| [data-systems.md](./data-systems.md) | Системы данных | 8 KB | 2.8K | ✅ |
| [FUNCTIONS.md](./FUNCTIONS.md) | Справочник функций и API | 25 KB | 8.3K | ⚠️ |
| [generators.md](./generators.md) | Все генераторы системы | 21 KB | 6.9K | ⚠️ |
| [faction-system.md](./faction-system.md) | Система фракций | 24 KB | 8.0K | ⚠️ |
| [TIME_SYSTEM.md](./TIME_SYSTEM.md) | Система времени (TickTimer) | 29 KB | 9.7K | ⚠️ |
| [physics-system.md](./physics-system.md) | Физическая система | 5 KB | 1.7K | ✅ |

---

## Планирование и справка (17 файлов)

| Файл | Описание | Размер | Токены ≈ | |
|---|---|---|---|---|
| [PROJECT_ROADMAP.md](./PROJECT_ROADMAP.md) | Roadmap проекта | 20 KB | 6.7K | ⚠️ |
| [PHASE3-PHASER-PROGRESS.md](./PHASE3-PHASER-PROGRESS.md) | Прогресс Phase 3 Phaser | 15 KB | 5.1K | ⚠️ |
| [TRAINING_GROUND_ROADMAP.md](./TRAINING_GROUND_ROADMAP.md) | План тренировочного полигона | 11 KB | 3.8K | ✅ |
| [TEST_WORLD_TARGETS.md](./TEST_WORLD_TARGETS.md) | Тестовый полигон | 15 KB | 4.9K | ✅ |
| [implementation-plan-body-development.md](./implementation-plan-body-development.md) | План развития тела | 14 KB | 4.7K | ✅ |
| [start_lore.md](./start_lore.md) | Лор мира культивации | 56 KB | 18.8K | 🔥 |
| [INSTALL.md](./INSTALL.md) | Установка и запуск (Phaser) | 20 KB | 6.7K | ⚠️ |
| [PROMPT-EXAMPLES.md](./PROMPT-EXAMPLES.md) | Примеры промптов | 14 KB | 4.8K | ✅ |
| [CHEATS.md](./CHEATS.md) | Чит-команды | 2 KB | 0.8K | ✅ |
| [ui-terminology.md](./ui-terminology.md) | Терминология UI | 10 KB | 3.2K | ✅ |
| [PLAYER_SPRITES.md](./PLAYER_SPRITES.md) | Спрайты игрока | 6 KB | 1.9K | ✅ |
| [combat-system.md](./combat-system.md) | Боевая система (Phaser) | 7 KB | 2.3K | ✅ |
| [event-bus-system.md](./event-bus-system.md) | Шина событий | 6 KB | 1.9K | ✅ |
| [phaser-game-analysis.md](./phaser-game-analysis.md) | Анализ Phaser игры | 8 KB | 2.7K | ✅ |
| [optimization-techniques-snippet.txt](./optimization-techniques-snippet.txt) | Оптимизация (сниппет) | 4 KB | 1.4K | ✅ |
| [Listing.md](./Listing.md) | Этот файл | 8 KB | 2.6K | ✅ |
| [README.md](./README.md) | Описание папки docs_old | 1 KB | 0.3K | ✅ |

---

## 💰 Топ-5 самых дорогих файлов docs_old/

| Файл | Токены ≈ | |
|---|---|---|
| body_monsters.md | 44.2K | 🔥 |
| body_armor.md | 41.4K | 🔥 |
| NPC_AI_NEUROTHEORY.md | 35.1K | 🔥 |
| NPC_AI_THEORY.md | 33.7K | 🔥 |
| weapon-armor-system.md | 26.8K | 🔥 |

> ⚡ docs_old/ = **~668K токенов**. Читать только при миграции лора/механик из Phaser-эры.

---

## 🔗 Соседние папки

| Папка | Листинг | Описание |
|---|---|---|
| `docs/` | [!LISTING.md](../docs/!LISTING.md) | Основная документация (42 файла) |
| `docs_asset_setup/` | [!LISTING.md](../docs_asset_setup/!LISTING.md) | Инструкции для Unity Editor (34 файла) |
| `docs_temp/` | [!listing.md](../docs_temp/!listing.md) | Черновики, аудиты (57 файлов) |

---

*Создано: 2026-04-30 08:09:40 UTC (v2.0 — полная переработка листинга)*
