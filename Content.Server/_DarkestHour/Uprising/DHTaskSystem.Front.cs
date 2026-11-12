using System.Numerics;
using Content.Server._DarkestHour.City;
using Content.Shared._DarkestHour.City;
using Content.Shared._DarkestHour.Uprising;
using Content.Shared.Cuffs.Components;
using Content.Shared.Explosion.Components;
using Content.Shared.Mind;
using Content.Shared.Trigger.Components;

namespace Content.Server._DarkestHour.Uprising;

/// <summary>
/// Задания ОФСК (решение автора 2026-11-12): вымогательство (сумма со счёта жертвы на счёт ОФСК; жертва получает
/// сообщение со скрытого номера; погибла — провал; военпол узнаёт только от людей) и подрыв объекта (взведённая
/// взрывчатка сработала у метки объекта). Награды ОФСК за пленного военпола (в наручниках в логове) и за убитого.
/// </summary>
public sealed partial class DHTaskSystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private DHPhoneSystem _phones = default!;

    public const int CaptureReward = 500;
    public const int FrontKillReward = 200;

    /// <summary>Скрытый номер, с которого жертва получает требование.</summary>
    private const string AnonymousNumber = "***-**-**";

    #region Вымогательство

    private bool StartExtortion(ActiveTask task)
    {
        // Кандидаты: живые игроки не из ОФСК, у кого есть личный счёт. Военпол и LA-V — реже, но дороже.
        var candidates = new List<(EntityUid Uid, DHUprisingSide Side, float Weight)>();
        var query = EntityQueryEnumerator<DHUprisingMemberComponent>();
        while (query.MoveNext(out var uid, out var member))
        {
            if (member.Side == DHUprisingSide.Front || !_mobState.IsAlive(uid) || !_mind.TryGetMind(uid, out _, out _) ||
                _bank.AccountOf(uid) == null)
            {
                continue;
            }

            var vip = member.Side is DHUprisingSide.Police or DHUprisingSide.Vanguard;
            candidates.Add((uid, member.Side, vip ? task.Proto.VipWeight : 1f));
        }

        if (candidates.Count == 0)
            return false;

        var total = 0f;
        foreach (var c in candidates)
        {
            total += c.Weight;
        }

        var roll = _random.NextFloat() * total;
        var pick = candidates[^1];
        foreach (var c in candidates)
        {
            roll -= c.Weight;
            if (roll > 0)
                continue;

            pick = c;
            break;
        }

        var isVip = pick.Side is DHUprisingSide.Police or DHUprisingSide.Vanguard;
        task.Victim = pick.Uid;
        task.VictimAccount = _bank.AccountOf(pick.Uid)!.Number;
        task.Amount = (isVip
            ? _random.Next(task.Proto.VipAmountMin, task.Proto.VipAmountMax + 1)
            : _random.Next(task.Proto.AmountMin, task.Proto.AmountMax + 1)) / 50 * 50;
        if (isVip)
            task.Reward = _random.Next(task.Proto.VipRewardMin, task.Proto.VipRewardMax + 1) / 10 * 10;

        var front = _bank.OrgAccount(DHBankOrg.Front);
        var name = Name(pick.Uid);
        var deadline = DHCityClock.TimeText(_ticker.RoundDuration() + task.Proto.TimeLimit);
        task.Text = Loc.GetString("dh-task-extortion-text", ("name", name),
            ("side", Loc.GetString("dh-task-side-" + pick.Side.ToString().ToLowerInvariant())),
            ("amount", task.Amount), ("account", front.Number), ("time", deadline));

        // Требование — на телефон жертвы, со скрытого номера.
        if (_phones.FindPhoneByOwner(name) is { } phone)
        {
            _phones.SendAnonymous(phone, AnonymousNumber, Loc.GetString("dh-task-extortion-sms", ("name", name),
                ("amount", task.Amount), ("account", front.Number), ("time", deadline)));
        }

        return true;
    }

    private void OnTransfer(DHBankTransferEvent ev)
    {
        var front = _bank.OrgAccount(DHBankOrg.Front);
        if (ev.To != front.Number || !_sides.TryGetValue(DHTaskSide.Front, out var state) ||
            state.Active is not { } task || task.Proto.Kind != DHTaskKind.Extortion || ev.From != task.VictimAccount)
        {
            return;
        }

        task.Paid += ev.Amount;
        if (task.Paid >= task.Amount)
        {
            Finish(DHTaskSide.Front, task, true, Loc.GetString("dh-task-log-extortion-paid",
                ("name", task.Victim is { } victim ? Name(victim) : "?"), ("amount", task.Paid)));
        }
        else
        {
            UpdateConsoles(DHTaskSide.Front);
        }
    }

    private void OnExtortionVictimKilled(EntityUid victim)
    {
        if (!_sides.TryGetValue(DHTaskSide.Front, out var state) || state.Active is not { } task ||
            task.Proto.Kind != DHTaskKind.Extortion || task.Victim != victim)
        {
            return;
        }

        Finish(DHTaskSide.Front, task, false, Loc.GetString("dh-task-log-extortion-dead", ("name", Name(victim))));
    }

    #endregion

    #region Подрыв

    private bool StartBombing(ActiveTask task)
    {
        var targets = new List<EntityUid>();
        var query = EntityQueryEnumerator<DHBombTargetComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            targets.Add(uid);
        }

        if (targets.Count == 0)
            return false;

        var target = targets[_random.Next(targets.Count)];
        var comp = Comp<DHBombTargetComponent>(target);
        task.BombTarget = target;
        task.Text = Loc.GetString("dh-task-bombing-text", ("target", Loc.GetString(comp.TargetName)),
            ("hint", Hint(Transform(target).Coordinates)), ("radius", (int) comp.Radius));
        return true;
    }

    /// <summary>
    /// Подрыв засчитывается, когда взведённая (с таймером) взрывчатка у метки объекта исчезла — сработала.
    /// Обезвреженная (таймер снят) из списка выпадает.
    /// </summary>
    private void CheckBombings()
    {
        if (!_sides.TryGetValue(DHTaskSide.Front, out var state) || state.Active is not { } task ||
            task.Proto.Kind != DHTaskKind.Bombing || task.BombTarget is not { } targetUid ||
            !TryComp<DHBombTargetComponent>(targetUid, out var target))
        {
            return;
        }

        var targetXform = Transform(targetUid);
        var center = _transform.GetWorldPosition(targetXform);

        // Сработавшие: были взведены у цели, теперь их нет.
        foreach (var armed in task.ArmedNear)
        {
            if (!TerminatingOrDeleted(armed))
                continue;

            Finish(DHTaskSide.Front, task, true, Loc.GetString("dh-task-log-done", ("name", Loc.GetString(task.Proto.Name))));
            return;
        }

        task.ArmedNear.RemoveWhere(e => !HasComp<ActiveTimerTriggerComponent>(e));

        var query = EntityQueryEnumerator<ExplosiveComponent, ActiveTimerTriggerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var xform))
        {
            if (xform.MapUid != targetXform.MapUid ||
                Vector2.Distance(_transform.GetWorldPosition(xform), center) > target.Radius)
            {
                continue;
            }

            task.ArmedNear.Add(uid);
        }
    }

    #endregion

    #region Пленные

    /// <summary>Военпол в наручниках в логове ОФСК — пленный (раз за человека).</summary>
    private void CheckCaptures()
    {
        var lairs = new List<(EntityUid? Map, Vector2 Position, float Radius)>();
        var lairQuery = EntityQueryEnumerator<DHFrontLairComponent, TransformComponent>();
        while (lairQuery.MoveNext(out _, out var lair, out var xform))
        {
            lairs.Add((xform.MapUid, _transform.GetWorldPosition(xform), lair.Radius));
        }

        if (lairs.Count == 0)
            return;

        var query = EntityQueryEnumerator<DHUprisingMemberComponent, CuffableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var member, out var cuffable, out var xform))
        {
            if (member.Side != DHUprisingSide.Police || cuffable.CuffedHandCount == 0 || _arrested.Contains(uid) ||
                !_mobState.IsAlive(uid))
            {
                continue;
            }

            var position = _transform.GetWorldPosition(xform);
            foreach (var (map, lairPosition, radius) in lairs)
            {
                if (map != xform.MapUid || Vector2.Distance(position, lairPosition) > radius)
                    continue;

                _arrested.Add(uid);
                Reward(DHTaskSide.Front, CaptureReward, Loc.GetString("dh-task-log-capture", ("name", Name(uid))));
                break;
            }
        }
    }

    #endregion
}
