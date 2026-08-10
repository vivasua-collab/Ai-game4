# 🔬 Unity 6.3 LTS: Исследование API

**Версия:** 1.0  
**Дата:** 2026-03-30  
**Источники:** docs.unity3d.com (получено через curl)

---

## ⚠️ Важное уточнение

> **Unity 6.3 использует Entities 1.3 (DOTS 1.x), а НЕ DOTS 2.0!**  
> Предыдущая информация в документах была неточной.

---

## 1️⃣ Entities (DOTS) — Версия 1.3.15

### Поддерживаемые версии Unity
- Unity 2022.3 LTS
- Unity 2023.3+ (включая Unity 6.3)

### Что такое Entities
Entities — это пакет, входящий в Data-Oriented Technology Stack (DOTS). Предоставляет data-oriented реализацию Entity Component System (ECS) архитектуры.

### Ресурсы для изучения
- [DOTS Guide and Samples](https://github.com/Unity-Technologies/EntityComponentSystemSamples)
- Официальная документация: com.unity.entities@1.3

### Установка
```
Window > Package Manager > Add package by name: com.unity.entities
```

### Ключевые концепции

| Концепция | Описание |
|-----------|----------|
| Entity | Уникальный ID, представляет игровой объект |
| Component | Только данные, без логики |
| System | Только логика, обрабатывает компоненты |
| Archetype | Уникальная комбинация компонентов |
| Chunk | Блок памяти для сущностей одного архетипа |

---

## 2️⃣ Новые фичи Unity 6.3

### 2D система

#### Low-level 2D physics APIs
- Интеграция Box2D v3
- Multi-threaded performance improvements
- Enhanced determinism
- Visual debugging support
- Namespace: `UnityEngine.LowLevelPhysics2D`

#### Render 3D as 2D
- 2D Renderer поддерживает Mesh Renderer и Skinned Mesh Renderer
- Совместимость с 2D Lights
- Sprite Mask interaction
- Sort 3D As 2D в Sorting Group

### Accessibility

#### Native screen reader support
- Windows (Narrator)
- macOS (VoiceOver)
- Android (TalkBack)
- iOS (VoiceOver)

### Audio

#### Scriptable audio pipeline
- Burst-compiled C# units
- Scriptable processors
- Кастомная обработка аудио

### Graphics

#### Render Graph
- Shared foundation URP/HDRP
- Render Graph Viewer on devices
- Smarter Helper Passes

#### Ray tracing API
- Улучшения в ray tracing

#### DLSS4
- Super Resolution support

#### Metal support
- NativeGraphicsJobsSplitThreading

### Editor

#### New Hierarchy window
- Улучшенная иерархия объектов

#### Build Profiles
- Новый UI для настроек сборки

#### Dynamic overlays
- Динамические оверлеи в UI

### Optimization

#### Profiler improvements
- Улучшения профайлера

#### Frame Debugger
- Variable Rate Shading (VRS) support

### Physics

- Disable and remove physics back end

---

## 3️⃣ ScriptableObject

### Основное назначение
- **Data store** — хранение данных
- **Shared data** — общие данные для множества объектов
- **Memory efficiency** — избежание копирования значений

### Ключевое ограничение
```
⚠️ В runtime (standalone player) ScriptableObject — READ ONLY!
   Изменения не сохраняются на диск.
```

### Use cases

| Use case | Описание |
|----------|----------|
| Configuration data | Настройки игры |
| Item definitions | Определения предметов |
| Technique presets | Пресеты техник |
| NPC templates | Шаблоны NPC |
| Material data | Данные материалов |

### Пример структуры (теория)

```
ScriptableObject для техники:
├── id: string
├── name: string
├── techniqueType: enum
├── element: enum
├── level: int
├── qiCost: int
├── baseCapacity: int
├── effects: List<EffectData>
└── requirements: RequirementsData
```

---

## 4️⃣ Сохранение данных в Unity

### Проблема
ScriptableObject не сохраняет изменения в runtime.

### Решения для Cultivation World Simulator

| Метод | Use case | Формат |
|-------|----------|--------|
| JSON | Сохранения, конфиги | .json |
| PlayerPrefs | Простые настройки | registry/file |
| BinaryFormatter | Быстрое сохранение | .dat |
| SQLite | Локальная БД (опционально) | .db |

### Рекомендуемый подход

```
Статические данные (пресеты):
├── ScriptableObjects (read-only в runtime)
├── Техники, материалы, предметы
└── Редактируются только в Editor

Динамические данные (сохранения):
├── JSON файлы
├── Персонаж, инвентарь, состояние мира
└── Загружаются/сохраняются в runtime
```

---

## 5️⃣ Сравнение DOTS 1.x vs MonoBehaviour

| Критерий | MonoBehaviour | DOTS (Entities 1.3) |
|----------|---------------|---------------------|
| Сложность | Низкая | Высокая |
| Производительность | Хорошая | Отличная (10-100x) |
| Множество объектов | 1000s | 100,000s+ |
| Отладка | Простая | Сложнее |
| Интеграция с UI | Простая | Требует моста |

### Рекомендация для проекта

```
Фаза 1 (Прототип):
├── MonoBehaviour
├── Быстрая разработка
└── Простая отладка

Фаза 2 (Оптимизация):
├── Миграция NPC AI на DOTS
├── Системы с большим количеством объектов
└── Профайлинг и оптимизация
```

---

## 6️⃣ Полезные ссылки

| Ресурс | URL |
|--------|-----|
| Unity 6.3 What's New | https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html |
| Entities 1.3 Docs | https://docs.unity3d.com/Packages/com.unity.entities@1.3 |
| DOTS Samples | https://github.com/Unity-Technologies/EntityComponentSystemSamples |
| ScriptableObject | https://docs.unity3d.com/6000.3/Documentation/Manual/class-ScriptableObject.html |

---

## 📝 Следующие шаги

1. ✅ Изучить DOTS tutorials из репозитория samples
2. ✅ Определить какие системы перенести на DOTS
3. ✅ Спроектировать структуру ScriptableObjects для пресетов
4. ✅ Реализовать JSON serialization для сохранений

---

*Документ создан: 2026-03-30*  
*Данные получены из официальной документации Unity*
