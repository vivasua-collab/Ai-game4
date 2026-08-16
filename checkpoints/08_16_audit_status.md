# Чекпоинт: Аудит 16 августа — статус реализаций и оставшиеся проблемы

**Дата:** 2026-08-16 07:15 UTC
**Тип:** audit | progress

---

## Контекст

Новый день. Окружение снова умерло (пересоздание песочницы). Восстановлено:
.NET SDK 8+9, Godot 4.7.1, Ai-game4 клонирован с GitHub (коммит c072db2).

Из лога компиляции пользователя: 5 ошибок CS0246 (BiomeType not found) + 36 warnings CS0105 (duplicate usings).

## Статус критических проблем из аудита 15.08

| # | Проблема | Статус | Как исправлено |
|---|----------|--------|----------------|
| 1 | Mouse click не детектится | ✅ FIXED | Добавлен `mouse_click` action (ЛКМ) в InputMapInitializer |
| 2 | Sticky flags очищаются до чтения Adapter | ❌ NOT FIXED | Нужно перенести ResetFrameFlags после _PhysicsProcess |
| 3 | Двойной Spawn игрока | ❌ NOT FIXED | PlayerService.Spawn не проверяет _spawned |
| 4 | Дублирующая Camera2D | ✅ FIXED | Убрана из SceneBuilder |
| 5 | Дублирующий Sprite2D | ✅ FIXED | Убран из SceneBuilder |
| 6 | Зарегистрирован не тот SaveFileHandler | ❌ NOT FIXED | SaveModule использует Modules.Save.SaveFileHandler вместо Adapter |

## Статус запланированных реализаций

| Что | План | Статус |
|-----|------|--------|
| Free pixel movement | SMOOTH_MOVEMENT_PLAN.md | ✅ IMPLEMENTED — _visualPosition, HandleFreeMovement, Input.GetVector |
| Strata 0/1 separation | WORLD_STRATA_DESIGN.md | ✅ IMPLEMENTED — BiomeType enum, GameTile.Biome, MapToBiome/MapToSurface |
| Biome colors (stratum 0) | BiomeColors в SceneBuilder | ✅ IMPLEMENTED — muted biome colors via MultiMesh |
| Transition sprites | TransitionSpriteGenerator.cs | ✅ IMPLEMENTED — quarter-circle PNGs with anti-aliasing |
| Biome transition pairs | BIOME_TRANSITION_PAIRS.md | ✅ DOCUMENTED — 10 pairs identified |
| Camera zoom (mouse wheel) | GameWorldController._Input | ✅ IMPLEMENTED — wheel up/down, middle reset |
| HUD layout (time top, legend bottom) | GameWorldController.SetupHUD | ✅ IMPLEMENTED |
| Time speed hotkeys (+/-, PageUp/Down) | InputMapInitializer | ✅ IMPLEMENTED — debounce 1 sec |
| TimeSpeed enum values (0,1,5,15) | Enums.cs | ✅ FIXED — explicit values |
| 2.5D demo study | demo_25d_techniques.md | ✅ DOCUMENTED |

## Оставшиеся проблемы (из аудита 15.08 + новые)

### 🔴 Критические (3)
1. **Sticky flags race condition** — ResetFrameFlags в PlayerModule.Tick() вызывается ДО того, как GameWorldController._PhysicsProcess прочитает флаги
2. **Double Spawn** — PlayerService.Spawn не идемпотентен
3. **Wrong SaveFileHandler** — Modules.Save.SaveFileHandler вместо Adapter.Persistence.SaveFileHandler

### 🟡 Важные (8)
4. Adapter содержит игровую логику (Pause/Speed/Save)
5. Adapter напрямую резолвит PlayerModule
6. SetOverUI никогда не вызывается
7. TransitionTileRenderer не перерисовывается при SetTile
8. Двойная генерация тайлов (TileModule.Start + TileMapGenPhase)
9. Мёртвые alias-проверки в PlayerInputService
10. WorldConfig.StartHour игнорируется
11. Хардкод границ мира `49`

### 🟢 Косметика (5)
12. 256 warnings (duplicate usings, unused fields, null literals)
13. Invalid UID в MainMenu.tscn
14. Debug-логи в production
15. Два класса в WorldService.cs
16. _debugFrameCount в production

## Рекомендации по порядку исправления

1. **BiomeType.cs** — ✅ ИСПРАВЛЕНО (закоммичен)
2. **Duplicate usings** — массовая очистка (36 файлов)
3. **Sticky flags** — перенести ResetFrameFlags после _PhysicsProcess
4. **Double Spawn** — добавить проверку _spawned в Spawn()
5. **SaveFileHandler** — зарегистрировать Adapter-версию

## Root cause: повторяющаяся проблема с незакоммиченными файлами

4-й раз: ValueNoise.cs, TransitionTileRenderer.cs, TransitionSpriteGenerator.cs, BiomeType.cs.

**Причина:** Python-скрипты создают файлы на диске, но `git add` вызывается из неправильной директории (my-project вместо Ai-game4).

**Решение:**
1. ВСЕГДА `git -C /home/z/my-project/Ai-game4 add -A` (с указанием пути)
2. ВСЕГДА проверять `git ls-files --others --exclude-standard -- "*.cs"` перед коммитом
3. ВСЕГДА проверять `git show --stat HEAD` после коммита
4. ВСЕГДА проверять через GitHub API после push

## Текущее состояние сборки

```
dotnet build: 0 errors, 256 warnings
Headless run: all 17 startables, 16 tickables started
All .cs files tracked in git
```
