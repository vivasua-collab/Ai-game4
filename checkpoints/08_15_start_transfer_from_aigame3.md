# Чекпоинт: Начало переноса кода из Ai-game3

**Дата:** 2026-08-15 06:47 UTC
**Тип:** migration | decision

---

## Контекст

Окружение пересоздалось (сбой 15 августа). После восстановления (clone Ai-game4 с GitHub) принято решение перенести код из Ai-game3 (Unity итерация, 378 .cs файлов, 2.7 MB) вместо написания с нуля.

Анализ Ai-game3 показал:
- Архитектура идентична (Hub-and-Spoke, те же 16 модулей, тот же namespace `CultivationGame`)
- Core слой (108 файлов) — чистый C#, переносится без изменений
- Modules (192 файла) — реальная логика, нужна адаптация MessagePipe→EventBus, VContainer→наш DI
- Entry (35 файлов) — Unity-specific, НЕ переносится
- Tests (67 файлов) — чистый C#, переносится с минимальной адаптацией

## Решения

- **Переносить код из Ai-game3** (не писать с нуля) — экономия 4-6 часов AI-агентов
- **Core переносится напрямую** (чистый C#, нет Unity зависимостей)
- **Modules: Calculators + Configs напрямую, Services с адаптацией** (MessagePipe→EventBus)
- **Entry НЕ переносится** (Unity MonoBehaviour, переписан под Godot в Adapter)
- **Создать START_PROMPT.md + SESSION_SUMMARY.md + checkpoint rules** — для устойчивости к сбоям окружения

## План переноса

| Этап | Что | Параллельность | Время |
|------|-----|----------------|-------|
| 1 | Core/Data (Constants, Enums, Structs, DataModels) | Агент A | 15 мин |
| 2 | Core/Interfaces (37 файлов) | Агент B | 10 мин |
| 3 | Core/Messaging/Contracts (22 файла) | Агент C | 10 мин |
| 4 | Modules/*Calculator.cs, *Config.cs (чистая логика) | Агент D | 20 мин |
| 5 | Modules/*Service.cs (адаптация MessagePipe→EventBus) | Агент E | 40 мин |
| 6 | Tests (67 файлов) | Агент F | 15 мин |
| 7 | Верификация (build + headless + screenshots) | Orchestrator | 15 мин |

Этапы 1-3 параллельны (независимы). Этап 4 после 1-3. Этап 5 после 4. Этап 6 после 5.

## Файлы созданы

- `START_PROMPT.md` — правила работы, точка входа для AI-агентов
- `SESSION_SUMMARY.md` — компактный контекст последних сессий
- `checkpoints/08_15_start_transfer_from_aigame3.md` — этот чекпоинт

## Следующие шаги

1. Запустить параллельных агентов для переноса Core (Data + Interfaces + Messaging)
2. После завершения — перенос Modules
3. Верификация сборки
4. Git commit + push
