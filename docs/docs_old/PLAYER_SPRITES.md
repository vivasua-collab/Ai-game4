# Спрайты игрока - AI Generated Sprites

## Обзор

Спрайты игрока сгенерированы через AI (z-ai-web-dev-sdk) в стиле аниме/манга для симулятора мира культивации.

## Структура файлов

```
public/sprites/
├── player/
│   ├── player-directions.png    # Спрайт-лист 8 направлений
│   ├── player-idle-frames.png   # Анимация покоя
│   ├── player-walk-frames.png   # Анимация ходьбы
│   ├── player-level-1.png       # Уровень 1: Пробуждённое Ядро
│   ├── player-level-3.png       # Уровень 3: Пламя Внутреннего Огня
│   ├── player-level-5.png       # Уровень 5: Сердце Небес
│   ├── player-level-7.png       # Уровень 7: Вечное Кольцо
│   └── player-level-9.png       # Уровень 9: Бессмертное Ядро
└── effects/
    ├── qi-glow.png              # Эффект свечения Ци
    ├── breakthrough-effect.png  # Эффект прорыва
    └── meditation-effect.png    # Эффект медитации
```

## Система уровней культивации

| Уровень | Название | Цвет | Эффект |
|---------|----------|------|--------|
| 1 | Пробуждённое Ядро | Белый | Тусклое свечение |
| 2 | Течение Жизни | Зелёный | Мягкие потоки |
| 3 | Пламя Внутреннего Огня | Оранжевый | Языки пламени |
| 4 | Объединение Тела и Духа | Синий | Текучая энергия |
| 5 | Сердце Небес | Фиолетовый | Молнии |
| 6 | Разрыв Пелены | Серебряный | Искажение пространства |
| 7 | Вечное Кольцо | Золотой | Золотой нимб |
| 8 | Глас Небес | Белое золото | Сияние |
| 9 | Бессмертное Ядро | Радужный | Небесная энергия |

## Использование в Phaser

### Загрузка спрайтов

```typescript
import { SpriteLoader } from '@/game/services/sprite-loader';

const spriteLoader = new SpriteLoader(scene);

// Загрузить спрайты направлений
spriteLoader.loadPlayerDirectionalSprites();

// Загрузить спрайт уровня культивации
spriteLoader.loadCultivationLevelSprite(character.cultivationLevel);

// Загрузить эффекты
spriteLoader.loadEffectSprites();
```

### Создание ауры Ци

```typescript
// Создаёт динамическую ауру вокруг игрока
const qiAura = spriteLoader.createQiAura(
  player.x, 
  player.y,
  character.cultivationLevel,  // Уровень культивации
  character.currentQi,          // Текущее Ци
  character.maxQi               // Максимальное Ци
);

// Аура автоматически:
// - Пульсирует с интенсивностью, зависящей от % Ци
// - Меняет цвет по уровню культивации
// - Добавляет частицы на уровнях 3+
```

### Эффект прорыва

```typescript
// При повышении уровня культивации
spriteLoader.createBreakthroughEffect(
  player.x, 
  player.y, 
  newLevel
);
```

### Эффект медитации

```typescript
// При медитации
const meditationEffect = spriteLoader.createMeditationEffect(
  player.x, 
  player.y, 
  character.cultivationLevel
);
```

## Fallback (программная генерация)

Если AI-спрайты не загрузились, используется программная генерация:

```typescript
import { createFallbackPlayerTexture } from '@/game/services/sprite-loader';

// Создаёт базовую текстуру игрока с учётом уровня культивации
createFallbackPlayerTexture(scene, character.cultivationLevel);
```

## Конфигурация

Все параметры настраиваются в `src/game/config/sprites.config.ts`:

- `CULTIVATION_THEMES` - цвета и эффекты по уровням
- `SPRITE_PATHS` - пути к файлам спрайтов
- `ANIMATION_CONFIG` - параметры анимации
- `QI_AURA_CONFIG` - параметры ауры Ци

## Генерация новых спрайтов

Для перегенерации спрайтов используйте скрипт:

```bash
bun run scripts/generate-player-sprite.ts
```

Или CLI напрямую:

```bash
# Спрайт-лист направлений
z-ai image -p "anime manga style game sprite sheet, cultivator character..." -o "./public/sprites/player/player-directions.png" -s 1344x768

# Спрайт уровня
z-ai image -p "anime manga style character portrait, cultivator level 5..." -o "./public/sprites/player/player-level-5.png" -s 768x1344

# Эффект
z-ai image -p "magical energy aura effect..." -o "./public/sprites/effects/qi-glow.png" -s 1024x1024
```

## Примечания

1. Все спрайты генерируются с transparent background для наложения
2. Размеры должны быть кратны 32 (ограничение API)
3. Поддерживаемые размеры: 1024x1024, 768x1344, 1344x768, и т.д.
4. Спрайты автоматически кэшируются после первой загрузки
