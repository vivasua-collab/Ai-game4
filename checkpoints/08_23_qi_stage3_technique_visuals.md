# Чекпоинт: Этап 3 — Схематические визуальные эффекты техник

**Дата:** 2026-08-23
**План:** `checkpoints/08_23_qi_impl_plan.md` (этап 3)
**Статус:** ✅ Завершён, сборка чистая, headless-проверка пройдена.

## Что сделано

### TechniqueEffectRenderer (`Adapter/Scene/TechniqueEffectRenderer.cs`)
Node2D в мировом пространстве (child of world root), custom `_Draw` — без PNG
(принцип проекта на этом этапе; спрайтовый каталог effect_* — позже).
ZIndex = Player+5 (выше персонажей, ниже HUD).

**Виды визуалов** (по `TechniqueCastResultEvent.VisualKind`):
| Kind | Типы техник | Отрисовка |
|------|-------------|-----------|
| Directional | Combat (melee/ranged) | Снаряд: круг 11px летит origin→target за 0.45с + шлейф из 3 затухающих кругов |
| Expanding | RangedAoe | Растущая окружность 12→102px с заливкой 22% и затуханием |
| Self | Support/Sensory/Movement | Пульсирующее двойное кольцо вокруг игрока (46px, sin 10Гц) |
| Heal | Healing | Зелёное расширяющееся кольцо + 4 восходящие искры |
| Shield | Defense | Двойная голубая дуга 52px, живёт пока активен Ци-буфер |
| Meditation | (V) | Золотое мягкое кольцо + 5 восходящих частиц Ци |

**Цвета по стихиям** — ELEMENTS_SYSTEM.md §2: fire (1,.35,.12), water (.2,.5,1),
earth (.6,.4,.2), air (.75,.78,.75), lightning (.95,.88,.25), void (.42,.1,.55),
light (1,.9,.45), poison (.5,.15,.7), neutral (.95,.95,.95).

**Механика:**
- Подписки: TechniqueCastResultEvent (успешные касты), MeditationStateChangedEvent,
  QiBufferStateChangedEvent (щит по активации буфера игрока).
- Пул: Stack<ActiveVisual> — переиспользование без аллокаций (zero-GC принцип).
- _Process: обновление elapsed, возврат в пул; QueueRedraw только при активных визуалах.
- WithAlpha-хелпер (Godot Color не имеет конструктора (Color, float)).

### Интеграция
GameWorldController.SetupWorld: рендерер добавлен в _worldRoot после SceneBuilder.

## Проверка
```
[TechniqueEffectRenderer] Ready
```
Сборка 0 ошибок; регрессий нет.
