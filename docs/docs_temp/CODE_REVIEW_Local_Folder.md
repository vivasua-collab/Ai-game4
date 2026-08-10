# РЕВЬЮ ПАПКИ UnityProject/Local

**Дата:** 2026-04-09
**Исполнитель:** AI Assistant
**Заказчик:** Ведущий разработчик Cultivation World Simulator

---

## 1. ВЕРДИКТ

**Папку `UnityProject/Local` МОЖНО УДАЛИТЬ.**

Вся актуальная версия кода находится в `UnityProject/Assets/Scripts/`. Local содержит только устаревшие дубликаты.

---

## 2. СТАТИСТИКА

| Показатель | Local | Основной Assets |
|------------|-------|-----------------|
| .cs файлов | 196 | 122 |
| Дата редактирования | 2026-03-31 | 2026-04-09 |
| Tile система | ❌ НЕТ | ✅ ЕСТЬ (12 файлов) |

---

## 3. СТРУКТУРА LOCAL

```
UnityProject/Local/
├── Assets/
│   ├── Scripts/          # Дубликаты скриптов (УСТАРЕВШИЕ)
│   ├── Sprites/          # Дубликаты спрайтов
│   ├── Scenes/           # Дубликаты сцен
│   ├── Data/JSON/        # Дубликаты JSON
│   ├── Settings/         # Настройки URP
│   ├── TextMesh Pro/     # Стандарт Unity (Examples & Extras)
│   └── Prefabs/          # Дубликаты префабов
└── Scripts/              # Альтернативный путь (дубликаты)
```

**Проблема:** Два пути для скриптов:
- `Local/Assets/Scripts/...`
- `Local/Scripts/...`

Оба содержат одинаковые устаревшие файлы.

---

## 4. СРАВНЕНИЕ КЛЮЧЕВЫХ ФАЙЛОВ

### 4.1 QiController.cs

| Параметр | Local | Основной |
|----------|-------|----------|
| Версия | 1.1 | **1.3** |
| Дата редактирования | 2026-03-31 | **2026-04-09** |
| `conductivityBonus` | ❌ НЕТ | ✅ ЕСТЬ |
| `baseConductivity` | ❌ НЕТ | ✅ ЕСТЬ |
| `dailyAccumulator` | float | **double** (точность) |
| Защита от переполнения long | ❌ НЕТ | ✅ ЕСТЬ |
| PerformBreakthrough: `currentQi = 0` | ❌ НЕТ (SpendQi) | ✅ ЕСТЬ |
| Formation Integration | ❌ НЕТ | ✅ ЕСТЬ |

**КРИТИЧЕСКОЕ ОТЛИЧИЕ:** В Local версия QiController тратит только `required` Ци при прорыве, а в основной версии — **всё накопленное Ци = 0**. Это исправление соответствует ЛОР и документации.

### 4.2 Tile система

| Папка | Local | Основной |
|-------|-------|----------|
| Scripts/Tile/ | ❌ НЕТ | ✅ ЕСТЬ |

**НОВЫЕ ФАЙЛЫ** (только в основном проекте):
- `GameTile.cs`
- `TileData.cs`
- `TileMapController.cs`
- `TileMapData.cs`
- `TileEnums.cs`
- `DestructibleSystem.cs`
- `DestructibleObjectController.cs`
- `ResourcePickup.cs`
- `CultivationGame.TileSystem.asmdef`
- `Editor/TestLocationSetup.cs`
- `Editor/TileSpriteGenerator.cs`

---

## 5. УНИКАЛЬНЫЕ ФАЙЛЫ В LOCAL

Все "уникальные" файлы — это **TextMesh Pro Examples & Extras**:

```
Local/Assets/TextMesh Pro/Examples & Extras/Scripts/
├── Benchmark01.cs
├── Benchmark02.cs
├── Benchmark03.cs
├── Benchmark04.cs
├── CameraController.cs
├── ChatController.cs
├── ... (30 файлов)
```

**Это стандартные примеры Unity, НЕ код проекта.**

---

## 6. ПРОВЕРКА НА НОВЫЙ КОД

**Результат поиска файлов в Local новее QiController:**
```
(пусто — нет таких файлов)
```

Ни один файл в Local не новее, чем соответствующий файл в основном проекте.

---

## 7. РЕКОМЕНДАЦИИ

### 7.1 Удаление

```bash
# Безопасное удаление (рекомендуется)
rm -rf UnityProject/Local
```

### 7.2 Перед удалением

1. **Проверить .gitignore** — добавить `Local/` если нужно
2. **Убедиться**, что Unity закрыт (иначе могут быть метаданные)

### 7.3 После удаления

1. Проверить компиляцию в Unity
2. Запустить тесты

---

## 8. ПРИЧИНЫ ПОЯВЛЕНИЯ LOCAL

Папка `Local` — это локальный кэш Unity на конкретном ПК. Возможные причины:

1. **Два разных ПК** разработчика (синхронизация через Git)
2. **Разные версии Unity** на разных машинах
3. **Ручное копирование** папок проекта

---

## 9. ЗАКЛЮЧЕНИЕ

| Вопрос | Ответ |
|--------|-------|
| Есть ли в Local новый код? | ❌ НЕТ |
| Есть ли в Local уникальный код? | ❌ НЕТ (только TextMesh Pro Examples) |
| Основной проект новее? | ✅ ДА |
| Можно удалить? | ✅ ДА |

**Папка `UnityProject/Local` содержит только устаревшие дубликаты. Удаление безопасно.**

---

*Отчёт создан: 2026-04-09*
