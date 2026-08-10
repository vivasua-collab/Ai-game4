# Система фракций и государств

**Версия:** 1.0 | **Дата:** 2026-03-03

---

## 📋 Обзор

Документ описывает систему фракций, сект и государств в мире культивации. Культиваторы стоят над светской жизнью, но секты служат признаком государственной принадлежности и формируют политическую карту мира.

---

## 🏛️ Иерархия принадлежности

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          ИЕРАРХИЯ ПРИНАДЛЕЖНОСТИ                         │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   1. ГОСУДАРСТВО (Nation)                                               │
│      │                                                                   │
│      ├─ Территория (границы, законы)                                    │
│      ├─ Светская власть (император, наместники)                         │
│      └─ Союза сект (официальные и неофициальные)                        │
│                                                                          │
│   2. ФРАКЦИЯ (Faction) — Альянс сект                                    │
│      │                                                                   │
│      ├─ Идеология (праведный путь, демонический, нейтральный)           │
│      ├─ Политическое влияние                                            │
│      └─ Секты-участники                                                 │
│                                                                          │
│   3. СЕКТА (Sect)                                                       │
│      │                                                                   │
│      ├─ Организация культиваторов                                       │
│      ├─ Ресурсы, территории, техники                                    │
│      ├─ Иерархия (ученики, старейшины, мастер)                          │
│      └─ Принадлежность к государству/фракции                            │
│                                                                          │
│   4. КУЛЬТИВАТОР (Character/NPC)                                        │
│      │                                                                   │
│      ├─ Личность (над государственными границами)                       │
│      ├─ Членство в секте (опционально)                                  │
│      └─ Одеяние секты (визуальная принадлежность)                       │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 📊 Модели данных

### Nation (Государство)

```typescript
interface Nation {
  id: string;
  
  // === Идентификация ===
  name: string;                    // "Империя Великого Неба"
  nameEn?: string;                 // "Great Sky Empire"
  description?: string;
  
  // === Тип правления ===
  governmentType: GovernmentType;
  
  // === Территория ===
  capitalLocationId?: string;      // ID столицы
  territoryBounds?: {
    xMin: number;
    xMax: number;
    yMin: number;
    yMax: number;
  };
  
  // === Политика ===
  relations: NationRelations;      // Отношения с другими государствами
  
  // === Ресурсы ===
  resources?: {
    treasury: number;              // Казна
    armyStrength: number;          // Военная мощь
  };
  
  // === Связи ===
  officialSects: string[];         // Официальные секты
  undergroundSects: string[];      // Подпольные секты
}

type GovernmentType = 
  | 'monarchy'      // Монархия (империя)
  | 'republic'      // Республика
  | 'theocracy'     // Теократия (власть секты)
  | 'federation'    // Федерация государств
  | 'warlord';      // Раздробленность (военачальники)
```

### Faction (Фракция/Альянс)

```typescript
interface Faction {
  id: string;
  
  // === Идентификация ===
  name: string;                    // "Альянс Праведного Пути"
  nameEn?: string;
  description?: string;
  
  // === Идеология ===
  ideology: FactionIdeology;
  
  // === Лидерство ===
  leaderSectId?: string;           // Главная секта альянса
  leaderCharacterId?: string;      // Лидер (если персонаж)
  
  // === Состав ===
  memberSectIds: string[];         // Секты-участники
  
  // === Ресурсы ===
  combinedPower: number;           // Суммарная мощь
  
  // === Отношения ===
  relations: FactionRelations;     // Отношения с другими фракциями
  
  // === Территория ===
  controlledTerritories: string[]; // ID локаций
}

type FactionIdeology =
  | 'righteous'     // Праведный путь (традиционная культивация)
  | 'demonic'       // Демонический путь (запретные техники)
  | 'neutral'       // Нейтральный (баланс)
  | 'pragmatic'     // Прагматичный (цель оправдывает средства)
  | 'isolationist'; // Изоляционизм (отстранённость от мира)
```

### Sect (Секта) — расширение существующей модели

```typescript
interface Sect {
  id: string;
  sessionId: string;
  
  // === Существующие поля ===
  name: string;
  description?: string;
  locationId: string;
  powerLevel: number;
  resources?: string;              // JSON
  
  // === НОВЫЕ ПОЛЯ ===
  
  // Принадлежность
  nationId?: string;               // Государство
  factionId?: string;              // Фракция/Альянс
  
  // Типология
  sectType: SectType;
  standing: SectStanding;
  
  // Отношения
  relations: SectRelations;        // Отношения с другими сектами
  
  // Визуальная идентификация
  insignia?: SectInsignia;
  
  // Репутация
  reputation: {
    overall: number;               // -100 до 100
    byNation: Record<string, number>;
    byFaction: Record<string, number>;
  };
}

type SectType =
  | 'orthodox'      // Ортодоксальная (традиционная)
  | 'unorthodox'    // Неортодоксальная (нетрадиционные методы)
  | 'demonic'       // Демоническая (запретные практики)
  | 'neutral'       // Нейтральная
  | 'scholarly'     // Учебная (академия)
  | 'martial';      // Боевая (военная)

type SectStanding =
  | 'official'      // Официальная (признана государством)
  | 'underground'   // Подпольная (скрытая)
  | 'exiled'        // Изгнанная
  | 'nomadic'       // Кочевая
  | 'independent';  // Независимая

interface SectRelations {
  // ID секты -> disposition (-100 до 100)
  sects: Record<string, number>;
  
  // Войны и союзы
  atWar: string[];                 // С кем в состоянии войны
  allied: string[];                // С кем в союзе
  vassalOf?: string;               // Вассалитет
  vassals: string[];               // Вассальные секты
}

interface SectInsignia {
  primaryColor: string;            // Основной цвет одеяния
  secondaryColor?: string;         // Дополнительный цвет
  symbol?: string;                 // Эмодзи символ
  pattern?: string;                // Узор на одежде
  emblemDescription?: string;      // Описание эмблемы
}
```

---

## 🎽 Одеяние секты (Sect Attire)

### Концепция

Одеяние секты — это элемент экипировки, который:
1. Показывает принадлежность культиватора к секте
2. Влияет на отношение других NPC
3. Даёт бонусы (репутация, защита)
4. Может быть снято для скрытия принадлежности

### Модель экипировки

```typescript
interface InventoryItem {
  // ... существующие поля
  
  // === НОВОЕ: Одеяние секты ===
  isSectAttire: boolean;           // Является ли одеянием секты
  sectId?: string;                 // Секта-владелец дизайна
  
  // Визуальные эффекты
  attireVisuals?: {
    primaryColor: string;
    secondaryColor?: string;
    symbol?: string;
  };
  
  // Социальные эффекты
  socialEffects?: {
    dispositionBonusWithSect: number;     // + disposition к членам секты
    dispositionPenaltyWithEnemies: number; // - disposition к врагам секты
    reputationModifier: number;            // Множитель репутации
  };
}
```

### Слоты экипировки

```typescript
// Сектское одеяние занимает слот torso
// Дополнительные элементы:
// - sect_badge — значок секты (accessory слот)
// - sect_sash — пояс секты (accessory слот)

const SECT_ATTIRE_SLOTS = {
  attire: 'torso',           // Основное одеяние
  badge: 'accessory1',       // Значок/эмблема
  sash: 'accessory2',        // Пояс
};
```

### Примеры одеяний

```typescript
const SECT_ATTIRES: Record<string, SectAttire> = {
  // Секта Небесного Меча
  heavenly_sword_robe: {
    id: 'attire_heavenly_sword',
    name: 'Одеяние Небесного Меча',
    sectId: 'sect_heavenly_sword',
    
    attireVisuals: {
      primaryColor: '#4A90D9',     // Небесно-голубой
      secondaryColor: '#C0C0C0',   // Серебристый
      symbol: '⚔️',
    },
    
    socialEffects: {
      dispositionBonusWithSect: 20,
      dispositionPenaltyWithEnemies: -15,
      reputationModifier: 1.1,
    },
    
    stats: {
      defense: 10,
      qiBonus: 50,
    },
  },
  
  // Демоническая секта Кровавой Луны
  blood_moon_robe: {
    id: 'attire_blood_moon',
    name: 'Одеяние Кровавой Луны',
    sectId: 'sect_blood_moon',
    
    attireVisuals: {
      primaryColor: '#8B0000',     // Тёмно-красный
      secondaryColor: '#1a1a2e',   // Чёрно-синий
      symbol: '🌙',
    },
    
    socialEffects: {
      dispositionBonusWithSect: 25,
      dispositionPenaltyWithEnemies: -30,
      reputationModifier: 0.8,     // Репутация падает быстрее
    },
    
    stats: {
      defense: 8,
      qiBonus: 30,
      damageBonus: 5,
    },
  },
};
```

---

## 🗺️ Пример политической карты

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     ПОЛИТИЧЕСКАЯ КАРТА МИРА                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   ┌──────────────────────────────┐   ┌──────────────────────────────┐  │
│   │    ИМПЕРИЯ ВЕЛИКОГО НЕБА      │   │     ТЕОКРАТИЯ ЮЖНОГО        │  │
│   │    (Government: monarchy)    │   │         ПУТИ                 │  │
│   │                              │   │    (Government: theocracy)   │  │
│   │   ┌────────────────────┐    │   │                              │  │
│   │   │ ПРАВЕДНЫЙ ПУТЬ     │    │   │   ┌────────────────────┐    │  │
│   │   │ (ideology:        │    │   │   │ СВЯЩЕННЫЙ СОЮЗ     │    │  │
│   │   │  righteous)       │    │   │   │ (ideology:         │    │  │
│   │   │                    │    │   │   │  righteous)        │    │  │
│   │   │ Секты:            │    │   │   │                    │    │  │
│   │   │ - Небесного Меча  │    │   │   │ Секты:             │    │  │
│   │   │ - Золотого Лотоса │    │   │   │ - Белого Лотоса    │    │  │
│   │   │ - Белого Облака   │    │   │   │ - Священного Огня  │    │  │
│   │   └────────────────────┘    │   │   └────────────────────┘    │  │
│   │                              │   │                              │  │
│   │   ┌────────────────────┐    │   │   ┌────────────────────┐    │  │
│   │   │ НЕЗАВИСИМЫЕ        │    │   │   │ ПОДПОЛЬНЫЕ         │    │  │
│   │   │                    │    │   │   │                    │    │  │
│   │   │ Секты:             │    │   │   │ Секты:             │    │  │
│   │   │ - Одинокого Меча   │    │   │   │ - Кровавой Луны    │    │  │
│   │   │ - Бродячего Ветра  │    │   │   │ - Тёмного Тумана   │    │  │
│   │   └────────────────────┘    │   │   └────────────────────┘    │  │
│   └──────────────────────────────┘   └──────────────────────────────┘  │
│                                                                          │
│   ┌──────────────────────────────────────────────────────────────────┐  │
│   │                      ДИКИЕ ЗЕМЛИ                                  │  │
│   │              (Нет центральной власти)                             │  │
│   │                                                                   │  │
│   │   ┌─────────────────────────────────────────────────────────┐    │  │
│   │   │     ДЕМОНИЧЕСКИЙ АЛЬЯНС                                   │    │  │
│   │   │     (ideology: demonic)                                  │    │  │
│   │   │                                                          │    │  │
│   │   │     Секты:                                               │    │  │
│   │   │     - Кровавой Луны (в изгнании)                         │    │  │
│   │   │     - Пожирающих Душ                                     │    │  │
│   │   │     - Тёмного Солнца                                     │    │  │
│   │   └─────────────────────────────────────────────────────────┘    │  │
│   └──────────────────────────────────────────────────────────────────┘  │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 🔄 Расчёт отношений

### Формула итогового disposition

```typescript
function calculateFinalDisposition(
  npc: NPC,
  player: Character,
  context: InteractionContext
): number {
  let disposition = 0;
  
  // 1. Базовое личное отношение NPC к игроку
  disposition += npc.disposition || 0;
  
  // 2. Отношение к секте игрока (если NPC в секте, игрок в секте)
  if (player.sectId && npc.sectId) {
    const sectRelation = getSectRelation(npc.sectId, player.sectId);
    disposition += sectRelation * 0.5;  // 50% вес
  }
  
  // 3. Отношение к фракции игрока
  if (player.factionId && npc.factionId) {
    const factionRelation = getFactionRelation(npc.factionId, player.factionId);
    disposition += factionRelation * 0.3;  // 30% вес
  }
  
  // 4. Отношение к государству игрока
  if (player.nationId && npc.nationId) {
    const nationRelation = getNationRelation(npc.nationId, player.nationId);
    disposition += nationRelation * 0.2;  // 20% вес
  }
  
  // 5. Модификаторы экипировки (одеяние секты)
  const playerAttire = getPlayerSectAttire(player);
  if (playerAttire) {
    if (npc.sectId === playerAttire.sectId) {
      disposition += playerAttire.socialEffects.dispositionBonusWithSect;
    } else if (areSectsAtWar(npc.sectId, playerAttire.sectId)) {
      disposition += playerAttire.socialEffects.dispositionPenaltyWithEnemies;
    }
  }
  
  // 6. Модификаторы репутации
  disposition += player.reputation.overall * 0.1;
  
  // Ограничение диапазона
  return Math.max(-100, Math.min(100, disposition));
}
```

### Пороги disposition

| Значение | Отношение | Поведение |
|----------|-----------|-----------|
| 80-100 | Дружелюбный | Помощь, торговля со скидкой, обучение |
| 50-79 | Благосклонный | Мирный диалог, базовая торговля |
| 20-49 | Нейтральный | Осторожность, ограниченное взаимодействие |
| -20-19 | Подозрительный | Избегание, отказ в помощи |
| -50--21 | Враждебный | Агрессия при приближении |
| -100--51 | Ненависть | Атака без предупреждения |

---

## 📁 Prisma Schema — расширения

```prisma
// ==================== NATION ====================

model Nation {
  id        String   @id @default(cuid())
  createdAt DateTime @default(now())
  updatedAt DateTime @updatedAt
  
  // === Идентификация ===
  name        String
  nameEn      String?
  description String?
  
  // === Правление ===
  governmentType String  @default("monarchy")
  
  // === Территория ===
  capitalLocationId String?
  territoryBounds   String? // JSON: { xMin, xMax, yMin, yMax }
  
  // === Отношения ===
  relations String @default("{}") // JSON: { nationId: disposition }
  
  // === Ресурсы ===
  treasury     Int @default(0)
  armyStrength Int @default(0)
  
  // === Связи ===
  officialSects   String @default("[]")  // JSON array
  undergroundSects String @default("[]") // JSON array
  sects           Sect[]
  
  @@index([governmentType])
}

// ==================== FACTION ====================

model Faction {
  id        String   @id @default(cuid())
  createdAt DateTime @default(now())
  updatedAt DateTime @updatedAt
  
  // === Идентификация ===
  name        String
  nameEn      String?
  description String?
  
  // === Идеология ===
  ideology String @default("neutral")
  
  // === Лидерство ===
  leaderSectId    String?
  leaderCharacterId String?
  
  // === Состав ===
  memberSectIds String @default("[]") // JSON array
  
  // === Ресурсы ===
  combinedPower Float @default(0)
  
  // === Отношения ===
  relations String @default("{}") // JSON: { factionId: disposition }
  
  // === Территория ===
  controlledTerritories String @default("[]") // JSON array
  
  // === Связи ===
  sects Sect[]
  
  @@index([ideology])
}

// ==================== SECT (расширение) ====================

model Sect {
  // ... существующие поля ...
  
  // === Принадлежность ===
  nationId  String?
  nation    Nation?  @relation(fields: [nationId], references: [id])
  factionId String?
  faction   Faction? @relation(fields: [factionId], references: [id])
  
  // === Типология ===
  sectType String  @default("orthodox")
  standing String  @default("independent")
  
  // === Отношения ===
  relations String @default("{}") // JSON: SectRelations
  
  // === Визуальная идентификация ===
  insignia String @default("{}") // JSON: SectInsignia
  
  // === Репутация ===
  reputationOverall Int    @default(0)
  reputationByNation String @default("{}")
  reputationByFaction String @default("{}")
  
  // === Войны и союзы ===
  atWar  String @default("[]")  // JSON array
  allied String @default("[]")  // JSON array
  
  @@index([sectType])
  @@index([standing])
}
```

---

## 🚀 Следующие шаги

1. **Миграция Prisma схемы** — добавить Nation, Faction, расширить Sect
2. **Создать пресеты государств и фракций** — для стартового мира
3. **Создать пресеты сект с одеяниями** — для каждой секты
4. **API для работы с отношениями** — расчёт disposition
5. **Интеграция с NPC** — применение отношений в взаимодействиях
