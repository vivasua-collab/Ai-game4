# R16 — Боевая система: ИИ NPC, рабочий бой игрок↔NPC, анимация удара

**Дата:** 2026-09-10 (сессия №5, 09:39–... UTC)
**Задача пользователя:** «Следующий этап — доработка боевой системы. ИИ NPC для
боевой системы. Необходима рабочая реализация боев между игроком и NPC. Так же,
необходима простая анимация нанесения удара. Сначала изучение документации,
изучение кода, что уже реализовано, после план внедрения и непосредственно
реализация в коде.»

---

## 1. Аудит (что уже реализовано / чего не хватает)

### Есть (COMBAT_SYSTEM.md §1.4, NPC_AI_SYSTEM.md):
- Turn-based 1v1 ядро: CombatService (владение ходом, инициатива, тайм-аут
  чужого хода 2.5с), 11-слойный пайплайн урона (DamageService), pending-каст.
- Путь атаки игрока: Space → PlayerCombatAdapter (ближ. цель, LOS, кулдаун
  §8.1) → AttackIntentEvent → CombatModule → CombatService → DamageAppliedEvent.
- Путь атаки NPC: NPCAIService (угрозы/диспозиции) → Attacking →
  NPCMovementService сближение → NPCModule.ProcessNpcAttacks (кулдаун 1.6с,
  дальность оружия) → AttackIntentEvent.
- Визуал: цифры урона (DamageNumberRenderer), HP-бары/имена NPC, трассеры
  стрел (ProjectileRenderer), kill-feed, оружие в руках (R15).

### Дефекты (почему бой игрок↔NPC НЕ «рабочий»):
- **D1 (критично) — NPC не отвечает на атаку игрока.** Игрок начинает бой →
  CombatStartedEvent → NPCCombatAdapter ставит IsInCombat=true, но AIState
  остаётся Idle/Wandering; NPCAIService.EvaluateAndDecide выходит по гейту
  `if (state.IsInCombat) return;` → NPC никогда не входит в Attacking →
  ProcessNpcAttacks его не бьёт. Игрок избивает «манекен» (ответ — только если
  NPC сам агрился первым).
- **D2 — NPC в бою не бежит при HP<20%** (проверка Flee внутри EvaluateAndDecide
  ПОСЛЕ гейта IsInCombat; спека NPC_AI_SYSTEM §4.2: Attacking → HP<20% → Fleeing).
- **D3 — нет leash (aggro-drop):** NPC в бою преследует игрока вечно; игрок не
  может выйти из боя бегством.
- **D4 — NPC не защищается:** CombatService передаёт DefenseSubtype.None для
  NPC-защитника → слои Dodge/Parry/Block (§7) мертвы для NPC.
- **D5 — «мёртвая проводка» защиты игрока:** IsDefendPressed никем не читается,
  действие "defend" даже не зарегистрировано в InputMapInitializer.
- **D6 — фантомный CombatStartedEvent:** NPCCombatAdapter.MarkNpcCombatStarted
  публикует событие напрямую (CombatService боя ещё нет) → двойной publish при
  реальном старте (первый принятый интент).
- **D7 — лучники лезут в melee:** ProcessAttacking всегда сближает до
  AttackRadius=1.5, дальность оружия не используется для kiting.
- **D8 — нет анимации удара** (запрос пользователя): только цифры урона.

## 2. План внедрения

### A. ИИ NPC для боя (модуль NPC)
1. **Месть/ответ (D1):** NPCAIService подписывается на CombatStartedEvent —
   участник-NPC решает «драться или бежать» (RetaliateOrFlee по личности:
   Guard/Enemy/Monster/Aggressive/Vengeful → Attacking; Pacifist/Cautious →
   Fleeing; прочие → Attacking). OnDamageApplied дублирует решение (ответ на
   удар вне формального боя).
2. **Бегство в бою (D2):** проверка healthRatio ≤ FleeHealthRatio переносится
   ДО гейта IsInCombat; при бегстве из активного боя публикуется
   CombatDisengageEvent.
3. **Leash (D3):** IsInCombat + цель-игрок дальше AggroRadius×3 →
   CombatDisengageEvent + Wandering + очистка угроз.
4. **MarkNpcCombatStarted (D6):** прямая пометка IsInCombat/TargetId обоим
   NPC-участникам БЕЗ фантомного publish (событие публикует только
   CombatService.StartCombat при первом принятом интенте).
5. **Kiting лучников (D7):** NPCMovementService.ProcessAttacking — дальнобойное
   оружие: dist<3 → отход, dist≤AttackRange оружия → стоять, иначе сближение.

### B. Боевые механики (модуль Combat)
6. **NPCDefenseSelector (D4, новый pure-C# класс):** щит (WeaponOff) → Block;
   оружие и STR>AGI+4 → Parry; иначе Dodge. CombatService: для NPC-защитника
   подставляет выбор селектора (заодно чинит NPC-vs-NPC: раньше защитник-NPC
   получал стойку ИГРОКА _lastPlayerDefense).
7. **CombatService.AbandonCombat(entityId):** стадия Flee → EndCombat —
   авторитетное завершение при disengage. CurrentPlayerDefense — QA-геттер.
8. **Стойка защиты игрока (D5):** клавиша G (InputMapInitializer+InputAdapter
   sticky "defend") → PlayerCombatAdapter циклирует стойку
   None→Dodge→(Parry если оружие)→(Shield если WeaponOff)→… и публикует
   DefenseIntentEvent → CombatModule → ICombatService.ExecuteDefense. Тост —
   GameWorldController (подписка на DefenseIntentEvent).

### C. Анимация удара (Adapter/Scene, статичные спрайты — без PNG)
9. **StrikeFxRenderer (новый, Node2D+_Draw):** AttackIntentEvent (melee) →
   «свип»-дуга у цели по направлению атаки (бледная); DamageAppliedEvent
   (melee Hit/Crit/Parry/Block) → яркий слэш + искры (крит — золотой, крупнее).
   Пул структур, счётчики для QA.
10. **Замах оружия игрока:** GWC подписка AttackIntentEvent (attacker=player,
    melee) → таймер 0.42с → выпад PlayerMainHand к цели (sin-кривая) в
    _PhysicsProcess.
11. **Замах оружия NPC:** NPCSpriteRenderer подписка AttackIntentEvent
    (attacker=NPC) → per-NPC swing → DrawNpcWeapon смещает спрайт оружия к
    цели (зеркалирование учтено: drawn dx = ±dirX·k при facingLeft/right).

### D. QA
12. **CombatAISimDebug (GODOT_COMBATAI_DEBUG=1):** (1) игрок атакует NPC → NPC
    отвечает (Attacking/Fleeing); (2) урон в обе стороны; (3) селектор защит —
    детерминированные кейсы; (4) HP<20% → Fleeing + CombatDisengage;
    (5) leash — телепорт игрока → disengage; (6) стойка игрока →
    CurrentPlayerDefense; (7) StrikeFx счётчики. VERDICT PASS/FAIL.
13. Регрессии: базовый build 0 errors + существующие хуки
    (COMBAT/LOOT/SAVELOAD/KILLFEED/...).

### E. Документация
14. docs_v2: COMBAT_SYSTEM §1.4/§7 (retaliation/leash/flee/NPC-защита/стойка G),
    NPC_AI_SYSTEM (статус реализации), MODULE_STRUCTURE §2.7/§4,
    TESTING_RULES §0.1 (+хук), SPRITE_CATALOG (StrikeFX §).
    SESSION_CONTEXT/SESSION_SUMMARY.

## 3. Границы (осознанно НЕ в этой задаче)
- Анимации движений/смерти (этап «внедрение анимаций» — позже).
- NPC-vs-NPC массовый бой, групповое AI (GROUP_SYSTEM — отдельная фаза).
- PNG-ассеты (StrikeFX — процедурный _Draw).
- Авто-замедление времени в бою (§1.2 superSuperSlow) — кандидат на R17.

---

## 4. Результаты внедрения (вторая фаза работы)

### Реализовано (все пункты плана §2 A–D)
- **A. ИИ NPC:** месть (RetaliateOrFlee по личности), бегство в бою ДО
  гейта IsInCombat, leash 15 тайлов (CombatDisengageEvent), анти-flip-flop
  (гейт healthRatio на пути угроз — иначе раненый NPC входил/выходил из
  боя бесконечно), MarkNpcCombatStarted без фантомного publish,
  OnCombatDisengage сброс участников, kiting лучников (KiteMinRange=3).
- **B. Механики:** NPCDefenseSelector в CombatService (isPlayerTarget-
  ветвление — заодно чинит NPC-vs-NPC), стойка G (PlayerCombatAdapter
  цикл + DefenseIntentEvent + CombatModule мост + тост GWC),
  AbandonCombat/CurrentPlayerDefense в ICombatService.
- **C. Анимация:** StrikeFxRenderer (свип 0.25с/слэш+искры 0.22с/крит
  золотой, ZIndex Objects+2), замах игрока (MainHand sin-выпад 12px +
  Scale-пульс, Rotation исключён — конфликт FlipH), замах NPC
  (NPCSpriteRenderer, инверсия dx при facingLeft-зеркале 2cx−x).
- **D. QA:** CombatAISimDebug (GODOT_COMBATAI_DEBUG=1, 7 шагов) +
  визуальный HOLD (GODOT_STRIKEFX_HOLD=1).

### Попутно исправленные баги (найдены QA-прогоном)
1. **NPCService.GetNPCState(null) → ArgumentNullException** (спящий
   пре-existing: CombatEndedEvent при Flee-финале несёт null Winner/Loser;
   OnCombatEnded падал, УБИВАЯ NPCModule.Tick — AI всей карты замерзал).
   Фикс: null-гвард в GetNPCState + null-проверки в OnCombatEnded.
   Диагностировано через stack trace (GameEntryPoint теперь логирует стек).
2. **ExecuteDefense игнорировал вызовы вне боя** — стойка игрока не
   ставилась между боями (ранний `if (!_isInCombat) return`).
3. QA-баг счётчика урона по NPC (не инкрементировался) — _trackedNpcId.

### QA (полный прогон 2026-09-10)
- build: **0 errors** (warning-набор прежний).
- **GODOT_COMBATAI_DEBUG=1 → VERDICT: PASS** (7/7: месть, двусторонний
  урон, селектор, бегство, leash, стойка, анимация-счётчики).
- Регрессии 12/12 PASS: COMBAT_SIM, KILLFEED, TRASHDROP, SAVELOAD, LOOT,
  WEAPONVIS, REASSEMBLY, CONTEXT, STORAGE, QUEST, HOTBAR, DOT, CHARGE.
- **Визуально (Xvfb 1920×1080 opengl3 + VLM):** подтверждены дуга-слэш
  между персонажами, искры, цифры урона «−27», выпад оружия NPC в
  сторону игрока, HP игрока 482/500 (реальный урон в живом бою).

### Документация (workflow §3.3)
- COMBAT_SYSTEM.md §1.4.1 (боевой ИИ NPC), §1.4.2 (стойка G), §7 (шапка).
- NPC_AI_SYSTEM.md — статус реализации R16 в шапке.
- MODULE_STRUCTURE.md §2.4 (Combat: контракты/методы/селектор),
  §2.7 (NPC: подписки/сервисы/визуал).
- TESTING_RULES.md §0.1 — реестр 31→33 (+COMBATAI_DEBUG, +STRIKEFX_HOLD).
- SPRITE_CATALOG.md §22 (StrikeFX состав/параметры).
- SESSION_CONTEXT.md / SESSION_SUMMARY.md — сессия №5.
- HotkeysWindow (F1): строка «G — стойка защиты».
