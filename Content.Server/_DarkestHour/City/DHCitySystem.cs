using Content.Server._DarkestHour.Uprising;
using Content.Shared._DarkestHour.City;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Paper;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.City;

/// <summary>Строки окна персонажа (C): номер телефона, счёт, логин и пароль банка.</summary>
[RegisterComponent]
public sealed partial class DHCityBriefingComponent : Component
{
    [ViewVariables]
    public List<string> Lines = new();
}

/// <summary>
/// «Восстание»: при появлении каждому жителю — личный счёт (баланс по стороне), банковская карта (в кошелёк, если
/// он есть) и телефон. Логин, пароль, номер счёта и телефона пишутся в окно персонажа.
/// </summary>
public sealed partial class DHCitySystem : EntitySystem
{
    [Dependency] private DHBankSystem _bank = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private DHPhoneSystem _phones = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStorageSystem _storage = default!;

    private const string PhoneProto = "DHPhone";
    private const string CardProto = "DHBankCard";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<DHCityBriefingComponent, GetBriefingEvent>(OnGetBriefing);
    }

    private bool UprisingActive()
    {
        var query = EntityQueryEnumerator<DHUprisingRuleComponent, ActiveGameRuleComponent>();
        return query.MoveNext(out _, out _);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (!UprisingActive())
            return;

        var mob = ev.Mob;
        var name = Name(mob);
        var account = _bank.Create(name, StartBalance(ev.JobId));

        // Карта: из стартового набора (кошелёк) или новая — в сумку.
        var card = FindChild<DHBankCardComponent>(mob, c => c.Number.Length == 0 && c.Org == DHBankOrg.None)
                   ?? Give(mob, CardProto);
        if (card is { } cardUid && TryComp<DHBankCardComponent>(cardUid, out var cardComp))
        {
            cardComp.Number = account.Number;
            cardComp.Holder = name;
            Dirty(cardUid, cardComp);
        }

        // Телефон: из стартового набора или новый — в сумку.
        var phone = FindChild<DHPhoneComponent>(mob, p => p.OwnerName.Length == 0) ?? Give(mob, PhoneProto);
        var number = string.Empty;
        if (phone is { } phoneUid && TryComp<DHPhoneComponent>(phoneUid, out var phoneComp))
        {
            _phones.SetOwner((phoneUid, phoneComp), name);
            number = phoneComp.Number;
        }

        FillPassport(mob, name);

        var briefing = new List<string>
        {
            Loc.GetString("dh-city-briefing-header"),
            Loc.GetString("dh-city-briefing-phone", ("number", number)),
            Loc.GetString("dh-city-briefing-account", ("number", account.Number), ("balance", account.Balance)),
            Loc.GetString("dh-city-briefing-login", ("login", account.Login), ("password", account.Password)),
        };

        // Главам организаций — реквизиты счёта организации на бумаге (и в брифинге).
        if (ev.JobId != null && RequisitesOrg(ev.JobId) is { } org)
        {
            var orgAccount = _bank.OrgAccount(org);
            GiveRequisites(mob, orgAccount);
            if (org == DHBankOrg.Front)
                Give(mob, FrontCardProto);

            briefing.Add(Loc.GetString("dh-city-briefing-org", ("org", orgAccount.Holder), ("number", orgAccount.Number),
                ("login", orgAccount.Login), ("password", orgAccount.Password)));
        }

        AddBriefing(mob, briefing);
    }

    private const string RequisitesProto = "DHBankRequisites";
    private const string FrontCardProto = "DHBankCardFront";

    /// <summary>Чей счёт доверен должности (решение автора 2026-11-12).</summary>
    private static DHBankOrg? RequisitesOrg(string job)
    {
        return job switch
        {
            "DHLAMPChief" => DHBankOrg.Police,
            "DHLAVRepresentative" => DHBankOrg.Vanguard,
            "DHLAFrontLeader" => DHBankOrg.Front,
            _ => null,
        };
    }

    private void GiveRequisites(EntityUid mob, DHBankAccount account)
    {
        var paper = Give(mob, RequisitesProto);
        if (!TryComp<PaperComponent>(paper, out var comp))
            return;

        _paper.SetContent((paper, comp), Loc.GetString("dh-bank-requisites-text", ("org", account.Holder),
            ("number", account.Number), ("login", account.Login), ("password", account.Password)));
    }

    private static readonly string[] BirthPlaces =
    [
        "dh-passport-place-lost-angels", "dh-passport-place-lost-angels", "dh-passport-place-new-tallinn",
        "dh-passport-place-prosperity", "dh-passport-place-titan", "dh-passport-place-earth",
    ];

    /// <summary>Паспорт в слоте документа: номер, дата рождения (2318 − возраст), место, пол, раса.</summary>
    private void FillPassport(EntityUid mob, string name)
    {
        if (!_inventory.TryGetSlotEntity(mob, "id", out var id) || !TryComp<DHPassportComponent>(id, out var passport))
            return;

        var age = 30;
        var sex = Loc.GetString("dh-passport-sex-unknown");
        var species = Loc.GetString("dh-passport-species-unknown");
        if (TryComp<HumanoidProfileComponent>(mob, out var profile))
        {
            age = profile.Age;
            sex = Loc.GetString(profile.Sex switch
            {
                Sex.Male => "dh-passport-sex-male",
                Sex.Female => "dh-passport-sex-female",
                _ => "dh-passport-sex-unknown",
            });
            if (_proto.TryIndex<SpeciesPrototype>(profile.Species, out var speciesProto))
                species = Loc.GetString(speciesProto.Name);
        }

        var birth = new DateTime(DHCityClock.Year - age, 1, 1).AddDays(_random.Next(0, 365));
        if (birth > DHCityClock.Start.AddYears(-age))
            birth = birth.AddYears(-1);

        passport.Number = $"LA {_random.Next(10, 100)} {_random.Next(100000, 1000000)}";
        passport.Holder = name;
        passport.BirthDate = birth.ToString("dd.MM.yyyy");
        passport.BirthPlace = Loc.GetString(_random.Pick(BirthPlaces));
        passport.Sex = sex;
        passport.Species = species;
        passport.Issued = new DateTime(DHCityClock.Year - _random.Next(1, 10), _random.Next(1, 13), _random.Next(1, 29))
            .ToString("dd.MM.yyyy");
        Dirty(id.Value, passport);
    }

    private static int StartBalance(string? job)
    {
        if (job == null)
            return 300;

        if (job.StartsWith("DHLAV"))
            return 1500;

        if (job.StartsWith("DHLAMP"))
            return 600;

        if (job.StartsWith("DHLAFront"))
            return 200;

        return job switch
        {
            "DHLADistrictHead" => 1200,
            "DHLAEntrepreneur" => 900,
            "DHLADistrictStaff" or "DHLAOfficeWorker" => 600,
            _ => 300,
        };
    }

    /// <summary>Строки — на роль должности в разуме (её брифинг показывается в окне персонажа).</summary>
    private void AddBriefing(EntityUid mob, List<string> lines)
    {
        if (!_mind.TryGetMind(mob, out _, out var mind))
            return;

        foreach (var role in mind.MindRoleContainer.ContainedEntities)
        {
            if (!HasComp<JobRoleComponent>(role))
                continue;

            EnsureComp<DHCityBriefingComponent>(role).Lines.AddRange(lines);
            return;
        }
    }

    private void OnGetBriefing(Entity<DHCityBriefingComponent> ent, ref GetBriefingEvent args)
    {
        foreach (var line in ent.Comp.Lines)
        {
            args.Append(line);
        }
    }

    /// <summary>Предмет с компонентом где-то в снаряжении (до трёх уровней вложенности).</summary>
    private EntityUid? FindChild<T>(EntityUid root, Func<T, bool> predicate, int depth = 0) where T : IComponent
    {
        if (depth > 3 || !TryComp<ContainerManagerComponent>(root, out var manager))
            return null;

        foreach (var container in _container.GetAllContainers(root, manager))
        {
            foreach (var child in container.ContainedEntities)
            {
                if (TryComp<T>(child, out var comp) && predicate(comp))
                    return child;

                if (FindChild(child, predicate, depth + 1) is { } found)
                    return found;
            }
        }

        return null;
    }

    /// <summary>Новый предмет — в сумку за спиной, иначе в руки или под ноги.</summary>
    private EntityUid Give(EntityUid mob, string proto)
    {
        var item = Spawn(proto, Transform(mob).Coordinates);
        if (_inventory.TryGetSlotEntity(mob, "back", out var back) &&
            _storage.Insert(back.Value, item, out _, user: mob, playSound: false))
        {
            return item;
        }

        _hands.PickupOrDrop(mob, item);
        return item;
    }
}
