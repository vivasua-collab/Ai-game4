# Чекпоинт: Переход на COLD START (минимальная структура окружения)

**Дата:** 2026-08-19 07:35 UTC
**Сессия:** web-d86b1055 (продолжение)
**Тип:** decision + implementation

---

## Контекст

После применения Variant D (5 симлинков: `aigame4`, `checkpoints`, `game`, `game-docs`, `godot`) пользователь предложил более радикальный подход: "получения Ai-game4 на прямую в локальное окружение, без линковки в других местах, сразу же получится у меня структура как в gitHub". Цель — минимизировать симлинки, получить структуру 1-в-1 как GitHub.

Также пользователь спросил: можно ли дать доступ в другое окружение на платформе chat.z.ai.

## Что сделано

### 1. Обнаружено платформенное ограничение
`/home/z/my-project/` — **сам является git-репозиторием** Z.ai Code:
```
remote origin = github.com/vivasua-collab/Ai-game4.git
commits: bbde5dc "2dca0a15-5aba-450d-831f-e217249cb9b0" (UUID сессий)
```
Z.ai Code использует этот git-репо для персистентности sandbox между сессиями. **Следствие:** Ai-game4 не может стать корнем workspace — два git-репо не могут занимать одну папку. Ai-game4 обязан быть подпапкой.

### 2. Убраны избыточные симлинки
Удалены: `checkpoints`, `game`, `game-docs` (все доступны через `aigame4/`)
Остались: `aigame4` (весь репо) + `godot` (toolchain) = **всего 2 симлинка**

### 3. Создан `cold_start.sh`
Чистый idempotent скрипт (замена `recover_sandbox.sh`):
- Шаг 1: .NET SDK 8+9 в `/home/z/.dotnet/`
- Шаг 2: Godot 4.7.1 в `/home/z/godot/`
- Шаг 3: `git clone` Ai-game4 (или `git pull` если есть)
- Шаг 4: 2 симлинка (`aigame4` + `godot`), удаляет устаревшие
- Шаг 5: NuGet.config (gitignored)
- Шаг 6: Верификация (`dotnet build` + headless + проверка симлинков)

### 4. `recover_sandbox.sh` → deprecated wrapper
Теперь просто перенаправляет на `cold_start.sh`.

### 5. Создан дизайн-документ
`docs/docs_v2/09_workflow/COLD_START.md` — включает:
- Платформенное ограничение
- Финальная структура (2 симлинка)
- Процедура холодного старта
- Сравнение Variant D vs Cold Start
- Ответ на вопрос про "другое окружение"

## Решения

- **Минимум симлинков (2 вместо 5)** — убраны `checkpoints`, `game`, `game-docs`. Доступ через `aigame4/` даёт структуру 1-в-1 как GitHub. Убирает загромождение корня sandbox.
- **`cold_start.sh` вместо `recover_sandbox.sh`** — новое имя отражает суть (холодный старт, не "восстановление"). Idempotent, безопасно запускать многократно.
- **`recover_sandbox.sh` сохранён как wrapper** — обратная совместимость, чтобы не сломать существующие инструкции.
- **Не слиять Ai-game4 в корень sandbox** — невозможно из-за git конфликта (платформенное ограничение).
- **Ответ на "другое окружение"** — НЕТ, платформа chat.z.ai даёт один sandbox на сессию. Нет доступа к другим окружениям или изолированным ИИ моделям. Сессии изолированы.

## Найденные проблемы

- **`/home/z/my-project/.git` указывает на Ai-game4 remote** — это особенность Z.ai Code (использует Ai-game4 remote для своего sandbox git). Не баг, но может путать. Задокументировано.
- **Нет способа сделать "Ai-game4 = workspace"** — платформенное ограничение. Лучшее приближение — `aigame4` симлинк.

## Следующие шаги

- [ ] Закоммитить и запушить
- [ ] При следующем сбое — запустить `bash aigame4/cold_start.sh`
- [ ] Пользователь видит: `aigame4/` (весь репо) + `godot/` (движок) — больше ничего
- [ ] В будущих сессиях AI-агенты используют `aigame4/` как корень проекта

## Файлы

**Созданные:**
- `/home/z/my-project/Ai-game4/cold_start.sh` — скрипт холодного старта (idempotent)
- `/home/z/my-project/Ai-game4/docs/docs_v2/09_workflow/COLD_START.md` — дизайн-документ
- `/home/z/my-project/Ai-game4/checkpoints/08_19_cold_start.md` — этот чекпоинт

**Изменённые:**
- `/home/z/my-project/Ai-game4/START_PROMPT.md` — §3 обновлён (2 симлинка)
- `/home/z/my-project/Ai-game4/recover_sandbox.sh` — deprecated wrapper на cold_start.sh
- `/home/z/my-project/Ai-game4/worklog.md` — добавлена запись 07:35

**Симлинки (в sandbox):**
- `/home/z/my-project/aigame4` → `Ai-game4/` (сохранён)
- `/home/z/my-project/godot` → `/home/z/godot` (сохранён)
- ~~`/home/z/my-project/checkpoints`~~ — удалён
- ~~`/home/z/my-project/game`~~ — удалён
- ~~`/home/z/my-project/game-docs`~~ — удалён
