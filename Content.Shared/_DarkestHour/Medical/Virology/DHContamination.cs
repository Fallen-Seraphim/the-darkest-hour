using Content.Shared._DarkestHour.Medical.Disease;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Medical.Virology;

// Стерильность лаборатории и медотдела (этап 2 медицины): заражённые предметы, автоклав, дезинфицирующие салфетки,
// вытяжной шкаф. Логика заражения — на сервере (DHContaminationSystem).

/// <summary>
/// Предмет заражён возбудителями (инструмент после операции на больном, мазок). Заражает того, кто берёт его
/// без перчаток, и пациента, которого им оперируют. Снимается автоклавом или дезинфицирующими салфетками.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHContaminatedComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<DHDiseasePrototype>> Diseases = new();
}

/// <summary>
/// Автоклав: предмет в руке применить к машине — через несколько секунд он стерилен. Нужно питание.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHAutoclaveComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Дезинфицирующие салфетки: протереть заражённый предмет. Ограниченное число использований.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHDisinfectantComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Uses = 10;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);
}

/// <summary>
/// Вытяжной шкаф: анализатор рядом с работающим шкафом не выпускает аэрозоль образца в лабораторию.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHFumeHoodComponent : Component
{
    [DataField]
    public float Range = 1.6f;
}

[Serializable, NetSerializable]
public sealed partial class DHAutoclaveDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHDisinfectDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Осмотр: заражённый предмет выглядит грязным, у салфеток видно остаток.
/// </summary>
public sealed partial class DHContaminationExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHContaminatedComponent, ExaminedEvent>(OnContaminatedExamined);
        SubscribeLocalEvent<DHDisinfectantComponent, ExaminedEvent>(OnDisinfectantExamined);
    }

    private void OnContaminatedExamined(Entity<DHContaminatedComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Diseases.Count > 0)
            args.PushMarkup(Loc.GetString("dh-contaminated-examine"));
    }

    private void OnDisinfectantExamined(Entity<DHDisinfectantComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("dh-disinfectant-examine", ("uses", ent.Comp.Uses)));
    }
}
