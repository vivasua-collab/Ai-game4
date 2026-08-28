# Чекпоинт-план: Книга Техник + библиотека (cap/мастерство/свитки) + F1-справка + чит-окно

**Дата:** 2026-08-28 10:55 MSK (UTC+3)
**Сессия:** основная (Z.ai Code sandbox, без субагентов, без TodoWrite)
**HEAD на старте:** `cd60c2d` (synced с origin/main)
**Запрос:** по итогам теоретических изысканий (сессия 2026-08-28, первое сообщение) пользователь зафиксировал решения:

1. **Мастерство при забвении** — перенос («осмысление», ~15% в эхо того же профиля тип+стихия).
2. **Свитки техник** — запись базовой (не улучшенной, без мастерства) техники на свиток,
   чтобы позже изучить повторно (нишевые цели). Свиток расходуется при изучении,
   изучение со свитка обходит окно резонанса.
3. **Cap библиотеки** — единый, растёт с уровнем культивации (тело/разум развиваются),
   расширяемый (ExtraLibraryCapacity под перки/предметы в будущем).
4. **Книга Техник** — отдельное окно (Old School RPG), матрица: вкладки=уровни,
   блоки=типы (с выделением), строки=стихии. Архив — отдельная вкладка.
   Техники культивации остаются в CultivationWindow.
5. **Режим сравнения** — не нужен (пока).
6. **Упрощение UI:** все подсказки по горячим клавишам убираются из панелей;
   F1 — отдельное окно-справка с полным перечнем горячих клавиш проекта,
   с обязательным фоном (текст не накладывается на окружение).
   F1 освобождается: чит-меню переезжает на F2 и становится отдельным окном
   с однородным фоном (как инвентарь).

## Архитектурные решения

- **Слой библиотеки** (сколько ЗНАЕМ): `TechniqueService` — единый cap
  `LibraryCapacityBase(L) = 8 + 2×(L−1)` (+ Extra), категории Cultivation/Curse/Formation
  сохраняют персональные лимиты ×1; Combat-пул §12 (3+(L−1)) больше НЕ ограничивает
  изучение — его роль играет библиотечный cap.
- **Слой лодаута** (что под рукой): без изменений — TechniqueSlotService (слоты 3–9) + хотбар.
- **Эхо мастерства**: `Dictionary<string,float>`, ключ `"{type}:{element}"`;
  при забвении +15% мастерства, при изучении техники того же профиля — стартовое
  мастерство из эха (поглощается, cap 50).
- **Свитки**: реестр внутри TechniqueService (`_scrolls`), вкладка «Свитки» в Книге.
  Запись: стоимость Ци = 2×QiCost (через QiConsumeRequestEvent, EVT-01 паттерн).
  Предметизация свитков (в физический инвентарь) — отложена до этапа экономики
  (задел: реестр + ISaveable).
- **Персистентность**: TechniqueService становится ISaveable (`techniques`):
  изученные техники + выбранная + эхо + свитки + ExtraLibraryCapacity.
  Регистрация в CombatModuleServices по паттерну `Register<ISaveable, T>`.
- **Найдено (латентный баг, НЕ фикс в этой сессии):** System.Text.Json без
  `IncludeFields=true` не сериализует public-поля DTO (SlotState и др.), а Load
  отдаёт JsonElement — типизированные `state is X` касты молча падают.
  Зарегистрировано как находка для отдельной сессии загрузки.

## Этапы

- [x] S1. Ядро: TechniqueService — cap библиотеки, эхо мастерства, свитки, ISaveable; регистрация в DI.
- [x] S2. Инпут: F1=help_hotkeys (новый action), F2=cheat_menu, input_log освобождён от F1; InputAdapter + PlayerInputService sticky-флаг.
- [x] S3. ElementStyle — единая палитра стихий для Книги/слотов.
- [x] S4. TechniqueBookWindow — матричная книга (вкладки/блоки/строки), детальная панель, слоты 3–9, свитки, забвение с эхом.
- [x] S5. HotkeysWindow — окно-справка F1 с фоном и полным перечнем.
- [x] S6. CheatPanel → модальное окно с однородным фоном (F2), вся логика кнопок сохранена.
- [x] S7. GameWorldController: Книга вместо TechniquesPanel (T, с паузой), F1/F2, Esc-цепочка; TechniquesPanel.cs удалён.
- [x] S8. Зачистка хинтов клавиш из UI (InventoryWindow, CultivationWindow).
- [x] S9. Сборка dotnet build — 0 errors / 271 warnings (базовый уровень; dotnet SDK восстановлен после сброса окружения).
- [x] S10. Чекпоинт + worklog + локальный коммит (push — при наличии токена; рантайм-смоук вечером после 19:00 МСК).

## Work Log

- Изучены: TechniqueService/TechniquesPanel/CultivationWindow/TechniqueSlotService/
  HotbarPanel/CheatPanel/InventoryWindow/SaveModule/SaveDataAggregator/
  SaveFileHandler/InputAdapter/InputMapInitializer/PlayerInputService/
  GameWorldController/TechniqueGrantPhase/TechniqueCapacity/ItemData/UIContracts.
- S1: TechniqueService — LibraryCapacityBase/ExtraLibraryCapacity/LibraryUsed/LibraryFree;
  LearnTechnique: резонанс (обход при fromScroll) + категории ×1 + cap библиотеки;
  эхо (EchoTransferRatio 0.15, cap 50, поглощается); InscribeScroll/LearnFromScroll
  (свиток расходуется, изучение обходит резонанс, но не cap/категории);
  ISaveable (SaveKey "techniques", DTO на свойствах); ForgetAll не трогает свитки/эхо
  (девайс-семантика), ForgetAllWithLibrary — полный сброс.
- S1: CombatModuleServices + Register<ISaveable, TechniqueService>.
- S2: InputMapInitializer — cheat_menu → F2; новый help_hotkeys → F1; input_log оставлен на backquote.
  InputAdapter — sticky «help_hotkeys». PlayerInputService — IsHelpHotkeysPressed.
- S3: ElementStyle (Adapter/UI) — цвета стихий + рамки блоков типов.
- S4: TechniqueBookWindow — вкладки [Все][L…][Архив][Свитки]; блоки 8 типов
  (кроме Cultivation), строки-стихии; чипы = кнопки; правая панель деталей;
  действия: слоты 3–9, свиток, забвение (ConfirmationDialog + эхо-тост);
  нижний бар слотов; подписки Learned/Forgotten/Selection/SlotAssigned/SlotCleared/Qi.
- S5: HotkeysWindow — модальное окно, тёмный фон 0.75, 7 групп, полный перечень
  клавиш (18 записей + чит F2 отдельной группой).
- S6: CheatPanel — Control-оверлей + центрированная панель 760×640 +
  ScrollContainer, фон 0.7; логика хендлеров не тронута; F2.
- S7: GameWorldController — _techniqueBook (T + пауза как у инвентаря),
  _hotkeysWindow (F1 + пауза), _cheatPanel (F2, без паузы — девайс),
  Esc-цепочка: hotkeys → cheat → book(+resume) → trade → dialogue → …;
  TechniquesPanel удалён (git rm).
- S8: InventoryWindow — футер «B или Esc — закрыть…» убран; CultivationWindow —
  «(K)» из кнопки закрытия, «(клавиши 3–9)» из подписи слотов.
- S9: `dotnet build` — 0 errors / 271 warnings (ровно базовый уровень:
  новые варнинги почищены, мёртвое поле _usedCapacity удалено).
  Окружение: dotnet SDK 8.0 переустановлен (сброс песочницы), Godot на месте.
  Рантайм-проверка (NEWGAME smoke) отложена до вечернего окна (после 19:00
  МСК — правило сессии).

## Stage Summary

- Реализована двухслойная модель «Библиотека + Лодаут»: cap 8+2(L−1), расширяемый;
  эхо мастерства 15% (cap 50) при забвении; свитки (запись за 2×QiCost, изучение
  вне окна резонанса, расходуются).
- Книга Техник (T): матрица вкладки-уровни / блоки-типы / строки-стихии, архив,
  свитки, назначение слотов 3–9, пауза как у инвентаря. TechniquesPanel удалён.
- F1 — окно-справка всех горячих клавиш (фон, Old School); чит-меню → F2,
  модальное окно с однородным фоном; инлайн-хинты клавиш убраны из окон.
- TechniqueService → ISaveable: техники теперь в сейве (раньше сохранялись только
  слоты — рассинхрон при загрузке).
- Найден латентный баг сейв-конвейера (public-поля DTO не сериализуются,
  JsonElement-касты при загрузке) — задокументирован, фикс вне скоупа сессии.
