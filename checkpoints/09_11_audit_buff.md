# АУДИТ: Buff

**Дата:** 2026-09-11 (bodybuff-audit-agent). **HEAD:** `d0b065d`
**Скоуп (READ-ONLY):** Modules/Buff — 7 файлов (BuffService 532, ActiveBuff 66,
BuffCalculator 121, BuffConfig 21, BuffModule 45, BuffModuleServices 40,
BuffTickProcessor 64). Проводка: BuffContracts.cs, IBuffService; продюсеры —
Combat/CombatConsequencesService, Combat/ElementalEffectService,
Body/SeveredDebuffSystem, NPC/PerkService; потребители — Combat/DamageService
(слои 3a/3b), Body/BodyModule (BuffTickedEvent); очистка —
NPC/NPCSpawnerService (деспавн/ResetWorld), NPC/AnimalService.ClearAnimals.
Единица времени: DeltaTime=1.0/тик (WorldService.cs:80-89) — «секунды» = игроминуты.

---

## Сводка

- Находок: 11 (2×P1, 5×P2, 4×P3).
- Системный вывод: **стат-модифицирующие баффы не влияют на игру вообще**
  (BUF-1: модификатор процентных баффов всегда 0). Живые эффекты модуля —
  DoT-тики Poison/Burn с хардкод-уроном (BUF-4) и Purify. Кровотечение не
  работает (BUF-2), стан без потребителя (BUF-6), баффы не сохраняются (BUF-7).
- DoT-проводка с Body/Combat (единый DamageAppliedEvent-пайплайн, смерть от
  DoT, BUFF-A1/A3-фиксы) — корректна, QA DOT подтверждает.

---

## Находки

### BUF-1 [P1] Процентные баффы дают нулевой модификатор (контракт GetStatModifierPermil нарушен)

**Файлы:** BuffCalculator.cs:30-54; BuffService.cs:219-236; потребители
DamageService.cs:162-168 (слой 3a), 277-283 (слой 3b).
**Доказательство:** CalculateStatModifier: modifier = flatSum×(1+percentSum) +
baseValue×percentSum — корректная «дельта от базы», но GetStatModifier вызывает
её с baseValue=0 (BuffService.cs:224), а все стат-баффы эвристики —
IsPercentage=true (AttackBoost/DefenseBoost/SpeedBoost/Slow и default) →
flatSum=0 → **modifier=0 всегда**. Потребители боя ждут промилле-дельту
(IBuffService.cs:38-43: «1200 = ×1.2»; DamageService: ×(1000+atkBuffPermil)/1000)
— получают 0. Затронуто: combat_shock, elemental_slow, перки, любые boost-типы.
StatModifierChangedEvent публикуется с 0, подписчиков нет (grep). SoftCaps
(B.3) применяются, но на входе всегда 0.
**Фикс:** percent-only баффы → возвращать percentSum (для permil-контракта —
percentSum×1000) или передавать baseValue из потребителя. Сверить с докой B.2.

### BUF-2 [P1] «combat_bleed» не распознан эвристикой — кровотечение не существует

**Файлы:** CombatConsequencesService.cs:118-124; BuffService.cs:444-527.
**Доказательство:** порог >30% maxHP slashing/piercing → ApplyBuff(target,
"combat_bleed", 9f, potency=maxHP×5%). MapBuffIdToType не имеет ветки «bleed»
(и «freeze») → default (BuffService.cs:519-527): AttackBoost, Damage, +10%,
IsDebuff=false. DoT-урона нет (BodyModule.cs:99-121 case BuffType.Bleed
недостижим); в UI — «бафф»; Purify не снимает; HasBuff-гвард блокирует
повтор на 9 тиков. BODY_SYSTEM §8 (5 уровней кровотечения) не реализован.
Также: TICK_SECONDS=3 — пережиток Unity-секунд: 9f = 9 тиков вместо 3.
**Фикс:** ветка «bleed» → BuffType.Bleed, HasTickEffect, TickDamage из potency
(см. BUF-4), IsDebuff=true, длительность 3 тика.

### BUF-3 [P2] «combat_shock»/«elemental_void_pierce» маппятся в AttackBoost-БАФФ (инверсия)

**Файлы:** CombatConsequencesService.cs:173-177; ElementalEffectService.cs:184-188; BuffService.cs:519-527.
**Доказательство:** combat_shock (ожидание −20% статов) → default AttackBoost
+10% с potency 200 → TotalValue=+20 → percentSum +2000%. Сейчас эффект 0
(BUF-1), но после починки BUF-1 превратится в +2000% урона цели — бомба
замедленного действия. Фиксировать вместе с BUF-1/BUF-2.

### BUF-4 [P2] Мощность DoT игнорируется: тиковый урон — хардкод эвристики

**Файлы:** BuffTickProcessor.cs:42-50; BuffService.cs:469-484 (poison
TickDamage=10, burn=15); продюсеры ElementalEffectService.cs:124-131 (burn:
potency=5% урона удара), 209-219 (poison: 3% maxHP цели).
**Доказательство:** tickValue = TickDamage × CurrentStacks — Potency не
участвует. Горение ВСЕГДА 15/тик, яд 10/тик: кролик (~115 RedHP) — яд 10
вместо 3; фаербол 40 урона — 15/тик вместо 2. Промилле-баланс продюсеров
отбрасывается на входе в BuffService.
**Фикс:** TickDamage = база × Potency (семантика BF-A02) или явный tickValue-параметр.

### BUF-5 [P2] duration ≤ 0 не означает Permanent — «постоянные» баффы живут 30 тиков

**Файлы:** BuffService.cs:403-429 (duration≤0 → Duration=30f, Application=Duration);
ActiveBuff.cs:58 (IsExpired ложен только для Permanent — который никто не выставляет).
**Доказательство:** SeveredDebuffSystem.cs:225 вызывает ApplyBuff(duration:-1f)
с комментарием «Permanent = длится пока не снят» — фактически дебафф ампутации
истекает через 30 игроминут. IBuffService.cs:21 («duration=-1 — длительность
из BuffData») — BuffData не существует. Дока A.5 Permanent не реализована.
PerkService использует float.MaxValue — работает за счёт точности float (хак).
**Фикс:** duration<0 → Application=Permanent.

### BUF-6 [P2] Стан не имеет потребителя; иммунитеты не запрашиваются

**Файлы:** CombatConsequencesService.cs:149-155 (combat_stun);
ElementalEffectService.cs:149-155 (earth_stun 15%). Grep BuffType.Stun: только
EffectToImmunityMap (BuffService.cs:42) и маппинг ID — ни бой, ни AI не
проверяют стан (не блокирует ни хода, ни атаки). HasImmunity — 0 вызовов: яд
вешается даже при иммунитете (продюсеров иммунитетов тоже нет — латентно).
**Фикс:** проверка стана в turn-логике боя + HasImmunity при наложении.

### BUF-7 [P2] Баффы не сериализуются: Save/Load теряет эффекты; баффы переживают смерть игрока

**Файлы:** BuffModuleServices.cs:21-39 (нет ISaveable); очистка только
NPCSpawnerService.cs:205/:261 (деспавн/ResetWorld).
**Сценарий:** (а) сейв/лоад — баффы игрока (bleed/burn/poison/shock от NPC)
пропадают молча; (б) перки NPC (PerkService.cs:107, duration=MaxValue) и
_entityPerks не сохраняются — проводимость после загрузки без перков;
(в) смерть/respawn игрока НЕ снимает баффы (RespawnAfterDeath лечит тело,
Revive не трогает баффы) — DoT может тикать в «новой жизни»; (г) животные:
AnimalService.ClearAnimals (226-232) не вызывает RemoveAllBuffs (в отличие от
NPC-пути P2-X1) — баффы зверей утекают до истечения (ID не переиспользуются,
`_nextId++` — эффект P3: лог-спам).
**Фикс:** BuffSaveData per-entity + RestoreState; RemoveAllBuffs при
respawn'е игрока и в ClearAnimals.

### BUF-8 [P3] Стекинг: двойной учёт Value и Stacks

BuffService.cs:108-115 (existing.Value += existing.Potency при стеке) +
ActiveBuff.cs:55 (TotalValue = Value×Potency×CurrentStacks): 2 стека → 0.6
вместо 0.4. Путь латентный (все эвристики MaxStacks=1/Refresh).

### BUF-9 [P3] QiRestoration/StaminaRegen-тики — заглушка; HoT лечит только торс игрока

BodyModule.cs:92-97 (HealthRegen → HealPart(Torso) игрока независимо от
e.EntityId — HoT на NPC лечил бы торс ИГРОКА; TODO P1-08 в коде), 123-128
(default → Console.WriteLine: QiRestoration TickHealing=50 не восстанавливает
Ци никому). Продюсеров нет — латентно. Спам лога в default-ветке.

### BUF-10 [P3] GetElementResistance — мёртвый API (0 вызовов)

BuffService.cs:239-259. Резисты/уязвимости не запрашиваются пайплайном урона.
Латентно (продюсеров vulnerability/shield-ID тоже нет).

### BUF-11 [P3] Неизвестный buffId → тихая подмена семантики на AttackBoost +10%

BuffService.cs:519-527. Каждый severed_* (см. BOD-4), combat_shock,
elemental_void_pierce получает AttackBoost+10% Damage + предупреждение в
лог. «Загрузка из BuffData SO/JSON» (комментарий) не реализована — подстрочная
эвристика уже привела к BUF-2/BUF-3. **Фикс:** явный реестр Dictionary<string,
BuffDef> с fail-fast на неизвестные ID.

---

## Проверено чисто

- **TickBuffs (BUFF-A1/A3/A10):** снапшот сущностей ДО итерации, копия списка
  баффов, отложенное удаление после цикла, StatModifierChangedEvent
  пересчитывается ПОСЛЕ удаления (BuffService.cs:296-379) — мутации во время
  итерации исключены; пустые списки вычищаются.
- **DoT-проводка с Qi/Combat/Body:** BuffTickedEvent → BodyModule.cs:99-121 →
  DamageAppliedEvent(«dot:{BuffId}», цель, Torso, Qi|Physical по природе) →
  BodyService per-part → NPCCombatAdapter/AnimalService смерть. DoT реально
  убивает (QA DOT); Math.Max(1,(int)TickValue) — урон ≥1.
- **Purify (Light):** ElementalEffectService.cs:195-203 — итерация по копии
  (P2-7.1), снимает все IsDebuff — работает для poison/burn/slow/stun.
- **BuffTickProcessor:** сброс таймера вместо добавления (BF-I04) — дрейфа нет;
  тик срабатывает и в последний тик длительности.
- **RemoveAllBuffs:** BUFF-A3 — сбор affectedStats ДО Clear, события после;
  RemoveBuff/BuffRemovedEvent парность; null-гварды.
- **Лимит MaxBuffsPerEntity=20** с исключением «тот же buffId» (BuffService.cs:90-91).
- **DI:** BuffModule инжектит IBuffService + concrete BuffService — один
  синглтон (impl-forward контейнера); Configure один раз.
- **BuffContracts:** readonly-структуры Applied/Removed/Expired/Ticked/
  StatModifierChanged — zero-GC, поля на месте.

---

## Соответствие docs_v2 (BUFF_MODIFIERS_SYSTEM.md)

| Пункт | Код | Статус |
|---|---|---|
| A.2 баффы не трогают первичные статы | не трогают | ✅ (но SeveredDebuff проектирует STR/AGI/VIT — конфликт замысла, BOD-4) |
| A.3 28 типов | enum согласован по основным | ✅ [UNVERIFIED-полная сверка 28] |
| A.5 Permanent (пока не снят) | duration≤0 → 30 тиков | ❌ BUF-5 (P2) |
| A.5 Stack до maxStacks | двойной учёт (латентно) | ⚠ BUF-8 (P3) |
| A.6 длительность в игровых минутах | float, DeltaTime=1/тик | ✅ (TICK_SECONDS=3 — пережиток) |
| B.2 формула (base+flat)×(1+percent) | baseValue=0 у потребителя | ❌ BUF-1 (P1) |
| B.3 мягкие капы | ApplySoftCap/GetSoftCapParams совпадают | ✅ (вход всегда 0) |
| A.8 attack_boost_20 +20% | модификатор 0 | ❌ BUF-1 |
| ИНТЕГРАЦИЯ «с техниками/предметами» | продюсеры только combat-эффекты | ⚠ частично |

**Следующий шаг:** BUF-1+BUF-2+BUF-3 одним коммитом (реестр BuffDef вместо
эвристики + percent-семантика модификатора); затем BUF-5 (Permanent) и BUF-7
(Save/Load), в связке с BOD-4 аудита Body.
