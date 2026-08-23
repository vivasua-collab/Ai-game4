# Чекпоинт: Этап 5 — Создание формаций игроком + жизненный цикл + эффекты

**Дата:** 2026-08-23
**План:** `checkpoints/08_23_qi_impl_plan.md` (этап 5)
**Статус:** ✅ Завершён. Smoke-тест: Drawing→Filling→Active (800/800 Ци).

## Что сделано

### 1. FormationService расширения
- `StartDrawing(formationId, casterId, posX, posY)` — формация рисуется в позиции создателя;
  свойства `CurrentFormation`, `PositionX/PositionY` (для визуализатора, этап 6).
- `AutoFillTick(delta)` — автонаполнение создателем-одиночкой со скоростью
  conductivity ед/сек (FORMATION_SYSTEM §9.1 «каждый вносит 100% способности»),
  double-аккумулятор. Вызывается FormationModule.Tick.
- Одноразовые формации (вариант А, IsReusable=false) при истощении ИСЧЕЗАЮТ:
  Depleted → DeactivateFormation (дока §2.1: «без ядра — исчезает»).
- FormationActivatedEvent теперь несёт Type/PositionX/Y/EffectRadiusMeters;
  FormationDeactivatedEvent — Type (старые конструкторы сохранены).

### 2. Фикс race condition (ВАЖНО)
Модуль Qi стартует раньше Formation (DI-порядок) → начальное QiChangedEvent
публикуется ДО подписки FormationService → кэш `_cachedCurrentQi = 0` →
StartDrawing всегда падал на проверке contourQi.
Фикс: FormationModule.Start публикует `QiAddRequestEvent(0)` → AddQi(0) →
пере-публикация QiChangedEvent с текущими значениями (без изменения Ци).

### 3. Каст Formation-техники (PlayerTechniqueCaster)
- Генерация формации: тип случайный из {Barrier, Amplification, Suppression,
  Gathering}, размер Small, уровень = уровень техники.
- StartDrawing в позиции игрока: расход contourQi (QiConsumeRequestEvent).
- Дальше автонаполнение (FormationModule) → 100% → Active.
- Защита: «Формация уже создаётся» при повторном касте.

### 4. Эффекты активных формаций (схематически, но реальные)
- **Gathering**: QiModule подписан на FormationActivated/Deactivated →
  медитация в зоне ×2 (envMult «Богатая Ци», FORMATION_SYSTEM §10.2).
- **Amplification**: PlayerTechniqueCaster при касте Combat-техники проверяет
  активную формацию + нахождение игрока в радиусе (Chebyshev vs EffectRadius/2м)
  → `TechniqueService.ExternalDamageBonusPermil`; CombatService.GetTechniqueDamage
  применяет пермил-бонус (ЗАПРЕТ 3.9: baseDamage × bonus / 1000).
- **Barrier**: при активации → QiBufferActivateRequestEvent (щит = 10% ёмкости
  формации, кап 2000) → буфер поглощает урон в бою.
- **Suppression**: контур/визуал + эффекты-записи (замедление врагов — после
  подключения к NPCMovementService, отложено).

### 5. IFormationService
+StartDrawing(4 арг.) — позиционная прорисовка.

### 6. Headless smoke-тест
`GODOT_FORMATION_TEST=1` — TechniqueGrantPhase создаёт формацию напрямую:
```
[FormationTest] id=form_Suppression_Small_Fire_Star_L1_0FFB
  name='Печать подавления Огня · Звезда · L1 (Малая)' started=True stage=Filling pool=0/800
[FormationTest] after fill: stage=Active active=True pool=800/800
```
Ёмкость 800 = contourQi 80 × sizeMult 10 (L1 Small) — совпадает с FORMATION_SYSTEM §7.1.

### 7. Тосты (GameWorldController)
Стадии формации (рисуется/наполняется/истощена) + активация с типом.
