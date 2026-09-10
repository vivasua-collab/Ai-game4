# R15 — План: Отображение оружия в «руках» (hero + NPC)

**Дата:** 2026-09-10 09:45 UTC
**Статус:** КОНЦЕПЦИЯ/ПЛАН — на согласование с пользователем (внедрение после одобрения)
**Тип:** feature (визуальная система, RimWorld-подобные слои экипировки)
**Источник:** запрос пользователя 2026-09-10 + SPRITE_CATALOG.md §7 «Equipped-спрайты (планируется)»

---

## 1. Концепция

> «Для каждого типа оружия есть свой спрайт; при экипировке оружия спрайт
> прорисовывается как в слоте быстрого доступа (на основном экране), так и
> должен отображаться на теле персонажа. Как пример RimWorld система».

Подход — **слои поверх базового спрайта** (то, что в спеке названо
«equipped-спрайты»): базовое тело персонажа + overlay-слой оружия в руке.
В сочетании с правилами проекта (процедурные спрайты, PNG-ассетов нет —
SPRITE_CATALOG §19) каждый тип оружия получает **рецепт генерации**.

### 1.1. Два спрайта на тип оружия

| Спрайт | Размер | Ориентация | Где используется |
|---|---|---|---|
| **Icon** | 32×32 | вертикально (как «предмет») | хотбар (слоты 1–2), кукла (WeaponMain/Off), инвентарь, лут-окно, ground items |
| **Hand** | 48×48 | диагональ 45°, рукоять у правой кисти | наложение на тело игрока и NPC |

Один рецепт формы → две ориентации. Ключ кэша: `(weaponClass, materialTier, rarity)`.

### 1.2. Типы оружия (7 классов — EquipmentGenerationTables §10.2)

| Класс | Hand-рецепт (пиксельный эскиз 48×48) | Одноручное? |
|---|---|---|
| dagger | короткий клинок 8px + гарда | 1H |
| sword | клинок 14px + гарда + навершие | 1H |
| axe | древко 12px + лезвие-полумесяц | 1H |
| spear | древко 22px + наконечник | 2H |
| greatsword | широкий клинок 20px + длинная гарда | 2H |
| bow | дуга + тетива (в левой руке, вертикально) | 2H |
| staff | древко 24px + навершие-кристалл | 2H |

Цвет = материал (Tier 1–5): железо (серо-стальной) → сталь (светлее) →
spirit iron/нефрит (зелёный отлив) → star metal (сине-белый) → void (тёмный
с фиолетовым). Редкость Legendary+ — тонкая золотая обводка (свечение).

### 1.3. Размещение на теле

- **1H оружие**: у правой кисти (рука уже нарисована в базовых спрайтах:
  `CreatePlayerSprite`/`CreateNPCSprite` — кисти на (cx−6, 30) и (cx+6, 30)).
- **2H оружие**: диагональ через корпус (копьё/посох — от нижнего-левого к
  верхнему-правому; двуручный меч — на плече; лук — в левой руке вертикально).
- **Facing/зеркалирование**: персонаж смотрит «вправо» по умолчанию; при
  движении влево весь композит зеркалится (flipX, SPRITE_CATALOG §16).
  Direction enum (8 направлений) уже есть в PlayerData, но никто не пишет
  его — R15 включает wiring: PlayerModule.SetFacing по MoveDirection.

### 1.4. Что НЕ входит (осознанно)

- Анимация замаха (sprite-swap 2–3 кадра, §17) — отдельная фаза позже.
- Off-hand щит/второе оружие — фаза C (слот WeaponOff рисуем пустым).
- Броня-слои на теле (robe/helmet overlays) — та же архитектура, но
  отдельная задача (R16-кандидат: «Equipped-спрайты брони»).

---

## 2. Текущее состояние (аудит кода 2026-09-10)

| Компонент | Состояние | Файл |
|---|---|---|
| Игрок | один статичный `Sprite2D` (48×48), facing НЕ подключён | GameWorldController.cs:535 |
| NPC | `_Draw()` per-frame, кэш по роли, оружия нет | NPCSpriteRenderer.cs |
| Хотбар слоты 1–2 | ТЕКСТ (`ShortName(NameRu, 6)`), иконок нет | HotbarPanel.cs:480 |
| `CreateEquipmentIcon` | определён, но НИГДЕ не используется | ProceduralSpriteGenerator.cs:207 |
| WeaponClassId | НЕТ поля — только парсинг префикса `eq_wep_{subtype}_…` | EquipmentGenerator.cs:349 |
| `EquipmentChangedEvent` | есть (EntityId, Slot, ItemId, OldItemId) — подходит как есть | InventoryContracts.cs:30 |
| NPC экипировка | есть (NPCModule.ProcessNpcAttacks читает GetEquipped) | IEquipmentDataProvider |
| Direction/Facing | enum 8 направлений + PlayerService.SetFacing — мёртвый код, никто не вызывает | PlayerService.cs:147 |

Вывод: событийная инфраструктура и слоты готовы; не хватает слоя
генерации спрайтов оружия, композита персонажа и wiring facing.

---

## 3. Архитектура решения

### 3.1. WeaponVisualCatalog (новый, Adapter/Scene)

```
ProceduralSpriteGenerator (существующий) — добавить:
  CreateWeaponIcon(weaponClass, materialTier, rarity) → 32×32
  CreateWeaponHandSprite(weaponClass, materialTier, rarity) → 48×48
WeaponVisualCatalog (новый статик):
  кэш Dictionary<(class,tier,rarity), Texture2D> — обе ориентации
  Resolve(EquipmentData) → (Texture2D icon, Texture2D hand)
  WeaponClassOf(EquipmentData) → string  // поле → префикс ItemId → "sword"
```

Engine-agnostic замечание: рецепты — чистые функции Image→Texture, весь
каталог в Adapter (Godot), Core не трогаем (кроме поля данных, см. 3.2).

### 3.2. WeaponClassId в EquipmentData (Core)

- Новое поле `string WeaponClassId = ""` (default — обратная совместимость
  со старыми сейвами R11: пусто → fallback-парсинг ItemId → generic sword).
- EquipmentGenerator пишет `WeaponClassId = @class.Id` при генерации.

### 3.3. Композит игрока (GameWorldController)

```
PlayerVisual (Node2D) — вместо одиночного _playerSprite
├── Body (Sprite2D)                 — CreatePlayerSprite()
└── MainHand (Sprite2D)             — hand-спрайт, offset (+10, +6)
```

- `EquipmentChangedEvent(Slot==WeaponMain)` → RefreshMainHand() (смена
  текстуры / скрытие при пустом слоте).
- `_PhysicsProcess`: flipX по facing (SetFacing уже есть, вызвать из
  PlayerModule.HandleKeyboardMovement + мышиный aim опционально).
- Публичное QA-поле `MainHandTextureId` (строка-ключ) — headless-проверки.

### 3.4. Overlay NPC (NPCSpriteRenderer._Draw)

- После `DrawTexture(body)`: `DrawTexture(hand, pos + offset)` с flip по
  знаку последнего dx движения NPC (NPCState позиция — сравнение с
  предыдущим кадром, локальный кэш).
- Кэш оружия: `Dictionary<npcId, (itemId, Texture2D)>`; перечитывание
  `GetEquipped(npcId, WeaponMain)` — по EquipmentChangedEvent (подписка
  в _Ready) + раз в 30 кадров страховка.
- 2H оружие NPC — те же диагональные рецепты.

### 3.5. Хотбар + кукла

- HotbarPanel.RefreshWeapon: `TextureRect` (иконка 32×32) в слоте 1–2 +
  короткое имя снизу (иконка главнее текста — «спрайт прорисовывается в
  слоте быстрого доступа», запрос пользователя).
- CharacterDollPanel: слоты WeaponMain/WeaponOff — те же иконки.

---

## 4. План внедрения (фазы)

### Фаза A — ядро (одна сессия, ~1 день)
1. `EquipmentData.WeaponClassId` + EquipmentGenerator (+QA: генерация
   7 классов даёт 7 разных WeaponClassId).
2. ProceduralSpriteGenerator: `CreateWeaponIcon` + `CreateWeaponHandSprite`
   (7 рецептов × 5 тиров материала, Legendary-обводка).
3. WeaponVisualCatalog (кэш + Resolve + fallback-парсинг).
4. Композит игрока + EquipmentChangedEvent-wiring + SetFacing wiring.
5. Иконки в хотбаре (слоты 1–2) + кукла.
6. QA: расширить `HotbarSimDebug` или новый `WeaponVisSimDebug`
   (GODOT_WEAPONVIS_DEBUG=1): экип 3 разных классов → 3 разных текстуры
   (QA-ключи), unequip → MainHand скрыт, flip по направлению (public поля),
   build 0 err; Xvfb-скриншот для визуальной проверки (GODOT_SCREENSHOT).

### Фаза B — NPC (вторая сессия)
7. NPCSpriteRenderer overlay + кэш + EquipmentChangedEvent-инвалидация.
8. Flip NPC по направлению движения.
9. QA: NPC с мечом/копьём/луком (спавн с subtype через NPCSpawner),
   headless: текстуры != null, разные ключи по классу.

### Фаза C — опционально (по отдельному решению)
10. Замах-анимация (sprite-swap, §17), off-hand щит, иконки в лут-окне
    трупов и на земле (ground items), броня-слои (R16).

### Синхронизация docs_v2 (вместе с фазой A/B, workflow §3.3)
- SPRITE_CATALOG §7: «Equipped-спрайты (планируется)» → актуализировать
  (7 классов оружия, 2 ориентации, палитры материалов, правила 1H/2H);
  §18 таблица покрытия + WeaponClassId.
- MODULE_STRUCTURE: WeaponVisualCatalog в Adapter/Scene.
- TESTING_RULES §0.1: +GODOT_WEAPONVIS_DEBUG.

---

## 5. Риски/нюансы

- **Zero-GC**: текстуры генерируются один раз и кэшируются; per-frame —
  только DrawTexture/flip (без аллокаций).
- **Старые сейвы**: WeaponClassId="" → fallback-парсинг `eq_wep_sword_…`
  → класс; нераспознано → generic sword icon (не краш).
- **Лук/посох длинные (2H)**: диагональ 48×48 может вылезать за тайл —
  допустимо (ZIndex Objects, соседний тайл перекрывается телом NPC выше
  по Y — стандарт 2.5D-сортировки уже есть в рендерах).
- **NPC flip**: движение дёргается по тикам → сглаживание: flip только при
  |dx| ≥ 2 тайлов между кадрами (гистерезис).

---

## 6. Вопросы к пользователю (ответы по умолчанию — в скобках)

1. Двуручное: диагональ через корпус, как описано? (да)
2. Лук рисовать вертикально в левой руке? (да; натянутость — фаза C)
3. Иконка в хотбаре ЗАМЕНЯЕТ текст или иконка+подпись? (иконка + короткая
   подпись под ней)
4. Анимация замаха — сейчас или позже? (позже, фаза C)
5. Палитра материалов: 5 тиров как в §1.2? (да)

---

## 7. Definition of Done (фаза A+B)

- [ ] Экип меча/копья/лука: у игрока в руке и в хотбаре виден СВОЙ спрайт
- [ ] NPC с оружием: overlay в руке, flip по движению
- [ ] Unequip: рука пустая, слот хотбара — номер
- [ ] 7 классов × материалы дают различимые спрайты
- [ ] QA-хук PASS + скриншот Xvfb + build 0 err
- [ ] docs_v2 синхронизированы
