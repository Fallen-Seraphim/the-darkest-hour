using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Orbit;

// Орбита станции (этап 5): высота медленно падает (сопротивление верхних слоёв атмосферы REP-27-5D растёт по мере
// снижения), удерживают её манёвровые двигатели на любом газе — чем больше молей сожжено, тем больше подъём.
// Низкая орбита — чаще удары обломков; ниже порога «Терринум» — автоматический код угрозы; ниже «Экс» — орбита
// неисправима, станция падает.

/// <summary>Компьютер орбиты: высота, скорость снижения, двигатели, тяга.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHOrbitConsoleComponent : Component;

/// <summary>Манёвровый двигатель орбиты: жжёт газ из подключённой трубы, когда включена коррекция.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHOrbitalThrusterComponent : Component
{
    /// <summary>Наибольший расход газа, моль в секунду (при тяге 100%).</summary>
    [DataField]
    public float MaxMolesPerSecond = 4f;

    /// <summary>Подъём орбиты за один сожжённый моль, км.</summary>
    [DataField]
    public float LiftPerMole = 0.012f;

    [DataField]
    public string InletNode = "pipe";

    [ViewVariables]
    public float LastMolesPerSecond;
}

[Serializable, NetSerializable]
public enum DHOrbitUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum DHOrbitalThrusterVisuals : byte
{
    Burning,
}

[Serializable, NetSerializable]
public sealed class DHOrbitConsoleState(
    string body,
    float altitude,
    float maxAltitude,
    float ratePerMinute,
    float terrinumAltitude,
    float exAltitude,
    bool lost,
    TimeSpan? impactIn,
    int thrustersWorking,
    int thrustersTotal,
    bool burning,
    float throttle,
    float molesPerSecond) : BoundUserInterfaceState
{
    public readonly string Body = body;
    public readonly float Altitude = altitude;
    public readonly float MaxAltitude = maxAltitude;
    public readonly float RatePerMinute = ratePerMinute;
    public readonly float TerrinumAltitude = terrinumAltitude;
    public readonly float ExAltitude = exAltitude;
    public readonly bool Lost = lost;
    public readonly TimeSpan? ImpactIn = impactIn;
    public readonly int ThrustersWorking = thrustersWorking;
    public readonly int ThrustersTotal = thrustersTotal;
    public readonly bool Burning = burning;
    public readonly float Throttle = throttle;
    public readonly float MolesPerSecond = molesPerSecond;
}

[Serializable, NetSerializable]
public sealed class DHOrbitSetBurnMessage(bool burning) : BoundUserInterfaceMessage
{
    public readonly bool Burning = burning;
}

[Serializable, NetSerializable]
public sealed class DHOrbitSetThrottleMessage(float throttle) : BoundUserInterfaceMessage
{
    public readonly float Throttle = throttle;
}
