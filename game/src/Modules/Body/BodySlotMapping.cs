#nullable enable
// Создано: 2026-05-08 15:46:00 UTC
// Редактировано: 2026-05-09 12:00:00 UTC — аудит: BD-02/BD-40 EnsureReverseMap типизация, BD-25 Hands mapping
// Редактировано: 2026-05-18 12:00:00 UTC — P2-03 FIX: расширен маппинг для не-гуманоидных морфологий
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot).
// Маппинг Body→Equipment: ампутированная часть блокирует слот экипировки.
// КРИТИЧЕСКАЯ СВЯЗЬ, которой не было в Legacy.
// Источник: plan_03_body.md, BODY_SYSTEM.md
using System.Collections.Generic;
using System.Linq;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Body
{
    /// <summary>
    /// Статический маппинг между частями тела и слотами экипировки.
    ///
    /// Правило: ампутированная часть тела блокирует связанные слоты экипировки.
    /// Пример: отрубленная правая рука → нельзя экипировать оружие в WeaponMain.
    ///
    /// Маппинг:
    /// Head     → [Head]
    /// Torso    → [Torso]
    /// Heart    → [Torso] (сердце → торс, критический удар блокирует броню)
    /// LeftArm  → [WeaponOff]
    /// RightArm → [WeaponMain]
    /// LeftHand → [WeaponOff, Hands]
    /// RightHand→ [WeaponMain, Hands]
    /// LeftLeg  → [Legs]
    /// RightLeg → [Legs]
    /// LeftFoot → [Feet]
    /// RightFoot→ [Feet]
    /// </summary>
    public static class BodySlotMapping
    {
        // Прямой маппинг: BodyPartType → EquipmentSlot[]
        private static readonly Dictionary<BodyPartType, EquipmentSlot[]> BodyToSlotMap = new()
        {
            { BodyPartType.Head,     new[] { EquipmentSlot.Head } },
            { BodyPartType.Torso,    new[] { EquipmentSlot.Torso } },
            { BodyPartType.Heart,    new[] { EquipmentSlot.Torso } },
            { BodyPartType.LeftArm,  new[] { EquipmentSlot.WeaponOff } },
            { BodyPartType.RightArm, new[] { EquipmentSlot.WeaponMain } },
            // BD-25: Hands добавлены в маппинг для LeftHand/RightHand
            { BodyPartType.LeftHand, new[] { EquipmentSlot.WeaponOff, EquipmentSlot.Hands } },
            { BodyPartType.RightHand,new[] { EquipmentSlot.WeaponMain, EquipmentSlot.Hands } },
            { BodyPartType.LeftLeg,  new[] { EquipmentSlot.Legs } },
            { BodyPartType.RightLeg, new[] { EquipmentSlot.Legs } },
            { BodyPartType.LeftFoot, new[] { EquipmentSlot.Feet } },
            { BodyPartType.RightFoot,new[] { EquipmentSlot.Feet } },

            // === Не-гуманоидные морфологии (P2-03 FIX) ===
            // Четвероногие
            { BodyPartType.FrontLeftLeg,  new[] { EquipmentSlot.Legs } },
            { BodyPartType.FrontRightLeg, new[] { EquipmentSlot.Legs } },
            { BodyPartType.BackLeftLeg,   new[] { EquipmentSlot.Legs } },
            { BodyPartType.BackRightLeg,  new[] { EquipmentSlot.Legs } },
            { BodyPartType.Tail,          System.Array.Empty<EquipmentSlot>() },

            // Птицы
            { BodyPartType.LeftWing,  new[] { EquipmentSlot.WeaponOff } },
            { BodyPartType.RightWing, new[] { EquipmentSlot.WeaponMain } },
            { BodyPartType.BirdTail,  System.Array.Empty<EquipmentSlot>() },

            // Змееподобные
            { BodyPartType.BodySegment1,  System.Array.Empty<EquipmentSlot>() },
            { BodyPartType.BodySegment2,  System.Array.Empty<EquipmentSlot>() },
            { BodyPartType.SerpentineTail,System.Array.Empty<EquipmentSlot>() },

            // Членистоногие
            { BodyPartType.Cephalothorax, new[] { EquipmentSlot.Torso, EquipmentSlot.Head } },
            { BodyPartType.Abdomen,       System.Array.Empty<EquipmentSlot>() },
            { BodyPartType.Leg1,  System.Array.Empty<EquipmentSlot>() },
            { BodyPartType.Leg2,  System.Array.Empty<EquipmentSlot>() },
            { BodyPartType.Leg3,  System.Array.Empty<EquipmentSlot>() },
            { BodyPartType.Leg4,  System.Array.Empty<EquipmentSlot>() },
            { BodyPartType.Leg5,  System.Array.Empty<EquipmentSlot>() },
            { BodyPartType.Leg6,  System.Array.Empty<EquipmentSlot>() },
            { BodyPartType.Leg7,  System.Array.Empty<EquipmentSlot>() },
            { BodyPartType.Leg8,  System.Array.Empty<EquipmentSlot>() },
            { BodyPartType.Pedipalps,  new[] { EquipmentSlot.WeaponOff } },
            { BodyPartType.Chelicerae, new[] { EquipmentSlot.WeaponMain } },

            // Бесформенные
            { BodyPartType.Core,    new[] { EquipmentSlot.Torso } },
            { BodyPartType.Essence, System.Array.Empty<EquipmentSlot>() },
        };

        // Обратный маппинг: EquipmentSlot → BodyPartType[]
        // Кэшируется при первом обращении
        private static Dictionary<EquipmentSlot, BodyPartType[]>? _slotToBodyMap;

        /// <summary>
        /// Получить слоты экипировки, заблокированные при ампутации данной части тела.
        /// </summary>
        public static EquipmentSlot[] GetBlockedSlots(BodyPartType partType)
        {
            if (BodyToSlotMap.TryGetValue(partType, out var slots))
                return slots;
            return System.Array.Empty<EquipmentSlot>();
        }

        /// <summary>
        /// Получить части тела, маппящиеся на данный слот экипировки.
        /// </summary>
        public static BodyPartType[] GetMappedParts(EquipmentSlot slot)
        {
            EnsureReverseMap();
            if (_slotToBodyMap!.TryGetValue(slot, out var parts))
                return parts;
            return System.Array.Empty<BodyPartType>();
        }

        /// <summary>
        /// Проверить: блокирован ли слот экипировки из-за ампутации.
        /// Слот заблокирован, если хотя бы одна маппящаяся часть отрублена.
        /// </summary>
        public static bool IsSlotBlocked(EquipmentSlot slot, HashSet<BodyPartType> severedParts)
        {
            if (severedParts == null || severedParts.Count == 0)
                return false;

            var mappedParts = GetMappedParts(slot);
            foreach (var part in mappedParts)
            {
                if (severedParts.Contains(part))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Построить обратный маппинг (ленивая инициализация).
        /// BD-02/BD-40: Исправлена типизация — промежуточный словарь List→ToArray.
        /// </summary>
        private static void EnsureReverseMap()
        {
            if (_slotToBodyMap != null) return;

            // Промежуточный словарь со списками для построения
            var tempMap = new Dictionary<EquipmentSlot, List<BodyPartType>>();

            foreach (var kvp in BodyToSlotMap)
            {
                foreach (var slot in kvp.Value)
                {
                    if (!tempMap.TryGetValue(slot, out var list))
                    {
                        list = new List<BodyPartType>();
                        tempMap[slot] = list;
                    }
                    list.Add(kvp.Key);
                }
            }

            // Конвертируем списки в массивы для zero-GC доступа
            _slotToBodyMap = new Dictionary<EquipmentSlot, BodyPartType[]>();
            foreach (var kvp in tempMap)
            {
                _slotToBodyMap[kvp.Key] = kvp.Value.ToArray();
            }
        }
    }
}
