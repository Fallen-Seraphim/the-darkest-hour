using Content.Shared._DarkestHour.Assault;
using Content.Shared.Input;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input;

namespace Content.Client._DarkestHour.Assault;

/// <summary>
/// Ручной полёт челнока (решение автора 2026-11-27): пока пилот ведёт челнок с пульта, у него свой набор клавиш —
/// WASD двигают челнок, Q/E поворачивают корпус, R/F меняют высоту. Остальные клавиши человека (бросить, выкинуть
/// и т. п.) не срабатывают, чтобы Q/E не роняли вещи.
/// </summary>
public sealed partial class DHDropshipPilotInputSystem : EntitySystem
{
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;

    private const string Context = "dhdropship";

    public override void Initialize()
    {
        base.Initialize();

        var context = _input.Contexts.New(Context, "common");
        context.AddFunction(EngineKeyFunctions.MoveUp);
        context.AddFunction(EngineKeyFunctions.MoveDown);
        context.AddFunction(EngineKeyFunctions.MoveLeft);
        context.AddFunction(EngineKeyFunctions.MoveRight);
        context.AddFunction(EngineKeyFunctions.Walk);
        context.AddFunction(ContentKeyFunctions.ShuttleRotateLeft);
        context.AddFunction(ContentKeyFunctions.ShuttleRotateRight);
        context.AddFunction(DHDropshipKeyFunctions.Ascend);
        context.AddFunction(DHDropshipKeyFunctions.Descend);

        SubscribeLocalEvent<DHDropshipPilotComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DHDropshipPilotComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _input.Contexts.Remove(Context);
    }

    private void OnStartup(Entity<DHDropshipPilotComponent> ent, ref ComponentStartup args)
    {
        if (_player.LocalEntity == ent.Owner)
            _input.Contexts.SetActiveContext(Context);
    }

    private void OnShutdown(Entity<DHDropshipPilotComponent> ent, ref ComponentShutdown args)
    {
        if (_player.LocalEntity == ent.Owner && _input.Contexts.ActiveContext.Name == Context)
            _input.Contexts.SetActiveContext("human");
    }
}
