# 🛠️ Unity Setup Guide — Cultivation World Simulator

**Версия:** 2.0
**Дата:** 2026-07-14
**Unity:** 6000.3 (6000.3.0f1)
**Статус:** Актуальный workflow (модульная архитектура)

---

## 📋 Чек-лист перед началом

- [ ] Unity Hub установлен
- [ ] Unity 6000.3 установлен (2D Core + URP)
- [ ] Git установлен
- [ ] GitHub Personal Access Token (classic) с правом `repo`

---

## 🚀 Шаг 1: Клонирование репозитория

### 1.1 Клонировать через git

```bash
git clone https://github.com/vivasua-collab/Ai-game3.git
```

Или с токеном (для push):
```bash
git clone https://<TOKEN>@github.com/vivasua-collab/Ai-game3.git
```

### 1.2 Рабочая ветка

```bash
cd Ai-game3
git checkout main
```

> **Важно:** Работать ТОЛЬКО с веткой `main`. Ветка `master` — не трогать.

---

## 📁 Шаг 2: Открыть проект в Unity Hub

### 2.1 Добавить проект

1. Запустите Unity Hub
2. Нажмите **"Add"** → "Add project from disk"
3. Выберите папку `UnityProject/` внутри клонированного репозитория
4. Unity Hub определит версию Unity 6000.3

### 2.2 Открыть проект

1. Нажмите на проект в списке
2. Дождитесь импорта (статус-бар внизу)
3. Дождитесь компиляции (статус-бар)

### 2.3 Проверить Console

- `Window → General → Console`
- Должно быть 0 красных ошибок компиляции
- Жёлтые warnings допустимы

---

## ⚙️ Шаг 3: Пакеты (автоматически из manifest.json)

Проект использует `Packages/manifest.json` — пакеты устанавливаются автоматически:

| Пакет | Версия | Назначение |
|-------|--------|------------|
| `com.unity.ugui` | 2.0.0 | UnityEngine.UI (Canvas, Text, Image) |
| `com.unity.textmeshpro` | 3.0.6 | TMP Essentials (опционально, UI использует legacy Text) |
| `com.unity.inputsystem` | latest | Input System (Keyboard.current, Mouse.current) |
| `jp.hadashikick.vcontainer` | 1.17.0 | DI-контейнер |
| `com.cysharp.messagepipe` | 1.8.1 | Шина сообщений |
| `com.cysharp.unitask` | 2.5.10 | Zero-alloc async |

### 3.1 Input System — ОБЯЗАТЕЛЕН

`Edit → Project Settings → Player → Active Input Handling` = **"Input System Package (New)"** или **"Both"**

UI Views используют `Keyboard.current`, `Mouse.current` (Input System API).

---

## 🔧 Шаг 4: Авто-настройка при первом запуске

При первом открытии проекта Unity Editor автоматически запускает фазы настройки (через `[InitializeOnLoadMethod]`):

### Phase00: URP Setup (авто-фикс)
- Создаёт `UniversalRP.asset` + `Renderer2D.asset` в `Assets/Settings/`
- Чинит связи в `ProjectSettings/GraphicsSettings.asset` (если GUID изменился)
- Лог: `[Phase00] ✅ URP Setup завершён`

### Phase01: Sprite Import (авто-настройка)
- Настраивает 184 спрайта: PPU=64, Point filter, Alpha
- Лог: `[Phase01] ✅ Завершено: настроено 184, пропущено 0, ошибок 0`

### Phase01B: TMP Essentials (опционально)
- Пытается автоимпортировать TMP Essential Resources
- Если не находит — открывает окно `Window → TextMeshPro → Import TMP Essential Resources`
- Нажмите **"Import TMP Essentials"** в открывшемся окне
- UI использует legacy Text, но TMP нужен для будущих компонентов

### Phase02: Tags & Layers (авто-настройка)
- Настраивает теги и слои (Default, Background, Terrain, Objects, Player, UI)

---

## 🎬 Шаг 5: Создание игровой сцены

### 5.1 Запустить MainGameSceneCreator

`Tools → Full Scene Builder → Create Main Game Scene`

Или手动:

1. `File → New Scene` → пустая сцена
2. Создать пустой GameObject "GameLifetimeScope"
3. Добавить компонент `GameLifetimeScope`
4. Сохранить как `Assets/Scenes/MainGame.unity`

### 5.2 Сцена в Build Settings

`File → Build Settings → Add Open Scenes` — `MainGame` должна быть в списке (index 0).

---

## ▶️ Шаг 6: Запуск (Play)

### 6.1 Открыть сцену MainGame

Двойной клик на `Assets/Scenes/MainGame.unity`

### 6.2 Нажать Play

- `RuntimeSceneBuilder.Start()` соберёт сцену программно (~2 сек)
- В Console: `[GameEntryPoint] ✅ Сцена готова! N фаз выполнено`
- Создаётся: Camera, Canvas (3 слоя), EventSystem, World Root, Player, NPC, GameInputAdapter

### 6.3 Проверить UI

| Элемент | Ожидание |
|---------|----------|
| HUD (слева сверху) | StatusPanel: HP/Qi/Stamina/Cultivation полосы + метки |
| Hotbar (внизу центр) | 9 слотов с цифрами 1–9 |
| MiniMap (справа сверху) | Видна по умолчанию, N-key toggle |
| Toast (справа сверху) | Уведомления при событиях |
| BuffBar (слева, под HUD) | Активные баффы |

### 6.4 Управление

| Клавиша | Действие |
|---------|----------|
| WASD | Движение игрока |
| 1–9 | Выбор слота хотбара |
| E | Взаимодействие с NPC |
| I | Инвентарь (этап 6, pending) |
| N | Скрыть/показать миникарту |
| Escape | Пауза |
| F1 | InputLogPanel (DEBUG) |
| F2 | NPCInspectorPanel (DEBUG) |

---

## 🔄 Workflow: обновление кода

### Пользовательский workflow (GitHub Desktop + отдельная папка)

1. В GitHub Desktop: **Pull** origin/main
2. Скопировать `Assets/` из GitHub-папки в рабочую папку Unity (замена)
3. Запустить Unity Editor
4. Phase00-02 авто-настроят окружение (GUID-rot → авто-фикс)
5. Play → проверка

### Git workflow (два ПК)

```bash
# Перед началом работы
git pull origin main

# После работы
git add -A
git commit -m "описание изменений"
git pull --rebase
git push
```

---

## 🐛 Устранение проблем

### "Default Renderer is missing" (24 сообщения в Console)

**Причина:** После копирования `Assets/` GUID меняются, `GraphicsSettings.asset` не находит URP ассет.

**Решение:** `Phase00URPSetup` авто-чинит связи при запуске Unity. Дождаться завершения (лог `[Phase00] ✅ URP Setup завершён`). Ошибки преходящие.

### VContainerException: No such registration

**Причина:** Прямой `scope.Container.Inject()` до завершения регистрации MessagePipe брокеров.

**Решение:** Views должны использовать `UIComponentResolver.TryInject()` в `Start()`, не прямой inject в `CreateXxx()`. См. `CreateInventoryScreen` (RuntimeSceneBuilder.cs).

### Текст не рендерится (size=0x0)

**Причина:** Текстовые элементы в `VerticalLayoutGroup` без `ContentSizeFitter` получают `sizeDelta=0`.

**Решение:** `UIFactory.CreateText()` добавляет `ContentSizeFitter` + `LayoutElement`. Все тексты должны создаваться через `CreateText`, не `AddComponent<Text>` напрямую.

### FindFirstObjectByType возвращает null для UI View

**Причина:** View деактивирован (`SetActive(false)`), `FindFirstObjectByType<T>()` без флага не находит деактивированные объекты.

**Решение:** Использовать `FindFirstObjectByType<T>(FindObjectsInactive.Include)` в `WireUIViews()`.

---

## 📚 Следующие шаги

После завершения настройки:

1. ✅ Проект клонирован
2. ✅ Unity 6000.3 открыл `UnityProject/`
3. ✅ Phase00-02 авто-настроили окружение
4. ✅ MainGame сцена создана
5. ✅ Play — сцена собирается, UI работает

**Текущий статус:** UI V3 Фазы 0–4 реализованы, **в активной переработке** (не прошли полное тестирование). Фаза C (этапы 5–9) — pending. См. [README.md §Этапы разработки](../../README.md).

---

## 📖 См. также

- [ARCHITECTURE.md](./ARCHITECTURE.md) — Архитектура систем
- [SCENE_BUILDER_SYSTEM.md](./SCENE_BUILDER_SYSTEM.md) — Система сборки сцены
- [../checkpoints/06_17_history_reconstruction.md](../checkpoints/06_17_history_reconstruction.md) — История разработки
- [../checkpoints/06_18_ui_verification_step_by_step_plan.md](../checkpoints/06_18_ui_verification_step_by_step_plan.md) — План верификации UI
- [START_PROMPT.md](../../START_PROMPT.md) — Стартовый промпт для ИИ агента

---

*Документ создан: 2026-03-30*
*Редактировано: 2026-07-14 06:50:00 UTC — полная переработка под актуальный workflow (clone + VContainer + Phase00-02 + RuntimeSceneBuilder). Убраны Legacy-классы (BodyController/NPCController/NavMeshAgent), добавлены пакеты, авто-настройка, troubleshooting 06_18.*
