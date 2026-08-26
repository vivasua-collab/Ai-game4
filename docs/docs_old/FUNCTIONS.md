# Функции и типы проекта Cultivation World Simulator

**Последнее обновление:** 2026-03-24
**Версия:** 4.0 (Time Scaling, Activity Manager)

---

## ⏰ Система времени (TickTimer)

### TickTimer Singleton (tick-timer.ts)

| Функция | Назначение |
|---------|------------|
| `tickTimer.start()` | Запустить таймер |
| `tickTimer.pause()` | Пауза |
| `tickTimer.resume()` | Продолжить |
| `tickTimer.stop()` | Остановить |
| `tickTimer.isRunning()` | Проверка запуска |
| `tickTimer.setQiProcessor(processor)` | Установить процессор Ци |
| `tickTimer.initDefaultQiProcessor()` | Инициализация процессора |

### Time Store (time.store.ts)

| Селектор | Назначение |
|----------|------------|
| `useTimePaused()` | Пауза |
| `useTimeRunning()` | Запущен ли таймер |
| `useTickCount()` | Счётчик тиков |
| `useTimeSpeed()` | Текущая скорость |
| `useGameTime()` | Игровое время (WorldTime) |
| `useFormattedGameTime()` | Отформатированное время |

| Функция | Назначение |
|---------|------------|
| `togglePause()` | Переключить паузу |
| `setSpeed(speed)` | Установить скорость |

### Time Scaling (time-scaling.ts) — NEW

| Функция | Назначение |
|---------|------------|
| `getScalingFactor(speed)` | Получить множитель скорости |
| `getInverseScalingFactor(speed)` | Получить обратный множитель |
| `scaleMovementSpeed(baseSpeed, speed)` | ⚠️ Масштабирование скорости (инверсия!) |
| `scaleMovementSpeedInverse(baseSpeed, speed)` | ✅ ПРАВИЛЬНОЕ масштабирование скорости |
| `scaleCooldown(baseCooldown, speed)` | Масштабирование кулдауна |
| `gameMinutesToRealMs(gameMinutes, speed)` | Конвертация игрового времени в реальное |
| `realMsToGameMinutes(realMs, speed)` | Конвертация реального времени в игровое |
| `calculateRealDuration(actionDuration, speed)` | Реальная длительность действия |
| `getTimePerception(speed)` | Описание восприятия времени |
| `getSpeedMultiplierLabel(speed)` | Метка множителя для UI |
| `isCombatSpeed(speed)` | Проверка боевой скорости |
| `isTravelSpeed(speed)` | Проверка скорости путешествия |

### Activity Manager (activity-manager.ts) — NEW

| Функция | Назначение |
|---------|------------|
| `activityManager.setActivity(activity, forceSwitch?)` | Установить активность |
| `activityManager.endActivity()` | Завершить активность |
| `activityManager.getActivity()` | Получить текущую активность |
| `activityManager.getProfile()` | Получить профиль активности |
| `activityManager.getPreviousSpeed()` | Получить предыдущую скорость |
| `activityManager.restorePreviousSpeed()` | Восстановить предыдущую скорость |
| `activityManager.isInCombat()` | Проверка боевого режима |
| `activityManager.isMeditating()` | Проверка режима медитации |
| `activityManager.isTraveling()` | Проверка режима путешествия |
| `activityManager.isResting()` | Проверка режима отдыха |
| `startCombat()` | Начать бой (авто-переключение на superSuperSlow) |
| `endCombat()` | Завершить бой |
| `startTravel()` | Начать путешествие (авто-переключение на fast) |
| `endTravel()` | Завершить путешествие |
| `startMeditation()` | Начать медитацию (авто-переключение на ultra) |
| `endMeditation()` | Завершить медитацию |
| `startRest()` | Начать отдых |
| `endRest()` | Завершить отдых |

### Activity Profiles (action-speeds.ts) — NEW

| Функция | Назначение |
|---------|------------|
| `getActivityProfile(activity)` | Получить профиль активности |
| `getPreferredSpeed(activity)` | Получить предпочитаемую скорость |
| `shouldAutoSwitch(activity)` | Проверка авто-переключения |
| `getAutoSwitchActivities()` | Список активностей с авто-переключением |
| `getActivityName(activity)` | Название активности (RU) |
| `getActivityDescription(activity)` | Описание активности |

### Утилиты времени

| Функция | Назначение |
|---------|------------|
| `getTimeOfDay(hour)` | Время суток по часу |
| `getTimeOfDayName(hour)` | Название времени суток |
| `getSeasonFromMonth(month)` | Сезон по месяцу |
| `getSeasonName(season)` | Название сезона |
| `formatTimeOnly(gameTime)` | Формат HH:MM |
| `formatDateOnly(gameTime)` | Формат даты |
| `formatGameDateTime(gameTime)` | Полное время |
| `formatGameTimeWithSeason(gameTime)` | С сезоном |

### Qi Tick Processor (qi-tick-processor.ts)

| Функция | Назначение |
|---------|------------|
| `getQiTickProcessor()` | Получить singleton |
| `initQiTickProcessor(sessionId, characterId)` | Инициализация |
| `processor.setSession(sessionId, characterId)` | Установить сессию |
| `processor.processTick(detail)` | Обработать тик |
| `processor.flush()` | Отправить batch на сервер |
| `processor.getPendingTicks()` | Количество pending тиков |

---

## ⚠️ ВАЖНО: Единый источник расчётов ядра и меридиан

### Принцип единой точки истины

**ВСЕ расчёты, связанные с ядром Ци и системой меридиан, находятся в ОДНОМ месте:**

| Система | Файл | Главная функция |
|---------|------|-----------------|
| **Проводимость** | `src/lib/game/conductivity-system.ts` | `calculateTotalConductivity()` |
| **Расчёты Ци** | `src/lib/game/qi-shared.ts` | Все функции с префиксом `calculate*` |
| **Эффекты времени** | `src/services/time-tick.service.ts` | `processTimeTickEffects()` |

### Правила добавления новых расчётов

1. **НЕ дублировать расчёты** в других файлах
2. **Импортировать** функции из qi-shared.ts или conductivity-system.ts
3. **Использовать** time-tick.service.ts для всех эффектов времени
4. **При изменении формулы** обновлять только один файл

---

## 🆔 Система ID префиксов (id-config.ts)

### Формат ID

```
PREFIX_NNNNNN

Примеры:
- MS_000001 — Удар телом
- MW_000042 — Оружейная техника
- RG_000123 — Дальняя атака
- DF_000007 — Защитная техника
- WP_000001 — Оружие
- AR_000042 — Броня
```

### Префиксы техник (13 шт)

| Префикс | Тип | Описание |
|---------|-----|----------|
| `MS` | combat/melee_strike | Удар телом (руки/ноги) |
| `MW` | combat/melee_weapon | Удар оружием |
| `RG` | combat/ranged_* | Дальние техники (снаряд, луч, AOE) |
| `DF` | defense | Защитные техники |
| `CU` | cultivation | Техники культивации |
| `SP` | support | Поддержка (баффы) |
| `MV` | movement | Перемещение |
| `SN` | sensory | Восприятие |
| `HL` | healing | Исцеление |
| `CR` | curse | Проклятия |
| `PN` | poison | Яды |

### Префиксы экипировки v2 (6 шт)

| Префикс | Тип | Описание |
|---------|-----|----------|
| `WP` | weapon | Оружие |
| `AR` | armor | Броня |
| `CH` | charger | Зарядники |
| `AC` | accessory | Аксессуары |
| `AF` | artifact | Артефакты |
| `IT` | item | Общий предмет |

### Префиксы расходников и прочих (7 шт)

| Префикс | Тип | Описание |
|---------|-----|----------|
| `CS` | consumable | Расходники |
| `QS` | qi_stone | Камни Ци |
| `FM` | formation | Формации |
| `NP` | npc | NPC персонажи |
| `TC` | legacy | Устаревший (боевые техники) |

### Функции ID

| Функция | Назначение |
|---------|------------|
| `getPrefixForTechniqueType(type, subtype?)` | Получить префикс для типа техники |
| `getIdPrefixConfig(prefix)` | Конфигурация префикса |
| `generateId(prefix, counter)` | Сгенерировать ID |
| `parseId(id)` | Парсинг ID → { prefix, counter } |
| `isCombatPrefix(prefix)` | Проверка атакующего префикса |
| `getIdPrefixList()` | Список всех префиксов |
| `getCombatPrefixes()` | Список атакующих префиксов |

---

## ⚔️ Система Грейдов v2 (grade-system.ts)

### ⚠️ ВАЖНО: Grade ≠ Rarity

**Grade (качество)** — текущее состояние предмета:
- `damaged` — Повреждённый (×0.5 прочность, ×0.8 урон)
- `common` — Обычный (×1.0)
- `refined` — Улучшенный (×1.5 прочность, ×1.3 урон)
- `perfect` — Совершенный (×2.5 прочность, ×1.7 урон)
- `transcendent` — Превосходящий (×4.0 прочность, ×2.5 урон)

**Rarity (редкость)** — вероятность выпадения:
- `common` — Обычная (множитель 1.0x)
- `uncommon` — Необычная (множитель 1.25x)
- `rare` — Редкая (множитель 1.5x)
- `legendary` — Легендарная (множитель 2.0x)

### Функции грейдов

| Функция | Назначение |
|---------|------------|
| `selectGrade(level, rng)` | Выбор грейда по распределению уровня |
| `getGradeConfig(grade)` | Конфигурация грейда |
| `applyGradeToStats(stats, grade)` | Применение множителей грейда |
| `getGradeDistribution(level)` | Распределение грейдов для уровня |

---

## 💾 Хранилище пресетов (preset-storage.ts)

### Функции

| Функция | Назначение |
|---------|------------|
| `initialize()` | Инициализация хранилища |
| `saveTechniques(techniques, mode)` | Сохранить техники |
| `loadTechniques()` | Загрузить все техники |
| `loadTechniquesByType(type)` | Загрузить по типу |
| `loadTechniquesBySubtype(subtype)` | Загрузить combat по подтипу |
| `getTechniqueById(id)` | Получить технику по ID |
| `clearAll(preserveCounters?)` | Полная очистка |
| `getManifest()` | Получить манифест |
| `analyzeStorage()` | Анализ хранилища |

---

## 🎯 Пул техник (technique-pool.service.ts)

### Функции

| Функция | Назначение |
|---------|------------|
| `generateTechniquePool(options)` | Генерация пула техник |
| `getActivePool(characterId)` | Получить активный пул |
| `revealTechnique(poolItemId)` | Раскрыть технику |
| `selectTechniqueFromPool(poolItemId, characterId)` | Выбрать технику |
| `checkAndGenerateOnBreakthrough(characterId, newLevel)` | Автогенерация при прорыве |

---

## ⚡ Система Истинности (truth-system.ts)

### 🔄 ПАМЯТЬ ПЕРВИЧНА, БД ВТОРИЧНА

### Управление сессиями

| Функция | Назначение |
|---------|------------|
| `getInstance()` | Получить singleton |
| `loadSession(sessionId)` | Загрузить сессию из БД в память |
| `getSessionState(sessionId)` | Получить состояние из памяти |
| `unloadSession(sessionId)` | Выгрузить с сохранением |
| `isSessionLoaded(sessionId)` | Проверка загрузки |

### Операции с персонажем

| Функция | Назначение |
|---------|------------|
| `getCharacter(sessionId)` | Получить персонажа |
| `updateCharacter(sessionId, updates)` | Обновить (память, isDirty=true) |
| `addQi(sessionId, amount)` | Добавить Ци |
| `spendQi(sessionId, amount)` | Потратить Ци |

### КРИТИЧЕСКИЕ операции (БД + память)

| Функция | Назначение |
|---------|------------|
| `applyBreakthrough(sessionId, data)` | Прорыв уровня |
| `updateConductivity(sessionId, value, gained)` | Проводимость |
| `addTechnique(sessionId, data)` | Новая техника |
| `addInventoryItem(sessionId, data)` | Новый предмет |
| `changeLocation(sessionId, locationId)` | Смена локации |

### Сохранение

| Функция | Назначение |
|---------|------------|
| `saveToDatabase(sessionId)` | Полное сохранение |
| `quickSave(sessionId)` | Быстрое сохранение |

---

## ⚡ Система Ци (qi-shared.ts)

### Расчёты скорости

| Функция | Назначение |
|---------|------------|
| `calculateCoreGenerationRate(coreCapacity)` | Скорость выработки ядром |
| `getConductivityMultiplier(cultivationLevel)` | Множитель проводимости |
| `calculateEnvironmentalAbsorptionRate(conductivity, qiDensity, level)` | Поглощение из среды |
| `calculateQiRates(character, location)` | Полные скорости накопления |

### Прорыв

| Функция | Назначение |
|---------|------------|
| `calculateBreakthroughRequirements(level, subLevel, accumulated, capacity)` | Требования прорыва |
| `calculateBreakthroughResult(...)` | Результат попытки |
| `getBreakthroughProgress(...)` | Прогресс прорыва |

### Пассивное накопление

| Функция | Назначение |
|---------|------------|
| `calculatePassiveQiGain(currentQi, coreCapacity, rate, delta)` | Пассивный прирост |
| `calculatePassiveQiDissipation(currentQi, coreCapacity, conductivity, delta)` | Рассеивание избытка |
| `clampQiWithOverflow(newQi, coreCapacity, previousQi)` | Защита от переполнения |

---

## ⏰ Система времени (time-system.ts)

### Основные функции

| Функция | Назначение |
|---------|------------|
| `createInitialTime()` | Создать начальное время |
| `addTicks(time, ticks)` | Добавить тики (минуты) |
| `addMinutes(time, minutes)` | Добавить минуты |
| `addHours(time, hours)` | Добавить часы |

### Форматирование

| Функция | Назначение |
|---------|------------|
| `formatTime(time)` | Формат HH:MM |
| `formatDate(time)` | Формат даты |
| `formatDateTime(time)` | Полное время |
| `formatDuration(ticks)` | Длительность |

### Время суток и сезоны

| Функция | Назначение |
|---------|------------|
| `getTimeOfDay(time)` | Время суток |
| `getSeason(time)` | Сезон |

---

## ⚡ Проводимость меридиан (conductivity-system.ts)

### Основные функции

| Функция | Назначение |
|---------|------------|
| `calculateTotalConductivity(coreCapacity, level, meditations)` | **Главная функция** |
| `getBaseConductivity(coreCapacity)` | Базовая проводимость |
| `getBaseConductivityForLevel(coreCapacity, level)` | С множителем уровня |
| `calculateConductivityBonusFromMeditations(meditations, coreCapacity)` | Бонус от медитаций |

### Ограничения

| Функция | Назначение |
|---------|------------|
| `getMaxConductivity(level)` | Максимум для уровня |
| `getMaxConductivityMeditations(level)` | Максимум медитаций |
| `canDoConductivityMeditation(level, meditations)` | Проверка возможности |

---

## ⚔️ Боевая система (combat-system.ts)

### Время каста

| Функция | Назначение |
|---------|------------|
| `calculateCastTime(qiCost, conductivity, level, mastery)` | Время наполнения |
| `formatCastTime(seconds)` | Форматирование |

### Масштабирование

| Функция | Назначение |
|---------|------------|
| `calculateStatScalingByType(character, combatType)` | Множитель от характеристик |
| `calculateMasteryMultiplier(mastery, masteryBonus)` | Множитель мастерства |

### Типы техник

| Функция | Назначение |
|---------|------------|
| `isMeleeTechnique(combatType)` | Проверка melee |
| `isRangedTechnique(combatType)` | Проверка ranged |
| `isDefenseTechnique(combatType)` | Проверка защиты |
| `getEffectiveRange(technique)` | Эффективная дальность |

### Урон

| Функция | Назначение |
|---------|------------|
| `calculateDamageAtDistance(baseDamage, distance, range)` | Урон на дистанции |
| `checkDodge(attackerPos, targetPos, dodgeChance, agility)` | Проверка уклонения |
| `calculateAttackDamage(technique, character, target, distance, mastery)` | Итоговый урон |

---

## 🎮 NPC Damage Calculator (npc-damage-calculator.ts)

### Функции урона NPC→Player

| Функция | Назначение |
|---------|------------|
| `calculateDamageFromNPC(params)` | **Полный расчёт урона NPC→Player** |
| `calculateNPCQiSpent(npcCurrentQi, techniqueQiCost)` | Qi для траты NPC |
| `calculateQiDensity(cultivationLevel)` | Плотность Ци (2^level-1) |
| `checkNPCCritical(npcAgility, techniqueBonus)` | Проверка крита |
| `calculateNPCMaxHP(cultivationLevel, vitality)` | HP NPC по уровню |
| `determineNPCAttackType(npc, technique)` | Тип атаки по статам |
| `getNPCAttackRange(npc, technique)` | Дальность атаки |

---

## 🚀 Projectile Manager (ProjectileManager.ts)

### Функции управления снарядами

| Функция | Назначение |
|---------|------------|
| `fire(config)` | Создать снаряд с конфигурацией |
| `fireFromPlayer(playerX, playerY, targetX, targetY, technique)` | Снаряд от игрока |
| `fireAOE(x, y, technique)` | AOE техника в точке |
| `fireBeam(playerX, playerY, targetX, targetY, technique)` | Луч от игрока |
| `update(delta)` | Обновление снарядов |
| `clear()` | Удалить все снаряды |

---

## 🚌 Event Bus Client (event-bus/client.ts)

### Техники

| Метод | Назначение |
|-------|------------|
| `useTechnique(techniqueId, position)` | Использовать технику |
| `startCharging(techniqueId)` | Начать зарядку |
| `cancelCharging()` | Отменить зарядку |
| `reportDamageDealt(targetId, targetType, techniqueId, position, distance, rotation, damageMultiplier)` | Отчёт об уроне |

### Бой NPC→Player (NEW)

| Метод | Назначение |
|-------|------------|
| `reportDamageReceived(params)` | Игрок получил урон |
| `reportPlayerDeath(params)` | Смерть игрока |
| `reportPlayerRespawn(params)` | Респаун игрока |
| `npcAttackPlayer(params)` | NPC атакует игрока |

### Инвентарь

| Метод | Назначение |
|-------|------------|
| `useItem(itemId, quantity)` | Использовать предмет |
| `equipItem(itemId, slotId)` | Экипировать предмет |
| `unequipItem(slotId)` | Снять предмет |
| `dropItem(itemId, quantity, position)` | Выбросить предмет |
| `pickupItem(worldItemId)` | Подобрать предмет |

### Движение

| Метод | Назначение |
|-------|------------|
| `move(tilesMoved)` | Перемещение |

### Окружение

| Метод | Назначение |
|-------|------------|
| `interactWithEnvironment(objectId, objectType, action, position)` | Взаимодействие |
| `enterZone(zoneId, zoneType, position)` | Войти в зону |
| `leaveZone(zoneId, zoneType, position)` | Выйти из зоны |

---

## 📦 Утилиты пресетов (src/data/presets/index.ts)

| Функция | Назначение |
|---------|------------|
| `getAllPresets()` | Все пресеты в массиве |
| `getStarterPack(presetId)` | Стартовый набор |
| `findPresetById(id)` | Универсальный поиск |
| `filterByCategory(presets, cat)` | Фильтр по категории |
| `filterByRarity(presets, rarity)` | Фильтр по редкости |
| `filterByCultivationLevel(presets, level)` | Фильтр по уровню |
| `isPresetAvailable(preset, character)` | Проверка доступности |

---

## ⏱️ Единый сервис тиков времени (time-tick.service.ts)

### Главная функция

| Функция | Назначение |
|---------|------------|
| `processTimeTickEffects(options)` | **Полная обработка тиков времени** |

### Параметры options:
- `characterId` — ID персонажа
- `sessionId` — ID сессии
- `ticks` — количество тиков (минуты)
- `restType` — тип отдыха ('light' | 'sleep')
- `applyPassiveQi` — пассивная генерация Ци
- `applyDissipation` — рассеивание избытка

### Быстрые функции

| Функция | Назначение |
|---------|------------|
| `quickProcessQiTick(characterId, sessionId, ticks)` | Быстрая обработка Qi |
| `getCharacterConductivity(characterId)` | Информация о проводимости |

---

## 😴 Система усталости (fatigue-system.ts)

| Функция | Назначение |
|---------|------------|
| `getFatigueAccumulationMultiplier(level)` | Множитель накопления |
| `getFatigueRecoveryMultiplier(level)` | Множитель восстановления |
| `calculateFatigueFromAction(character, action, duration, qiSpent)` | Усталость от действия |
| `calculateRestRecovery(character, duration, isSleep)` | Восстановление при отдыхе |
| `calculateEfficiencyModifiers(physicalFatigue, mentalFatigue)` | Множители эффективности |

---

## 🛡️ Безопасность

### Rate Limiting (rate-limit.ts)

| Функция | Назначение |
|---------|------------|
| `checkRateLimit(identifier, maxRequests, windowMs)` | Проверка лимита |
| `resetRateLimit(identifier)` | Сброс лимита |
| `createRateLimiter(maxRequests, windowMs)` | Создать лимитер |

### Готовые лимитеры

- `rateLimiters.chat` — 30 запросов/мин
- `rateLimiters.game` — 60 запросов/мин
- `rateLimiters.auth` — 5 запросов/мин
- `rateLimiters.api` — 100 запросов/мин

---

## 🔗 Связанные документы

- [ARCHITECTURE.md](./ARCHITECTURE.md) — Архитектура проекта
- [equip-v2.md](./equip-v2.md) — Система экипировки v2
- [NPC_COMBAT_INTERACTIONS.md](./NPC_COMBAT_INTERACTIONS.md) — Взаимодействия NPC
- [PHASER_STACK.md](./PHASER_STACK.md) — Phaser 3 интеграция
