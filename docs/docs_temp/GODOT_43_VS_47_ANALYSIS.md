# Анализ: Godot 4.3 vs Godot 4.7/4.7.1 — обновляться или нет

> **Статус:** Исследование. Решение принимается пользователем.
> **Дата:** Текущая итерация.
> **Связанные документы:** `docs_v2/00_overview/TECHNOLOGY_DECISIONS.md`

---

## 1. Контекст

Текущий проект (`Ai-game4`) разработан на **Godot 4.3 stable (.NET build)**. Окружение настроено, сборка работает, все 15 модулей стартуют. Вопрос: есть ли смысл обновиться до **Godot 4.7.1** (последний stable release на момент анализа)?

---

## 2. Что нового в Godot 4.7 (между 4.3 и 4.7)

### 2.1. Godot 4.7 "Lights, Camera, Action!" — ключевые фичи

| Фича | Релевантность нашему проекту | Оценка |
|------|------------------------------|--------|
| **AreaLight3D** — прямоугольные источники света в 3D | ❌ Не релевантно (у нас 2D) | 0/10 |
| **HDR output** — расширенный динамический диапазон | ❌ Не релевантно (тема «Древний Пергамент», приглушённые цвета) | 1/10 |
| **Control offset transforms** — translate/rotate/scale Control узлов без влияния на layout | ✅ **ПОЛЕЗНО** для тяжёлого UI (22 Views, Inventory, Drag&Drop, tooltips) | **8/10** |
| **DrawableTexture2D** — программная отрисовка текстур | ✅ Полезно для procedural sprites (персонаж, эффекты) | **7/10** |
| **Inline shader previews** — предпросмотр шейдеров в редакторе | ❌ Не релевантно (мы не пишем кастомные шейдеры) | 1/10 |
| **New Asset Store** — улучшенный магазин ассетов | ⚠️ Nice to have, но мы используем procedural generation | 3/10 |
| **Standalone Android export** — экспорт на Android без редактора | ❌ Не релевантно (десктопный single-player) | 1/10 |
| **XR improvements** | ❌ Не релевантно | 0/10 |
| **Threading in Asset Store** | ❌ Не релевантно | 0/10 |

### 2.2. Накопленные breaking changes (4.3 → 4.4 → 4.5 → 4.6 → 4.7)

Migration guide Godot покрывает **4 основных версии** breaking changes:

| Переход | Затронутые области (для нашего проекта) |
|---------|------------------------------------------|
| 4.3 → 4.4 | Core, GUI nodes, Physics, Rendering, Navigation, Editor plugins + Behavior: Core, Rendering, CSG, Android |
| 4.4 → 4.5 | Core, Rendering, GLTF, Text, XR + Behavior: TileMapLayer, 3D Import, Navigation, Physics, Text |
| 4.5 → 4.6 | Core, Animation, 3D, Rendering, GUI, Networking, OpenXR + Behavior: Android, Core, Rendering, Navigation, defaults |
| 4.6 → 4.7 | Core, 2D, 3D, GUI, Text, Rendering, Animation, Physics, Audio, XR, Editor + Behavior: Animation, Rendering, Physics, **Input**, GDScript, Platforms, defaults |

**Ключевые области риска для нашего проекта:**
- **GUI nodes** — затронуто в каждом переходе (наш UI-heavy проект)
- **Rendering** — затронуто в каждом переходе (2D rendering)
- **TileMapLayer** — изменение в 4.5 (мы используем TileMapLayer)
- **Input** — behavior change в 4.7 (наш InputAdapter)
- **Physics** — затронуто в 4.4, 4.6, 4.7

### 2.3. Конкретные breaking changes, которые могут затронуть наш код

На основе анализа migration guides:

**TileMap (4.4 → 4.5):** `TileMap` node deprecated → `TileMapLayer` (мы уже используем TileMapLayer в SceneBuilder, но нужно проверить API).

**Control offset transforms (4.7):** Новая функциональность, но может изменить поведение layout — наш UIFactory и ParchmentTheme могут потребовать адаптации.

**Input handling (4.7):** Behavior changes в Input — наш InputAdapter и InputMapInitializer могут потребовать обновления.

**C#/.NET support:** Godot 4.3+ использует .NET 8. В 4.7 .NET support стабилен, но возможны изменения в Godot.NET.Sdk versioning (с 4.3.0 на 4.7.0).

---

## 3. Сравнение: остаться на 4.3 vs обновиться до 4.7.1

### 3.1. Аргументы ЗА обновление до 4.7.1

| # | Аргумент | Вес |
|---|----------|-----|
| 1 | **Control offset transforms** — критично для тяжёлого UI (22 Views, Inventory с drag&drop, tooltips). Позволяет анимировать UI элементы без поломки layout. | 🔴 Высокий |
| 2 | **DrawableTexture2D** — упростит procedural generation спрайтов (персонаж, эффекты, тайлы). | 🟠 Средний |
| 3 | **LTS поддержка** — 4.7 будет поддерживаться дольше (bug fixes, security). 4.3 уже не получает новых фич. | 🟠 Средний |
| 4 | **Bug fixes** — 4 версии багфиксов в rendering, physics, GUI. | 🟠 Средний |
| 5 | **Лучше сейчас, чем потом** — проект только начинается (~120 .cs файлов stubs). Миграция сейчас дешевле, чем когда будет 1000+ файлов с реальной логикой. | 🔴 Высокий |
| 6 | **Производительность** — 4.7 включает оптимизации rendering и physics. | 🟡 Низкий (наш tick-based sim не упирается в rendering) |
| 7 | **Community/ecosystem** — 4.7 актуальная версия, больше туториалов, ответов на StackOverflow. | 🟡 Низкий |

### 3.2. Аргументы ПРОТИВ обновления

| # | Аргумент | Вес |
|---|----------|-----|
| 1 | **Рабочее окружение уже настроено** на 4.3 — Godot binary скачан, .NET SDK работает, проект компилируется и запускается. | 🟠 Средний |
| 2 | **Breaking changes** — 4 версии breaking changes могут затронуть TileMapLayer, Control, Input, Physics. Потребуется отладка. | 🟠 Средний |
| 3 | **Risk of new bugs** — 4.7.1 только вышел (Jul 2026), могут быть regressions. | 🟡 Низкий (4.7.1 — maintenance release, стабилен) |
| 4 | **Время на миграцию** — нужно скачать новый Godot, проверить сборку, исправить breaking changes. | 🟡 Низкий (1-2 часа) |

### 3.3. Оценка рисков миграции

**Что точно сломается:**
- `Godot.NET.Sdk` version в .csproj: `4.3.0` → `4.7.0`
- Возможно, API изменения в `TileMapLayer` (4.5 behavior change)
- Возможно, Input handling nuances (4.7 behavior change)

**Что НЕ сломается:**
- Весь Core слой (pure C#, нет Godot зависимостей)
- Все 16 Modules (pure C#, нет Godot зависимостей)
- Весь Entry слой (pure C#)
- EventBus, DI Container, контракты — всё engine-agnostic

**Что нужно проверить:**
- Adapter слой (10 файлов): GameBoot, InputAdapter, SceneBuilder, GameWorldController, MainMenuController, UIFactory, ParchmentTheme, SaveFileHandler, ContainerAdapter, InputMapInitializer

---

## 4. Рекомендация

### 4.1. Основная рекомендация: ✅ ОБНОВИТЬСЯ до Godot 4.7.1

**Обоснование:**

1. **Control offset transforms** — killer feature для нашего UI-heavy проекта. Тяжёлая UI-инфраструктура (22 Views, Inventory с 15 слотами, Drag&Drop, tooltips, body silhouette) получит мощный инструмент для анимаций без поломки layout. Это **та самая область, где застопорилась предыдущая итерация** (UI V3 Phase 0–4 не тестированы).

2. **Миграция сейчас дешёвая:**
   - 120 .cs файлов, из них только 10 в Adapter слое зависят от Godot API
   - Core/Modules/Entry — 110 файлов pure C#, не затронуты
   - Stub реализации минимальны — ломаться нечему
   - Реальной игровой логики пока нет (только stubs)

3. **LTS и bug fixes** — 4.7.1 стабильный, будет поддерживаться годами.

4. **DrawableTexture2D** — упростит procedural sprite generation (персонаж, тайлы, эффекты).

### 4.2. План миграции (если решение — обновиться)

```
Шаг 1: Скачать Godot 4.7.1 .NET build (Linux x86_64)
  → https://godotengine.org/download/archive/4.7.1-stable
  → заменить /home/z/godot/Godot_v4.3-... на 4.7.1

Шаг 2: Обновить .csproj
  → <Project Sdk="Godot.NET.Sdk/4.7.0">  (было 4.3.0)
  → dotnet restore + build

Шаг 3: Проверить breaking changes
  → TileMapLayer API (4.5 change)
  → Control offset transforms (4.7 — могут потребовать адаптации UIFactory)
  → Input handling (4.7 behavior change)
  → theme.SetStylebox (проверить, не изменился ли API)

Шаг 4: Headless тест
  → dotnet build
  → godot --headless --path . --quit
  → проверить, что все 15 модулей стартуют

Шаг 5: Визуальный тест в Godot Editor
  → открыть project.godot
  → запустить MainMenu
  → проверить GameWorld
```

**Оценка времени:** 1–2 часа (скачивание + проверка + фиксы breaking changes).

### 4.3. Альтернатива: остаться на 4.3

Если решение — остаться на 4.3, аргументы:
- Окружение уже работает
- Нет риска breaking changes
- Можно обновиться позже, когда будет больше мотивации (например, когда UI разработка упрётся в ограничения 4.3)

**Риск:** чем дольше откладываем, тем больше кода напишем под 4.3 API, тем дороже миграция.

### 4.4. Что НЕ рекомендуется

- **Обновляться до 4.7.0** (без .1) — 4.7.1 включает bug fixes, всегда берите последний patch release.
- **Обновляться до 4.5 или 4.6** — нет смысла, это промежуточные версии. Если обновляться — то сразу до 4.7.1.

---

## 5. Итоговая таблица

| Критерий | Godot 4.3 (текущая) | Godot 4.7.1 (рекомендация) |
|----------|---------------------|----------------------------|
| Стабильность | ✅ Проверена в нашем проекте | ✅ Stable + maintenance |
| LTS поддержка | ⚠️ Только critical bugs | ✅ Активная поддержка |
| Control offset transforms | ❌ Нет | ✅ Есть — критично для UI |
| DrawableTexture2D | ❌ Нет | ✅ Есть — для procedural sprites |
| HDR output | ❌ Нет | ✅ Есть (не нужно нам) |
| AreaLight3D | ❌ Нет | ✅ Есть (не нужно нам, 2D) |
| Asset Store | Старый | ✅ Новый |
| C# .NET 8 support | ✅ Works | ✅ Works (возможно стабильнее) |
| TileMapLayer | ✅ Используем | ⚠️ Проверить API changes |
| Input handling | ✅ Работает | ⚠️ Behavior changes — проверить |
| Migration effort | 0 (уже настроено) | 1–2 часа |
| Risk | Низкий | Средний (breaking changes) |

---

## 6. Решение

**Рекомендация: обновиться до Godot 4.7.1.**

Ключевые факторы:
1. **Control offset transforms** — решает проблему, на которой застопорилась предыдущая итерация (UI)
2. **Миграция сейчас дешёвая** — 90% кода engine-agnostic, ломаться нечему
3. **LTS поддержка** — 4.7.1 будет поддерживаться дольше

**Если пользователь согласен — следующий шаг:**
1. Скачать Godot 4.7.1 .NET build
2. Обновить .csproj (4.3.0 → 4.7.0)
3. Прогнать headless тест
4. Исправить breaking changes (если есть)
5. Закоммитить обновлённый проект

---

## Приложение A. Источники

- Godot 4.7 release notes: https://godotengine.org/releases/4.7
- Godot 4.7.1 maintenance: https://godotengine.org/article/maintenance-release-godot-4-7-1
- Migration guide 4.6 → 4.7: https://docs.godotengine.org/en/4.7/tutorials/migrating/upgrading_to_godot_4.7.html
- Download 4.7.1: https://godotengine.org/download/archive/4.7.1-stable
- What's new in Godot 4.7 (godotlearning): https://godotlearning.com/blog/godot-4-7-whats-new
