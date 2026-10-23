# Чекпоинт: Этап-10 аудита владельца — жизненный цикл мира (ST10-01..04)

**Дата:** 2026-10-23 08:45 UTC
**Сессия:** coder-1023 (main Z.ai Code, режим lite)
**Тип:** audit → fix + regression
**Задание:** checkpoints/10_23_stage10_lifecycle_audit_request.md ([STAGE 10 AUDIT])

---

## Коммиты

| Коммит | Тип | Содержимое |
|--------|-----|-----------|
| `952251d` | test | Stage10WorldLifecycleTruthSimDebug (16 ассертов) + регистрация GODOT_STAGE10_DEBUG (STAGE10, №28) + префикс-лог |
| `712ecdc` | fix | **ST10-01** (ResourceService + IWorldResettable) + **ST10-03** (NPCGroupService подписка NPCDeathEvent, wiring NPCModule.Start) |
| `b62babc` | fix | **ST10-04b** (новый дефект, найден симом): календарные компоненты независимы, Month/Year не вложены в ветку дня |

## Окружение и команды

- Сборка: `dotnet build` — **0 errors** (.NET SDK 8.0.425/9.0.319).
- Сим: `GODOT_NEWGAME=1 GODOT_STAGE10_DEBUG=1 $GODOT --headless --path . scenes/MainMenu.tscn`
  (Godot 4.7.2 stable mono, `ed1daf0bf`).
- Зона воздействия: `bash tools/qa_regression.sh AUDIT0922 QUEST RESPAWN STAGE4 SAVELOAD REASSEMBLY ANIMALQA` — **7/7 PASS**;
  смежные: `STAGE9 KILLFEED LOOT` — **3/3 PASS** (STAGE9-сим жив после фиксов Этапа-10).
- Логи: `checkpoints/10_23_stage10_prefix.log` (до фиксов),
  `checkpoints/10_23_stage10_lifecycle.log` (итоговый, постфикс).

## Верификация находок

| ID | Вердикт | Доказательство |
|----|---------|----------------|
| ST10-01 | **ПОДТВЕРЖДЁН → ИСПРАВЛЕН** (изоляция миров) + **GAP** (save/load) | Код: ResourceService — singleton, НЕ IWorldResettable, НЕ ISaveable; _depleted переживал пересборку; TileService.OnResourceRespawned восстанавливает объект по X/Y нового мира. Runtime-репро: префикс A4/A5 FAIL («НЕ IWorldResettable», «очередь остаётся armed»). Фикс 712ecdc: ResetWorld сбрасывает очередь (координатор ResolveAll — инстанс-матчинг, без регистрации). Постфикс A4/A5 PASS: после ResetWorld RespawnCheck(+365 дней) = 0 событий. **GAP (решение владельца):** тайлы не сохраняются в сейв (карта регенерируется) — состояние «истощённости» невозможно восстановить без персистентности тайлов; после LoadGame ресурс на регенерированной карте доступен сразу. Варианты: сохранить tile-deltas (новая фича) или зафиксировать поведение в доках |
| ST10-02 | **ОПРОВЕРГНУТ** | TTL-очистка ПОДКЛЮЧЕНА к игровому тику: NPCModule.Tick:177 → RemoveOldCorpses(1440 игросек) (R13; статический проход не нашёл вызов — он есть). Сервисный уровень: B1 — свежие трупы (возраст < TTL) живут и доступны для обыска; B2 — состаренный (2000+ > 1440) снят, CorpseRemovedEvent опубликован (рендер-маркер/окно обыска чистятся — RemoveInternal); B3 — выборочность (снят только состаренный). CorpseService : ISaveable+IWorldResettable (R13/P1-4) |
| ST10-03 | **ПОДТВЕРЖДЁН → ИСПРАВЛЕН** | Код: никто не вызывал RemoveMember при смерти (только перенос между группами и QA). Runtime-репро: префикс C1-C3 FAIL — мёртвый оставался участником и лидером, пустая группа не расформировывалась (висячие ссылки переживали Save/Load). Фикс 712ecdc: Initialize() из NPCModule.Start (паттерн CorpseService), NPCDeathEvent → RemoveMember: лидерство → следующему, пустая → DisbandGroup; группы смешанные NPC+звери (GAP-014) — подписка на общий NPCDeathEvent покрывает оба. Постфикс C1-C3 PASS |
| ST10-04 | **КОНТРАКТ ПОДТВЕРЖДЁН** (P1-13 жив) + **НОВЫЙ ДЕФЕКТ ST10-04b → ИСПРАВЛЕН** | Задокументированное поведение подтверждено: D1 — в BulkAdvanceTicks ровно 1 TimeHitchedEvent, календарные события в скачке не публикуются; D2 — первый естественный тик: ровно 1 DayChanged (финальная дата), маркеры синхронизированы, последующие тики молчат. **ST10-04b** (найден симом D3a/D3b, префикс FAIL): Month/Year события были ВЛОЖЕНЫ в ветку t.Day != _lastDay — скачок ровно на N месяцев/лет (day-of-month не меняется) молчал ВООБЩЕ. Фикс b62babc: компоненты независимы, маркеры синхронизируются каждый тик; живой мир (день 30→1) не изменился. Постфикс D3a (+30 дней): Month 1×, день/год молчат; D3b (+360 дней): Year 1×, месяц/день молчат |
| ST10-04-остаток | **GAP** | Подписчики, считающие события (квесты «прожить N дней», старение), недосчитывают hitched-интервал: ≤1 DayChanged на любой скачок; скачок ровно в N месяцев/лет не меняет младший компонент — его событие молчит по значению. Дизайн-решение за владельцем (считать hitched-время по TimeHitchedEvent или оставить). D4 в сводке сима |

## Результаты по сценариям (16 PASS / 0 FAIL / 0 NOT TESTED / 1 GAP / 1 REF)

```
A1  PASS  граница задержки: до срока 0, в срок 1, повторный RespawnCheck — 0 (изъят)
A2  PASS  мульти-ресурсы на разных координатах: берёза (5) и руда (30) в свои сроки
A3  PASS  повторное истощение клетки — новая запись, респаун повторяется
A4  PASS  ResourceService : IWorldResettable (префикс FAIL — воспроизведение)
A5  PASS  ResetWorld → очередь очищена; RespawnCheck(+365) — 0 событий (изоляция миров)
A6  PASS  Initialize ×3 идемпотентен; DayChanged-цепочка — ровно 1 респаун due-записи
B1  PASS  свежие трупы живы после RemoveOldCorpses(1440) (возраст < TTL)
B2  PASS  состаренный (2000+) снят; CorpseRemovedEvent → рендер/окно обыска чистятся
B3  PASS  выборочность; wiring — NPCModule.Tick:177 (R13)
C1  PASS  смерть лидера → исключён, лидерство следующему (префикс FAIL — воспроизведение)
C2  PASS  последовательные смерти: лидерство migrating, состав сжимается
C3  PASS  пустая группа расформирована (счётчик −1), member→group реестр чист
D1  PASS  BulkAdvanceTicks: ровно 1 TimeHitched, календарные события в скачке молчат
D2  PASS  первый тик после скачка: 1 DayChanged (финальная дата), стабилен
D3a PASS  +30 дней: MonthChanged 1× (префикс FAIL — воспроизведение ST10-04b)
D3b PASS  +360 дней: YearChanged 1× (префикс FAIL)
D4  GAP   hitched-счёт дней/месяцев подписчиками — решение за владельцем
E2  REF   PT-C02 (агро-сброс на смерть игрока) — RESPAWN-сим в зоне контроля этапа; физический пункт плейтеста симулятором не закрывается (канон)
Итого: PASS=16 FAIL=0 NOT TESTED=0 GAP=1 REF=1
VERDICT: PASS — ST10-01..04 проверены
```

## Порядок фаз Reset → генерация → Restore (ответ на A4 задания)

WorldDomainResetCoordinator сбрасывает ResolveAll<IWorldResettable> в
фазе WorldDomainResetPhase ДО генерации карты и RestoreState: ResourceService
(теперь IWorldResettable) сбрасывается автоматически в корректной точке —
armed-очередь старого мира умирает ДО того, как новая карта существует.
Проверено REASSEMBLY-симом (реестры world-scoped не переживают пересборку,
включая группы/трупы/время) в зоне контроля.

## Регрессия зоны воздействия (после всех фиксов)

AUDIT0922 (включая P1-13/P1-15 time-ассерты — календарный фикс совместим),
QUEST, RESPAWN (агро-сброс PT-001 жив), STAGE4, SAVELOAD, REASSEMBLY,
ANIMALQA — **7/7 PASS**; STAGE9/KILLFEED/LOOT — **3/3 PASS**.

## Остаточные риски и непроверенные сценарии

1. **GAP-сохранение ресурсов**: без персистентности тайлов LoadGame
   восстанавливает ресурсы регенерацией карты (немедленная доступность
   истощённого ранее ресурса) — задокументировано, решение за владельцем.
2. **GAP-hitched-счёт**: см. D4.
3. **Despawn без смерти**: TrySpawnEventNpc-сущности живут до пересборки;
   иных внутрисессионных удалений нет (R14). Если появятся — им нужен тот же
   контракт исключения из группы (сейчас только смерть).
4. NPCGroupService.Initialize-токен не диспозится при пересборке (паттерн
   CorpseService-токена: процесс-синглтон; ResetWorld очищает карты, которые
   читает обработчик — межмировых протечек нет, REASSEMBLY подтверждает).
5. Физический плейтест пунктов этапа (реальная смерть в UI, реальные
   караваны/патрули после загрузки) — не выполнялся; сим работает через
   сервисные контракты и события.

## Следующие шаги

- Этапы 11/12 — задания в git (`10_23_stage11_body_audit_request.md`,
  `10_23_stage12_progression_audit_request.md`): ST11-001..007, ST12-001..006,
  playtest-дополнения PT-B01..B06 / PT-PR01..PR06 + правка PT-C02 в
  portal/data/playtest.json (статусы владельца не трогаются).
- Общий анализ десяти этапов — после отчётов 11/12.

## Файлы

- Сим: `game/src/Adapter/Scene/Stage10WorldLifecycleTruthSimDebug.cs`
- Фиксы: `game/src/Modules/Tile/ResourceService.cs`,
  `game/src/Modules/NPC/NPCGroupService.cs`, `game/src/Modules/NPC/NPCModule.cs`,
  `game/src/Modules/World/WorldModule.cs`
- Логи: `checkpoints/10_23_stage10_prefix.log`, `checkpoints/10_23_stage10_lifecycle.log`
- Отчёт: этот файл
