#nullable enable
// Создано: 2026-08-27 — Phase C: интерфейс VerificationService.
// Методы Validate для TechniqueData, EquipmentData, FormationData.
// ValidationResult — статус + список out-of-bounds полей.
using System.Collections.Generic;
using CultivationGame.Core.Data;
using CultivationGame.Modules.Formation.Data;

namespace CultivationGame.Core.Interfaces
{
    /// <summary>
    /// Сервис верификации сгенерированных объектов (техник/экипировки/формаций).
    /// Использует LevelBoundaries для проверки: стат попадает в [min..max]
    /// с учётом легендарного оверсама (+1 уровень по OvershootPolicy).
    /// </summary>
    public interface IVerificationService
    {
        /// <summary>
        /// Проверить технику на соответствие границам (level, type, grade).
        /// Возвращает отчёт: IsValid, список полей вне границ, severity.
        /// </summary>
        ValidationResult Validate(TechniqueData tech, int cultivationLevel);

        /// <summary>
        /// Проверить экипировку на соответствие границам (level, class, grade, rarity).
        /// </summary>
        ValidationResult Validate(EquipmentData item, int cultivationLevel);

        /// <summary>
        /// Проверить формацию на соответствие границам (level, size).
        /// </summary>
        ValidationResult Validate(FormationData form, int cultivationLevel);

        /// <summary>
        /// Отфильтровать пачку техник: вернуть только валидные.
        /// Используется в PreGenTechniquePhase.
        /// </summary>
        List<TechniqueData> FilterValid(IEnumerable<TechniqueData> techniques, int cultivationLevel);

        /// <summary>
        /// Отфильтровать пачку экипировки: вернуть только валидную.
        /// </summary>
        List<EquipmentData> FilterValid(IEnumerable<EquipmentData> items, int cultivationLevel);

        /// <summary>
        /// Отфильтровать пачку формаций: вернуть только валидные.
        /// </summary>
        List<FormationData> FilterValid(IEnumerable<FormationData> forms, int cultivationLevel);
    }

    /// <summary>
    /// Результат верификации. Содержит флаг валидности + список полей,
    /// которые вышли за границы. Используется для логирования и CheatPanel.
    /// </summary>
    public sealed class ValidationResult
    {
        /// <summary>True, если объект прошёл верификацию.</summary>
        public bool IsValid { get; set; }

        /// <summary>Список полей, вышедших за границы.</summary>
        public List<string> OutOfBoundsFields { get; set; } = new();

        /// <summary>Severity: None/Minor/Major/Critical.</summary>
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.None;

        /// <summary>Опциональное человеко-читаемое сообщение.</summary>
        public string Message { get; set; } = string.Empty;

        public static ValidationResult Ok() => new() { IsValid = true, Severity = ValidationSeverity.None };

        public static ValidationResult Fail(string field, string message, ValidationSeverity severity = ValidationSeverity.Major)
        {
            var r = new ValidationResult { IsValid = false, Severity = severity, Message = message };
            r.OutOfBoundsFields.Add(field);
            return r;
        }

        public void AddOutOfBounds(string field)
        {
            OutOfBoundsFields.Add(field);
            IsValid = false;
        }
    }

    /// <summary>
    /// Серьёзность нарушения границ.
    /// Minor: мелкое отклонение (≤5% от max). Major: заметное (5-25%).
    /// Critical: грубое (>25% или недопустимое поле).
    /// </summary>
    public enum ValidationSeverity { None, Minor, Major, Critical }
}
