using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Characters;

/// <summary>
/// Подробное описание персонажа (по образцу CMU, решение автора 2026-11-11): короткая строка при осмотре, рост, вес,
/// телосложение, место рождения и «документы» — биография, медицинская карта, судимости. Хранится в профиле
/// (одна JSON-колонка <c>dh_details</c>), на персонаже — <see cref="DHCharacterDescriptionComponent"/>; читается окном
/// «Подробное описание» при осмотре.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class DHCharacterDetails
{
    public const int MaxShortExamine = 200;
    public const int MaxRecord = 2000;
    public const int MinHeight = 140;
    public const int MaxHeight = 230;
    public const int MinWeight = 40;
    public const int MaxWeight = 180;

    /// <summary>Строка при обычном осмотре («Высокий, со шрамом через бровь»).</summary>
    [DataField]
    public string ShortExamine = string.Empty;

    /// <summary>Рост, см; 0 — не указан.</summary>
    [DataField]
    public int Height;

    /// <summary>Вес, кг; 0 — не указан.</summary>
    [DataField]
    public int Weight;

    [DataField]
    public DHBuild Build = DHBuild.Average;

    /// <summary>Место рождения; null — не указано.</summary>
    [DataField]
    public ProtoId<DHBirthplacePrototype>? Birthplace;

    /// <summary>Общие сведения — биография, послужной список.</summary>
    [DataField]
    public string GeneralRecord = string.Empty;

    [DataField]
    public string MedicalRecord = string.Empty;

    [DataField]
    public string CriminalRecord = string.Empty;

    /// <summary>
    /// Любимый отряд Республики (решение автора 2026-11-19, как в RMC): при распределении пехоты на старте раунда боец
    /// попадает в него, если отряд не перегружен. null — без разницы.
    /// </summary>
    [DataField]
    public string? RepublicSquad;

    /// <summary>Любимый отряд Империи; null — без разницы.</summary>
    [DataField]
    public string? EmpireSquad;

    public DHCharacterDetails Clone()
    {
        return (DHCharacterDetails) MemberwiseClone();
    }

    public bool MemberwiseEquals(DHCharacterDetails other)
    {
        return ShortExamine == other.ShortExamine && Height == other.Height && Weight == other.Weight &&
               Build == other.Build && Birthplace == other.Birthplace && GeneralRecord == other.GeneralRecord &&
               MedicalRecord == other.MedicalRecord && CriminalRecord == other.CriminalRecord &&
               RepublicSquad == other.RepublicSquad && EmpireSquad == other.EmpireSquad;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(HashCode.Combine(ShortExamine, Height, Weight, Build, Birthplace, GeneralRecord,
            MedicalRecord, CriminalRecord), RepublicSquad, EmpireSquad);
    }

    /// <summary>Обрезает тексты, зажимает рост/вес, сбрасывает место рождения, невозможное для года рождения.</summary>
    public void EnsureValid(IPrototypeManager proto, int age)
    {
        ShortExamine = Clip(ShortExamine, MaxShortExamine);
        GeneralRecord = Clip(GeneralRecord, MaxRecord);
        MedicalRecord = Clip(MedicalRecord, MaxRecord);
        CriminalRecord = Clip(CriminalRecord, MaxRecord);
        Height = Height == 0 ? 0 : Math.Clamp(Height, MinHeight, MaxHeight);
        Weight = Weight == 0 ? 0 : Math.Clamp(Weight, MinWeight, MaxWeight);
        if (!Enum.IsDefined(Build))
            Build = DHBuild.Average;

        RepublicSquad = ValidSquad(proto, RepublicSquad);
        EmpireSquad = ValidSquad(proto, EmpireSquad);

        if (Birthplace is { } place &&
            (!proto.TryIndex(place, out var birthplace) || !birthplace.AvailableIn(DHBirthplaces.BirthYear(age))))
        {
            Birthplace = null;
        }
    }

    /// <summary>Несуществующий отряд (удалён или опечатка) — «без разницы».</summary>
    private static string? ValidSquad(IPrototypeManager proto, string? squad)
    {
        return squad != null && proto.HasIndex<Content.Shared._DarkestHour.Squads.DHSquadPrototype>(squad) ? squad : null;
    }

    private static string Clip(string text, int max)
    {
        text = text.Trim();
        return text.Length > max ? text[..max] : text;
    }
}

[Serializable, NetSerializable]
public enum DHBuild : byte
{
    Thin,
    Average,
    Athletic,
    Muscular,
    Stocky,
    Heavy,
}
