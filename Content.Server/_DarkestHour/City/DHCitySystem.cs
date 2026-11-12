using Content.Server._DarkestHour.Uprising;
using Content.Shared._DarkestHour.Characters;
using Content.Shared._DarkestHour.City;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
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
    [Dependency] private DHPoliceDatabaseSystem _database = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
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
        _bank.SetOwner(mob, account);

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

    /// <summary>Город — самое частое место рождения жителей без выбранного в профиле.</summary>
    private const string LocalBirthPlace = "dh-passport-place-lost-angels";

    /// <summary>
    /// Все незаполненные паспорта в снаряжении: в слоте документа и поддельный в сумке (ОФСК). Данные одни и те же —
    /// поддельный паспорт выписан на настоящее имя, чтобы сойти за жителя.
    /// </summary>
    private void FillPassport(EntityUid mob, string name)
    {
        var data = PassportData(mob);
        var filled = new HashSet<EntityUid>();

        if (_inventory.TryGetSlotEntity(mob, "id", out var id) && TryComp<DHPassportComponent>(id, out var inSlot) &&
            inSlot.Number.Length == 0)
        {
            Fill((id.Value, inSlot), name, data);
            filled.Add(id.Value);
        }

        for (var i = 0; i < 4; i++)
        {
            if (FindChild<DHPassportComponent>(mob, p => p.Number.Length == 0) is not { } other || !filled.Add(other))
                break;

            Fill((other, Comp<DHPassportComponent>(other)), name, data);
        }
    }

    private (string Birth, string Place, string Sex, string Species) PassportData(EntityUid mob)
    {
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

        return (birth.ToString("dd.MM.yyyy"), BirthPlace(mob, birth.Year), sex, species);
    }

    /// <summary>Место рождения из профиля персонажа, иначе случайное: чаще всего город, иначе мир, где в год рождения жили.</summary>
    private string BirthPlace(EntityUid mob, int birthYear)
    {
        if (TryComp<DHCharacterDescriptionComponent>(mob, out var description) &&
            description.Details.Birthplace is { } chosen &&
            _proto.TryIndex(chosen, out var chosenPlace))
        {
            return Loc.GetString(chosenPlace.Name);
        }

        if (_random.Prob(0.5f))
            return Loc.GetString(LocalBirthPlace);

        var places = new List<DHBirthplacePrototype>();
        foreach (var place in _proto.EnumeratePrototypes<DHBirthplacePrototype>())
        {
            if (place.AvailableIn(birthYear))
                places.Add(place);
        }

        return places.Count == 0
            ? Loc.GetString(LocalBirthPlace)
            : Loc.GetString(places[_random.Next(places.Count)].Name);
    }

    private void Fill(Entity<DHPassportComponent> passport, string name,
        (string Birth, string Place, string Sex, string Species) data)
    {
        // Серия номера — две последние цифры года выдачи (правило, по которому военпол проверяет базу). Поддельный
        // паспорт ОФСК выдаёт только серия: она на год не совпадает с датой выдачи.
        var issued = new DateTime(DHCityClock.Year - _random.Next(1, 10), _random.Next(1, 13), _random.Next(1, 29));
        // Паспорт выдаётся не раньше 14 лет (тоже правило проверки базы).
        if (DateTime.TryParseExact(data.Birth, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var born) &&
            issued < born.AddYears(14))
        {
            issued = born.AddYears(14).AddDays(_random.Next(0, 200));
            if (issued > DHCityClock.Start)
                issued = DHCityClock.Start.AddDays(-_random.Next(1, 30));
        }

        var series = issued.Year % 100;
        if (passport.Comp.Forged)
            series = (series + (_random.Prob(0.5f) ? 1 : 99)) % 100;

        passport.Comp.Number = $"LA {series:D2} {_random.Next(100000, 1000000)}";
        passport.Comp.Holder = name;
        passport.Comp.BirthDate = data.Birth;
        passport.Comp.BirthPlace = data.Place;
        passport.Comp.Sex = data.Sex;
        passport.Comp.Species = data.Species;
        passport.Comp.Issued = issued.ToString("dd.MM.yyyy");
        Dirty(passport);

        // В базу данных военпола (поддельный — с пометкой: за его удаление военполу платят больше).
        _database.AddRecord(passport.Comp, passport.Comp.Forged);

        // Паспорт не в слоте документа (поддельный в сумке): имя на удостоверении ставим сами — станция заполняет
        // только карту в слоте.
        if (TryComp<IdCardComponent>(passport, out var card) && string.IsNullOrEmpty(card.FullName))
            _idCard.TryChangeFullName(passport, name, card);
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
