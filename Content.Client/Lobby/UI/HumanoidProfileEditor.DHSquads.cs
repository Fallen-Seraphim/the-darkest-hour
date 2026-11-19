using System.Linq;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared.Roles;
using Content.Shared._DarkestHour.Squads;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

// DarkestHour: любимый отряд во вкладках ролей Империи и Республики (решение автора 2026-11-19, как в RMC).
public sealed partial class HumanoidProfileEditor
{
    /// <summary>Вкладка стороны → сторона «Осады», у которой есть отряды.</summary>
    private static readonly Dictionary<string, DHAssaultFaction> DHSquadSides = new()
    {
        ["dh-jobs-side-republic"] = DHAssaultFaction.Republic,
        ["dh-jobs-side-empire"] = DHAssaultFaction.Empire,
    };

    /// <summary>Строка «Любимый отряд» наверху вкладки стороны.</summary>
    private void DHAddSquadSelector(BoxContainer list, string sideKey)
    {
        if (!DHSquadSides.TryGetValue(sideKey, out var faction))
            return;

        var squads = _prototypeManager.EnumeratePrototypes<DHSquadPrototype>()
            .Where(s => s.Faction == faction)
            .OrderBy(s => s.Order)
            .ToList();
        if (squads.Count == 0)
            return;

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(5f, 5f, 5f, 8f),
            SeparationOverride = 8,
        };
        row.AddChild(new Label { Text = Loc.GetString("dh-squad-preference-label") });

        var button = new OptionButton { MinWidth = 180 };
        button.AddItem(Loc.GetString("dh-squad-preference-any"), 0);
        for (var i = 0; i < squads.Count; i++)
        {
            button.AddItem(Loc.GetString(squads[i].Name), i + 1);
        }

        var current = faction == DHAssaultFaction.Republic
            ? Profile?.DHDetails.RepublicSquad
            : Profile?.DHDetails.EmpireSquad;
        var index = squads.FindIndex(s => s.ID == current);
        button.SelectId(index + 1);

        button.OnItemSelected += args =>
        {
            button.SelectId(args.Id);
            if (Profile is null)
                return;

            var squad = args.Id == 0 ? null : squads[args.Id - 1].ID;
            var details = Profile.DHDetails.Clone();
            if (faction == DHAssaultFaction.Republic)
                details.RepublicSquad = squad;
            else
                details.EmpireSquad = squad;

            Profile = Profile.WithDHDetails(details);
            UpdateDHDetails();
            SetDirty();
        };

        row.AddChild(button);
        row.AddChild(new Label
        {
            Text = Loc.GetString("dh-squad-preference-hint"),
            FontColorOverride = Color.Gray,
        });
        list.AddChild(row);
    }

    /// <summary>
    /// Описание должности с её званиями, как в RMC (решение автора 2026-11-19): от младшего к старшему, с наигранным
    /// временем на должности, нужным для звания.
    /// </summary>
    private string? DHJobDescription(JobPrototype job)
    {
        var description = job.LocalizedDescription;
        if (job.Ranks is not { Count: > 0 } ranks)
            return description;

        var lines = new List<string>();
        foreach (var (rankId, requirements) in ranks.Reverse())
        {
            if (!_prototypeManager.TryIndex(rankId, out var rank))
                continue;

            var hours = requirements?.OfType<RoleTimeRequirement>().Select(r => r.Time.TotalHours).DefaultIfEmpty(0).Max() ?? 0;
            lines.Add(hours > 0
                ? Loc.GetString("dh-job-rank-line-time", ("rank", Loc.GetString(rank.Name)), ("hours", (int) hours))
                : Loc.GetString("dh-job-rank-line", ("rank", Loc.GetString(rank.Name))));
        }

        if (lines.Count == 0)
            return description;

        var ranksText = Loc.GetString("dh-job-ranks-header") + "\n" + string.Join("\n", lines);
        return string.IsNullOrEmpty(description) ? ranksText : description + "\n\n" + ranksText;
    }
}
