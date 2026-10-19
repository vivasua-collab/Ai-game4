# R29: Графика боя (VFX) — план эпизода

> ✅ РЕАЛИЗОВАНО 2026-09-21 (полностью, все пункты плана). Итог:
> AoeFxRenderer (DrawFan-фикс: steps+2 — off-by-one рвал _Draw),
> HomingProjectileRenderer (буст читаемости: ядро 13px/сердцевина 7px/
> ореол/хвост — бледный Air-болт не читался на llvmpipe-кадрах),
> аура удержания в TechniqueEffectRenderer, ElementPalette, подавление
> дублей PublishSuccess, QA 3k + GODOT_VFX_SHOT (VERDICT PASS в headless
> И opengl3; VLM подтвердил все 3 эффекта). Побочно захарденжен
> CombatSimDebug: TeleportPlayer (SetPosition-откат к _visualPosition),
> чистые зоны в границах карты, немедленный замер Threats (затухание),
> dmg-sum-критерий урона (HP-кламп разрушенной части), multi(b)
> attempt-0. Скриншоты: screenshots/r29_vfx_{aoe,homing,hold}.png.

Дата: 2026-09-21. Источник: роадмап дизайн-дока R23 §6.7
(`2026-09-20_r23_multicombat_aoe_design.md`), строка R29+:
«Графика боя (VFX): AoE-вспышки/формы, полёты снарядов, аура-эффекты
удержания». События-носители уже изданы в R25/R28 ИМЕННО под эту фазу:

- `AoeImpactEvent` (R25) — форма/эпицентр/радиус/стихия/TargetIds;
- `ProjectileSpawnedEvent` (R28) — точка выпуска/цель/скорость/стихия;
- `HeldTechniqueChangedEvent` (Stage 1) — стихия «для цвета ауры»
  (комментарий контракта прямо обещает «Adapter (визуал ауры)»).

## 0. Текущее состояние (аудит 21.09)

| Потребность | Кто сегодня | Пробел |
|---|---|---|
| AoE-вспышка | TechniqueEffectRenderer «Expanding» — generic-круг, БЕЗ формы | игрок видит кольцо-заглушку; NPC-кастеры не видны вовсе (CastResult публикует ТОЛЬКО PlayerTechniqueCaster) |
| Полёт хоуминга | TechniqueEffectRenderer «Directional» — прямая фейк-линия за 0.45с | реальный снаряд ЗАКРУЧИВАЕТСЯ (turn-budget) и бьёт при контакте — фейк врёт |
| Аура удержания | НЕТ (только хотбар R26) | мир не показывает, что техника запаркована |

Паттерн-эталон: `StrikeFxRenderer` (R16) — Node2D + _Draw, пул структур,
`MaxConcurrent`, **QA-статики инкрементятся в обработчике события**
(headless-прогоны без рендера всё равно считают), позиционный резолв
NPC/игрок/зверь (паттерн DamageNumberRenderer D5).

## 1. Решения

### 1.1 AoeFxRenderer (новый, Adapter/Scene)
- Подписка: `AoeImpactEvent`. ZIndex = Objects+2 (уровень свипов: над
  телами/барами, под цифрами +3 и стрелами +4 — вспышка НЕ перекрывает
  боевой текст).
- Рисует по ФОРМЕ события (все координаты — тайлы → пиксели, TILE_PIXELS):
  - **Circle**: эпицентр → расширяющееся кольцо до R + заливка-вспышка;
  - **Cone**: клин от Origin (кастера) к направлению Origin→Epicenter,
    полуугол HalfAngleDeg, дуга на радиусе + 2 грани + веер-заливка;
  - **Semicircle**: полукруг от Origin по направлению к Epicenter;
  - **Line**: полоса-луч Origin→Epicenter (длина = RadiusTiles) шириной
    ~1 тайл + яркое ядро-линия.
- Цвет — стихия (общий `ElementPalette`, см. 1.4). Жизнь ~0.5с,
  квадратичное затухание (паттерн Strike).
- **Маркеры целей**: маленькое расширяющееся кольцо на каждой цели из
  TargetIds (позиции через NPC ∪ звери ∪ игрок) — «у каждого своя
  вспышка» + цифры урона уже рисует DamageNumberRenderer.
- Пул ≤16 залпов (анти-спам толпы), QA-статики `TotalVolleys` /
  `TotalTargetFlashes` — в обработчике.

### 1.2 HomingProjectileRenderer (новый, Adapter/Scene)
- Подписка: `ProjectileSpawnedEvent`. ZIndex = Objects+4 (паттерн
  ProjectileRenderer: «снаряд читается поверх боевого текста»).
- Полёт — ПРЕДСТАВЛЕНИЕ, не источник истины (симуляция живёт в
  CombatService-тике; шапка TechniqueEffectRenderer уже декларирует
  принцип). Визуальный снаряд:
  - стартует в FromX/Y (милли-тайлы → пиксели), летит со скоростью
    события (тайлов/сек → пиксели/сек, real-time);
  - наведение: каждый кадр доворачивает вектор к ТЕКУЩЕЙ позиции цели
    (ре-резолв через сервисы) с визуальным лимитом поворота ~π/4 в сек —
    «закручивание» читается, кайт виден;
  - контакт (~0.8 тайла) → вспышка-кольцо 0.12с; цель пропала → летит в
    последнюю точку и тает; потолок жизни 5с (как сим).
- Рисунок: ядро-круг цвета стихии + белая сердцевина + хвост из 3
  затухающих кругов (паттерн Directional).
- Пул ≤16, QA-статик `TotalLaunches` — в обработчике.

### 1.3 Аура удержания (расширение TechniqueEffectRenderer)
- Подписка: `HeldTechniqueChangedEvent` (EntityId == player). TechniqueId
  ≠ "" → аура включена (цвет = Element события); "" → выключена.
- Рисунок: пульсирующее кольцо-обод вокруг игрока цвета стихии + 4
  вращающиеся искры-руны (гармонично с DrawMeditationAura, но плотнее и
  цвет техники; НЕ конфликтует с медитацией/щитом — отдельные слои
  кольца: радиус ~34px против медитации 40px и щита 52px).
- QA-статик `TotalHoldAuraChanges` — в обработчике.

### 1.4 ElementPalette (новый общий статик, Adapter/Scene)
- `ElementPalette.ToColor(Element)` — единственный источник цветов стихий
  для мировых VFX (сейчас приватная копия в TechniqueEffectRenderer).
  Рефактор TechniqueEffectRenderer.ElementColor → ElementPalette (чисто).

### 1.5 Подавление дублей визуалов (PlayerTechniqueCaster.PublishSuccess)
- `RangedAoe` → НЕ публиковать CastResult-визуал «Expanding» (форма
  теперь у AoeFxRenderer; иначе игрок видит generic-кольцо + форму).
- `RangedProjectile && IsHoming` → НЕ публиковать «Directional»-фейк
  (полёт теперь у HomingProjectileRenderer; фейк-прямая врёт о закрутке).
- CastResult-событие остаётся для прочих подтипов (там потребитель один
  — TechniqueEffectRenderer; UI тосты идут другими событиями).
- NPC-касты дублей не имеют (NPCCombatAdapter CastResult не публикует) —
  новые рендереры дают NPC-вспышки ВПЕРВЫЕ (строгое улучшение).

## 2. QA (COMBAT_SIM 3k + скриншоты)

### 2k (headless, как 3h/3i/3j)
- (a) AoE-залп: GenerateAoe(Cone) + 2 NPC перед игроком → ExecuteAttack
  с aim → `TotalVolleys` +1, `TotalTargetFlashes` ≥ +2;
- (b) хоуминг-пуск: GenerateHoming + NPC на 5 тайлов → ExecuteAttack →
  `TotalLaunches` +1 (урон отложен — уже гард 3j, здесь рендер-проводка);
- (c) аура: AuraHoldService.Hold/Release напрямую → `TotalHoldAuraChanges`
  +2 (DI-проводка события → рендерер);
- Итог в общий VERDICT (`vfxOk`).

### Скриншоты (GODOT_VFX_SHOT, паттерн GODOT_SCREENSHOT_MENU)
- В 3k: env `GODOT_VFX_SHOT=/path/prefix` → volley → await 0.28с →
  `_aoe.png`; homing launch → await 0.45с → `_homing.png`; Hold → await
  0.35с → `_hold.png`. Сохранение через GetViewport().GetTexture()
  .GetImage().SavePng (прецедент ContextMenuSimDebug).
- Запуск: Xvfb :99 + opengl3 (прецедент R16/R19) → VLM-разбор кадров
  (пользователь без локальных тестов до конца дня — визуальную честность
  проверяем мы, screenshots/ — в git).

## 3. Регресс-риски
- TechniqueEffectRenderer: рефактор цвета — поведение идентично (чистый
  перенос). Новая подписка — dispose-токен по паттерну.
- PlayerTechniqueCaster: подавление 2 веток — PublishSuccess вызывается
  только из FireTechnique (grep-проверено), потребитель один.
- SceneBuilder: 2 новых ребёнка ПОСЛЕ StrikeFx (порядок дерева — только
  для равных ZIndex; +2/+4 не пересекаются со свипами +2? StrikeFx тоже
  Objects+2 → AoeFx после StrikeFx в дереве = рисуется поверх при равном
  ZIndex — вспышка важнее свипа, ОК).
- Сейвы: рендереры эфемерны, статики QA — вне сейв-блока. Ноль миграций.

## 4. Доки
COMBAT_SYSTEM §4.7 (VFX-фаза: события→рендереры, слои), TESTING_RULES
(3k + GODOT_VFX_SHOT в реестр), SPRITE_CATALOG §22+ (мировые VFX-слои),
дизайн-док §6.7 R29 → ✅, worklog.
