using Content.Shared._DarkestHour.Assault;
using Robust.Shared.Network;

namespace Content.Server._DarkestHour.Assault;

// Итоги раунда «Осады» (план п. 6, решения автора 2026-11-20): хронология захвата точек, потери сторон, сбитое,
// лучшие бойцы (убийства, спасения, захваты) и награды командиров. Копится в правиле, выводится в итогах раунда
// (DHAssaultRuleSystem.Stats.cs).

/// <summary>Точка сменила владельца.</summary>
public sealed class DHAssaultCaptureRecord
{
    public TimeSpan At;
    public string Point = string.Empty;
    public DHAssaultFaction Faction;
    public DHAssaultFaction? Previous;
}

/// <summary>Счёт одного игрока за раунд.</summary>
public sealed class DHAssaultFighterRecord
{
    public string Name = string.Empty;
    public DHAssaultFaction Faction;
    public int Kills;
    public int Saves;
    public int Captures;
}

/// <summary>Награда, которую вручил командир.</summary>
public sealed class DHAssaultMedalRecord
{
    public TimeSpan At;
    public DHAssaultFaction Faction;
    public LocId Medal;
    public string Recipient = string.Empty;
    public string Giver = string.Empty;
    public string Citation = string.Empty;
}

public sealed partial class DHAssaultRuleComponent
{
    [ViewVariables]
    public List<DHAssaultCaptureRecord> CaptureLog = new();

    [ViewVariables]
    public Dictionary<DHAssaultFaction, int> Losses = new();

    [ViewVariables]
    public Dictionary<NetUserId, DHAssaultFighterRecord> FighterStats = new();

    [ViewVariables]
    public List<DHAssaultMedalRecord> Medals = new();

    /// <summary>Сколько штурмовиков потеряла Империя (сбит или пилот катапультировался).</summary>
    [ViewVariables]
    public int AircraftLost;
}

/// <summary>Штурмовик потерян: сбит, разбился или пилот катапультировался (рассылает DHAircraftSystem).</summary>
public sealed class DHAircraftLostEvent : EntityEventArgs
{
    public readonly EntityUid Aircraft;

    public DHAircraftLostEvent(EntityUid aircraft)
    {
        Aircraft = aircraft;
    }
}
