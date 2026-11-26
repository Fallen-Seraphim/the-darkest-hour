using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.ThirdParty;

// Событие «ЧВК LA-V» в «Осаде» (план осады п. 11, решения автора 2026-11-26): отряд из 10 призраков высаживается на
// своём шаттле у свободной ЗП и выполняет задание корпорации — удержать объект до конца операции или вывезти образец.

/// <summary>Пульт шаттла LA-V: «Эвакуация» — шаттл уходит со всеми бойцами LA-V на борту (и образцом, если он там).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHLAVShuttleConsoleComponent : Component
{
    [DataField]
    public TimeSpan EvacTime = TimeSpan.FromSeconds(10);
}

/// <summary>Точка появления бойца LA-V на шаттле (метка на карте шаттла).</summary>
[RegisterComponent]
public sealed partial class DHLAVSpawnPointComponent : Component
{
    /// <summary>Место командира.</summary>
    [DataField]
    public bool Leader;
}

/// <summary>Способность «Задание»: напомнить цель операции и что засчитывается.</summary>
public sealed partial class DHLAVStatusActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class DHLAVEvacDoAfterEvent : SimpleDoAfterEvent;
