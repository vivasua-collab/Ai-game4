# План: Сборка тела — визуализация + животные

**Дата:** 2026-08-22 10:00 UTC
**Task ID:** BODY-IMPL-PLAN

---

## Контекст

Backend body system ПОЛНОСТЬЮ реализован:
- BodyPart.cs (273 LOC) — dual HP, states, damage/heal
- BodyService.cs (932 LOC) — Initialize, ApplyDamage, GetPartState, GetAllParts
- BodyFactory.cs (81 LOC) — CreateBody from template
- BodyTemplateProvider.cs (310 LOC) — 10 templates (Humanoid, Quadruped, Bird, etc.)
- SpeciesRegistry.cs (142 LOC) — 11 species (human, wolf, tiger, dragon, etc.)
- Player body initialized at startup (BodyModule.Start → BodyService.Initialize)

**Не реализовано:**
- Визуализация частей тела (BodyStatusPanel)
- Character Sheet UI (View #12, hotkey C)
- Простые животные на тестовой карте

---

## План реализации

### Phase A: BodyStatusPanel — схематическое отображение тела

**Цель:** Показать части тела игрока с HP и состояниями.

**Компоненты:**
1. `BodyStatusPanel.cs` (Adapter/UI) — Godot Control
2. Схематический силуэт: гуманоид (голова круг, торс прямоугольник, 4 конечности линии)
3. Каждая часть окрашена по состоянию (Healthy=зелёный, Bruised=жёлтый, Wounded=оранжевый, Disabled=красный, Severed=серый)
4. HP бары: RedHP (функциональный) + BlackHP (структурный)
5. Подписка на BodyPartDamagedEvent/HealedEvent/SeveredEvent для live updates
6. Inject IBodyService для чтения GetAllParts()

**Morphology support:**
- Humanoid: голова + торс + сердце + 2 руки + 2 кисти + 2 ноги + 2 ступни
- Quadruped: голова + торс + сердце + 4 ноги + хвост
- Bird: голова + торс + сердце + 2 крыла + хвост + 2 ноги
- (другие морфологии — позже)

### Phase B: Character Sheet UI

**Цель:** Окно Character Sheet (hotkey C) с телом, статами, культивацией.

**Компоненты:**
1. `CharacterSheetWindow.cs` — XL окно (как Inventory)
2. Layout: слева BodyStatusPanel, справа статы/перки/культивация
3. Toggle по клавише C (как B для инвентаря)
4. Pause при открытии (как инвентарь)

### Phase C: Простые животные на тестовой карте

**Цель:** 3-5 животных (волк, олень, кролик) бродят по карте.

**Компоненты:**
1. `AnimalEntity.cs` — простой NPC (без AI, случайное блуждание)
2. `AnimalSpawnPhase.cs` — спавн животных при старте
3. `AnimalSpriteRenderer.cs` — процедурные спрайты (разные цвета per species)
4. Body assembly для животных: BodyService.Initialize с Quadruped morphology
5. Per-entity body parts: `IBodyDataProvider.SetBodyParts(entityId, parts)`

**Животные:**
- Wolf (Quadruped, Organic, Medium) — агрессивный
- Deer (Quadruped, Organic, Medium) — мирный
- Rabbit (Quadruped, Organic, Small) — мирный, быстрый

---

## Порядок выполнения

1. **Phase A** — BodyStatusPanel (схематическое тело игрока)
2. **Phase B** — Character Sheet UI (hotkey C)
3. **Phase C** — Простые животные + спавн
4. Build + verify + commit
