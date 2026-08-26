# Cold Start — Структура окружения (финальная)

> **Дата:** 2026-08-19
> **Статус:** ACCEPTED (замена Variant D)
> **Назначение:** Чистая процедура холодного старта sandbox без лишних симлинков.

---

## 1. Платформенное ограничение (критично)

`/home/z/my-project/` — **это сам git-репозиторий** Z.ai Code для персистентности sandbox:
```
/home/z/my-project/.git/config → remote origin = github.com/vivasua-collab/Ai-game4.git
commits: bbde5dc "2dca0a15-5aba-..." (UUID сессий Z.ai Code)
```

**Следствие:** Ai-game4 **не может** стать корнем workspace — два git-репо не могут занимать одну папку. Ai-game4 обязан быть подпапкой.

---

## 2. Финальная структура (минимальная)

```
/home/z/my-project/                  ← Z.ai Code sandbox (git repo, НЕ менять)
├── Ai-game4/                        ← BASE: git clone с GitHub (структура 1-в-1 как GitHub)
│   ├── START_PROMPT.md
│   ├── SESSION_SUMMARY.md
│   ├── README.md
│   ├── .gitignore / .gitattributes
│   ├── checkpoints/                 ← все чекпоинты
│   ├── docs/                        ← документация
│   ├── game/                        ← Godot проект
│   ├── worklog.md
│   ├── cold_start.sh                ← этот скрипт
│   └── recover_sandbox.sh           ← (deprecated, alias на cold_start.sh)
│
├── aigame4 -> Ai-game4              ← ЕДИНЫЙ симлинк (точка входа в workspace)
├── godot -> /home/z/godot           ← toolchain симлинк
│
├── Ai-game3-ref/                    ← reference clone (read-only)
└── (Z.ai Code Next.js файлы)
```

**Симлинков: ВСЕГО 2** (вместо 5 в Variant D):
1. `aigame4` → `Ai-game4/` — единая точка входа
2. `godot` → `/home/z/godot` — движок

---

## 3. Доступ к файлам (всё через aigame4/)

| Файл/папка | Путь |
|------------|------|
| Чекпоинты | `aigame4/checkpoints/` |
| Документация | `aigame4/docs/` |
| Код игры | `aigame4/game/` |
| START_PROMPT | `aigame4/START_PROMPT.md` |
| worklog | `aigame4/worklog.md` |
| cold_start.sh | `aigame4/cold_start.sh` |
| Godot binary | `godot/Godot_v4.7.1-.../Godot_..._mono_linux.x86_64` |

**Убранные симлинки** (были в Variant D, избыточны):
- ~~`game` → `Ai-game4/game`~~ → использовать `aigame4/game/`
- ~~`game-docs` → `Ai-game4/docs`~~ → использовать `aigame4/docs/`
- ~~`checkpoints` → `Ai-game4/checkpoints`~~ → использовать `aigame4/checkpoints/`

---

## 4. Холодный старт (процедура)

### Когда запускать
- После сброса sandbox (контейнер пересоздан)
- При новой сессии, если Ai-game4 отсутствует
- При подозрении на сломанное окружение

### Команда
```bash
bash /home/z/my-project/aigame4/cold_start.sh
```
(или `bash /home/z/my-project/Ai-game4/cold_start.sh` если симлинка ещё нет)

### Что делает (idempotent — безопасно запускать многократно)
1. **.NET SDK** — установить в `/home/z/.dotnet/` если отсутствует
2. **Godot 4.7.1** — скачать в `/home/z/godot/` если отсутствует
3. **Ai-game4** — `git clone` если отсутствует, `git pull` если есть
4. **Симлинки** — создать `aigame4` и `godot` (удалить старые если есть)
5. **NuGet.config** — создать локальный (gitignored)
6. **Верификация** — `dotnet build` + headless проверка + проверка симлинков

### Что НЕ делает (намеренно)
- НЕ создаёт `game`, `game-docs`, `checkpoints` симлинки (избыточно)
- НЕ модифицирует Next.js проект
- НЕ трогает `Ai-game3-ref/`
- НЕ коммитит ничего в git (это ручная операция)

---

## 5. Двухуровневая модель

| Уровень | Что | Где | Восстановление |
|---------|-----|-----|----------------|
| **BASE** | Код, доки, чекпоинты | `Ai-game4/` (в git) | `git clone` из GitHub |
| **TOOLCHAIN** | Godot, .NET SDK | `/home/z/godot/`, `/home/z/.dotnet/` | download + install |

**Принцип:** BASE всегда восстанавливается из GitHub (источник истины). TOOLCHAIN восстанавливается из интернет (не в git).

---

## 6. Почему не "Ai-game4 напрямую в корень"

Пользователь хотел: "получения Ai-game4 на прямую в локальное окружение, без линковки в других местах, сразу же получится у меня структура как в gitHub"

**Это невозможно** из-за платформенного ограничения:
1. `/home/z/my-project/` — git-репо Z.ai Code (для персистентности sandbox)
2. Ai-game4 — тоже git-репо
3. Два git-репо не могут занимать одну папку (конфликт `.git/`)
4. Plus: конфликт файлов (`README.md`, `worklog.md`, `.gitignore`)

**Ближайшее решение:** Ai-game4 как подпапка + ОДИН симлинк `aigame4` для прямого доступа. Структура `aigame4/` = структура GitHub 1-в-1.

---

## 7. Сравнение подходов

| Аспект | Variant D (прежний) | Cold Start (новый) |
|--------|---------------------|---------------------|
| Симлинков | 5 (aigame4, checkpoints, game, game-docs, godot) | **2** (aigame4, godot) |
| Доступ к checkpoints | `checkpoints/` или `aigame4/checkpoints/` | `aigame4/checkpoints/` |
| Доступ к game | `game/` или `aigame4/game/` | `aigame4/game/` |
| Загромождение корня | 5 симлинков | 2 симлинка |
| Backward compat | ✅ (game, game-docs сохранены) | ❌ (убраны, использовать aigame4/) |
| Скрипт | `recover_sandbox.sh` | `cold_start.sh` |
| Принцип | Добавить симлинки чтобы всё было видно | Минимум симлинков, один вход |

---

## 8. Миграция с Variant D на Cold Start

1. Запустить `cold_start.sh` — он удалит старые симлинки и создаст новые
2. Обновить пути в документации: `game/` → `aigame4/game/`, `game-docs/` → `aigame4/docs/`
3. `recover_sandbox.sh` оставить как alias/deprecated wrapper на `cold_start.sh`
4. Проверить что нигде в коде нет жёстких ссылок на `/home/z/my-project/game/` (проверено — нет)

---

## 9. Ответ на вопрос про "другое окружение"

**Можно ли дать доступ в другое окружение на chat.z.ai?**

Нет. Платформенное ограничение:
- Каждая сессия Z.ai Code работает в **одном sandbox** (`/home/z/my-project/`)
- Нет доступа к другим sandbox или окружениям на платформе
- Нет "изолированной ИИ модели" — я работаю в этом же sandbox
- Сессии изолированы друг от друга (нет shared state между сессиями)
- Персистентность только через git (Z.ai Code коммитит состояние sandbox) и через файлы которые переживают reset

**Что доступно:**
- Этот sandbox: Next.js + Bun + Node + .NET + Godot (после cold_start)
- GitHub (через token) для push/pull
- Интернет для download toolchain
- Subagents (Task tool) — но они работают в том же sandbox
