using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Shuttles.Rift;

// Навигационная консоль разрывных прыжков: карта звёздной системы (тела, орбиты, корабли), выбор системы и
// точки выхода, прыжок. Прыжок в другую систему — только отсюда (разрыв: генератор, накопитель, хронит);
// в пределах своей системы консоль прыгает обычным FTL.

[RegisterComponent, NetworkedComponent]
public sealed partial class DHRiftNavConsoleComponent : Component
{
    /// <summary>Система, которую сейчас смотрит консоль (null — текущая система корабля).</summary>
    [ViewVariables]
    public string? ViewedSystem;
}

[Serializable, NetSerializable]
public enum DHRiftNavUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHRiftNavSystemEntry(string id, string name, bool current)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly bool Current = current;
}

/// <summary>Корабль или станция на карте системы.</summary>
[Serializable, NetSerializable]
public sealed class DHRiftNavGrid(string name, Vector2 position, Color color, bool self)
{
    public readonly string Name = name;

    /// <summary>Позиция в координатах системы (от начала отсчёта).</summary>
    public readonly Vector2 Position = position;

    public readonly Color Color = color;
    public readonly bool Self = self;
}

[Serializable, NetSerializable]
public sealed class DHRiftNavState(
    List<DHRiftNavSystemEntry> systems,
    string viewedSystem,
    List<DHRiftNavGrid> grids,
    string status,
    bool canJump,
    bool rift) : BoundUserInterfaceState
{
    public readonly List<DHRiftNavSystemEntry> Systems = systems;

    /// <summary>Id прототипа системы, показанной на карте.</summary>
    public readonly string ViewedSystem = viewedSystem;

    public readonly List<DHRiftNavGrid> Grids = grids;

    /// <summary>Готовность к прыжку или причина, почему нельзя.</summary>
    public readonly string Status = status;

    public readonly bool CanJump = canJump;

    /// <summary>Прыжок в показанную систему пойдёт через разрыв.</summary>
    public readonly bool Rift = rift;
}

[Serializable, NetSerializable]
public sealed class DHRiftNavViewSystemMessage(string system) : BoundUserInterfaceMessage
{
    public readonly string System = system;
}

[Serializable, NetSerializable]
public sealed class DHRiftNavJumpMessage(string system, Vector2 position) : BoundUserInterfaceMessage
{
    public readonly string System = system;

    /// <summary>Точка выхода в координатах системы.</summary>
    public readonly Vector2 Position = position;
}
