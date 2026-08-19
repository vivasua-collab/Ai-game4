# Worklog — Cultivation World Simulator (Ai-game4)

> Хроника работы (append-only). Новые записи добавляются в конец.
> Формат записи см. в START_PROMPT.md §6.

---

## 2026-08-19

### 07:12 — Реструктуризация линкования окружения

**Task ID:** 08_19-linking
**Agent:** main (Z.ai Code)

**Проблема:**
Папка `checkpoints/` не линковалась в окружение sandbox. Пользователь не видел чекпоинты с 16 августа. Корневая причина: `recover_sandbox.sh` создавал только 3 симлинка (`game`, `game-docs`, `godot`), а вся папка `Ai-game4/` целиком не была видна.

**Анализ:**
- `Ai-game3-ref/` работает без проблем, потому что папка видна напрямую (нет симлинков внутрь).
- `Ai-game4/` не видна в workspace — только симлинки внутрь.
- `checkpoints/`, `worklog.md`, `START_PROMPT.md`, `SESSION_SUMMARY.md`, `recover_sandbox.sh` — всё невидимо.

**Решение (Вариант D — гибридный):**
- `aigame4` → `Ai-game4/` (единая точка входа, как Ai-game3-ref)
- `checkpoints` → `Ai-game4/checkpoints` (прямой доступ)
- `game`, `game-docs`, `godot` — сохранены для обратной совместимости

**Документация:**
- `docs/docs_v2/09_workflow/ENVIRONMENT_LINKING.md` — дизайн-документ с 4 вариантами
- `recover_sandbox.sh` шаг 4 обновлён (5 симлинков вместо 3)
- `START_PROMPT.md` §3 обновлён

**Stage Summary:**
- 5 симлинков созданы и проверены
- 18 чекпоинтов доступны через `checkpoints/` и `aigame4/checkpoints/`
- Структура задокументирована, восстанавливается через `recover_sandbox.sh`

---

### 07:35 — Переход на COLD START (минимальная структура)

**Task ID:** 08_19-cold-start
**Agent:** main (Z.ai Code)

**Причина:**
Пользователь хотел чистый холодный старт: "получения Ai-game4 на прямую в локальное окружение, без линковки в других местах, сразу же получится у меня структура как в gitHub". Variant D (5 симлинков) всё ещё выглядел как band-aid. Нужна структурная минимизация.

**Платформенное ограничение (обнаружено):**
`/home/z/my-project/` — сам git-репозиторий Z.ai Code (remote = Ai-game4, но коммиты = UUID сессий). Используется для персистентности sandbox. Следствие: Ai-game4 не может стать корнем workspace (git конфликт). Должен быть подпапкой.

**Решение:**
- Убраны избыточные симлинки: `checkpoints`, `game`, `game-docs`
- Осталось только 2: `aigame4` (весь репо) + `godot` (toolchain)
- Создан `cold_start.sh` — чистый idempotent скрипт (замена recover_sandbox.sh)
- `recover_sandbox.sh` → deprecated wrapper на `cold_start.sh`

**Файлы:**
- `cold_start.sh` — новый скрипт (6 шагов: .NET, Godot, git clone, 2 симлинка, NuGet, верификация)
- `docs/docs_v2/09_workflow/COLD_START.md` — дизайн-документ (сравнение с Variant D, ответ на вопрос про "другое окружение")
- `START_PROMPT.md` §3 — обновлён (2 симлинка вместо 5)
- `recover_sandbox.sh` — deprecated alias

**Stage Summary:**
- 2 симлинка: `aigame4` + `godot` (вместо 5)
- Доступ ко всему через `aigame4/` (структура 1-в-1 как GitHub)
- 19 чекпоинтов доступны через `aigame4/checkpoints/`
- cold_start.sh idempotent, безопасно запускать многократно

