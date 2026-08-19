# Environment Linking Structure — Design

> **Дата:** 2026-08-19
> **Статус:** ACCEPTED
> **Причина:** `checkpoints/` не виден в рабочем окружении пользователя после восстановления песочницы.

---

## 1. Проблема

### Симптом
После восстановления песочницы (через `recover_sandbox.sh`) пользователь видит в workspace только:
- `game/`        (симлинк → `Ai-game4/game`)
- `game-docs/`   (симлинк → `Ai-game4/docs`)
- `godot/`       (симлинк → `/home/z/godot`)

**НЕ видны:**
- `checkpoints/`           ← все чекпоинты с 16 августа недоступны
- `worklog.md`             ← хроника работы
- `START_PROMPT.md`        ← правила для AI-агентов
- `SESSION_SUMMARY.md`     ← контекст сессий
- `recover_sandbox.sh`     ← скрипт восстановления
- `README.md`              ← описание проекта

### Корневая причина
`recover_sandbox.sh` (шаг 4) создаёт только 3 симлинка:
```bash
ln -sf /home/z/my-project/Ai-game4/game      /home/z/my-project/game
ln -sf /home/z/my-project/Ai-game4/docs      /home/z/my-project/game-docs
ln -sf /home/z/godot                         /home/z/my-project/godot
```

Папка `Ai-game4/` целиком **не имеет симлинка** и не отображается в workspace пользователя. Всё, что внутри `Ai-game4/` без отдельного симлинка — невидимо.

### Сравнение с Ai-game3-ref
| Аспект | Ai-game3-ref (работает) | Ai-game4 (сломано) |
|--------|-------------------------|---------------------|
| Расположение | `/home/z/my-project/Ai-game3-ref/` | `/home/z/my-project/Ai-game4/` |
| Симлинки на папку | **НЕТ** — папка видна напрямую | **НЕТ** — папка не видна |
| Симлинки внутрь | **НЕТ** | `game`, `game-docs`, `godot` |
| `checkpoints/` виден? | ✅ Да (внутри видимой папки) | ❌ Нет |
| `worklog.md` виден? | ✅ Да | ❌ Нет |

**Вывод:** В Ai-game3-ref папка репозитория видна напрямую, поэтому всё содержимое доступно. В Ai-game4 папка репозитория не видна — только 3 симлинка внутрь.

---

## 2. Принципы новой структуры

### Принцип 1: Двухуровневая модель
| Уровень | Что хранит | Восстановимость | Где живёт |
|---------|------------|-----------------|-----------|
| **Base** (основа) | docs, checkpoints, worklog, prompts, recover-скрипт | В git, восстанавливается через `git clone` | `Ai-game4/` |
| **Toolchain** (инструменты) | Godot binary, .NET SDK | НЕ в git, восстанавливается через download/install | `/home/z/godot/`, `/home/z/.dotnet/` |

### Принцип 2: Единая точка входа
Пользователь видит ОДИН симлинк `aigame4/` → весь репозиторий. Это эквивалентно `Ai-game3-ref/` — одна папка, всё внутри.

### Принцип 3: Обратная совместимость
Старые симлинки (`game`, `game-docs`, `godot`) сохраняются, чтобы не сломать существующие скрипты и привычки.

### Принцип 4: Прямой доступ к критичным путям
Для часто используемых путей (`checkpoints/`) создаются отдельные симлинки — чтобы не открывать `aigame4/` целиком ради одного файла.

---

## 3. Примеры структур (рассмотренные варианты)

### Вариант A: Один симлинк на весь репозиторий (как Ai-game3)
```
/home/z/my-project/
├── Ai-game4/                    ← git репозиторий (источник истины)
├── aigame4 -> Ai-game4          ← ЕДИНЫЙ симлинк (как Ai-game3-ref)
├── godot -> /home/z/godot       ← движок (восстанавливаемый)
```
**Плюсы:** максимально просто, 1-в-1 как Ai-game3, нет дублирования.
**Минусы:** ломает обратную совместимость (нет `game/`, `game-docs/`).

### Вариант B: Добавить недостающие симлинки (аддитивный)
```
/home/z/my-project/
├── Ai-game4/
├── game -> Ai-game4/game              ← (существующий)
├── game-docs -> Ai-game4/docs         ← (существующий)
├── godot -> /home/z/godot             ← (существующий)
├── checkpoints -> Ai-game4/checkpoints ← НОВЫЙ
├── game-worklog -> Ai-game4/worklog.md ← НОВЫЙ
├── game-start -> Ai-game4/START_PROMPT.md ← НОВЫЙ
├── game-session -> Ai-game4/SESSION_SUMMARY.md ← НОВЫЙ
├── game-recover -> Ai-game4/recover_sandbox.sh ← НОВЫЙ
```
**Плюсы:** не ломает существующее, каждый файл доступен напрямую.
**Минусы:** много симлинков, загромождает корень sandbox, файловые симлинки некрасивы.

### Вариант C: Перенос base в корень sandbox (постоянное место)
```
/home/z/my-project/
├── checkpoints/                 ← ПЕРЕНЕСЁН из Ai-game4/
├── START_PROMPT.md              ← ПЕРЕНЕСЁН
├── SESSION_SUMMARY.md           ← ПЕРЕНЕСЁН
├── worklog.md                   ← (уже здесь)
├── Ai-game4/                    ← только game/ и docs/ остаются
├── game -> Ai-game4/game
├── game-docs -> Ai-game4/docs
├── godot -> /home/z/godot
```
**Плюсы:** base всегда на месте, даже если Ai-game4/ удалён.
**Минусы:** ломает git-структуру (checkpoints больше не в репо Ai-game4), дублирование, сложнее поддерживать.

### Вариант D: ГИБРИДНЫЙ (РЕКОМЕНДОВАННЫЙ) ✅
```
/home/z/my-project/
├── Ai-game4/                             ← git репозиторий (источник истины)
├── aigame4 -> Ai-game4                   ← НОВЫЙ: единая точка входа (как Ai-game3-ref)
├── checkpoints -> Ai-game4/checkpoints   ← НОВЫЙ: прямой доступ к чекпоинтам
├── game -> Ai-game4/game                 ← (существующий, backward compat)
├── game-docs -> Ai-game4/docs            ← (существующий, backward compat)
├── godot -> /home/z/godot                ← (существующий, toolchain)
```
**Плюсы:**
- ✅ `aigame4/` — полный доступ ко всему репозиторию (как Ai-game3-ref)
- ✅ `checkpoints/` — прямой доступ (решает главную проблему)
- ✅ Обратная совместимость (`game/`, `game-docs/`, `godot/` остаются)
- ✅ Base в git (восстанавливается через `git clone`)
- ✅ Toolchain отделён (`godot/`, `.dotnet/`)
- ✅ Минимум добавлений (2 новых симлинка)

**Минусы:** нет значительных.

---

## 4. Решение: Вариант D (Гибридный)

### Финальная структура симлинков
```bash
# ── Единая точка входа (как Ai-game3-ref) ──
ln -sf Ai-game4                   /home/z/my-project/aigame4

# ── Прямой доступ к критичным путям ──
ln -sf Ai-game4/checkpoints       /home/z/my-project/checkpoints

# ── Backward compat (существующие) ──
ln -sf Ai-game4/game              /home/z/my-project/game
ln -sf Ai-game4/docs              /home/z/my-project/game-docs

# ── Toolchain (движок, восстанавливаемый) ──
ln -sf /home/z/godot              /home/z/my-project/godot
```

### Что видит пользователь после применения
```
/home/z/my-project/
├── aigame4/              ← ВЕСЬ репозиторий (checkpoints, docs, game, worklog, ...)
├── checkpoints/          ← чекпоинты напрямую
├── game/                 ← код игры (backward compat)
├── game-docs/            ← документация (backward compat)
├── godot/                ← Godot 4.7.1 binary
├── Ai-game4/             ← реальная папка (источник истины)
├── Ai-game3-ref/         ← reference clone (Unity, только для чтения)
└── ... (Next.js sandbox файлы)
```

### Доступ к файлам
| Файл/папка | Пути доступа |
|------------|--------------|
| `checkpoints/08_19_full_audit.md` | `aigame4/checkpoints/08_19_full_audit.md` **или** `checkpoints/08_19_full_audit.md` |
| `START_PROMPT.md` | `aigame4/START_PROMPT.md` |
| `worklog.md` | `aigame4/worklog.md` |
| `docs/docs_v2/` | `aigame4/docs/docs_v2/` **или** `game-docs/docs_v2/` |
| `game/src/` | `aigame4/game/src/` **или** `game/src/` |

---

## 5. Восстановление после сбоя

### Что теряется при сбое контейнера
| Компонент | В git? | Восстановление |
|-----------|--------|----------------|
| `Ai-game4/` (вся base) | ✅ Да | `git clone` (через `recover_sandbox.sh` шаг 3) |
| `/home/z/godot/` | ❌ Нет | download (через `recover_sandbox.sh` шаг 2) |
| `/home/z/.dotnet/` | ❌ Нет | dotnet-install (через `recover_sandbox.sh` шаг 1) |
| Симлинки | ❌ Нет | создаются заново (через `recover_sandbox.sh` шаг 4) |

### Что НЕ теряется
- `/home/z/my-project/` (persistent volume sandbox)
- `Ai-game3-ref/` (если не удалён вручную)
- `worklog.md` в корне sandbox (Z.ai Code worklog, 150KB)

### Скрипт восстановления
`recover_sandbox.sh` обновлён (шаг 4) — создаёт все 5 симлинков:
1. `aigame4` → `Ai-game4/`           ← НОВЫЙ
2. `checkpoints` → `Ai-game4/checkpoints` ← НОВЫЙ
3. `game` → `Ai-game4/game`           (существующий)
4. `game-docs` → `Ai-game4/docs`      (существующий)
5. `godot` → `/home/z/godot`          (существующий)

---

## 6. Правила поддержания структуры

1. **Новые папки в `Ai-game4/`** — автоматически видны через `aigame4/`. Отдельный симлинк создавать только если папка используется ОЧЕНЬ часто (как `checkpoints/`).

2. **Новые toolchain-компоненты** (не в git) — складывать в `/home/z/` (persistent), создавать симлинк в `/home/z/my-project/`.

3. **Файлы, которые должны быть всегда видны** — либо в `Ai-game4/` (через `aigame4/`), либо отдельный симлинк.

4. **Проверка после восстановления** — запускать `recover_sandbox.sh` и проверять:
   ```bash
   for link in aigame4 checkpoints game game-docs godot; do
       test -e /home/z/my-project/$link && echo "✅ $link" || echo "❌ $link"
   done
   ```

---

## 7. История изменений

| Дата | Изменение |
|------|-----------|
| 2026-08-15 | Создан `recover_sandbox.sh` с 3 симлинками (game, game-docs, godot) |
| 2026-08-19 | Добавлены `aigame4` и `checkpoints` симлинки (Вариант D) |
