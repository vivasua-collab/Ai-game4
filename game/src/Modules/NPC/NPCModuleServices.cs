#nullable enable
// Создано: 2026-05-10 — Phase 17B: делегат регистрации модуля
// Migrated from Ai-game3 (Unity+VContainer+MessagePipe) to Ai-game4 (Godot+DI+EventBus) 2026-08-15.
// Редактировано: 2026-08-22 — Phase C: регистрация AnimalService (простые животные).
// Редактировано: 2026-09-10 — R13 FULL-LOOT: регистрация CorpseService
//   (трупы-контейнеры лута) и NPCSpawnCompositionService (генерация состава).
using CultivationGame.Core.DI;
using CultivationGame.Core.Interfaces;
using CultivationGame.Modules.Inventory;
using CultivationGame.Modules.Qi;

namespace CultivationGame.Modules.NPC;

/// <summary>
/// Делегат регистрации публичных сервисов модуля NPC.
/// </summary>
public static class NPCModuleServices
{
    public static void Register(IContainerBuilder builder)
    {
        // === Конфигурация по умолчанию ===
        var config = new NPCConfig();
        builder.RegisterInstance(config);

        // === Публичные сервисы ===
        builder.Register<INPCService, NPCService>(Lifetime.Singleton);
        builder.Register<ISaveable, NPCService>(Lifetime.Singleton);
        builder.Register<INPCSpawnerService, NPCSpawnerService>(Lifetime.Singleton);

        // === Генерация NPC ===
        builder.Register<SoulGenerator>(Lifetime.Singleton);
        builder.Register<NPCSpeciesSelector>(Lifetime.Singleton);
        builder.Register<NPCAssemblyService>(Lifetime.Singleton);
        builder.Register<NPCNameGenerator>(Lifetime.Singleton);

        // === Провайдеры данных per-entity ===
        // NOTE: IQiDataProvider and IEquipmentDataProvider are registered in Qi/Inventory modules.
        // NPC module uses QiDataProvider + EquipmentDataProvider via concrete type for NPC-specific work.
        builder.Register<QiDataProvider>(Lifetime.Singleton);
        builder.Register<EquipmentDataProvider>(Lifetime.Singleton);

        // === Внутренние сервисы ===
        builder.Register<NPCRelationshipService>(Lifetime.Singleton);
        builder.Register<NPCAIService>(Lifetime.Singleton);
        builder.Register<NPCCombatAdapter>(Lifetime.Singleton);
        builder.Register<NPCQiRegenService>(Lifetime.Singleton);
        builder.Register<IPerkService, PerkService>(Lifetime.Singleton);
        builder.Register<NPCMovementService>(Lifetime.Singleton);
        builder.Register<NPCVisualService>(Lifetime.Singleton);

        // === Phase C: простые животные (волк/олень/кролик) ===
        // AnimalService — ITickable singleton; collected by GameEntryPoint's
        // ResolveAll<ITickable>() and ticked once per game tick for wandering.
        // Body assembly uses IBodyFactory + IBodyDataProvider (registered in
        // BodyModuleServices); ITileService (TileModule); SpeciesRegistry (Body).
        builder.Register<AnimalService>(Lifetime.Singleton);
        // 2026-09-11 (аудит боя с животными): Core-интерфейс для чужих модулей —
        // таргетинг Space (PlayerCombatAdapter), трупы (CorpseService), статы
        // вида (StatProviderAdapter), имена UI (DamageNumberRenderer/EventLog).
        // До интерфейса волки были невидимы для боевого таргетинга (D1).
        builder.Register<IAnimalService, AnimalService>(Lifetime.Singleton);

        // === GROUP-SPAWN: группы NPC (патруль, escort, guard area, hunting pack) ===
        // NPCGroupService — Singleton; управляет составом групп и обновляет
        // CurrentGroupTarget для участников в Tick(). NPCModule.Tick() вызывает
        // _groupService.Tick(tickCount) после _aiService.Tick(). NPCMovementService
        // читает CurrentGroupTarget как overlay над индивидуальным AI.
        builder.Register<INPCGroupService, NPCGroupService>(Lifetime.Singleton);

        // === R13 FULL-LOOT (2026-09-10): трупы NPC — контейнеры лута ===
        // CorpseService подписан на NPCDeathEvent (Initialize из NPCModule.Start):
        // снапшот экипировки/инвентаря/камней в CorpseData на месте смерти.
        // Обыск — SlotId-адресно (TryTakeItem) или «забрать всё» (LootAll);
        // выдача — ItemAddRequestEvent (EVT-02 command-паттерн).
        builder.Register<ICorpseService, CorpseService>(Lifetime.Singleton);

        // === R13 «NPC спаун через генерацию» (2026-09-10) ===
        // NPCSpawnCompositionService — процедурный состав населения локации
        // (тип локации + DangerLevel + сид) вместо хардкод-массива ролей.
        // R14: внутрисессионное восполнение запрещено — только ивенты
        // (TrySpawnEventNpc: караван/набег); естественное восстановление —
        // при (пере)сборке локации (TRANSITION_SYSTEM §5.3).
        builder.Register<NPCSpawnCompositionService>(Lifetime.Singleton);

        // === Точка входа модуля ===
        builder.Register<NPCModule>(Lifetime.Singleton);
    }
}
