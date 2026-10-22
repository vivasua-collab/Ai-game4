# Чекпоинт: Этап-6 аудита — инвентарь/торговля/экономика (BUG-018..021)

**Дата:** 2026-10-22 16:35 UTC
**Сессия:** Этапы 6-7 аудита (сессия пережила обрыв контекста на Stage7 — фиксы Этапа-6 были готовы до обрыва)
**Тип:** audit + fix

## Контекст

Этап-6 внешнего аудита: инвентарь/торговля/экономика. Аудитор передал 4 дефекта
(все P2): BUG-018 зацикливание TryAddItem при MaxStack ≤ 0, BUG-019 расхождение
баланса и событий при насыщении кошелька, BUG-020 отрицательные котировки обмена,
BUG-021 невалидированный RestoreState колец. Сессия была прервана падением после
сообщения «Stage6 PASS» — фиксы и сим Этапа-6 остались в рабочем дереве,
незакоммиченными.

## Что сделано

- BUG-018: инвариант «стакающийся предмет обязан иметь MaxStack ≥ 1» — три эшелона:
  TryAddItem контролируемый отказ (false, ничего не добавлено); ItemDatabaseService
  .Register/RestoreState валидация определений (Stackable=true ∧ MaxStack ≤ 0 →
  отклонение с журналом); событий с count ≤ 0 нет.
- BUG-019: CurrencyChangedEvent.Delta = фактически начисленное/списанное
  (Add(10) при Max−3 → Delta=3); TradeCompletedEvent.Price = зачисленное при
  потолке баланса (частичная выплата видна явно).
- BUG-020: котировки в long + контракт ExchangeQuote.IsRepresentable
  (false = непредставимо в int-валюте, фабрика Unrepresentable); аудит-кейс
  21474837 → +1073741850 (прежде отрицательное).
- BUG-021: StorageRingService.RestoreState валидирует кольца (Tier ≥ 0,
  MaxVolume > 0, финитен) и слоты (ItemId в каталоге, 1 ≤ Count ≤ MaxStack,
  живые enum-ы) — зеркало P2-35 инвентаря; отклонения с журналом.
- QA-сим Stage6TradeEconomySimDebug (GODOT_STAGE6_DEBUG, №24 в qa_regression.sh):
  дословные регрессии аудитора; VERDICT PASS (полный лог —
  qa_artifacts/stage6_sim_20261022.log).
- Разметка источников предметов доведена до продюсеров Этапа-6 зоны:
  TryAddItem(item, count, source) по всем точкам StartingGearPhase.

## Решения

- События несут ФАКТ, а не ЗАЯВКУ (BUG-019) — единый принцип честности доменов,
  продолжающий Этап-5 «тело = истина смерти».
- Непредставимость котировки — ЯВНЫЙ контракт (IsRepresentable), а не молчаливое
  число: вызывающий обязан отказаться (BUG-020).
- Валидация при восстановлении — эшелонированная (каталог → инвентарь → кольца):
  каждое хранилище обязано само защищать своё рабочее состояние (BUG-021).

## Найденные проблемы

- Фикстура AUDIT0922 P1-19 хранила в кольце предмет qa_p_probe БЕЗ регистрации
  в каталоге — новая валидация BUG-021 честно отклоняла его при восстановлении,
  P4/P5 падали. Это ошибка фикстуры (не игры): исправлено — зонда регистрируется
  в каталоге (комментарий в симе). Класс ошибки тот же, что у батареи Этапа-7:
  тестовая конструкция конфликтует с новым (верным) инвариантом.

## Следующие шаги

- Этап-7 (этой же сессией, отдельный чекпоинт).
- Аудитору: повторная проверка зоны торговли/экономики по свежему main.

## Файлы

- game/src/Modules/Inventory/{InventoryService,InventoryModule,BeltService,SpiritStorageService,StorageRingService}.cs
- game/src/Modules/Generator/ItemDatabaseService.cs
- game/src/Modules/Trade/{TradeService,CurrencyService,CurrencyExchangeService}.cs
- game/src/Modules/Charger/ChargerItemBridge.cs
- game/src/Core/Interfaces/{IInventoryService,ICurrencyExchangeService}.cs
- game/src/Core/Messaging/Contracts/InventoryContracts.cs
- game/src/Entry/Phases/StartingGearPhase.cs, game/src/Adapter/UI/CharacterDollPanel.cs
- game/src/Adapter/Scene/Stage6TradeEconomySimDebug.cs (новый), GameWorldController.cs
- tools/qa_regression.sh (+STAGE6), qa_artifacts/stage6_sim_20261022.log
