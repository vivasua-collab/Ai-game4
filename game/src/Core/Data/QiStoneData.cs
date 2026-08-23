#nullable enable
// Создано: 2026-08-23 — Этап 7 внедрения ЦИ: камни Ци (GENERATORS_SYSTEM.md §10).
// Камень Ци характеризуется ТОЛЬКО объёмом Ци и типом (calm/chaotic).
// «Качество» камней не предусмотрено лором. БЕЗ стихии (чистое Ци).
//
// Канон (GENERATORS_SYSTEM.md §10.3-4):
//   • Плотность кристалла: 1024 ед/см³ (постоянная).
//   • Содержание Ци = 1024 × объём_см³.
//   • Тип: calm (90%, безопасна) / chaotic (10%, −10% HP риск при использовании).
//
// Архитектура:
//   QiStoneData — РАСШИРЕНИЕ ItemData (наследуется, чтобы камень был предметом
//   инвентаря с категорией ItemCategory.QiStone). Доп. поля: размер, тип, объём
//   Ци (полный и остаток — на будущее для канального поглощения).
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Data
{
    /// <summary>
    /// Размер камня Ци. Канон объёма (GENERATORS_SYSTEM.md §10.3):
    ///   dust    — 1 см³   = 1 024 Ци
    ///   pebble  — 8 см³   = 8 192 Ци
    ///   shard   — 27 см³  = 27 648 Ци
    ///   stone   — 64 см³  = 65 536 Ци
    ///   boulder — 125 см³ = 128 000 Ци
    /// </summary>
    public enum QiStoneSize
    {
        Dust = 0,       // Пыль — 1 см³
        Pebble = 1,     // Галька — 8 см³
        Shard = 2,      // Осколок — 27 см³
        Stone = 3,      // Камень — 64 см³
        Boulder = 4,    // Глыба — 125 см³
    }

    /// <summary>
    /// Камень Ци — кристалл, хранящий чистое Ци.
    /// Используется для восполнения Ци практика (RMB в инвентаре → поглощение).
    /// БЕЗ стихийного окраса (чистое Ци).
    ///
    /// Этап 7 v1: мгновенное поглощение при использовании (RMB → Use):
    ///   • calm:    +QiAmount к CurrentQi игрока (AddQi), камень расходуется.
    ///   • chaotic: 10% шанс −10% MaxHP за тик (опасность хаотичной Ци), Ци добавляется.
    /// </summary>
    public sealed class QiStoneData : ItemData
    {
        /// <summary>Размер камня (определяет QiAmount по канону).</summary>
        public QiStoneSize Size;

        /// <summary>Тип Ци: true = chaotic (опасна), false = calm (безопасна).</summary>
        public bool IsChaotic;

        /// <summary>Полный объём Ци в камне (ед.) = 1024 × объём_см³.</summary>
        public long QiAmount;

        /// <summary>
        /// Остаток Ци (уменьшается при канальном поглощении; v1 = мгновенно → 0).
        /// </summary>
        public long QiRemaining;

        /// <summary>Физический объём камня (см³) — канон от размера.</summary>
        public float VolumeCm3;

        /// <summary>
        /// Создать камень Ци с указанными параметрами.
        /// Заполняет базовые поля ItemData (Category=QiStone, Stackable=true).
        /// </summary>
        public static QiStoneSize ResolveSize(string sizeToken) => sizeToken switch
        {
            "dust"    => QiStoneSize.Dust,
            "pebble"  => QiStoneSize.Pebble,
            "shard"   => QiStoneSize.Shard,
            "stone"   => QiStoneSize.Stone,
            "boulder" => QiStoneSize.Boulder,
            _ => QiStoneSize.Dust,
        };

        /// <summary>Канонический объём (см³) по размеру.</summary>
        public static int CanonicalVolumeCm3(QiStoneSize size) => size switch
        {
            QiStoneSize.Dust    => 1,
            QiStoneSize.Pebble  => 8,
            QiStoneSize.Shard   => 27,
            QiStoneSize.Stone   => 64,
            QiStoneSize.Boulder => 125,
            _ => 1,
        };

        /// <summary>Каноническое количество Ци = 1024 × объём_см³.</summary>
        public static long CanonicalQiAmount(QiStoneSize size)
            => 1024L * CanonicalVolumeCm3(size);
    }
}
