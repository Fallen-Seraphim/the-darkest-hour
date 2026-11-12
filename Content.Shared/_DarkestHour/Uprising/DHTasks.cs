using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Uprising;

// «Восстание», задания (решение автора 2026-11-12, документ проекта claude/uprising-economy.md): у каждой стороны
// (военпол, ОФСК, LA-V) на своём компьютере/планшете — три предложения на выбор и одно активное задание. Награда —
// на счёт организации. Каркас общий: задание — прототип dhTask (сторона, тип, награда); логика типа — на сервере.

/// <summary>Чьё задание.</summary>
[Serializable, NetSerializable]
public enum DHTaskSide : byte
{
    Police,
    Front,
    Vanguard,
}

/// <summary>Тип задания (логика — DHTaskSystem на сервере).</summary>
[Serializable, NetSerializable]
public enum DHTaskKind : byte
{
    /// <summary>Найти закладку: спавнится в случайном месте, дано указание; найти и щёлкнуть.</summary>
    Stash,

    /// <summary>Разминировать: бомба с таймером, код деактивации — в задании.</summary>
    Defuse,

    /// <summary>Проверить базу данных военпола: найти и удалить фальшивые записи.</summary>
    Database,

    /// <summary>ОФСК: вымогать сумму у жителя (перевод со счёта жертвы на счёт ОФСК). Жертва погибла — провал.</summary>
    Extortion,

    /// <summary>ОФСК: подорвать объект — сработавшая взрывчатка с таймером рядом с меткой объекта.</summary>
    Bombing,
}

[Prototype("dhTask")]
public sealed partial class DHTaskPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public DHTaskSide Side;

    [DataField(required: true)]
    public DHTaskKind Kind;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>Короткое описание в списке предложений.</summary>
    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public int RewardMin = 300;

    [DataField]
    public int RewardMax = 600;

    [DataField]
    public float Weight = 1f;

    /// <summary>Сколько даётся на выполнение.</summary>
    [DataField]
    public TimeSpan TimeLimit = TimeSpan.FromMinutes(20);

    /// <summary>Разминирование: таймер бомбы.</summary>
    [DataField]
    public TimeSpan BombTimer = TimeSpan.FromMinutes(8);

    /// <summary>База данных: сколько фальшивых записей появится.</summary>
    [DataField]
    public int Fakes = 3;

    /// <summary>Вымогательство: сумма с мирного.</summary>
    [DataField]
    public int AmountMin = 400;

    [DataField]
    public int AmountMax = 900;

    /// <summary>Вымогательство: сумма с военпола и LA-V (они выпадают реже, но и платят больше).</summary>
    [DataField]
    public int VipAmountMin = 1000;

    [DataField]
    public int VipAmountMax = 1800;

    /// <summary>Вымогательство: премия сверх самой суммы — за военпола и LA-V.</summary>
    [DataField]
    public int VipRewardMin = 500;

    [DataField]
    public int VipRewardMax = 800;

    /// <summary>Вымогательство: насколько реже выпадают военпол и LA-V, чем мирные.</summary>
    [DataField]
    public float VipWeight = 0.35f;
}

/// <summary>Объект для подрыва (задание ОФСК): метка, расставлена генератором карты.</summary>
[RegisterComponent]
public sealed partial class DHBombTargetComponent : Component
{
    [DataField(required: true)]
    public LocId TargetName;

    /// <summary>Взрывчатка должна сработать не дальше этого от метки.</summary>
    [DataField]
    public float Radius = 5f;
}

/// <summary>Логово ОФСК: пленный военпол в наручниках здесь — награда ОФСК.</summary>
[RegisterComponent]
public sealed partial class DHFrontLairComponent : Component
{
    [DataField]
    public float Radius = 5f;
}

/// <summary>Компьютер (или планшет) заданий стороны.</summary>
[RegisterComponent]
public sealed partial class DHTaskConsoleComponent : Component
{
    [DataField(required: true)]
    public DHTaskSide Side;
}

/// <summary>Место, где может появиться закладка или бомба (расставлены генератором карты).</summary>
[RegisterComponent]
public sealed partial class DHTaskSpotComponent : Component;

/// <summary>Закладка задания: военпол щёлкает по ней — задание выполнено.</summary>
[RegisterComponent]
public sealed partial class DHTaskStashComponent : Component
{
    [ViewVariables]
    public int TaskId;
}

/// <summary>Бомба задания: таймер, код деактивации. Неверный код — минус минута.</summary>
[RegisterComponent]
public sealed partial class DHTaskBombComponent : Component
{
    [ViewVariables]
    public int TaskId;

    [ViewVariables]
    public string Code = string.Empty;

    [ViewVariables]
    public TimeSpan ExplodeAt;

    [ViewVariables]
    public bool Defused;

    [DataField]
    public TimeSpan WrongCodePenalty = TimeSpan.FromMinutes(1);
}

/// <summary>Камера изолятора военпола: член ОФСК в наручниках здесь — арест (награда военполу).</summary>
[RegisterComponent]
public sealed partial class DHPoliceCellComponent : Component
{
    [DataField]
    public float Radius = 2f;
}

// ---------------------------------------------------------------- окна

[Serializable, NetSerializable]
public enum DHTaskConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHTaskOfferInfo(string name, string description, int reward)
{
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly int Reward = reward;
}

[Serializable, NetSerializable]
public sealed class DHTaskConsoleState(DHTaskSide side, List<DHTaskOfferInfo> offers, string? activeName,
    string activeText, int activeReward, int secondsLeft, int cooldown, int balance, List<string> log)
    : BoundUserInterfaceState
{
    public readonly DHTaskSide Side = side;
    public readonly List<DHTaskOfferInfo> Offers = offers;

    /// <summary>Активное задание (null — нет).</summary>
    public readonly string? ActiveName = activeName;

    /// <summary>Подробности активного задания: где, что, код.</summary>
    public readonly string ActiveText = activeText;

    public readonly int ActiveReward = activeReward;
    public readonly int SecondsLeft = secondsLeft;

    /// <summary>Через сколько секунд появятся новые предложения (0 — уже).</summary>
    public readonly int Cooldown = cooldown;

    /// <summary>Баланс счёта организации.</summary>
    public readonly int Balance = balance;

    /// <summary>Последние выплаты и события, новые сверху.</summary>
    public readonly List<string> Log = log;
}

[Serializable, NetSerializable]
public sealed class DHTaskAcceptMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}

[Serializable, NetSerializable]
public sealed class DHTaskAbandonMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public enum DHTaskBombUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHTaskBombState(int secondsLeft, bool defused, string? status) : BoundUserInterfaceState
{
    public readonly int SecondsLeft = secondsLeft;
    public readonly bool Defused = defused;
    public readonly string? Status = status;
}

[Serializable, NetSerializable]
public sealed class DHTaskBombCodeMessage(string code) : BoundUserInterfaceMessage
{
    public readonly string Code = code;
}

// ---------------------------------------------------------------- база данных военпола

/// <summary>Сервер базы данных военпола: без него терминалы не работают.</summary>
[RegisterComponent]
public sealed partial class DHPoliceDatabaseServerComponent : Component;

/// <summary>Терминал базы данных военпола.</summary>
[RegisterComponent]
public sealed partial class DHPoliceDatabaseTerminalComponent : Component;

[Serializable, NetSerializable]
public enum DHPoliceDatabaseUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHDatabaseRecordInfo(int id, string name, string number, string birthDate, string birthPlace,
    string issued, string sex, string species)
{
    public readonly int Id = id;
    public readonly string Name = name;
    public readonly string Number = number;
    public readonly string BirthDate = birthDate;
    public readonly string BirthPlace = birthPlace;
    public readonly string Issued = issued;
    public readonly string Sex = sex;
    public readonly string Species = species;
}

[Serializable, NetSerializable]
public sealed class DHPoliceDatabaseState(bool online, List<DHDatabaseRecordInfo> records, List<string> worlds,
    string? status) : BoundUserInterfaceState
{
    /// <summary>Сервер базы на месте и цел.</summary>
    public readonly bool Online = online;

    public readonly List<DHDatabaseRecordInfo> Records = records;

    /// <summary>Справка: миры и с какого года там живут (для проверки места рождения).</summary>
    public readonly List<string> Worlds = worlds;

    public readonly string? Status = status;
}

/// <summary>Удалить запись как фальшивую.</summary>
[Serializable, NetSerializable]
public sealed class DHPoliceDatabaseDeleteMessage(int id) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
}
