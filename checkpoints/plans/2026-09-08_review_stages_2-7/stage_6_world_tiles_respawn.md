# Этап 6 — Мир, время, тайлы, ресурсы, путешествия, взаимодействия

> Статус: ⬜ план → исполнение. Файлы: WorldService/WorldModule, TileService/TileModule/ResourceService,
> InteractionService/DialogueService, TradeService/CurrencyService/TradeModule, GameWorldController,
> ITimeService/IWorldService/ITileService, WorldContracts/TileContracts.

## Находки ревью → план валидации и фикса

### P0-1. Истощённые ресурсы никогда не восстанавливаются
**Клейм:** TileService при полном сборе очищает тайл и зовёт ResourceService.RegisterDepletedResource; через 7 игровых дней ResourceService.RespawnCheck публикует ResourceRespawnedEvent и удаляет запись; ПОДПИСЧИКА НЕТ — тайл не восстанавливается, объект/IsHarvestable/проходимость не возвращаются.
**Валидация:** grep потребителей ResourceRespawnedEvent; прочитать RegisterDepletedResource/RespawnCheck и хранение OriginalObject/ResourceMax/ResourceId.
**Фикс:**
- TileService подписывается на ResourceRespawnedEvent.
- Handler: восстановить тайл по координатам (OriginalObject, ResourceId, ResourceMax, IsHarvestable, проходимость).
- Опубликовать TileChangedEvent → renderer обновляется.
- QA: harvest до истощения → advance 7 дней → GetTile содержит исходный объект, harvestable=true.

### P1-2. TryTravel меняет ID локации, но не мир
**Клейм:** TravelStartedEvent → SetActiveLocation → LocationChangedEvent; TileModule.OnLocationChanged — только логирование и TODO; grid/NPC/спавн остаются от старой локации; TryTravel возвращает true.
**Валидация:** прочитать WorldService.TryTravel, TileModule.OnLocationChanged; проверить, какие локации зарегистрированы (test_polygon?).
**Фикс (MVP, без полного travel pipeline):**
- Честный контракт: TryTravel возвращает false и НЕ меняет active location, пока физическое переключение не реализовано (заглушка будущей фазы — сохранить явно, с тостом «в разработке»).
- Либо минимальный рабочий вариант: генерация нового grid по seed/размеру локации + респавн сущностей — если это вписывается в текущую архитектуру SceneOrchestrator. Решение по коду: если travel-путь не используется игрой (нет UI-триггера) — заглушить честно.

### P1-3. InteractionService — статический legacy registry, реальные NPC в обход
**Клейм:** зарегистрированы только elder_01/merchant_01/chest_01 (фикс-ИД); реальные NPC динамические; GameWorldController обходит INPCService.GetNearbyNPCIds и сам зовёт DialogueService.TryStartNpcDialogue; InteractionService определяет NPC по prefix-проверкам ID.
**Валидация:** прочитать InteractionService (registry, prefix-логика), GameWorldController E-path; найти всех потребителей IInteractionService.
**Фикс (единый authority — в духе санации):**
- Перенести поиск реальных NPC и запуск диалогов в InteractionService (реальные NPC добавляются/удаляются по spawn/despawn событиям, не по prefix).
- Либо, если потребители только legacy/test — удалить статический registry и prefix-логику, оставить честный API. Решение по коду после grep.
- Координаты статических NPC, не соответствующие процедурным сущностям, — не хранить.

### P2-4. TimeChangedEvent.Delta = 1/60 против ITimeService.DeltaTime = 1
**Клейм:** TimeService.AdvanceTick: DeltaTime=1f (1 tick = 1 игровая минута); WorldModule.Tick публикует TimeChangedEvent с delta=1f/60f; подписчиков нет, но первый получит таймеры в 60 раз медленнее.
**Валидация:** прочитать WorldModule.Tick, TimeService.AdvanceTick, WorldContracts.TimeChangedEvent.
**Фикс:** публиковать ts.DeltaTime (1f); тест согласованности Delta == ITimeService.DeltaTime.

### P2-5. ResourceService.TryPickup всегда возвращает null в out
**Клейм:** out ItemData = null!, публикует только ItemAddRequestEvent, возвращает true — «успех» не означает выдачу предмета (тот же паттерн, что SpiritStorage P0).
**Валидация:** прочитать TryPickup и потребителей.
**Фикс:**
- Убрать out ItemData из интерфейса → команда RequestPickup (переименовать), ЛИБО резолвить через IItemDatabaseService и присваивать item, false при отсутствии.
- Согласовать с фиксом P1-5 этапа 4 (pickup-путь уже правится там — проверить, не дублирует ли TryPickup тот же путь).

## QA-план

- Новые хуки: resource respawn (истощение → advance 7 дней → тайл восстановлен, TileChangedEvent), time-delta согласованность.
- Регрессия GEN + полная.

## Коммит
`fix(review-6): world/tile — resource respawn, honest travel, interaction authority, time delta` + push.
