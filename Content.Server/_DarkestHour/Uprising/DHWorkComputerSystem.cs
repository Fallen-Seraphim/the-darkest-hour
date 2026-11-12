using System.Text;
using Content.Server._DarkestHour.City;
using Content.Shared._DarkestHour.Uprising;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Uprising;

/// <summary>
/// Рабочие компьютеры офисов (решение автора 2026-11-12): раз в 4 минуты — практическое задание про счета, проценты,
/// налоги, графики и накладные. Ответ — число. Верно — 50–100 кр. на личный счёт работника; два промаха — задание
/// сгорает. Таймер у каждого работника свой (по человеку, не по компьютеру).
/// </summary>
public sealed partial class DHWorkComputerSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DHBankSystem _bank = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly SoundSpecifier OkSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");
    private static readonly SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

    private static readonly string[] Months = { "jan", "feb", "mar", "apr", "may", "jun" };

    private sealed class WorkTask
    {
        public string Text = string.Empty;
        public long Answer;
        public int Reward;
        public int AttemptsLeft;
    }

    private sealed class Worker
    {
        public WorkTask? Task;
        public TimeSpan Next;
        public int Earned;
    }

    private readonly Dictionary<EntityUid, Worker> _workers = new();
    private TimeSpan _nextUi;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _workers.Clear());
        Subs.BuiEvents<DHWorkComputerComponent>(DHWorkComputerUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<DHWorkComputerAnswerMessage>(OnAnswer);
        });
    }

    private Worker WorkerOf(EntityUid user)
    {
        if (!_workers.TryGetValue(user, out var worker))
        {
            worker = new Worker { Next = _timing.CurTime };
            _workers[user] = worker;
        }

        return worker;
    }

    private void OnOpened(Entity<DHWorkComputerComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent, args.Actor, null);
    }

    private void OnAnswer(Entity<DHWorkComputerComponent> ent, ref DHWorkComputerAnswerMessage args)
    {
        var worker = WorkerOf(args.Actor);
        if (worker.Task is not { } task)
            return;

        var text = args.Answer.Trim().Replace(" ", string.Empty).Replace(",", ".");
        var ok = long.TryParse(text, out var value) && value == task.Answer;
        string status;
        if (ok)
        {
            if (_bank.AccountOf(args.Actor) is { } account)
                _bank.Deposit(account, task.Reward, Loc.GetString("dh-work-note"));

            worker.Earned += task.Reward;
            worker.Task = null;
            worker.Next = _timing.CurTime + ent.Comp.Interval;
            status = Loc.GetString("dh-work-correct", ("reward", task.Reward));
            _audio.PlayPvs(OkSound, ent);
        }
        else
        {
            task.AttemptsLeft--;
            _audio.PlayPvs(DenySound, ent);
            if (task.AttemptsLeft <= 0)
            {
                worker.Task = null;
                worker.Next = _timing.CurTime + ent.Comp.Interval;
                status = Loc.GetString("dh-work-failed", ("answer", task.Answer));
            }
            else
            {
                status = Loc.GetString("dh-work-wrong", ("left", task.AttemptsLeft));
            }
        }

        UpdateUi(ent, args.Actor, status);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUi)
            return;

        _nextUi = now + TimeSpan.FromSeconds(1);
        var query = EntityQueryEnumerator<DHWorkComputerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            foreach (var actor in _ui.GetActors(uid, DHWorkComputerUiKey.Key))
            {
                UpdateUi((uid, comp), actor, null);
            }
        }
    }

    private void UpdateUi(Entity<DHWorkComputerComponent> ent, EntityUid user, string? status)
    {
        var worker = WorkerOf(user);
        var now = _timing.CurTime;
        if (worker.Task == null && now >= worker.Next)
        {
            worker.Task = Generate();
            worker.Task.Reward = _random.Next(ent.Comp.RewardMin, ent.Comp.RewardMax + 1) / 5 * 5;
            worker.Task.AttemptsLeft = ent.Comp.Attempts;
        }

        var next = worker.Task == null ? Math.Max(0, (int) (worker.Next - now).TotalSeconds) : 0;
        var state = new DHWorkComputerState(worker.Task?.Text, worker.Task?.Reward ?? 0, next,
            worker.Task?.AttemptsLeft ?? 0, worker.Earned, status);

        // Состояние у каждого работника своё — шлём лично ему.
        _ui.ServerSendUiMessage(ent.Owner, DHWorkComputerUiKey.Key, new DHWorkComputerStateMessage(state), user);
    }

    #region Задания

    private WorkTask Generate()
    {
        return _random.Next(6) switch
        {
            0 => Statement(),
            1 => Interest(),
            2 => Vat(),
            3 => Chart(),
            4 => Invoice(),
            _ => Payroll(),
        };
    }

    /// <summary>Выписка: начальный остаток и операции — найти итоговый остаток.</summary>
    private WorkTask Statement()
    {
        var balance = _random.Next(20, 200) * 100;
        var text = new StringBuilder(Loc.GetString("dh-work-statement-head", ("start", balance)));
        text.Append('\n');
        var total = (long) balance;
        for (var i = 0; i < 5; i++)
        {
            var amount = _random.Next(5, 80) * 10;
            var incoming = _random.Prob(0.45f);
            total += incoming ? amount : -amount;
            text.Append(Loc.GetString(incoming ? "dh-work-statement-in" : "dh-work-statement-out",
                ("n", i + 1), ("amount", amount), ("what", Loc.GetString($"dh-work-op-{_random.Next(8)}"))));
            text.Append('\n');
        }

        text.Append(Loc.GetString("dh-work-statement-ask"));
        return new WorkTask { Text = text.ToString(), Answer = total };
    }

    /// <summary>Простые проценты по кредиту или вкладу.</summary>
    private WorkTask Interest()
    {
        var principal = _random.Next(2, 30) * 1000;
        int[] rates = { 6, 9, 12, 15, 18, 24 };
        var rate = rates[_random.Next(rates.Length)];
        int[] terms = { 2, 3, 4, 6, 8, 12 };
        var months = terms[_random.Next(terms.Length)];
        var answer = (long) principal * rate * months / 1200;
        return new WorkTask
        {
            Text = Loc.GetString("dh-work-interest", ("principal", principal), ("rate", rate), ("months", months)),
            Answer = answer,
        };
    }

    /// <summary>Выделить налог из суммы «с налогом» (ставка 20%).</summary>
    private WorkTask Vat()
    {
        var net = _random.Next(5, 150) * 50;
        var gross = net * 6 / 5;
        return new WorkTask
        {
            Text = Loc.GetString("dh-work-vat", ("gross", gross)),
            Answer = gross - net,
        };
    }

    /// <summary>График выручки по месяцам — найти месяц с наибольшим или наименьшим значением.</summary>
    private WorkTask Chart()
    {
        var values = new int[Months.Length];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = _random.Next(3, 20) * 10;
        }

        var max = _random.Prob(0.5f);
        var best = 0;
        for (var i = 1; i < values.Length; i++)
        {
            if (max ? values[i] > values[best] : values[i] < values[best])
                best = i;
        }

        // Ничьи убираем, чтобы ответ был один.
        for (var i = 0; i < values.Length; i++)
        {
            if (i != best && values[i] == values[best])
                values[i] += max ? -10 : 10;
        }

        var text = new StringBuilder(Loc.GetString("dh-work-chart-head"));
        text.Append("\n[font=\"Monospace\"]");
        for (var i = 0; i < values.Length; i++)
        {
            text.Append($"\n{i + 1}. {Loc.GetString("dh-work-month-" + Months[i]),-4} {new string('█', values[i] / 10),-20} {values[i]}");
        }

        text.Append("[/font]\n");
        text.Append(Loc.GetString(max ? "dh-work-chart-ask-max" : "dh-work-chart-ask-min"));
        return new WorkTask { Text = text.ToString(), Answer = best + 1 };
    }

    /// <summary>Накладная: позиции «количество × цена» — итог.</summary>
    private WorkTask Invoice()
    {
        var text = new StringBuilder(Loc.GetString("dh-work-invoice-head"));
        long total = 0;
        var lines = _random.Next(3, 5);
        for (var i = 0; i < lines; i++)
        {
            var qty = _random.Next(1, 12);
            var price = _random.Next(3, 60) * 5;
            total += qty * price;
            text.Append('\n');
            text.Append(Loc.GetString("dh-work-invoice-line", ("n", i + 1),
                ("item", Loc.GetString($"dh-work-item-{_random.Next(8)}")), ("qty", qty), ("price", price)));
        }

        text.Append('\n');
        text.Append(Loc.GetString("dh-work-invoice-ask"));
        return new WorkTask { Text = text.ToString(), Answer = total };
    }

    /// <summary>Зарплатная ведомость: ставка в час, часы, сверхурочные ×1.5.</summary>
    private WorkTask Payroll()
    {
        var rate = _random.Next(4, 20) * 2;
        var hours = _random.Next(20, 41);
        var overtime = _random.Next(1, 6) * 2;
        var answer = (long) rate * hours + rate * overtime * 3 / 2;
        return new WorkTask
        {
            Text = Loc.GetString("dh-work-payroll", ("rate", rate), ("hours", hours), ("overtime", overtime)),
            Answer = answer,
        };
    }

    #endregion
}
