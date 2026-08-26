#nullable enable
// Создано: 2026-05-09 17:30:00 UTC
// Интерфейс службы мира.
// Управление локациями, секторами, фракциями и путешествиями.
using System.Collections.Generic;

using CultivationGame.Core;
using CultivationGame.Core.Data;
namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Служба мира.
    /// Предоставляет доступ к текущей локации, секторам, фракциям
    /// и управляет путешествиями между локациями.
    ///
    /// АРХИТЕКТУРА (EVT-01): Модули НЕ инжектят IWorldService напрямую.
    /// Кросс-модульные взаимодействия — через MessagePipe (LocationChangedEvent, TravelStartedEvent).
    /// </summary>
    public interface IWorldService
    {
        /// <summary>Идентификатор текущей локации</summary>
        string CurrentLocationId { get; }

        /// <summary>Идентификатор текущего сектора</summary>
        string CurrentSectorId { get; }

        /// <summary>
        /// Попытка путешествия в указанную локацию.
        /// Возвращает true, если путешествие началось успешно.
        /// Публикует TravelStartedEvent и (позже) LocationChangedEvent.
        /// </summary>
        bool TryTravel(string locationId);

        /// <summary>
        /// Установить активную локацию. Ai-game3 compatibility —
        /// вызывается из WorldInitPhase для установки стартовой локации.
        /// </summary>
        void SetActiveLocation(string locationId);

        /// <summary>Получить информацию о локации по идентификатору</summary>
        LocationInfo GetLocation(string locationId);

        /// <summary>Получить информацию о фракции по идентификатору</summary>
        FactionInfo GetFaction(string factionId);

        /// <summary>Получить тип отношения между двумя фракциями</summary>
        FactionRelationType GetFactionRelation(string factionA, string factionB);

        /// <summary>Список идентификаторов открытых секторов</summary>
        IReadOnlyList<string> GetDiscoveredSectors();

        /// <summary>Проверить, открыт ли сектор</summary>
        bool IsSectorDiscovered(string sectorId);
    }

    /// <summary>
    /// Информация о локации (value type для передачи через интерфейс).
    /// </summary>
    public readonly struct LocationInfo
    {
        public readonly string Id;
        public readonly string Name;
        public readonly LocationType Type;
        public readonly BiomeType Biome;
        public readonly int QiDensity;
        public readonly int DangerLevel;
        public readonly string ParentSectorId;

        public LocationInfo(string id, string name, LocationType type, BiomeType biome,
            int qiDensity, int dangerLevel, string parentSectorId)
        {
            Id = id;
            Name = name;
            Type = type;
            Biome = biome;
            QiDensity = qiDensity;
            DangerLevel = dangerLevel;
            ParentSectorId = parentSectorId;
        }
    }

    /// <summary>
    /// Информация о фракции (value type для передачи через интерфейс).
    /// </summary>
    public readonly struct FactionInfo
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Ideology;
        public readonly int Power;

        public FactionInfo(string id, string name, string ideology, int power)
        {
            Id = id;
            Name = name;
            Ideology = ideology;
            Power = power;
        }
    }
}
