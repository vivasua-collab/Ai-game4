# Чекпоинт: Реструктуризация линкования окружения (Вариант D)

**Дата:** 2026-08-19 07:12 UTC
**Сессия:** web-d86b1055 (продолжение после context limit)
**Тип:** decision + implementation

---

## Контекст

После восстановления песочницы пользователь обнаружил, что папка `checkpoints/` не видна в workspace. Соответственно, все чекпоинты с 16 августа (включая `08_16_audit_fixes.md`, `08_19_full_audit.md`) были недоступны.

Пользователь запросил:
1. Анализ причин повторяющихся сбоев окружения
2. Разработку примеров линкования
3. Принцип: base всегда доступен, modules восстанавливаемы
4. Рассмотреть воссоздание структуры ai-game3 (там нет проблем)

## Что сделано

### 1. Анализ текущей структуры
- `Ai-game4/` находится в `/home/z/my-project/Ai-game4/` (persistent volume sandbox)
- `recover_sandbox.sh` создавал только 3 симлинка: `game`, `game-docs`, `godot`
- Workspace пользователя показывает симлинки, но НЕ показывает папку `Ai-game4/` напрямую
- Результат: `checkpoints/`, `worklog.md`, `START_PROMPT.md`, `SESSION_SUMMARY.md`, `recover_sandbox.sh` — невидимы

### 2. Сравнение с Ai-game3-ref
- `Ai-game3-ref/` работает без проблем, потому что папка видна напрямую (нет симлинков внутрь)
- Пользователь открывает `Ai-game3-ref/` и видит все вложенные папки: `checkpoints/`, `docs/`, `UnityProject/`, `worklog.md`
- В Ai-game4 папка не видна — только симлинки внутрь

### 3. Разработаны 4 варианта (документированы)
- **A:** Один симлинк `aigame4` → `Ai-game4/` (максимально просто, ломает compat)
- **B:** Добавить симлинки на каждый файл (загромождает корень)
- **C:** Перенос base в корень sandbox (ломает git-структуру)
- **D:** Гибридный ✅ — `aigame4` + `checkpoints` + backward compat

### 4. Выбран и реализован Вариант D
Создано 5 симлинков:
```bash
ln -sf /home/z/my-project/Ai-game4            /home/z/my-project/aigame4      # НОВЫЙ
ln -sf /home/z/my-project/Ai-game4/checkpoints /home/z/my-project/checkpoints # НОВЫЙ
ln -sf /home/z/my-project/Ai-game4/game       /home/z/my-project/game         # существующий
ln -sf /home/z/my-project/Ai-game4/docs       /home/z/my-project/game-docs    # существующий
ln -sf /home/z/godot                          /home/z/my-project/godot        # существующий
```

### 5. Верификация
- 18 чекпоинтов доступны через `checkpoints/` ✅
- 18 чекпоинтов доступны через `aigame4/checkpoints/` ✅
- `aigame4/START_PROMPT.md` виден ✅
- `aigame4/SESSION_SUMMARY.md` виден ✅
- `aigame4/recover_sandbox.sh` виден ✅
- `aigame4/worklog.md` создан (ранее отсутствовал) ✅

## Решения

- **Вариант D (гибридный) выбран** — обеспечивает единая точка входа (`aigame4/`) + прямой доступ к `checkpoints/` + обратная совместимость со старыми симлинками.
- **`aigame4` как единая точка входа** — эквивалент `Ai-game3-ref/`, решает проблему "не вижу папку целиком".
- **`checkpoints` как отдельный симлинк** — потому что это критичный путь, используемый чаще других; прямой доступ удобнее, чем `aigame4/checkpoints/`.
- **Backward compat сохранён** (`game`, `game-docs`) — чтобы не сломать существующие скрипты и привычки.
- **Toolchain отделён** (`godot` → `/home/z/godot`) — движок восстанавливается отдельно через download, не из git.
- **`worklog.md` создан в Ai-game4** — ранее отсутствовал, хотя START_PROMPT.md §3 ссылался на него.

## Найденные проблемы

- **`worklog.md` отсутствовал в Ai-game4** — START_PROMPT.md §3 ссылался, но файла не было. Исправлено: создан с начальной записью.
- **В .gitignore нет явного исключения для `worklog.md`** — но файл не был в git. Решение: файл добавлен в коммит.
- **dev.log показывает `EADDRINUSE` port 3000** — Next.js dev server уже запущен (не критично, существующее состояние).

## Следующие шаги

- [ ] Закоммитить и запушить изменения на GitHub
- [ ] При следующем сбое песочницы — запустить `recover_sandbox.sh` и проверить, что все 5 симлинков создаются
- [ ] Пользователь должен увидеть `aigame4/` и `checkpoints/` в workspace (может потребоваться refresh IDE)
- [ ] В будущих сессиях AI-агенты должны использовать `aigame4/START_PROMPT.md` как точку входа

## Файлы

**Созданные:**
- `/home/z/my-project/Ai-game4/docs/docs_v2/09_workflow/ENVIRONMENT_LINKING.md` — дизайн-документ (4 варианта, обоснование)
- `/home/z/my-project/Ai-game4/worklog.md` — хроника работы (ранее отсутствовал)
- `/home/z/my-project/Ai-game4/checkpoints/08_19_linking_restructure.md` — этот чекпоинт

**Изменённые:**
- `/home/z/my-project/Ai-game4/recover_sandbox.sh` — шаг 4 обновлён (5 симлинков вместо 3), шаг 7 (верификация) обновлён
- `/home/z/my-project/Ai-game4/START_PROMPT.md` — §3 обновлён с таблицей симлинков

**Симлинки (в sandbox, не в git):**
- `/home/z/my-project/aigame4` → `Ai-game4/` (НОВЫЙ)
- `/home/z/my-project/checkpoints` → `Ai-game4/checkpoints` (НОВЫЙ)
- `/home/z/my-project/game` → `Ai-game4/game` (существующий)
- `/home/z/my-project/game-docs` → `Ai-game4/docs` (существующий)
- `/home/z/my-project/godot` → `/home/z/godot` (существующий)
