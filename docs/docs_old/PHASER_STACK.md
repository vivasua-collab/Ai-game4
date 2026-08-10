# Phaser 3 Минимальный стек для Cultivation World

## Текущая конфигурация

### Версия
- **Phaser**: 3.90.0
- **Размер в node_modules**: 148MB
- **Минифицированный бандл**: 1.2MB (phaser.min.js)

---

## Используемые модули Phaser

### В проекте используются:
| Модуль | Описание | Обязательный |
|--------|----------|--------------|
| `Phaser.Scene` | Система сцен | ✅ Да |
| `Phaser.Physics.Arcade` | Аркадная физика | ✅ Да |
| `Phaser.GameObjects` | Спрайты, текст, графика | ✅ Да |
| `Phaser.Input.Keyboard` | Клавиатурный ввод | ✅ Да |
| `Phaser.Cameras.Scene2D` | Камера, следование за игроком | ✅ Да |
| `Phaser.Scale` | Масштабирование | ✅ Да |
| `Phaser.Tweens` | Анимации частиц | ⚠️ Опционально |
| `Phaser.Math` | Математические утилиты | ⚠️ Опционально |

---

## Минимальные зависимости

### Обязательные (Next.js/React):
```
react: ^19.0.0
react-dom: ^19.0.0
next: ^16.0.0
phaser: ^3.90.0
```

### Для SSR совместимости:
```typescript
// Динамический импорт в useEffect
const PhaserModule = await import('phaser');
const Phaser = PhaserModule.default;
```

---

## Размер бандлов Phaser

| Файл | Размер | Содержимое |
|------|--------|------------|
| `phaser.min.js` | **1.2 MB** | Полный минифицированный |
| `phaser.esm.min.js` | **1.2 MB** | ES Module версия |
| `phaser-arcade-physics.min.js` | **1.1 MB** | Только Arcade физика |

---

## Минимальная конфигурация

### Phaser Game Config:
```typescript
const config: Phaser.Types.Core.GameConfig = {
  type: Phaser.AUTO,           // WebGL или Canvas
  width: 900,
  height: 550,
  parent: containerRef.current,
  backgroundColor: '#0a1a0a',
  physics: {
    default: 'arcade',         // Лёгкая физика
    arcade: {
      gravity: { x: 0, y: 0 },
      debug: false,
    },
  },
  scene: [GameSceneConfig],
  scale: {
    mode: Phaser.Scale.FIT,
    autoCenter: Phaser.Scale.CENTER_BOTH,
  },
};
```

### Render типы:
- `Phaser.AUTO` - автоматически выбирает WebGL/CSS3D (рекомендуется)
- `Phaser.WEBGL` - только WebGL
- `Phaser.CANVAS` - только Canvas (более совместим)

---

## Оптимизации размера

### 1. Использовать кастомный билд Phaser
Можно собрать только нужные модули:
```
phaser-arcade-physics.min.js (1.1 MB)
```
Выигрыш: ~100KB

### 2. Программная генерация текстур
Текущий проект НЕ использует внешние ассеты:
```typescript
// Текстуры создаются программно
const graphics = this.make.graphics();
graphics.fillStyle(0x4ade80);
graphics.fillCircle(24, 24, 24);
graphics.generateTexture('player', 48, 48);
```
Выигрыш: Нет загрузки изображений

### 3. Lazy loading Phaser
Уже реализовано:
```typescript
useEffect(() => {
  const initGame = async () => {
    const Phaser = (await import('phaser')).default;
    // ...
  };
  initGame();
}, []);
```

---

## Итоговый минимальный стек

### Зависимости (package.json):
```json
{
  "dependencies": {
    "next": "^16.0.0",
    "react": "^19.0.0",
    "react-dom": "^19.0.0",
    "phaser": "^3.90.0"
  }
}
```

### Размер клиентского бандла:
- Phaser: ~1.2 MB (gzip: ~350KB)
- React: ~140KB (gzip: ~45KB)
- Next.js runtime: ~100KB
- **Итого**: ~1.5 MB (gzip: ~400KB)

---

## Рекомендации

### ✅ Текущий подход оптимален:
1. Lazy loading Phaser через dynamic import
2. Программная генерация текстур (нет ассетов)
3. Arcade Physics (лёгкая физика)
4. SSR совместимость через useEffect

### ⚠️ Для дальнейшей оптимизации:
1. Разделить на chunks (code splitting)
2. Использовать `phaser-arcade-physics.min.js` если не нужен Matter.js
3. Предзагрузка Phaser при idle time

---

## Сравнение с альтернативами

| Движок | Размер | Сложность |
|--------|--------|-----------|
| **Phaser 3** | 1.2 MB | Средняя |
| PixiJS | 250 KB | Низкая (только рендер) |
| Three.js | 500 KB | Высокая (3D) |
| Konva.js | 150 KB | Низкая (Canvas) |

**Phaser 3 выбран** за баланс функционала и простоты разработки.
