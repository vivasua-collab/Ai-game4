# 🎨 Визуализация формаций в Phaser

**Дата:** 2026-03-20
**Статус:** 📋 Теоретические изыскания
**Зависит от:** `formation_analysis.md`

---

## 🎯 ОБЗОР

Документ описывает графическое отображение формаций в игровом движке Phaser 3, включая:
- Визуализацию контура формации
- Подсветку точек подключения для помощников
- Индикацию этапов создания
- Анимации и эффекты

---

## 📐 КОНЦЕПТУАЛЬНАЯ СХЕМА

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                     ВИЗУАЛИЗАЦИЯ ФОРМАЦИИ                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  СЛОЙ 5: ЭФФЕКТЫ (высший приоритет)                                          │
│  ────────────────────────────────────                                        │
│  - Свечение при активации                                                    │
│  - Частицы Ци (floating particles)                                          │
│  - Пульсация при наполнении                                                  │
│                                                                              │
│  СЛОЙ 4: НАДПИСИ И ИНДИКАТОРЫ                                                │
│  ────────────────────────────────────                                        │
│  - Название формации                                                         │
│  - Прогресс-бар наполнения                                                   │
│  - Таймер до завершения этапа                                                │
│  - Список участников                                                          │
│                                                                              │
│  СЛОЙ 3: ТОЧКИ ПОДКЛЮЧЕНИЯ                                                   │
│  ────────────────────────────────────                                        │
│  - Пульсирующие круги для помощников                                        │
│  - Индикация занятых/свободных мест                                         │
│  - Линии связи между участниками                                            │
│                                                                              │
│  СЛОЙ 2: КОНТУР ФОРМАЦИИ                                                     │
│  ────────────────────────────────────                                        │
│  - Геометрический узор (круг, треугольник, звезда)                          │
│  - Руны и символы                                                            │
│  - Линии потока Ци                                                          │
│                                                                              │
│  СЛОЙ 1: ФОН (земля)                                                         │
│  ────────────────────────────────────                                        │
│  - Текстура земли                                                            │
│  - Тень от формации                                                          │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 🔄 ЭТАПЫ ВИЗУАЛИЗАЦИИ

### 1. ЭТАП: ПРОРИСОВКА КОНТУРА (drawing)

```
┌─────────────────────────────────────────────────────────────────┐
│                     ПРОРИСОВКА КОНТУРА                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Визуальные эффекты:                                            │
│  - Линия контура "рисуется" от точки к точке                    │
│  - Цвет: золотистый (0xfbbf24) с прозрачностью                 │
│  - Толщина линии: 3-5 пикселей                                  │
│  - Скорость: пропорциональна conductivity                       │
│                                                                  │
│  Анимация:                                                      │
│  ┌───────┐                                                      │
│  │   ●   │ ← Точка рисования (движется по контуру)             │
│  │  ╱    │                                                      │
│  │ ●     │ ← Уже нарисованная часть                            │
│  │╱      │                                                      │
│  └───────┘                                                      │
│                                                                  │
│  Звук: мягкий "жужжащий" звук Ци                               │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. ЭТАП: СТАБИЛИЗАЦИЯ (stabilizing)

```
┌─────────────────────────────────────────────────────────────────┐
│                     СТАБИЛИЗАЦИЯ                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Визуальные эффекты:                                            │
│  - Контур "проявляется" (alpha от 0.5 до 1.0)                  │
│  - Руны появляются одна за другой                               │
│  - Точки подключения подсвечиваются                             │
│                                                                  │
│  Анимация:                                                      │
│  ┌───────┐                                                      │
│  │ ✧   ✧ │ ← Руны появляются                                   │
│  │   ◆   │ ← Центральный символ                                │
│  │ ✧   ✧ │ ← Точки подключения (пульсируют)                    │
│  └───────┘                                                      │
│                                                                  │
│  Цвет точек подключения:                                        │
│  - Свободные: зелёный (0x22c55e), пульсация                     │
│  - Занятые: синий (0x3b82f6), статичные                         │
│  - Недоступные: серый (0x6b7280), полупрозрачные               │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 3. ЭТАП: НАПОЛНЕНИЕ (filling)

```
┌─────────────────────────────────────────────────────────────────┐
│                     НАПОЛНЕНИЕ ЦИ                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Визуальные эффекты:                                            │
│  - Потоки Ци от каждого участника к центру                     │
│  - Прогресс-бар (круговой или линейный)                        │
│  - Пульсация контура в ритме с Ци                              │
│                                                                  │
│  Анимация потоков:                                              │
│  ┌───────────────────────────────────────────┐                 │
│  │        ╲    ╱                              │                 │
│  │  👤 ────→  ◆  ←─── 👤                      │                 │
│  │        ╱    ╲                              │                 │
│  │   потоки Ци от участников к центру        │                 │
│  └───────────────────────────────────────────┘                 │
│                                                                  │
│  Прогресс-бар:                                                  │
│  ┌────────────────────────────┐                                │
│  │ ████████░░░░░░░░░ 45%     │                                │
│  │ Ци: 45,000 / 100,000      │                                │
│  │ Скорость: 2,688 Ци/сек    │                                │
│  │ Участников: 5             │                                │
│  └────────────────────────────┘                                │
│                                                                  │
│  Цвет потоков:                                                  │
│  - По умолчанию: белый/голубой                                 │
│  - Огонь: оранжевый (0xff8844)                                 │
│  - Вода: синий (0x4488ff)                                      │
│  - Молния: жёлтый (0xffff44)                                   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 4. ЭТАП: АКТИВАЦИЯ (active)

```
┌─────────────────────────────────────────────────────────────────┐
│                     АКТИВНАЯ ФОРМАЦИЯ                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Визуальные эффекты:                                            │
│  - Яркое свечение контура                                       │
│  - Частицы Ци поднимаются вверх                                 │
│  - Купол/барьер над формацией (для защитных)                   │
│                                                                  │
│  Анимация:                                                      │
│  ┌───────────────────────────────────┐                         │
│  │         ∿∿∿∿∿∿∿∿∿∿∿               │                         │
│  │    ∿∿∿    ◆    ∿∿∿                │ ← Частицы Ци            │
│  │  ╭──────────────────╮              │                         │
│  │  │    ✧         ✧    │              │ ← Контур со свечением  │
│  │  │        ◆          │              │                         │
│  │  │    ✧         ✧    │              │                         │
│  │  ╰──────────────────╯              │                         │
│  └───────────────────────────────────┘                         │
│                                                                  │
│  Индикатор деградации:                                          │
│  - Цвет контура меняется: золотой → жёлтый → оранжевый         │
│  - Прозрачность снижается при низком заряде                     │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📍 ТОЧКИ ПОДКЛЮЧЕНИЯ

### Концепция

```
┌─────────────────────────────────────────────────────────────────┐
│                     ТОЧКИ ПОДКЛЮЧЕНИЯ                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Количество точек = размер формации × тип формации              │
│                                                                  │
│  Small:   2-3 точки                                             │
│  Medium:  4-6 точек                                             │
│  Large:   7-12 точек                                            │
│  Great:   13-20 точек                                           │
│                                                                  │
│  Расположение:                                                   │
│  - По периметру контура                                         │
│  - На равных расстояниях                                        │
│  - С учётом геометрии формации                                  │
│                                                                  │
│  Пример (Circle, 8 точек):                                      │
│  ┌─────────────────────┐                                        │
│  │    ●           ●    │                                        │
│  │  ●               ●  │                                        │
│  │                     │                                        │
│  │  ●       ◆       ●  │ ← ◆ = создатель (центр)               │
│  │                     │                                        │
│  │  ●               ●  │ ← ● = точки подключения               │
│  │    ●           ●    │                                        │
│  └─────────────────────┘                                        │
│                                                                  │
│  Пример (Triangle, 6 точек):                                    │
│  ┌─────────────────────┐                                        │
│  │         ●           │                                        │
│  │        ╱ ╲          │                                        │
│  │      ●     ●        │                                        │
│  │      ╱     ╲        │                                        │
│  │    ●   ◆   ●        │                                        │
│  │   ───────────       │                                        │
│  │      ●              │                                        │
│  └─────────────────────┘                                        │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Визуальное представление точки

```typescript
interface ConnectionPoint {
  x: number;
  y: number;
  status: 'available' | 'occupied' | 'locked';
  occupantId?: string;
  graphics: Phaser.GameObjects.Container;
}

// Визуальные стили точек:
const POINT_STYLES = {
  available: {
    color: 0x22c55e,      // Зелёный
    alpha: 0.8,
    pulseAnimation: true,  // Пульсация
    radius: 12,
    icon: '⊕',             // Символ "плюс в круге"
  },
  occupied: {
    color: 0x3b82f6,      // Синий
    alpha: 1.0,
    pulseAnimation: false,
    radius: 12,
    showParticipant: true, // Показать аватар участника
  },
  locked: {
    color: 0x6b7280,      // Серый
    alpha: 0.4,
    pulseAnimation: false,
    radius: 8,
  },
};
```

### Интерактивность

```
┌─────────────────────────────────────────────────────────────────┐
│                     ИНТЕРАКТИВНОСТЬ                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  При наведении мыши:                                            │
│  - Точка увеличивается (scale: 1.2)                             │
│  - Показывается tooltip:                                        │
│    ┌──────────────────────┐                                     │
│    │ Точка подключения #3 │                                     │
│    │ Статус: Свободна     │                                     │
│    │ Требуется: L3+       │                                     │
│    │ [Присоединиться]     │                                     │
│    └──────────────────────┘                                     │
│                                                                  │
│  При клике:                                                      │
│  - Если свободна → отправить запрос на присоединение           │
│  - Если занята → показать информацию об участнике               │
│                                                                  │
│  Подсветка для подходящих игроков:                              │
│  - Если игрок подходит по уровню → яркая зелёная подсветка     │
│  - Если не подходит → красный контур                            │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🎨 ГРАФИЧЕСКИЕ КОМПОНЕНТЫ PHASER

### Структура классов

```typescript
// ============================================================================
// ИЕРАРХИЯ КЛАССОВ
// ============================================================================

/**
 * Базовый класс для визуализации формации
 */
class FormationVisual extends Phaser.GameObjects.Container {
  // Слои
  private contourLayer: Phaser.GameObjects.Graphics;      // Контур
  private runeLayer: Phaser.GameObjects.Container;        // Руны
  private connectionPointsLayer: Phaser.GameObjects.Container;  // Точки
  private effectsLayer: Phaser.GameObjects.Container;     // Эффекты
  private uiLayer: Phaser.GameObjects.Container;          // UI элементы
  
  // Состояние
  private state: FormationState;
  private stage: FormationStage;
  
  // Анимации
  private pulseTween?: Phaser.Tweens.Tween;
  private particleEmitter?: Phaser.GameObjects.Particles.ParticleEmitter;
}

/**
 * Точка подключения
 */
class ConnectionPointVisual extends Phaser.GameObjects.Container {
  private circle: Phaser.GameObjects.Arc;
  private icon: Phaser.GameObjects.Text;
  private ring: Phaser.GameObjects.Arc;  // Внешнее кольцо
  private glow: Phaser.GameObjects.Arc;  // Свечение
  
  private status: 'available' | 'occupied' | 'locked';
  private pulseTween?: Phaser.Tweens.Tween;
  
  setStatus(status: ConnectionPointStatus): void;
  highlight(isEligible: boolean): void;
  showTooltip(): void;
}

/**
 * Поток Ци от участника
 */
class QiFlowVisual extends Phaser.GameObjects.Graphics {
  private startPoint: { x: number; y: number };
  private endPoint: { x: number; y: number };
  private color: number;
  private particles: Phaser.GameObjects.Particles.ParticleEmitter;
  
  // Анимация потока
  private flowOffset: number = 0;
  
  update(delta: number): void;
}
```

### Рендеринг контура

```typescript
/**
 * Отрисовка геометрических форм формации
 */
class FormationContourRenderer {
  /**
   * Рисование контура с анимацией "прорисовки"
   */
  static drawContour(
    graphics: Phaser.GameObjects.Graphics,
    shape: FormationShape,
    radius: number,
    progress: number,  // 0-1, какая часть контура нарисована
    style: ContourStyle
  ): void {
    graphics.clear();
    
    // Основной контур
    graphics.lineStyle(style.thickness, style.color, style.alpha);
    
    switch (shape) {
      case 'circle':
        this.drawCircleContour(graphics, radius, progress);
        break;
      case 'triangle':
        this.drawTriangleContour(graphics, radius, progress);
        break;
      case 'star':
        this.drawStarContour(graphics, radius, progress);
        break;
      // ... другие формы
    }
    
    // Добавляем руны в узловых точках
    if (progress >= 1.0) {
      this.drawRunes(graphics, shape, radius, style);
    }
  }
  
  private static drawCircleContour(
    graphics: Phaser.GameObjects.Graphics,
    radius: number,
    progress: number
  ): void {
    graphics.beginPath();
    graphics.arc(0, 0, radius, 0, Math.PI * 2 * progress);
    graphics.strokePath();
  }
  
  private static drawStarContour(
    graphics: Phaser.GameObjects.Graphics,
    radius: number,
    progress: number
  ): void {
    const points = 5;
    const innerRadius = radius * 0.5;
    
    graphics.beginPath();
    
    for (let i = 0; i < points * 2; i++) {
      const angle = (Math.PI * i) / points - Math.PI / 2;
      const r = i % 2 === 0 ? radius : innerRadius;
      const x = Math.cos(angle) * r;
      const y = Math.sin(angle) * r;
      
      if (i === 0) {
        graphics.moveTo(x, y);
      } else {
        graphics.lineTo(x, y);
      }
      
      // Прерываем, если достигли progress
      if (i / (points * 2) >= progress) break;
    }
    
    graphics.strokePath();
  }
}
```

### Система частиц Ци

```typescript
/**
 * Частицы Ци для визуализации потока
 */
class QiParticleManager {
  private emitter: Phaser.GameObjects.Particles.ParticleEmitter;
  
  constructor(scene: Phaser.Scene, config: QiParticleConfig) {
    this.emitter = scene.add.particles(0, 0, 'qi_particle', {
      speed: { min: 20, max: 50 },
      scale: { start: 0.5, end: 0 },
      alpha: { start: 0.8, end: 0 },
      lifespan: 2000,
      frequency: 100,
      blendMode: 'ADD',
      tint: config.color,
    });
  }
  
  /**
   * Создать поток частиц от точки к точке
   */
  createFlow(
    from: { x: number; y: number },
    to: { x: number; y: number },
    intensity: number
  ): void {
    const dx = to.x - from.x;
    const dy = to.y - from.y;
    const distance = Math.sqrt(dx * dx + dy * dy);
    
    // Эмиттер движется вдоль линии
    this.emitter.setEmitterCenter({
      x: from.x,
      y: from.y,
    });
    
    this.emitter.setSpeed({
      min: distance / 4,
      max: distance / 2,
    });
    
    // Направление частиц
    const angle = Math.atan2(dy, dx);
    this.emitter.setAngle({
      min: (angle * 180 / Math.PI) - 15,
      max: (angle * 180 / Math.PI) + 15,
    });
    
    // Интенсивность
    this.emitter.setFrequency(100 / intensity);
  }
  
  /**
   * Создать восходящий поток (для активной формации)
   */
  createUpwardFlow(
    center: { x: number; y: number },
    radius: number,
    color: number
  ): void {
    this.emitter.setEmitterCenter({
      x: center.x,
      y: center.y,
      width: radius * 2,
      height: 0,
    });
    
    this.emitter.setSpeed({ min: 30, max: 80 });
    this.emitter.setAngle({ min: -100, max: -80 }); // Вверх
    this.emitter.setTint(color);
  }
}
```

---

## 🎯 ПОДСВЕТКА ТОЧЕК ПОДКЛЮЧЕНИЯ

### Алгоритм определения доступности

```typescript
/**
 * Проверка, может ли игрок присоединиться к точке
 */
function checkConnectionPointEligibility(
  point: ConnectionPoint,
  player: PlayerCharacter,
  formation: FormationState
): {
  eligible: boolean;
  reason?: string;
  highlight: 'green' | 'red' | 'gray';
} {
  // Точка уже занята
  if (point.status === 'occupied') {
    return {
      eligible: false,
      reason: 'Точка занята',
      highlight: 'gray',
    };
  }
  
  // Точка заблокирована
  if (point.status === 'locked') {
    return {
      eligible: false,
      reason: 'Точка недоступна',
      highlight: 'gray',
    };
  }
  
  // Проверка уровня культивации
  const minLevel = formation.level - 2;
  if (player.cultivationLevel < minLevel) {
    return {
      eligible: false,
      reason: `Требуется уровень ${minLevel}+`,
      highlight: 'red',
    };
  }
  
  // Проверка элемента (если требуется)
  if (formation.recommendedElements?.length) {
    const hasElement = formation.recommendedElements.includes(player.element);
    if (!hasElement) {
      return {
        eligible: true, // Можно, но не оптимально
        reason: `Рекомендуется: ${formation.recommendedElements.join(', ')}`,
        highlight: 'green', // Всё равно зелёная, но с предупреждением
      };
    }
  }
  
  return {
    eligible: true,
    highlight: 'green',
  };
}
```

### Визуальная индикация

```typescript
/**
 * Обновление визуального состояния точки
 */
function updateConnectionPointVisual(
  point: ConnectionPointVisual,
  eligibility: EligibilityResult,
  isHovered: boolean
): void {
  // Базовый цвет
  let color = eligibility.highlight === 'green' ? 0x22c55e :
              eligibility.highlight === 'red' ? 0xef4444 :
              0x6b7280;
  
  // Свечение при наведении
  if (isHovered && eligibility.eligible) {
    color = 0x4ade80; // Ярко-зелёный
    point.setScale(1.3);
    point.showPulseRing();
  } else {
    point.setScale(1.0);
  }
  
  // Применяем цвет
  point.setColor(color);
  
  // Анимация для доступных точек
  if (point.status === 'available') {
    point.startPulseAnimation();
  }
}
```

---

## 📊 UI ЭЛЕМЕНТЫ

### Прогресс-бар наполнения

```typescript
/**
 * Круговой прогресс-бар для формации
 */
class FormationProgressBar extends Phaser.GameObjects.Container {
  private background: Phaser.GameObjects.Arc;
  private progress: Phaser.GameObjects.Graphics;
  private text: Phaser.GameObjects.Text;
  
  setProgress(current: number, max: number): void {
    const percent = current / max;
    
    // Рисуем дугу
    this.progress.clear();
    this.progress.lineStyle(4, 0x22c55e, 1);
    this.progress.beginPath();
    this.progress.arc(0, 0, 40, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * percent);
    this.progress.strokePath();
    
    // Обновляем текст
    this.text.setText(`${Math.floor(percent * 100)}%`);
  }
}
```

### Информационная панель

```typescript
/**
 * Панель информации о формации
 */
class FormationInfoPanel extends Phaser.GameObjects.Container {
  // Элементы
  private titleText: Phaser.GameObjects.Text;
  private stageText: Phaser.GameObjects.Text;
  private progressContainer: Phaser.GameObjects.Container;
  private participantsList: Phaser.GameObjects.Container;
  private timerText: Phaser.GameObjects.Text;
  
  update(state: FormationState): void {
    // Заголовок
    this.titleText.setText(state.name);
    
    // Этап
    this.stageText.setText(this.getStageText(state.stage));
    
    // Прогресс
    if (state.stage === 'filling') {
      this.showFillingProgress(state.currentQi, state.maxCapacity);
    }
    
    // Участники
    this.updateParticipantsList(state.fillers);
    
    // Таймер
    if (state.stage === 'drawing' || state.stage === 'stabilizing') {
      this.showTimer(state.estimatedCompletion);
    }
  }
  
  private getStageText(stage: FormationStage): string {
    const texts = {
      drawing: '⌛ Прорисовка контура...',
      stabilizing: '🔮 Стабилизация...',
      filling: '⚡ Наполнение Ци',
      active: '✨ Активна',
      depleted: '💀 Истощена',
    };
    return texts[stage];
  }
}
```

---

## 🔧 ИНТЕГРАЦИЯ С СУЩЕСТВУЮЩИМ КОДОМ

### Интеграция с LocationScene

```typescript
// В LocationScene.ts

class LocationScene extends BaseScene {
  // Добавляем менеджер формаций
  private formationManager!: FormationVisualManager;
  
  create(): void {
    // ... существующий код ...
    
    // Инициализация менеджера формаций
    this.formationManager = new FormationVisualManager(this);
  }
  
  update(time: number, delta: number): void {
    // ... существующий код ...
    
    // Обновление формаций
    this.formationManager.update(delta);
  }
}

/**
 * Менеджер визуализации формаций
 */
class FormationVisualManager {
  private scene: Phaser.Scene;
  private formations: Map<string, FormationVisual> = new Map();
  
  constructor(scene: Phaser.Scene) {
    this.scene = scene;
    this.setupEventListeners();
  }
  
  private setupEventListeners(): void {
    // Слушаем события от TruthSystem
    window.addEventListener('formation:create', ((event: CustomEvent) => {
      this.createFormationVisual(event.detail);
    }) as EventListener);
    
    window.addEventListener('formation:update', ((event: CustomEvent) => {
      this.updateFormation(event.detail);
    }) as EventListener);
    
    window.addEventListener('formation:destroy', ((event: CustomEvent) => {
      this.destroyFormation(event.detail.id);
    }) as EventListener);
  }
  
  createFormationVisual(data: FormationCreateEvent): FormationVisual {
    const visual = new FormationVisual(this.scene, data);
    this.formations.set(data.id, visual);
    return visual;
  }
  
  update(delta: number): void {
    for (const visual of this.formations.values()) {
      visual.update(delta);
    }
  }
}
```

---

## 📁 ФАЙЛОВАЯ СТРУКТУРА

```
src/game/formation/
├── FormationVisual.ts           # Основной класс визуализации
├── FormationContourRenderer.ts  # Рендеринг контура
├── ConnectionPointVisual.ts     # Точка подключения
├── QiFlowVisual.ts              # Поток Ци
├── QiParticleManager.ts         # Система частиц
├── FormationInfoPanel.ts        # UI панель
├── FormationProgressBar.ts      # Прогресс-бар
├── FormationVisualManager.ts    # Менеджер
├── constants.ts                 # Константы и цвета
└── index.ts                     # Экспорты
```

---

## 🎯 СЛЕДУЮЩИЕ ШАГИ

1. **Phase 1: Базовая визуализация**
   - Создать `FormationVisual` с контуром
   - Реализовать анимацию "прорисовки"
   - Добавить точки подключения

2. **Phase 2: Взаимодействие**
   - Подсветка при наведении
   - Tooltips для точек
   - События клика

3. **Phase 3: Эффекты**
   - Частицы Ци
   - Потоки от участников
   - Свечение при активации

4. **Phase 4: UI**
   - Информационная панель
   - Прогресс-бар
   - Список участников

---

*Документ создан: 2026-03-20*
*Статус: Ожидает реализации*
