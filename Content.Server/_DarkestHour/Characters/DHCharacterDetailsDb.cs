using System.Text.Json;
using Content.Shared._DarkestHour.Characters;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Characters;

/// <summary>Подробное описание персонажа ↔ JSON-колонка профиля <c>dh_details</c>.</summary>
public static class DHCharacterDetailsDb
{
    private sealed record Dto(
        string ShortExamine,
        int Height,
        int Weight,
        string Build,
        string? Birthplace,
        string GeneralRecord,
        string MedicalRecord,
        string CriminalRecord);

    public static string Serialize(DHCharacterDetails details)
    {
        return JsonSerializer.Serialize(new Dto(
            details.ShortExamine,
            details.Height,
            details.Weight,
            details.Build.ToString(),
            details.Birthplace?.Id,
            details.GeneralRecord,
            details.MedicalRecord,
            details.CriminalRecord));
    }

    public static DHCharacterDetails Deserialize(string? json)
    {
        var details = new DHCharacterDetails();
        if (string.IsNullOrWhiteSpace(json))
            return details;

        try
        {
            if (JsonSerializer.Deserialize<Dto>(json) is not { } dto)
                return details;

            details.ShortExamine = dto.ShortExamine ?? string.Empty;
            details.Height = dto.Height;
            details.Weight = dto.Weight;
            details.Build = Enum.TryParse<DHBuild>(dto.Build, out var build) ? build : DHBuild.Average;
            details.Birthplace = string.IsNullOrEmpty(dto.Birthplace) ? null : (ProtoId<DHBirthplacePrototype>?) new ProtoId<DHBirthplacePrototype>(dto.Birthplace);
            details.GeneralRecord = dto.GeneralRecord ?? string.Empty;
            details.MedicalRecord = dto.MedicalRecord ?? string.Empty;
            details.CriminalRecord = dto.CriminalRecord ?? string.Empty;
        }
        catch (JsonException)
        {
            // Повреждённые данные — пустое описание.
        }

        return details;
    }
}
