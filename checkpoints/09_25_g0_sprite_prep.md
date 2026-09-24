# G0 — Подготовительные задачи спрайтов (директива 25.09)

**Дата:** 2026-09-25 · **Раунд:** G0 · **Директива владельца:**
«Выполни все подготовительные задачи для спрайтов. Если они есть.
Генерация и доставка спрайтов будет выполнена позже.»

**Предыдущий раунд:** 938cda3 (GFX-PLAN-0924 — документация плана).
**Этот раунд:** код-инфраструктура по точкам I-1/I-2/I-5/I-7/I-11 плана,
манифест, инструменты доставки, QA-сим. Генерация спрайтов — НЕ выполнялась
(директива).

---

## §0. Что сделано (5 новых файлов кода, 5 правок, инструменты)

| Компонент | Артефакт | Суть |
|---|---|---|
| I-2 | `SpriteSheetCache.cs` (НОВЫЙ) | PNG→ImageTexture без editor-импорта (`Image.LoadFromFile` — доставка = копирование файла, работает в headless). Реестр 24 анимаций (fps/loop по плану §5.1/§6): player 13 / npc 6 / animal 5. Кадры = W/64 (авторитет PNG). Негативный кэш, ResetCache, LoadSheetDirect (QA/вьюер), TryLoadTexture (иконки/оружие). Битая геометрия → PrintErr + fallback (не краш). |
| I-1 | `PlayerAnimator.cs` (НОВЫЙ) | Sprite-swap: Sprite2D Hframes/Vframes/Frame в GWC._PhysicsProcess. Приоритет: death > melee > bow > hit > meditate > run/walk > idle. One-shot длительности синхронны коду: melee 0.42с = MainHandSwingSec, bow 0.25с, hit 0.17с; death — hold до NotifyRespawn. walk/run fps × speedMult (gameSpeed/Shift/вес; клэмп 0.25..4). Пауза времени = стоп-кадр (Update гейтится IsPaused). Fallback: листа нет → процедурная 48×48 КАК СЕЙЧАС. D2-готовность: Row=2 (side). |
| I-11 | `WeaponVisualCatalog.cs` (правка) | GetOrCreate PNG-первый: `equipment/icons/weapon_{class}_{tier}.png` + `equipped/weapon_hand_{class}_{tier}.png`; нет файла → процедурный R15. Ключи кэша `class\|tier\|rarity` НЕ менялись (контракт §5 PROCEDURAL_SPRITES). PngHandCount/PngIconCount для QA. |
| I-5 | `NPCSpriteRenderer.cs` (правка) | ResolveNpcAnim: npc_{role}_{kind} → npc_base_{kind} → процедурный кэш. walk-детект = дельта позиции (порог 0.02 тайла); melee = _npcSwings (R16); idle = медленный цикл; рассинхрон толпы = хэш id. Отрисовка листа — DrawTextureRectRegion (регион кадра). Звери-NPC (Quadruped) — animal_{species}_{kind}. QA: GetNpcAnimInfo/NpcPngAnimatedCount. |
| I-7 | `AnimalSpriteRenderer.cs` (правка) | ResolveAnimalAnim: animal_{species}_{walk/idle}; walk-детект — дельта (дискретный шаг зверя). Тот же региональный рендер. |
| Интеграция | `GameWorldController.cs` (правка) | Создание аниматора в SetupWorld; тик в _PhysicsProcess (после позиций, гейт паузы); снимок движения (_lastMoving/_lastRunning/_lastSpeedMult) в HandleFreeMovement (клавиатура/мышь/покой/пауза); нотификации: OnAttackIntentForSwing (melee/ranged), OnPlayerDamaged, OnPlayerDeath, RespawnAfterDeath; QA: PlayerAnimId/Frame/IsPng/FrameCount/PlayerAnimatorForQA; env-хук GODOT_ANIMQA_DEBUG. |
| QA-сим | `SpriteAnimSimDebug.cs` (НОВЫЙ) | 7 шагов + вердикт (см. §1). |
| Манифест | `game/resources/sprites/sprites_manifest.json` (НОВЫЙ, 221 позиция) | Контракт доставки: файл/кадры/fps/loop/фаза (G1:29, G2:8, G3:33, G4:146, G5:5). Генератор `tools/sprites/make_manifest.py`. Рантайм его НЕ читает (fps/loop — реестр кода; кадры — размеры PNG) — манифест для доставки/валидатора/людей. |
| Инструменты | `tools/sprites/` (НОВЫЙ) | `slice_sheet.py` (§6.2 промпт-дока: вырез фона по углу, сетка с авто-детектом разделителей, bbox-кроп, NEAREST-даунскейл, нормализация 64×64 с базой y=58, полоса/D2-сетка, диагностика базовой линии ±2px); `validate_sprites.py` (приёмка: размеры/кадры/прозрачность/базовая линия/ширина тела; отсутствующие ≠ FAIL — поэтапная поставка); `make_placeholders.py` (QA-листы). |
| Каталоги | `game/resources/sprites/` (НОВЫЙ) | characters/{player,npc}, animals, corpses, equipment/{icons,equipped}, items, _qa + README (контракт имён/условий/проверки). |
| Доки | SPRITE_DELIVERY.md (НОВЫЙ) | Регламент доставки: готовое (§1), процедура (§2), семантика fallback (§3), формат (§4), именование (§5), остаток задач (§6), QA-доступ (§7). + правки: SPRITE_ANIMATION_PLAN §11 (статусы G0), PROCEDURAL_SPRITES §5 (реализовано), TESTING_RULES §0.1 (сим №21), qa_regression.sh (+ANIMQA). |

## §1. Пруфы

- **ANIMQA (GODOT_ANIMQA_DEBUG=1): VERDICT PASS** — лог
  /tmp/animqa_run5.log: A) fallback (player_idle/walk → null, попытки
  кэша = 2), B) загрузчик QA-плейсхолдеров (полоса 384×64: frames=6,
  rows=1; сетка 384×320: rows=5; FrameAt-математика: 0→кадр0, 0.5→кадр3,
  ряд 3 → 3×6+3; тихий null на несуществующий путь), C) аниматор
  (fallback → override → IsPng=true, кадры прокручиваются f1=0→f2=1;
  melee one-shot истекает 0.42с; death-hold; respawn; возврат fallback),
  D) оружие (ключи sword\|1\|Common стабильны, 32/48, PNG-счётчики 0),
  E) NPC (fallback, PNG-счётчик рендерера 0), F) звери (wolf-сэмпл),
  G) реестр 13/6/5.
- **Регрессия: 21/21 PASS** (qa_regression.sh; +ANIMQA как №21) —
  /tmp/qa_reg_g0.log.
- **Build: 0 ошибок.**
- **Визуальный smoke (нулевая регрессия):** Xvfb :99 (вручную, без
  xauth — протокол R37) + opengl3 + GODOT_SCREENSHOT → /tmp/g0_screen.png
  1920×1080; VLM: мир (тайлы вода/песок/трава) ✔, игрок в фиолетовой робе
  с мечом ✔, HUD ✔. Fallback рендерит те же процедурные текстуры (кадр
  0 из 1), артефактов нет.
- **Инструменты:** манифест 221 позиций; плейсхолдеры (baseline OK по
  всем рядам); слайсер round-trip (ровная раскладка → 384×64, базовая
  линия 58/58; неровная → диагностика WARN — ловит риск §12-2);
  валидатор (0 доставленных → PASS).

## §2. Ключевые решения

1. **`Image.LoadFromFile` вместо ResourceLoader** — PNG работает без
   editor-импорта (.import): «доставка = положить файл». Критично для
   headless-QA и workflow «генерация → слайсер → каталог».
2. **Нулевая визуальная регрессия по построению** — fallback-путь
   рендерит те же процедурные текстуры (Hframes/Vframes=1, Frame=0);
   дистрибуция текстур не менялась.
3. **fps/loop — в коде (реестр), кадры — из PNG** — манифест остаётся
   контрактом доставки, рантайм детерминирован и не парсит JSON.
4. **One-shot длительности фиксированы** (0.42/0.25/0.17с) — синхрон с
   кодовыми таймерами боя независимо от числа кадров доставленного листа.
5. **NPC-кадры без нарезки текстур** — DrawTextureRectRegion по листу
   (кэш = 1 текстура на анимацию, не N кадров).
6. **Отсутствие файла ≠ ошибка** (негативный кэш, тихий fallback) —
   поэтапная доставка фазами G1–G5 безопасна.

## §3. Дефекты/инциденты раунда

- Сим-баг (найден и починен в раунде): инвертированное условие step3
  (`!startPng` — сбрасывало pass на ожидаемом fallback) + QA-свойство
  Frame проверяло _sheet вместо активного (override) листа.
- cold_start.sh тихо умер на шаге 1 (curl) — .NET уже стоял в персистентной
  зоне, собрано вручную; Xvfb стартует вручную (протокол R37).

## §4. NEXT

1. **Доставка спрайтов** (владелец/генерация) — регламент
   SPRITE_DELIVERY.md §2: промпты → слайсер → валидатор → ANIMQA →
   скрин-ревью. MVP: player_idle/walk/meditate/death + npc_base_idle/walk
   + animal_wolf_* (фазы G1–G2, ~30-40 генераций).
2. Код-раунды остатка: I-3 (октантный facing), I-8 (иконки UI), I-9/I-10
   (броня), I-4 (дэш/сон), cast-триггер (G3).
3. Незакрытый R37-хвост: тост EquipmentBlockedEvent (U2 «сапоги») +
   Xvfb-скрины сползания чит-панели → P2-пачки → интеграционная фаза.
