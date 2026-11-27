using Content.Shared.Dataset;
using Content.Shared.Random.Helpers;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._DarkestHour.Characters;

/// <summary>
/// Генератор имён вселенной TDH (план осады п. 14, решения автора 2026-11-27). Все говорят на позднем общечеловеческом,
/// поэтому набор один, но с оттенком мира рождения: государство, которому мир принадлежал в год рождения, сдвигает
/// вес к лигийским корням (Республика, Лига), евразийским (Империя, ЕФС, Федерация) или общему котлу (ЗГА, ОНК).
/// Имя и фамилия выбираются независимо — за два века всё перемешалось. У уроженцев Империи изредка «фон» (у офицеров
/// чаще). Наборы — Tools/_DarkestHour/names/build_names.py.
/// </summary>
public sealed partial class DHNameGeneratorSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;

    /// <summary>Вес лигийских, евразийских и общих корней по государству.</summary>
    private static readonly Dictionary<string, (float Lig, float Eur, float Common)> Weights = new()
    {
        ["dh-polity-republic"] = (0.55f, 0.10f, 0.35f),
        ["dh-polity-league"] = (0.60f, 0.05f, 0.35f),
        ["dh-polity-sol-nations"] = (0.45f, 0.45f, 0.10f),
        ["dh-polity-empire"] = (0.10f, 0.55f, 0.35f),
        ["dh-polity-north-wars"] = (0.10f, 0.55f, 0.35f),
        ["dh-polity-efs"] = (0.05f, 0.60f, 0.35f),
        ["dh-polity-federation"] = (0.20f, 0.40f, 0.40f),
        ["dh-polity-zga"] = (0.30f, 0.30f, 0.40f),
        ["dh-polity-onk"] = (0.25f, 0.20f, 0.55f),
    };

    private static readonly (float Lig, float Eur, float Common) DefaultWeights = (0.30f, 0.30f, 0.40f);

    /// <summary>Государства, где бывает «фон».</summary>
    private static readonly HashSet<string> NoblePolities = new() { "dh-polity-empire", "dh-polity-north-wars" };

    public const float NobleChance = 0.03f;
    public const float OfficerNobleChance = 0.08f;

    /// <summary>Случайное имя и фамилия; <paramref name="polity"/> — государство мира рождения в год рождения.</summary>
    public string Generate(Gender gender, string? polity = null, bool officer = false)
    {
        var weights = polity != null && Weights.TryGetValue(polity, out var w) ? w : DefaultWeights;

        var male = gender switch
        {
            Gender.Male => true,
            Gender.Female => false,
            _ => _random.Prob(0.5f),
        };

        var first = Pick((male ? "DHNamesMale" : "DHNamesFemale") + Flavor(weights));
        // У женщин — женская форма русских фамилий (Лебедева, Ланская).
        var last = Pick((male ? "DHNamesLast" : "DHNamesLastFemale") + Flavor(weights));

        if (polity != null && NoblePolities.Contains(polity) &&
            _random.Prob(officer ? OfficerNobleChance : NobleChance))
        {
            last = Loc.GetString("dh-name-noble", ("last", last));
        }

        return Loc.GetString("namepreset-firstlast", ("first", first), ("last", last));
    }

    /// <summary>Имя уроженца места рождения в возрасте <paramref name="age"/>; места нет — общий оттенок.</summary>
    public string Generate(Gender gender, ProtoId<DHBirthplacePrototype>? birthplace, int age, bool officer = false)
    {
        string? polity = null;
        if (birthplace is { } id && _proto.TryIndex(id, out var place))
            polity = place.PolityIn(DHBirthplaces.BirthYear(age))?.Id;

        return Generate(gender, polity, officer);
    }

    private string Flavor((float Lig, float Eur, float Common) weights)
    {
        var roll = _random.NextFloat() * (weights.Lig + weights.Eur + weights.Common);
        if (roll < weights.Lig)
            return "Lig";

        return roll < weights.Lig + weights.Eur ? "Eur" : "Common";
    }

    private string Pick(string dataset)
    {
        return _random.Pick(_proto.Index<LocalizedDatasetPrototype>(dataset));
    }
}
