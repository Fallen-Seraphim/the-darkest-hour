using System.Globalization;
using Content.Server._DarkestHour.City;
using Content.Shared._DarkestHour.Characters;
using Content.Shared._DarkestHour.City;
using Content.Shared._DarkestHour.Uprising;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Uprising;

/// <summary>Запись базы данных военпола.</summary>
public sealed class DHDatabaseRecord
{
    public int Id;
    public string Name = string.Empty;
    public string Number = string.Empty;
    public DateTime Birth;
    public string BirthPlace = string.Empty;
    public DateTime Issued;
    public string Sex = string.Empty;
    public string Species = string.Empty;
    public DHDatabaseRecordKind Kind;

    /// <summary>Фальшивка задания: номер задания.</summary>
    public int TaskId;
}

public enum DHDatabaseRecordKind : byte
{
    Genuine,

    /// <summary>Фальшивка, подброшенная заданием «проверить базу» (явная несостыковка).</summary>
    TaskFake,

    /// <summary>Поддельный паспорт ОФСК (тонкая несостыковка: серия номера на год расходится с датой выдачи).</summary>
    Forged,
}

/// <summary>
/// База данных военпола (решение автора 2026-11-12): записи о паспортах жителей района. Терминалы работают, пока цел
/// сервер базы. Фальшивки выдают несостыковки — правила проверки в справке терминала:
/// паспорт выдаётся с 14 лет; серия номера (LA NN ...) — две последние цифры года выдачи; номер уникален; дата выдачи
/// не позже сегодняшней; в месте рождения в год рождения уже жили. Удалить фальшивку задания — продвинуть задание;
/// поддельный паспорт ОФСК — награда; настоящую запись — штраф со счёта военпола.
/// </summary>
public sealed partial class DHPoliceDatabaseSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DHBankSystem _bank = default!;
    [Dependency] private NamingSystem _naming = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DHTaskSystem _tasks = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    /// <summary>Награда за найденный поддельный паспорт ОФСК.</summary>
    public const int ForgedReward = 400;

    /// <summary>Штраф за удалённую настоящую запись.</summary>
    public const int GenuinePenalty = 150;

    private const int GeneratedRecords = 24;

    private static readonly SoundSpecifier OkSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");
    private static readonly SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

    private readonly List<DHDatabaseRecord> _records = new();
    private int _nextId = 1;
    private bool _generated;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _records.Clear();
            _nextId = 1;
            _generated = false;
        });

        Subs.BuiEvents<DHPoliceDatabaseTerminalComponent>(DHPoliceDatabaseUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<DHPoliceDatabaseDeleteMessage>(OnDelete);
        });
    }

    #region Записи

    private static DateTime ParseDate(string text)
    {
        return DateTime.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : DHCityClock.Start.AddYears(-30);
    }

    /// <summary>Запись из выданного паспорта (DHCitySystem при появлении жителя).</summary>
    public void AddRecord(DHPassportComponent passport, bool forged)
    {
        EnsureGenerated();
        _records.Add(new DHDatabaseRecord
        {
            Id = _nextId++,
            Name = passport.Holder,
            Number = passport.Number,
            Birth = ParseDate(passport.BirthDate),
            BirthPlace = passport.BirthPlace,
            Issued = ParseDate(passport.Issued),
            Sex = passport.Sex,
            Species = passport.Species,
            Kind = forged ? DHDatabaseRecordKind.Forged : DHDatabaseRecordKind.Genuine,
        });
        Shuffle();
    }

    /// <summary>Жители района без игроков: база не должна состоять из одних игроков.</summary>
    private void EnsureGenerated()
    {
        if (_generated)
            return;

        _generated = true;
        for (var i = 0; i < GeneratedRecords; i++)
        {
            _records.Add(Generate(DHDatabaseRecordKind.Genuine, 0));
        }
    }

    /// <summary>Правдоподобная запись (без несостыковок).</summary>
    private DHDatabaseRecord Generate(DHDatabaseRecordKind kind, int taskId)
    {
        var male = _random.Prob(0.5f);
        var age = _random.Next(18, 70);
        var birth = new DateTime(DHCityClock.Year - age, 1, 1).AddDays(_random.Next(0, 365));
        var issued = new DateTime(DHCityClock.Year - _random.Next(1, 10), _random.Next(1, 13), _random.Next(1, 29));
        if (issued < birth.AddYears(14))
            issued = birth.AddYears(14).AddDays(_random.Next(0, 300));
        if (issued > DHCityClock.Start)
            issued = DHCityClock.Start.AddDays(-_random.Next(1, 60));

        return new DHDatabaseRecord
        {
            Id = _nextId++,
            Name = _naming.GetName("Human", male ? Gender.Male : Gender.Female),
            Number = $"LA {issued.Year % 100:D2} {_random.Next(100000, 1000000)}",
            Birth = birth,
            BirthPlace = BirthPlaceFor(birth.Year, true),
            Issued = issued,
            Sex = Loc.GetString(male ? "dh-passport-sex-male" : "dh-passport-sex-female"),
            Species = Loc.GetString("species-name-human"),
            Kind = kind,
            TaskId = taskId,
        };
    }

    /// <summary>Мир, где в этот год жили (inhabited) или ещё нет (иначе).</summary>
    private string BirthPlaceFor(int year, bool inhabited)
    {
        var places = new List<DHBirthplacePrototype>();
        foreach (var place in _proto.EnumeratePrototypes<DHBirthplacePrototype>())
        {
            if (place.AvailableIn(year) == inhabited)
                places.Add(place);
        }

        if (places.Count == 0)
            return Loc.GetString("dh-passport-place-lost-angels");

        return Loc.GetString(places[_random.Next(places.Count)].Name);
    }

    /// <summary>Задание «проверить базу»: подбросить фальшивки, у каждой — одна явная несостыковка.</summary>
    public void InjectFakes(int taskId, int count)
    {
        EnsureGenerated();
        for (var i = 0; i < count; i++)
        {
            var fake = Generate(DHDatabaseRecordKind.TaskFake, taskId);
            switch (_random.Next(5))
            {
                case 0: // выдан раньше 14 лет
                    fake.Issued = fake.Birth.AddYears(_random.Next(8, 14)).AddDays(_random.Next(0, 200));
                    fake.Number = $"LA {fake.Issued.Year % 100:D2} {_random.Next(100000, 1000000)}";
                    break;
                case 1: // в месте рождения тогда ещё не жили
                    var place = BirthPlaceFor(fake.Birth.Year, false);
                    if (place == Loc.GetString("dh-passport-place-lost-angels"))
                        goto case 2;
                    fake.BirthPlace = place;
                    break;
                case 2: // серия не совпадает с годом выдачи (явно)
                    fake.Number = $"LA {(fake.Issued.Year + _random.Next(2, 6)) % 100:D2} {_random.Next(100000, 1000000)}";
                    break;
                case 3: // номер чужого паспорта
                    var other = _records[_random.Next(_records.Count)];
                    fake.Number = other.Number;
                    break;
                default: // выдан «в будущем»
                    fake.Issued = DHCityClock.Start.AddDays(_random.Next(20, 400));
                    fake.Number = $"LA {fake.Issued.Year % 100:D2} {_random.Next(100000, 1000000)}";
                    break;
            }

            _records.Add(fake);
        }

        Shuffle();
        UpdateAllUi();
    }

    /// <summary>Убрать оставшиеся фальшивки задания (задание провалено или брошено).</summary>
    public void ClearFakes(int taskId)
    {
        _records.RemoveAll(r => r.Kind == DHDatabaseRecordKind.TaskFake && r.TaskId == taskId);
        UpdateAllUi();
    }

    private void Shuffle()
    {
        // Новые записи не должны выдавать себя местом в конце списка: порядок — по фамилии-имени.
        _records.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
    }

    #endregion

    #region Терминал

    /// <summary>Сервер базы на месте и цел.</summary>
    public bool Online()
    {
        var query = EntityQueryEnumerator<DHPoliceDatabaseServerComponent, MetaDataComponent>();
        while (query.MoveNext(out _, out _, out var meta))
        {
            if (meta.EntityLifeStage < EntityLifeStage.Terminating)
                return true;
        }

        return false;
    }

    private void OnOpened(Entity<DHPoliceDatabaseTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent, null);
    }

    private void OnDelete(Entity<DHPoliceDatabaseTerminalComponent> ent, ref DHPoliceDatabaseDeleteMessage args)
    {
        if (!Online())
        {
            UpdateUi(ent, Loc.GetString("dh-police-db-offline"));
            return;
        }

        var id = args.Id;
        var record = _records.Find(r => r.Id == id);
        if (record == null)
            return;

        _records.Remove(record);
        string status;
        switch (record.Kind)
        {
            case DHDatabaseRecordKind.TaskFake:
                status = Loc.GetString("dh-police-db-removed-fake", ("name", record.Name));
                _audio.PlayPvs(OkSound, ent);
                _tasks.OnFakeRemoved(record.TaskId);
                break;
            case DHDatabaseRecordKind.Forged:
                status = Loc.GetString("dh-police-db-removed-forged", ("name", record.Name), ("reward", ForgedReward));
                _audio.PlayPvs(OkSound, ent);
                _tasks.Reward(DHTaskSide.Police, ForgedReward, Loc.GetString("dh-task-log-forged", ("name", record.Name)));
                break;
            default:
                status = Loc.GetString("dh-police-db-removed-genuine", ("name", record.Name), ("fine", GenuinePenalty));
                _audio.PlayPvs(DenySound, ent);
                _bank.Withdraw(_bank.OrgAccount(DHBankOrg.Police), GenuinePenalty,
                    Loc.GetString("dh-police-db-fine-note", ("name", record.Name)));
                break;
        }

        UpdateAllUi();
        UpdateUi(ent, status);
    }

    private void UpdateAllUi()
    {
        var query = EntityQueryEnumerator<DHPoliceDatabaseTerminalComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateUi((uid, comp), null);
        }
    }

    private void UpdateUi(Entity<DHPoliceDatabaseTerminalComponent> ent, string? status)
    {
        if (!_ui.IsUiOpen(ent.Owner, DHPoliceDatabaseUiKey.Key))
            return;

        EnsureGenerated();
        var online = Online();
        var records = new List<DHDatabaseRecordInfo>();
        if (online)
        {
            foreach (var r in _records)
            {
                records.Add(new DHDatabaseRecordInfo(r.Id, r.Name, r.Number, r.Birth.ToString("dd.MM.yyyy"), r.BirthPlace,
                    r.Issued.ToString("dd.MM.yyyy"), r.Sex, r.Species));
            }
        }

        var worlds = new List<string>();
        var places = new List<DHBirthplacePrototype>(_proto.EnumeratePrototypes<DHBirthplacePrototype>());
        places.Sort((a, b) => a.Founded.CompareTo(b.Founded));
        foreach (var place in places)
        {
            worlds.Add(place.Until is { } until
                ? Loc.GetString("dh-police-db-world-until", ("name", Loc.GetString(place.Name)), ("from", place.Founded), ("until", until))
                : Loc.GetString("dh-police-db-world", ("name", Loc.GetString(place.Name)), ("from", place.Founded)));
        }

        _ui.SetUiState(ent.Owner, DHPoliceDatabaseUiKey.Key,
            new DHPoliceDatabaseState(online, records, worlds, online ? status : Loc.GetString("dh-police-db-offline")));
    }

    #endregion
}
