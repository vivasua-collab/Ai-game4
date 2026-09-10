# R15 — Оружие в «руках» (hero + NPC): внедрение фаз A+B

**Дата:** 2026-09-10 (сессия №4, агент Z.ai Code)
**Тип:** feature (визуальная система, RimWorld-подобные слои экипировки)
**План:** `checkpoints/plans/2026-09-10_r15_weapon_visualization_plan.md`
  (одобрен пользователем: дефолтные ответы §6, статичные спрайты,
  процедурные текстуры — PNG-ассеты отдельным планом, анимация — фаза C)
**QA-хук:** `GODOT_WEAPONVIS_DEBUG=1` → VERDICT: PASS

---

## 1. Что сделано

### 1.1. Core: WeaponClassId
- `EquipmentData.WeaponClassId` (string, default "") — ключ визуала.
- `EquipmentGenerator.GenerateWeaponCore` пишет `@class.Id` (7 классов).
- Fallback-цепочка `WeaponVisualCatalog.WeaponClassOf`:
  поле → парсинг `eq_wep_{subtype}_…` из ItemId (сейвы R11) → "sword".

### 1.2. Спрайты (ProceduralSpriteGenerator)
- `CreateWeaponIcon(class, tier, rarity)` — 32×32, вертикально «предмет».
- `CreateWeaponHandSprite(class, tier, rarity)` — 48×48, диагональ 45°
  (1H: рукоять G=(16,36); 2H: диагональ через корпус; лук — вертикально).
- 7 рецептов (dagger/sword/axe/spear/greatsword/bow/staff) × 5 тиров
  (iron/steel/spirit/star/void) × редкость; Legendary+ → золотая
  обводка `ApplyGoldenOutline` (4-соседний проход по альфе).
- Помощники: `WeaponMaterialPalette`, `DrawArc` (лук).

### 1.3. Каталог (WeaponVisualCatalog, новый Adapter/Scene)
- Кэш `(class|tier|rarity)` → WeaponVisuals {Icon, Hand, Key}.
- `Resolve(EquipmentData)` (null-безопасно), `GetOrCreate`, `HandOffset`
  (1H (+14,-6); 2H (+2,-2); лук (-2,+4)), `IsTwoHanded`, `ResetCache`.

### 1.4. Композит игрока (GameWorldController)
- `PlayerMainHand` Sprite2D (ZIndex Player+1) + тело (FlipH по facing).
- `EquipmentChangedEvent` (WeaponMain/Off, фильтр PlayerIdResolver) →
  `RefreshMainHand()`; страховка 0.5с в _PhysicsProcess (экип до _Ready
  при фазах сборки/загрузке сейва — сравнение по ItemId).
- Facing: HandleFreeMovement (клавиши |dx|>0.15; мышь |dx|>8px) →
  `_facingLeft`; позиция MainHand = тело + offset, зеркалирование
  ТОЛЬКО по X (offset → (−X, Y)); FlipH зеркалит содержимое.
- QA-API: MainHandTextureId / MainHandVisible / PlayerFacingLeft /
  DEBUG_SetFacingLeft.

### 1.5. Хотбар + кукла
- HotbarPanel слоты 1-2: TextureRect 32×32 по центру + короткая подпись
  снизу (§6.3 дефолт «иконка+подпись»); OnEquipChanged расширен на
  WeaponMain/Off; QA: WeaponIconTextureId(1|2).
- DollSlotRow (WeaponMain/WeaponOff): TextureRect 24×24 после метки.

### 1.6. NPC overlay (NPCSpriteRenderer)
- `[Inject] IEquipmentDataProvider`; перескан WeaponMain всех живых NPC
  раз в 0.5с (NPC-экип не публикует события — SetEquipment мутует
  провайдер).
- Кэш itemId→(texture, key); negative-кэш для нересолвящихся предметов.
- Facing-гистерезис: |Δx| ≥ 0.05 тайла → обновление, направление
  сохраняется между кадрами.
- Зеркалирование: DrawSetTransform((2cx,0), 0, (-1,1)) + DrawTexture в
  той же позиции — отражает и положение, и содержимое (§16).
- QA: NPCWeaponTextureIdOf(npcId) / NPCWeaponVisibleCount.

### 1.7. QA-хук (WeaponVisSimDebug, новый)
7 шагов: генерация 7/7 WeaponClassId → fallback (легаси/unknown) →
размеры/различимость ключей (класс/тир/редкость) → композит игрока
(стартовый кинжал → копьё → анэкип → меч) → иконки хотбара → NPC
(12 с оружием) → facing.

---

## 2. Верификация

- `dotnet build`: **0 errors** (354 warnings — базовый уровень, в
  изменённых файлах предупреждений нет).
- `GODOT_WEAPONVIS_DEBUG=1`: **VERDICT: PASS** (7/7 шагов, 12 NPC с
  оружием, экип/анэкип/фейсинг).
- Регрессии (все PASS): HOTBAR (панель менялась — обязательна), LOOT,
  COMBAT, SAVELOAD, TRASHDROP, KILLFEED, CONTEXT, REASSEMBLY, STORAGE,
  QUEST — 10/10.
- Xvfb-скриншот (1920×1080, opengl3) + VLM-ревью: «у игрока в правой
  руке серый клинок по диагонали вверх-вправо; в слоте 1 хотбара
  иконка кинжала с подписью "Кинжал"» — композит игрока и хотбар
  подтверждены визуально. NPC в кадр камеры (зум 3x, спаун по всей
  карте 50×50) не попали — их рендер верифицирован QA-ключами (тот же
  код-путь текстур/офсетов); живая проверка — у пользователя (P0).

---

## 3. Docs sync (workflow §3.3)

- SPRITE_CATALOG §7: «Equipped-спрайты (планируется)» → реализовано
  (модель ключей, ориентации, палитры, offsets, fallback); §18 +
  WeaponBaseClass 7×2×5; §20.1 equipped-примечание; §21 открытые
  вопросы (броня/PNG/анимация).
- MODULE_STRUCTURE §2.7 (визуал NPC R15), §4 (EquipmentChanged →
  GameWorldController R15).
- TESTING_RULES §0.1: +GODOT_WEAPONVIS_DEBUG (реестр 30→31).
- SESSION_CONTEXT / SESSION_SUMMARY обновлены (сессия 2026-09-10 №4).

---

## 4. Риски / следующие шаги

- **P0 (вечер, ПК):** живой визуальный QA — оружие у игрока и NPC всех
  7 классов, зеркалирование, 2H-вылеты за тайл; хотбар-иконки 5 тиров.
- Фаза C (по решению): замах-анимация (sprite-swap §17), off-hand щит,
  иконки в лут-окне трупов / ground items.
- PNG-ассеты: генерация + привязка (план пользователя) — смена слоя
  генерации при том же каталоге.
- Броня-слои (R16-кандидат): та же архитектура (overlay + catalog).
- Тонкая настройка HandOffset возможна после живого QA (константы в
  WeaponVisualCatalog.HandOffset).
