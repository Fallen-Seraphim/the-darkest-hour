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

    public DHCharacterDetails Clone()
    {
        return (DHCharacterDetails) MemberwiseClone();
    }

    public bool MemberwiseEquals(DHCharacterDetails other)
    {
        return ShortExamine == other.ShortExamine && Height == other.Height && Weight == other.Weight &&
               Build == other.Build && Birthplace == other.Birthplace && GeneralRecord == other.GeneralRecord &&
               MedicalRecord == other.MedicalRecord && CriminalRecord == other.CriminalRecord;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ShortExamine, Height, Weight, Build, Birthplace, GeneralRecord, MedicalRecord, CriminalRecord);
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

        if (Birthplace is { } place &&
            (!proto.TryIndex(place, out var birthplace) || !birthplace.AvailableIn(DHBirthplaces.BirthYear(age))))
        {
            Birthplace = null;
        }
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
