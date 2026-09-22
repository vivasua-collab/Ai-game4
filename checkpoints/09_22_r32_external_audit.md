# Чекпоинт: верификация внешнего аудита audit_09_22_07_30.txt (R32)

Дата: 2026-09-22
Baseline аудита: 949391c (R31)
Ответ для внешнего аудитора — по его прямому запросу (Фаза 3 / итог):
«проверить P1-5 и P1-8 реальным runtime-тестом, результат выгрузить в
checkpoints/ с пометкой для аудита». Runtime-тест реализован как
headless-сим `Audit0922SimDebug` (env `GODOT_AUDIT0922_DEBUG=1`, паттерн
QA-симов R16), прогнан ДО фиксов — факты ниже из живого прогона.

---

## 1. Ответы на вопросы аудитора (runtime-верификация)

### P1-5 — EventBus re-entrancy + exception: ПОДТВЕРЖДЕНО runtime, 4 факта

Стенд: свежий `EventBus`, три подписчика `PingMsg` —
(A) публикует событие того же типа внутри хендлера (re-entrancy),
(B) бросает исключение на каждом событии,
(C) записывает доставку, стоит ПОСЛЕ падающего. Затем независимый
`Publish(OtherMsg#9)`.

Факты префикс-прогона (лог `[Audit0922Sim]`, полный вывод в §2):

1. **Исключение покидает Publish** — `InvalidOperationException` из
   подписчика B пробросился в вызывающий код (A1 DEFECT). То есть
   выбросом «рвёт» уже и publisher-а, не только fan-out.
2. **Fan-out прерывается** — подписчик C, стоящий после B, НЕ получил
   исходное событие #1 (A2 DEFECT; доставка C = пусто).
3. **Re-entrant очередь протухает и взрывает посторонний Publish** —
   событие #2, поставленное в `_pendingQueue` во время Publish(#1),
   пережило исключение и исполнилось в дрене СЛЕДУЮЩЕГО, НЕСВЯЗАННОГО
   `Publish(OtherMsg#9)`; при этом падающий хендлер B прервал и его
   доставку: порядок доставки C = `[other#9]`, ping#2 не доставлен,
   исключение покинуло и второй Publish (строка
   «стэл-дрен очереди уронил ПОСТОРОННИЙ Publish — сценарий коррупции
   подтверждён»). Это в точности предсказанный аудитором сценарий
   «событие выполнится позже, в другом логическом контексте».
4. Статическая причина: дрен `_pendingQueue` (EventBus.cs:90–98)
   недостижим при исключении в `InvokeHandlers` — `finally` снимает
   флаг, но код после try/finally не выполняется; очередь
   [ThreadStatic] живёт до следующего Publish на потоке.

Ответ на вопрос «получают ли subscribers после throwing subscriber
исходное событие» — **НЕТ** (факт 2). Ответ на вопрос «будет ли ранее
поставленное re-entrant событие выполнено позже» — **ДА**, и оно
способно уронить посторонний Publish (факт 3).

### P1-8 — path traversal: ПОДТВЕРЖДЕНО runtime с одним уточнением

Стенд: `Modules.Save.SaveFileHandler` (headless-fallback, engine-agnostic
default) с `SaveDirectory=/tmp/audit0922_saves`, имя слота
`../escape_probe`.

- **Факт (B1 DEFECT):** `Save('../escape_probe')` → `true`, файл создан
  **вне** каталога сейвов — `/tmp/escape_probe.json`. Traversal
  работает ровно как описано в аудите; границы filesystem у
  persistence-дефолта нет.
- **Уточнение для аудитора:** боевая регистрация
  `Adapter.Persistence.SaveFileHandler` (GameBoot-оверрайд, продакшн)
  **уже санирует** имена слотов (`SanitizeSlotName`: только буквы/цифры/
  `-`/`_`, Adapter/Persistence/SaveFileHandler.cs:162–168) — в живой
  игре traversal недостижим. Дыра — именно в Modules-дефолте (общий
  контракт `ISaveFileHandler`), который остаётся зарегистрированным
  (см. P1-6) и документирован как fallback для headless/тестов.
  Severity согласен считать P1 (контракт интерфейса не должен зависеть
  от того, какая реализация зарегистрирована), fix — санация в
  Modules-хендлере по тем же правилам, что у Adapter.

---

## 2. Итоговый лог префикс-прогона (runtime-доказательства)

```
[Audit0922Sim] === A. EventBus: exception в подписчике + re-entrant очередь (P1-5) ===
[Audit0922Sim] DEFECT A1 исключение подписчика изолировано (не покидает Publish)
[Audit0922Sim] DEFECT A2 подписчики после падавшего получили исходное событие
[Audit0922Sim] DEFECT A3 re-entrant событие доставлено в дрене того же Publish (раньше чужих событий)
[Audit0922Sim] diag: стэл-дрен очереди уронил ПОСТОРОННИЙ Publish — сценарий коррупции подтверждён
[Audit0922Sim] diag: порядок доставки = [other#9]; исключение покинуло Publish: InvalidOperationException
[Audit0922Sim] === B. Modules-layer SaveFileHandler: path traversal (P1-8) ===
[Audit0922Sim] diag: Save('../escape_probe') → True; файл вне каталога: True (/tmp/escape_probe.json) — traversal сработал
[Audit0922Sim] DEFECT B1 имя '../escape_probe' не покидает каталог сейвов (санация)
[Audit0922Sim] === C. ContainerBuilder: override регистрации (P1-6) ===
[Audit0922Sim] diag: Resolve<Modules.SaveFileHandler> (stale concrete-ключ) → РЕЗОЛВИТСЯ в мёртвый fallback-инстанс
[Audit0922Sim] DEFECT C2 stale concrete-ключ старой регистрации удалён (override чистит хвосты)
[Audit0922Sim] === D. SaveService.DeleteSave: честный результат (P1-7) ===
[Audit0922Sim] diag: DeleteSave(несуществующий) → True — ложный успех
[Audit0922Sim] DEFECT D1 удаление несуществующего слота возвращает false
[Audit0922Sim] === E. Trade/Corpse: контракт IWorldResettable (P1-1/P1-4) ===
[Audit0922Sim] diag: TradeService is IWorldResettable → False
[Audit0922Sim] diag: CorpseService is IWorldResettable → False (сброс есть, но транзитивно через NPCModule — контракт неявный)
[Audit0922Sim] VERDICT: FAIL — см. DEFECT-строки выше (runtime-подтверждение аудита 09.22)
```

---

## 3. Верификация всех пунктов аудита (статика + runtime)

| ID | Вердикт | Комментарий |
|----|---------|-------------|
| P1-1 TradeService без IWorldResettable | ✅ ПОДТВЕРЖДЁН | Сброса нет ни явно, ни транзитивно. Уточнение: NPC-ID генерируются GUID (`npc_{Guid:N}`), коллизии «npc_0» из аудита невозможны — реальный эффект: утечка `_stockByMerchant` между мирами (unbounded) + висячий `_activeMerchantId` после NewGame (IsTrading=true с мёртвым торговцем до следующего OpenTrade). |
| P1-2 SaveAndQuit игнорирует bool | ✅ ПОДТВЕРЖДЁН | GameSession.cs:245 — результат `_save.Save()` не читается; 252–253 всегда Quitting+«saved». Уточнение: `SaveAndQuit()` сейчас НИКЕМ не вызывается (нет UI-кнопки) — дефект контракта API, до игрока не доходит, но должен быть закрыт до появления кнопки. |
| P1-3 NPCService.GetAllStates() GC | ✅ ПОДТВЕРЖДЁН | NPCService.cs:272–275 — новый List на вызов; вызовы в hot path: NPCAIService.Tick:127 + вложенный:180, NPCModule.ProcessNpcAttacks:161, NPCMovementService:135. Комментарий «снапшот-буфер (NPC-A07)» аллокацию не убирает. |
| P1-4 CorpseService без IWorldResettable | ⚠️ ЧАСТИЧНО | Формально верно: интерфейса нет, `ResolveAll` его не видит, `ResetWorld()` существует. НО сброс происходит — транзитивно: `NPCModule : IWorldResettable`.ResetWorld() (NPCModule.cs:123–128) вызывает `_corpseService.ResetWorld()` на ОБЕИХ путях (WorldDomainResetPhase NewGame + GameSession.LoadGame:151–153). Трупы НЕ переживают пересборку (QA REASSEMBLY 18/18 это инвариант). Реальный остаток — архнесогласованность (неявный контракт), а не «новый мир получает трупы старого». Фикс: добавить интерфейс, явный вызов из NPCModule удалить. |
| P1-5 EventBus exception/re-entrancy | ✅ ПОДТВЕРЖДЁН runtime | §1: исключение покидает Publish, fan-out прерывается, стэл-очередь протухает и рушит посторонний Publish. |
| P1-6 DI override stale concrete | ✅ ПОДТВЕРЖДЁН runtime | C2: после `RegisterInstance<ISaveFileHandler>(adapter)` поверх `Register<ISaveFileHandler, Modules.SaveFileHandler>` ключ `Modules.SaveFileHandler` остаётся → `Resolve<Modules.SaveFileHandler>` резолвит мёртвый fallback-инстанс; ResolveAll-обходы конструируют его впустую. NOTE: содержимое Фазы 4 в файле аудита ОТСУТСТВУЕТ (текст перескакивает с Фазы 3 на «Фаза 5 завершена») — трактовка P1-6 восстановлена из строки итога + анализ Container.cs. |
| P1-7 DeleteSave всегда true | ✅ ПОДТВЕРЖДЁН runtime | D1: три слоя — FileHandler честен → Aggregator прозрачно форвардит → SaveService.cs:87–92 игнорирует и возвращает true. |
| P1-8 slotName → filesystem path | ✅ ПОДТВЕРЖДЁН runtime (Modules-слой) | §1: файл создан вне каталога. Уточнение: Adapter-хендлер уже санирует (см. §1) — закрыт пробел в Modules-дефолте. |
| P2-1 RestoreState(object)→void | ✅ подтверждён (известный долг R31, не трогаем) | |
| P2-2 двойное AttitudeScore | ✅ подтверждён (в плане аудита доков R31 §6) | |
| P2-3/P2-8 ConfigureAwait latent | ✅ подтверждён как latent risk (SceneOrchestrator:135 + GameSession:101/194) — не текущий баг | |
| P2-4 Trade «толстый» | ✅ подтверждён — сознательный прототип, бэклог | |
| P2-5 string.GetHashCode() seed | ✅ подтверждён (CorpseService.cs:477/545) | |
| P2-6 RestoreState тихий отказ | ✅ подтверждён (семейство P2-1) | |
| P2-9 zero-GC формулировка | ✅ подтверждён — скорректирован комментарий в фиксе EventBus | |
| P2-10/P2-11 Initialize неидемпотентен | ✅ подтверждён (NPCService:88–92, NPCAIService) — hardening-бэклог | |
| P2-15..P2-19 (DeleteSave-семантика, метаданные GetAllSaves, missing-blocks, Load-after-Reset) | ✅ подтверждены как backlog | P2-15 закрывается попутно фиксом P1-7 (единая семантика «операция выполнена»: false = файла не было/I/O-отказ). |

Отдельно зафиксировано: **Фаза 4 в файле аудита отсутствует** (после
«Следующая фаза логично пойдёт в DI/container...» сразу идёт «Фаза 5
завершена»). В итоговом списке P1-6 присутствует. Проверьте, не потерялся
ли кусок при выгрузке — его содержание (P2-12..P2-14?) мы не видели.

---

## 4. P2-бэклог (по указанию аудитора — отдельным списком, НЕ смешивать с P1)

1. **P2-1/P2-6:** `ISaveable.RestoreState(object) → bool` (28+ реализаций) —
   решение контракта за пользователем; в плане аудита доков R31 §6.
2. **P2-2:** убрать legacy `NPCSaveEntry.AttitudeScore` после решения о
   миграции старых сейвов (план R31 §6).
3. **P2-3/P2-8:** async/lifecycle граница `SceneOrchestrator
   (ConfigureAwait(false)) ↔ Godot main thread` — гард/документация при
   появлении первой реально-асинхронной фазы.
4. **P2-4:** декомпозиция TradeService (TradeSession/MerchantStock/
   TradePricing/CurrencyExchange) — при развитии экономики.
5. **P2-5:** CorpseService — заменить `npcId.GetHashCode()` на FNV-1a
   (паттерн TradeService.Fnv1a) для кросс-процессного детерминизма лута.
6. **P2-10/P2-11:** идемпотентность `NPCService.Initialize()` /
   `NPCAIService.Initialize()` (guard `if (_initialized) return` или
   Dispose предыдущей подписки).
7. **P2-16/P2-17:** `GetAllSaves()` — читать `save_meta` (SavedAt/Version)
   или index-манифест для списка сейвов в UI.
8. **P2-18:** версия формата сейва + список обязательных блоков на версию
   (отличать старый сейв от повреждённого).
9. **P2-19:** сценарий «Load failed после ResetWorld» — пустой world-state
   до следующего NewGame; задокументировать/решить (например, Load до
   Reset при наличии валидного слота, или «safe-empty» маркер сессии).
10. **Aудит Фаза 3 (P2-9):** формулировку zero-GC уже уточнена в коде
    (стабильный publish-path — allocation-free; snapshot при мутации
    подписок и re-entrant closures — аллокации).

## 5. Порядок фиксов (выполнено в R32, тем же коммитом)

1. P1-1: `TradeService : IWorldResettable` (ResetWorld: честное закрытие
   сессии + очистка стока).
2. P1-2: `SaveAndQuit()` проверяет bool, при провале — тост + остаёмся
   в сессии (Playing), «Quitting (saved)» только при успехе.
3. P1-3: `CopyStatesTo(buffer)` + персистентные буферы в hot-path
   вызывающих (NPCAI ×2, NPCMovement, NPCModule attacks); GetAllStates()
   остаётся для холодных путей.
4. P1-4: `CorpseService : IWorldResettable`, явный вызов из
   NPCModule.ResetWorld() удалён (один владелец контракта).
5. P1-5: EventBus — пер-хендлерная изоляция исключений + дрен очереди в
   `finally` (стэл-события невозможны), честный комментарий аллокаций.
6. P1-6: ContainerBuilder — при замене регистрации ключом-K удалять
   «мёртвые» ключи старой регистрации (primary ServiceType потерян);
   мульти-форвард и last-wins не затрагиваются (C3/C4 — стражи).
7. P1-7: `SaveService.DeleteSave` — прозрачный bool; семантика
   «операция выполнена» (P2-15 попутно).
8. P1-8: Modules-layer `SaveFileHandler` — санация имени слота по
   правилам Adapter-хендлера (parity).

Регрессионный страж: сим 19 `AUDIT0922` в qa_regression.sh; после фиксов
VERDICT PASS (см. §6 после применения).

## 6. Постфикс-прогон

Все фиксы применены (R32); сим прогнан повторно:

```
[Audit0922Sim] OK   A1 исключение подписчика изолировано (не покидает Publish)
[Audit0922Sim] OK   A2 подписчики после падавшего получили исходное событие
[Audit0922Sim] OK   A3 re-entrant событие доставлено в дрене того же Publish (раньше чужих событий)
[Audit0922Sim] OK   A4 стэл-событий после чужого Publish нет (очередь не протухает)
[Audit0922Sim] diag: порядок доставки = [ping#1, ping#2, other#9]
[Audit0922Sim] OK   B1 имя '../escape_probe' не покидает каталог сейвов (санация)
[Audit0922Sim] OK   B2 легитимное имя слота сохраняется/читается
[Audit0922Sim] OK   C1 Resolve<ISaveFileHandler> даёт адаптер-инстанс (override работает)
[Audit0922Sim] diag: Resolve<Modules.SaveFileHandler> (stale concrete-ключ) → нет регистрации (ключ удалён)
[Audit0922Sim] OK   C2 stale concrete-ключ старой регистрации удалён (override чистит хвосты)
[Audit0922Sim] OK   C3 мульти-форвард impl на 2 интерфейса — один синглтон (prune не рвёт)
[Audit0922Sim] OK   C4 повторная регистрация интерфейса — последний побеждает
[Audit0922Sim] OK   C5 concrete-ключ наследуется живой регистрацией (NPC-паттерн мульти-форварда)
[Audit0922Sim] diag: DeleteSave(несуществующий) → False
[Audit0922Sim] OK   D1 удаление несуществующего слота возвращает false
[Audit0922Sim] OK   D2 удаление существующего слота — true и файл удалён
[Audit0922Sim] diag: TradeService is IWorldResettable → True
[Audit0922Sim] OK   E1 TradeService реализует IWorldResettable (ResolveAll видит сброс)
[Audit0922Sim] diag: CorpseService is IWorldResettable → True
[Audit0922Sim] OK   E2 CorpseService реализует IWorldResettable (явный контракт)
[Audit0922Sim] VERDICT: PASS — EventBus isolation/re-entrancy, path sanitisation, DI override prune, DeleteSave honesty, Trade/Corpse reset-контракты
```

Сим закреплён как №19 (AUDIT0922) в qa_regression.sh — постоянный
регрессионный страж инфраструктурных контрактов.

Примечание к C5 (для аудитора — важный архитектурный вывод): наивный
prune «смерть primary-ключа ⇒ удалить forwarding-ключи» НЕПРАВИЛЕН для
этого контейнера и был отвергнут двумя живыми регрессиями (первый вариант
ломал Resolve(NPCService) для конструктора CorpseService; второй —
Resolve(TechniqueService) для конструктора CombatService: COMBAT_SIM/
COMBATAI/ANIMALQA падали на NewGame). Мульти-интерфейсный паттерн
(`Register<INPCService,X>` + `Register<ISaveable,X>` + перезапись
ISaveable следующими модулями) ДЕРЖИТСЯ на выживании forwarding-ключей —
по ним резолвятся concrete-параметры конструкторов и собирается
ResolveAll<ISaveable>. Финальная семантика фикса: pruning только при
ИНСТАНС-оверрайде (RegisterInstance — адаптер подменяет single-provider
сервис: ISaveFileHandler); обычные Register<>-перезаписи ключей чужие
ключи не трогают (ключи независимы — префикс-семантика контейнера).
Страж C5 закрепляет это правило.
