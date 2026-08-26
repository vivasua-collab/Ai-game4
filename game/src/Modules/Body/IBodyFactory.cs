#nullable enable
// Создано: 2026-05-18 — P1-10 FIX: интерфейс фабрики тел для улучшения тестируемости
// BodyService инжектит IBodyFactory вместо конкретного BodyFactory
// Migrated from Ai-game3 (Unity) to Ai-game4 (Godot).
using System.Collections.Generic;
using CultivationGame.Core.Data;

namespace CultivationGame.Modules.Body
{
    /// <summary>
    /// Интерфейс фабрики создания тел.
    /// P1-10 FIX: позволяет тестировать BodyService с mock-фабрикой.
    /// Внутренний интерфейс модуля Body — не вынесен в Core/Interfaces,
    /// т.к. возвращает BodyPart (модульный тип).
    /// </summary>
    public interface IBodyFactory
    {
        /// <summary>
        /// Создать тело для новой сущности.
        /// Вычисляет HP на основе: baseHP × vitalityMultiplier × sizeMultiplier.
        /// </summary>
        List<BodyPart> CreateBody(Morphology morphology, SizeClass size, float vitality);
    }
}
