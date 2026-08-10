# 🔄 Workflow: GitHub ↔ Локальный Unity

**Версия:** 1.0  
**Дата:** 2026-03-30  
**Проект:** Cultivation World Simulator

---

## 📋 Обзор

Инструкция по синхронизации кода между:
- **Sandbox-окружение** (текущее, где пишется документация)
- **GitHub** (центральный репозиторий)
- **Локальный Unity** (место разработки и тестирования)

---

## 🏗️ Архитектура репозитория

```
GitHub Repository (main3Uniny branch)
         │
         ├── 📥 PULL → Локальный Unity (разработка)
         │
         └── 📥 PULL → Sandbox (документация)
```

**Важно:** Sandbox и локальный Unity НЕ синхронизируются напрямую — только через GitHub!

---

## 📥 Workflow: Sandbox → GitHub → Локальный Unity

### Шаг 1: Сохранение изменений в Sandbox

```
Когда AI завершает работу над документацией:
├── git add .
├── git commit -m "описание изменений"
└── git push
```

### Шаг 2: Получение изменений в локальном Unity

```bash
# В терминале локального Unity проекта
cd /path/to/your/UnityProject

# Получить изменения
git fetch origin
git pull origin main3Uniny

# Или с перезаписью локальных изменений
git fetch origin
git reset --hard origin/main3Uniny
```

### Шаг 3: Использование в Unity

После pull файлы появятся в проекте Unity:

```
Assets/
├── Docs/                    # Документация из sandbox
│   ├── ARCHITECTURE.md
│   ├── DATA_MODELS.md
│   ├── ALGORITHMS.md
│   └── ...
├── Scripts/                 # Код Unity
└── ScriptableObjects/       # Данные
```

---

## 📤 Workflow: Локальный Unity → GitHub → Sandbox

### Шаг 1: Сохранение изменений в Unity

```bash
# В терминале локального Unity проекта
cd /path/to/your/UnityProject

# Проверить изменения
git status

# Добавить изменения
git add .

# Зафиксировать
git commit -m "feat: реализована система X"

# Отправить
git push origin main3Uniny
```

### Шаг 2: Получение в Sandbox

AI выполнит:
```
git fetch origin
git pull origin main3Uniny
```

---

## 🌿 Рекомендуемая структура веток

```
main3Uniny (основная ветка документации)
    │
    ├── feature/combat-system    # Разработка боевой системы
    ├── feature/qi-system        # Разработка системы Ци
    ├── feature/npc-ai           # Разработка NPC AI
    └── docs/*                   # Ветки для документации
```

### Создание feature-ветки

```bash
# В локальном Unity
git checkout -b feature/combat-system

# Работа над функционалом...
git add .
git commit -m "feat: базовая структура боевой системы"
git push origin feature/combat-system

# Создать Pull Request на GitHub
# После review → merge в main3Uniny
```

---

## 🔧 Настройка .gitignore для Unity

Создайте файл `.gitignore` в корне репозитория:

```gitignore
# Unity generated
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Mm]emoryCaptures/

# IDE
.vs/
.idea/
*.csproj
*.sln

# OS
.DS_Store
Thumbs.db

# Build outputs
*.apk
*.aab
*.exe
*.app
```

---

## 📁 Рекомендуемая структура проекта Unity

```
UnityProject/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/              # Ядро игры
│   │   ├── Combat/            # Боевая система
│   │   ├── Qi/                # Система Ци
│   │   ├── NPC/               # NPC и AI
│   │   ├── World/             # Мир и локации
│   │   ├── Save/              # Сохранения
│   │   └── UI/                # Интерфейс
│   │
│   ├── ScriptableObjects/
│   │   ├── Config/            # Конфигурации
│   │   ├── Presets/           # Пресеты (техники, предметы)
│   │   └── Data/              # Данные
│   │
│   ├── Prefabs/               # Префабы
│   ├── Scenes/                # Сцены
│   ├── Art/                   # Графика
│   ├── Audio/                 # Звуки
│   └── Docs/                  # Документация
│
├── Packages/
├── ProjectSettings/
├── .gitignore
└── README.md
```

---

## 🔄 Типичный цикл разработки

### День 1: Начало работы над системой

```
Sandbox:
1. AI пишет теоретический документ (например, COMBAT_SYSTEM.md)
2. git push

Локальный Unity:
3. git pull
4. Чтение документа, понимание архитектуры
5. git checkout -b feature/combat-system
6. Создание структуры папок
7. Реализация базовых классов
8. git push origin feature/combat-system
```

### День 2-3: Итерации

```
Локальный Unity:
1. Продолжение разработки
2. Тестирование в Unity Editor
3. Регулярные коммиты
4. git push

Sandbox:
5. AI обновляет документацию по запросу
6. git push
```

### День 4: Завершение

```
Локальный Unity:
1. Финальное тестирование
2. git push origin feature/combat-system
3. Создание Pull Request на GitHub

GitHub:
4. Merge PR в main3Uniny

Sandbox:
5. git pull (AI получает обновлённый код)
6. Обновление документации при необходимости
```

---

## ⚠️ Важные замечания

### 1. Конфликты

Если оба окружения изменили один файл:

```bash
# При pull с конфликтами
git pull origin main3Uniny
# Git покажет конфликтующие файлы

# Разрешить конфликты вручную
# Затем:
git add .
git commit -m "resolve: разрешены конфликты"
git push
```

### 2. Бинарные файлы Unity

Unity генерирует много бинарных файлов. Рекомендации:

- Не коммитить `.meta` файлы, если не критично
- Использовать Git LFS для больших ассетов
- Регулярно делать бэкапы проекта

### 3. Синхронизация документации

Документация в `/docs/` — это **теория**, не код:
- Может редактироваться в Sandbox
- Переносится в Unity для справки
- Не должна конфликтовать с кодом

---

## 📝 Шпаргалка команд

| Действие | Команда |
|----------|---------|
| Получить изменения | `git pull origin main3Uniny` |
| Отправить изменения | `git push origin main3Uniny` |
| Создать ветку | `git checkout -b feature/имя` |
| Переключить ветку | `git checkout main3Uniny` |
| Статус | `git status` |
| История | `git log --oneline -10` |
| Отменить локальные изменения | `git reset --hard origin/main3Uniny` |

---

## 🆘 Устранение проблем

### Проблема: "fatal: refusing to merge unrelated histories"

```bash
git pull origin main3Uniny --allow-unrelated-histories
```

### Проблема: "Your branch is ahead of origin"

```bash
git push origin main3Uniny
# или если нужно перезаписать remote:
git push -f origin main3Uniny
```

### Проблема: Локальные изменения мешают pull

```bash
# Сохранить изменения временно
git stash

# Получить изменения
git pull origin main3Uniny

# Вернуть изменения
git stash pop
```

---

*Документ создан: 2026-03-30*  
*Для проекта: Cultivation World Simulator*
