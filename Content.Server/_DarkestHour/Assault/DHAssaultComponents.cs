using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._DarkestHour.Assault;

/// <summary>Итог раунда «Штурм».</summary>
public enum DHAssaultOutcome : byte
{
    None,

    /// <summary>Образец вывезен и войска Республики уничтожены.</summary>
    ImperialMajor,

    /// <summary>Войска Республики уничтожены без образца — или образец вывезен, но гарнизон жив.</summary>
    ImperialMinor,

    /// <summary>Десант отбит, Империя отступила.</summary>
    RepublicMinor,

    /// <summary>Авианосец сбит и упал на планету.</summary>
    RepublicMajor,
}

/// <summary>
/// Правило раунда «Штурм»: фаза подготовки, прыжок авианосца, образец, итоги, подкрепления волнами.
/// </summary>
[RegisterComponent, Access(typeof(DHAssaultRuleSystem))]
public sealed partial class DHAssaultRuleComponent : Component
{
    #region Настройка

    /// <summary>Шаттл авианосца (грид-файл).</summary>
    [DataField]
    public ResPath CarrierPath = new("/Maps/_DarkestHour/Shuttles/imperial_carrier.yml");

    /// <summary>Станция авианосца: должности экипажа и десанта.</summary>
    [DataField]
    public EntProtoId CarrierStation = "DHStationImperialCarrier";

    [DataField]
    public LocId CarrierName = "dh-assault-carrier-name";

    /// <summary>Соседняя система, откуда авианосец начинает раунд.</summary>
    [DataField]
    public ProtoId<DHStarSystemPrototype> StartSystem = "DHSystemVarda";

    /// <summary>Точка появления авианосца в соседней системе (метры от начала отсчёта системы).</summary>
    [DataField]
    public System.Numerics.Vector2 StartPosition = new(-2000f, 3000f);

    /// <summary>Система базы Республики.</summary>
    [DataField]
    public ProtoId<DHStarSystemPrototype> TargetSystem = "DHSystemREP27";

    /// <summary>Фаза подготовки: разрывной генератор авианосца заблокирован, гарнизон укрепляется.</summary>
    [DataField]
    public TimeSpan PrepTime = TimeSpan.FromMinutes(12);

    /// <summary>Лимит раунда: к этому сроку образец не вывезен — малая победа Республики.</summary>
    [DataField]
    public TimeSpan TimeLimit = TimeSpan.FromMinutes(75);

    /// <summary>Поисковая операция после падения авианосца: столько длится, если выжившие не добиты раньше.</summary>
    [DataField]
    public TimeSpan SearchTime = TimeSpan.FromMinutes(10);

    /// <summary>Сколько раз за раунд каждая сторона может вернуть погибшего бойца волной подкреплений.</summary>
    [DataField]
    public Dictionary<DHAssaultFaction, int> Tickets = new()
    {
        { DHAssaultFaction.Republic, 15 },
        { DHAssaultFaction.Empire, 25 },
    };

    /// <summary>Как часто приходят волны подкреплений.</summary>
    [DataField]
    public Dictionary<DHAssaultFaction, TimeSpan> WaveInterval = new()
    {
        { DHAssaultFaction.Republic, TimeSpan.FromMinutes(6) },
        { DHAssaultFaction.Empire, TimeSpan.FromMinutes(5) },
    };

    /// <summary>Должность, в которой возвращаются погибшие.</summary>
    [DataField]
    public Dictionary<DHAssaultFaction, ProtoId<JobPrototype>> ReinforcementJob = new()
    {
        { DHAssaultFaction.Republic, "DHGarrisonRifleman" },
        { DHAssaultFaction.Empire, "DHMarineRifleman" },
    };

    /// <summary>Прочность авианосца «для ПКО»: на нуле авианосец падает на планету.</summary>
    [DataField]
    public float CarrierHull = 100f;

    #endregion

    #region Состояние

    [DataField]
    public EntityUid? Carrier;

    [DataField]
    public EntityUid? CarrierStationUid;

    [DataField]
    public TimeSpan PrepEnd;

    [DataField]
    public TimeSpan RoundLimit;

    [DataField]
    public bool PrepAnnounced;

    /// <summary>Авианосец вошёл в систему базы.</summary>
    [DataField]
    public bool Arrived;

    [DataField]
    public bool RepublicDestroyedAnnounced;

    [DataField]
    public DHAssaultOutcome Outcome = DHAssaultOutcome.None;

    [DataField]
    public bool SampleExtracted;

    /// <summary>Авианосец сбит: когда закончится поисковая операция.</summary>
    [DataField]
    public TimeSpan? SearchEnd;

    /// <summary>Сторона каждого игрока раунда — для подкреплений.</summary>
    [ViewVariables]
    public Dictionary<NetUserId, DHAssaultFaction> Players = new();

    [DataField]
    public Dictionary<DHAssaultFaction, TimeSpan> NextWave = new();

    [DataField]
    public TimeSpan NextCheck;

    #endregion
}

/// <summary>Станция одной из сторон «Штурма»: кто на ней появляется — боец этой стороны.</summary>
[RegisterComponent]
public sealed partial class DHAssaultFactionStationComponent : Component
{
    [DataField(required: true)]
    public DHAssaultFaction Faction;
}

/// <summary>Боец «Штурма»: по нему считаются живые силы сторон.</summary>
[RegisterComponent]
public sealed partial class DHAssaultFighterComponent : Component
{
    [DataField]
    public DHAssaultFaction Faction;
}

/// <summary>Имперский десантный авианосец: прочность «для ПКО», падение.</summary>
[RegisterComponent]
public sealed partial class DHAssaultCarrierComponent : Component
{
    [DataField]
    public float Hull = 100f;

    [DataField]
    public float MaxHull = 100f;

    [DataField]
    public bool Downed;

    /// <summary>Падение: когда авианосец рушится на поверхность.</summary>
    [DataField]
    public TimeSpan? CrashAt;

    /// <summary>Падение: когда рвутся взрывы по корпусу (после выхода на поверхность).</summary>
    [DataField]
    public TimeSpan? ImpactAt;
}

/// <summary>
/// Станция на поверхности планеты: при создании карта станции становится поверхностью тела звёздной системы
/// (биом, атмосфера, освещение), и посадочные консоли садятся именно на неё.
/// </summary>
[RegisterComponent]
public sealed partial class DHPlanetBaseComponent : Component
{
    [DataField(required: true)]
    public ProtoId<DHStarSystemPrototype> System;

    /// <summary>Id тела в системе.</summary>
    [DataField(required: true)]
    public string Body = string.Empty;

    /// <summary>Запас вокруг базы, где биом не ставит деревья и камни, тайлов.</summary>
    [DataField]
    public int ClearMargin = 6;
}
