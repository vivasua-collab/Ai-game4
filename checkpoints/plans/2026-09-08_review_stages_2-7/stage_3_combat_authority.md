# Этап 3 — Боевой цикл, NPC-атаки, расход боеприпасов

> Статус: ⬜ план → исполнение. Файлы: CombatModule/CombatService/CombatAIService/CombatRangeGateService/
> CombatLos/DamageService, PlayerCombatAdapter, NPCModule/NPCCombatAdapter, CombatContracts.

## Находки ревью → план валидации и фикса

### P0-1. Turn-based state machine не является авторитетным гейтом
**Клейм ревью:** ExecuteAttack не проверяет стадию боя (PlayerTurn/EnemyTurn); intents публикуются адаптерами без проверки владения ходом; стадия меняется как побочный эффект удара.
**Валидация:** прочитать CombatService.ExecuteAttack, PlayerCombatAdapter, NPCModule.ProcessNpcAttacks — есть ли проверка стадии.
**Фикс:**
- В `CombatService` ввести `CanAct(entityId)`: игрок → PlayerTurn, NPC → EnemyTurn (или игрок-инициатор вне боя — старт нового боя).
- Проверка ДО расхода ресурсов и старта каста; при отказе — `AttackRejectedEvent` с причиной (не ваш ход).
- Адаптеры (Player/NPC) НЕ дублируют логику — authority только в CombatService.

### P0-2. Два конкурирующих NPC-AI механизма; фантомный "enemy"
**Клейм ревью:** CombatModule.Start() инициализирует CombatAIService с хардкодом entity ID "enemy"; CombatModule.Tick() на EnemyTurn зовёт ExecuteAIAction; реальные NPC уже шлют AttackIntentEvent сами через NPCModule.ProcessNpcAttacks. Фантом не имеет тела/Ци/характеристик.
**Валидация:** прочитать CombatModule.Start/Tick, CombatAIService, NPCModule.ProcessNpcAttacks; проверить, используется ли CombatAIService в рантайме (QA COMBAT_SIM показывает, кто реально атакует).
**Фикс (в духе решения пользователя о легаси):**
- Убрать CombatAIService из runtime-цикла CombatModule (legacy, вводит в заблуждение). Реальные NPC уже атакуют через NPCModule.
- Если фантомный путь действительно мёртв после P0-1 (гейт по стадии) — удалить CombatAIService и ExecuteAIAction, оставив реальный путь NPCModule единственным источником NPC-атак.
- Проверить QA COMBAT_SIM: бой должен проходить без фантомного "enemy".

### P1-3. Стрела списывается до валидации атаки
**Клейм ревью:** CombatModule.OnAttackIntent для ranged: LOS → TryConsumeRangedAmmo → ExecuteAttack; при отказе ExecuteAttack (каст идёт / не тот ход после P0-1) стрела уже потеряна.
**Валидация:** прочитать CombatModule.OnAttackIntent, CombatRangeGateService, TryConsumeRangedAmmo.
**Фикс:**
- Разделить `HasAmmo` (проверка) и `ConsumeAmmo` (списание).
- Списывать стрелу ТОЛЬКО после прохождения всех проверок CombatService (готовность атаки), непосредственно перед ударом/снарядом.
- Структурированный результат: accepted/reason — и списание в одной authoritative точке.

### P1-4. Чужой NPC переключает цель глобального боя
**Клейм ревью:** IsInCombat=false → новый бой; иначе intent всё равно передаётся в ExecuteAttack, который безусловно заменяет _currentTargetId. Несколько агрессивных NPC перетирают цель.
**Валидация:** прочитать CombatService.ExecuteAttack, поля _instigatorId/_currentTargetId, NPCModule tick-обход Attacking.
**Фикс (MVP-паттерн 1v1, явно зафиксировать):**
- Принимать intent только от участников текущего боя (_instigatorId/_currentTargetId).
- Чужие intents → AttackRejectedEvent("не участник боя").
- _currentTargetId меняется только явным target-switch от активного участника (игрока).
- Задокументировать в docs: бои 1v1 до encounter-модели.

### P2-5. NPCCombatAdapter.StartAttack — вводящий в заблуждение legacy API
**Клейм ревью:** публикует только CombatStartedEvent, на который CombatService не подписан; реальный путь — NPCModule.ProcessNpcAttacks.
**Валидация:** проверить потребителей CombatStartedEvent и StartAttack.
**Фикс:** переименовать в MarkNpcCombatStarted (состояние NPC) либо удалить, если мёртв. Не описывать как механизм начала атаки.

## QA-план

- Регрессия COMBAT_SIM (melee+ranged+LOS/ammo) — PASS обязателен.
- Новый хук/расширение: проверка turn-gate (атака в чужой ход → AttackRejected, Ци/стрелы не расходуются) и неизменности _currentTargetId при чужом intent.
- Полная регрессия остальных хуков (TOAST/LOWHP/.../REASSEMBLY).

## Коммит
`fix(review-3): combat authority — turn-gate, фантомный enemy, ammo-транзакция, целостность цели` + push.
