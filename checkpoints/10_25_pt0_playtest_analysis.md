# Чекпоинт: Плейтест-0 (внеплановая базовая проверка) — анализ 6 дефектов

**Дата:** 2026-10-25 05:40 UTC
**Сессия:** PT-0 (после сброса песочницы; ночная сессия заявляла коммит 9ebd018 — не существует ни локально, ни на GitHub, работа потеряна; эпизод переделан с нуля)
**Тип:** audit (диагностика) — фиксы отдельным эпизодом

---

## Контекст

Владелец начал серию плейтестов (раунд 2). Плейтест-0: базовая проверка меню
техник (T) на large_world, L9, чит-меню. 6 находок + скриншоты (upload/
pasted_image_1792875{230065,483428,93569}.png от 24.10 20:54–21:06 UTC) +
лог Godot (upload/logs.txt, 866 строк, RTX 5070 Ti / Vulkan — машина владельца).

Входы анализа: VLM-скрины (3 + зумы), grep-аудит кода, GDScript-тест
TabContainer, Python-реплика xorshift64* (2000 сидов), UIShot-репродьюс
(Xvfb:99 + opengl3 — метод гейт-сессии 24.10 работает).

## Найденные корневые причины (все 6 подтверждены)

### PT0-1. Лимит библиотеки техник
- `TechniqueService.cs`: `LibraryBaseSize=8`, `LibraryPerLevel=2` → L9=24.
- Введено **2026-08-28, коммит 2fcf9db** («Библиотека + Лодаут», тогда —
  решение пользователя; сейчас владелец требует лимит убрать).
- Пруф из лога: «Библиотека: 20/24» (скрин) → гранты «3/3 → 1/3 → 0/3»
  (24/24 — тишина: причина не показана игроку).
- РЕШЕНИЕ (владелец): кап убрать.

### PT0-2. Вкладки «L9-L8» → @VBoxContainer@NNNN
- `TechniqueBookWindow.RebuildTabs`: `child.QueueFree()` НЕ удаляет узел
  до конца кадра → при повторном RebuildTabs В ТОМ ЖЕ КАДРЕ новые вкладки
  с теми же именами конфликтуют → Godot даёт автореимя `@VBoxContainer@N`.
- ДОКАЗАНО чистым GDScript-тестом (tmp/tabtest): rebuild ×2 в одном кадре
  → `'Все' 'L9' 'L8' 'Архив' 'Свитки' '@VBoxContainer@11' '@VBoxContainer@13'…`
- Сценарий пользователя: книга открыта + 3 LearnTechnique за один клик
  (OnLearned → RebuildTabs ×3 в кадре) и/или level-up + открытие.
- ФИКС: `_tabs.RemoveChild(child)` (имя освобождается сразу) + `QueueFree()`.

### PT0-3. Меч убивает на 5-6 «клетках»
- Предмет КОРРЕКТЕН: меч `AttackRange=1` (тайл), скрин: «Дальность: 1 (ближний)».
- Причина: базовая melee-атака игрока = `PlayerCombatAdapter.AttackRangeTiles
  = 2.5f` ТАЙЛА = **5 МЕТРОВ** (`TILE_SIZE_M=2`, Constants.cs:1586) — меч
  бьёт дальше, чем любая melee-техника (1.5–2.0 м!). На резолве pending-каста
  (0.5 с) гейта дистанции НЕТ → цель уходит → добивание на ~6 м.
- Системный дефект: СМЕСЬ ЮНИТОВ — предметы/гейты считают ТАЙЛАМИ,
  техники — МЕТРАМИ (дисплей «Дальность: {Range}м»), CheatPanel пишет
  «дальность 18 м» про 18 ТАЙЛОВ (=36 м).
- ФИКС: гейт melee-дистанции на резолве + честные юниты (см. PT0-4).
  ЛОР-вариант владельца (летающий меч) НЕ требуется — это баг, не фича.

### PT0-4. «Все L9-техники 1-3 м, дальних нет»
- Генератор ЗДОРОВ: Python-реплика xorshift64* точного флоу
  DetermineSubtype (2000 сидов): RangedProjectile 22.3%, RangedBeam 23.4%,
  RangedAoe 9.4% → 45% дальних среди Combat.
- Выборка владельца неудачна: CheatPanel грантит типы циклом
  [Combat, Defense, Healing, Movement, Support, Sensory] — Combat только
  каждый 6-й; из Combat 55% melee → P(все melee у ~3 Combat-грантов) ≈ 17%;
  плюс библиотечный кап (PT0-1) не давал добрать дальние.
- НАЙДЕН сопутствующий ДЕФЕКТ ЮНИТОВ: `BaseRangeBySubtype` записан в
  «метрах», которые задумывались как тайлы (MeleeStrike 1.5 м = 0.75 тайла!)
  → флор `PlayerTechniqueCaster.MinAttackRangeTiles=2` (тайла) спасает
  игру, но дисплей «Дальность: 2.0м» врёт (реальный доступ 4 м).
- ФИКС: юнит-нормализация (BaseRangeBySubtype × TILE_SIZE_M → честные метры:
  melee 3–4 м, снаряды 30 м, луч 40 м, AoE 20 м) + сообщение причины при
  отказе гранта. Числа согласовать с QA-симами (COMBAT_SIM и др.).

### PT0-5. CheatPanel: сползание/обрезание
- VLM-скрин 3: кнопки обрезаны («Оружие слуе», «Утеч», «…Расконки…»),
  контент выше окна, полоса прокрутки. Код не вскрыт — эпизод 2.

### PT0-6. ProjectileRenderer: Invalid polygon data, triangulation failed
- Стек: DrawTriangle (152) ← DrawFlight (138) ← _Draw (115).
- DrawPolygon без guard: NaN/Inf или дегенеративные вершины (нулевая
  площадь) роняют нативную триангуляцию. Источник — битые позиции
  сущностей (ResolvePixelPos без валидации).
- ФИКС: guard в DrawTriangle (пропуск не-конечных/дегенеративных) +
  фильтр From/To в OnDamageApplied (не-конечные/абсурдные координаты).

## Артефакты
- GDScript-тест: /tmp/tabtest (Main.gd — воспроизводит @VBoxContainer).
- UIShot-репродьюс: /tmp/pt0_before_{inventory,technique,character}.png.
- Python-реплика: /tmp/subtype_dist.py.
- Скрины владельца: upload/pasted_image_1792875*.png, лог: upload/logs.txt.

## Следующие шаги (эпизод 2 — фиксы)
1. PT0-2: RebuildTabs — RemoveChild+QueueFree; проверить аналоги (другие окна).
2. PT0-1: убрать лимит библиотеки (LibraryCapacity → без капа; UI-счётчик
   «Библиотека: N»; причина отказа «библиотека полна» исчезает).
3. PT0-3/4: юнит-нормализация дальностей + melee-гейт на резолве pending-каста.
4. PT0-5: CheatPanel — размер под контент/скролл (эпизод 2 после вскрытия).
5. PT0-6: guard треугольников + фильтр позиций.
6. QA: dotnet build 0 err; регрессия qa_regression.sh; UIShot после-скрины.

## Файлы
- game/src/Modules/Combat/TechniqueService.cs (лимит: 59-62, 208+)
- game/src/Adapter/UI/TechniqueBookWindow.cs (RebuildTabs: 338+, 613, 796)
- game/src/Modules/Player/PlayerCombatAdapter.cs (AttackRangeTiles=2.5: 61, 227)
- game/src/Modules/Generator/TechniqueGeneratorService.cs (BaseRangeBySubtype: 103)
- game/src/Adapter/Scene/ProjectileRenderer.cs (DrawTriangle: 150-153)
- game/src/Adapter/UI/CheatPanel.cs (эпизод 2)
- game/src/Core/Data/Constants.cs:1586 (TILE_SIZE_M=2)
