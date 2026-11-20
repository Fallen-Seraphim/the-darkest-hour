using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Assault;

// Награды командиров «Осады» (план п. 6, решение автора 2026-11-20): наградной лист у командира базы, командующего
// операцией и капитана авианосца. Выбрать бойца своей стороны, медаль и написать формулировку — награда объявляется
// стороне и попадает в итоги раунда. Не больше трёх наград с одного листа за раунд.

/// <summary>Медаль стороны.</summary>
[Prototype("dhMedal")]
public sealed partial class DHMedalPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField(required: true)]
    public DHAssaultFaction Faction;

    [DataField]
    public int Order;
}

/// <summary>Наградной лист командира.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHMedalTabletComponent : Component
{
    [DataField(required: true)]
    public DHAssaultFaction Faction;

    [DataField]
    public int MaxAwards = 3;

    [DataField, AutoNetworkedField]
    public int Awarded;

    [DataField]
    public int MaxCitation = 200;
}

[Serializable, NetSerializable]
public enum DHMedalTabletUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHMedalCandidate
{
    public NetEntity Entity;
    public string Name = string.Empty;
    public string Job = string.Empty;
    public bool Dead;
}

[Serializable, NetSerializable]
public sealed class DHMedalTabletState : BoundUserInterfaceState
{
    public List<DHMedalCandidate> Candidates = new();
    public List<string> Medals = new();
    public int Remaining;
    public int MaxCitation;
}

[Serializable, NetSerializable]
public sealed class DHMedalAwardMessage : BoundUserInterfaceMessage
{
    public NetEntity Target;
    public string Medal;
    public string Citation;

    public DHMedalAwardMessage(NetEntity target, string medal, string citation)
    {
        Target = target;
        Medal = medal;
        Citation = citation;
    }
}
