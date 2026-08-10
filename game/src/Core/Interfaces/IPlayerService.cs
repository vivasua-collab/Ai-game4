#nullable enable
using CultivationGame.Core.Data;

namespace CultivationGame.Core.Interfaces;

/// <summary>Player character data + spatial state.</summary>
public interface IPlayerService
{
    CharacterData Player { get; }
    Position2D Position { get; }
    Direction Facing { get; }

    void MoveTo(int x, int y);
    void SetFacing(Direction dir);
    void Spawn(Position2D position);
}
