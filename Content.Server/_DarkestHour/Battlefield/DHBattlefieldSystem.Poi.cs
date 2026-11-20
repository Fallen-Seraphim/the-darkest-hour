using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Battlefield;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Server._DarkestHour.Battlefield;

/// <summary>
/// Объекты POI (решение автора 2026-11-19): вышедший из строя объект не работает, пока его не починят — даже если
/// точку никто не захватывал. Ремонт: сварка ~30 с с материалом в другой руке (сталь, у энергоблока — пласталь).
/// </summary>
public sealed partial class DHBattlefieldSystem
{
    /// <summary>Объект сломан (ПВО не стреляет, орудие не заряжается и т. п.).</summary>
    public bool IsBroken(EntityUid uid)
    {
        return TryComp<DHPoiObjectComponent>(uid, out var poi) && poi.Broken;
    }

    /// <summary>Какой-то объект, выключающий зону целиком, сейчас сломан.</summary>
    public bool PointOutOfAction(string pointId)
    {
        var query = EntityQueryEnumerator<DHPoiObjectComponent>();
        while (query.MoveNext(out var poi))
        {
            if (poi.Broken && poi.Point == pointId)
                return true;
        }

        return false;
    }

    private void OnPoiDamaged(Entity<DHPoiObjectComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.Broken || _damageable.GetTotalDamage((ent.Owner, args.Damageable)).Float() < ent.Comp.BreakDamage)
            return;

        ent.Comp.Broken = true;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("dh-poi-object-broken", ("object", Name(ent))), ent, PopupType.LargeCaution);

        foreach (var faction in new[] { DHAssaultFaction.Republic, DHAssaultFaction.Empire })
        {
            _assault.AnnounceToFaction(faction, Loc.GetString("dh-control-point-sender"),
                Loc.GetString("dh-poi-object-broken-announce", ("object", Name(ent))));
        }

        // Первая поломка энергоблока — заражение до конца раунда, даже если блок потом починят.
        if (ent.Comp.Contamination is { } contamination && !ent.Comp.Contaminated)
        {
            ent.Comp.Contaminated = true;
            Spawn(contamination, _transform.GetMapCoordinates(ent));
            foreach (var faction in new[] { DHAssaultFaction.Republic, DHAssaultFaction.Empire })
            {
                _assault.AnnounceToFaction(faction, Loc.GetString("dh-control-point-sender"),
                    Loc.GetString("dh-poi-object-contamination"));
            }
        }

        if (ent.Comp.Point is { } point)
            _controlPoints.RefreshPoint(point);
    }

    private void OnPoiInteract(Entity<DHPoiObjectComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !ent.Comp.Broken || !_tool.HasQuality(args.Used, WeldingQuality))
            return;

        args.Handled = true;
        if (StackInHands(args.User, ent.Comp.RepairStack, ent.Comp.RepairAmount) == null)
        {
            _popup.PopupEntity(Loc.GetString("dh-poi-object-need-material", ("amount", ent.Comp.RepairAmount),
                ("material", Loc.GetString("dh-poi-material-" + ent.Comp.RepairStack.ToLowerInvariant()))), ent, args.User);
            return;
        }

        _tool.UseTool(args.Used, args.User, ent, ent.Comp.RepairTime, new[] { WeldingQuality },
            new DHPoiRepairDoAfterEvent(), ent.Comp.RepairFuel);
    }

    private void OnPoiRepaired(Entity<DHPoiObjectComponent> ent, ref DHPoiRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !ent.Comp.Broken)
            return;

        args.Handled = true;
        if (StackInHands(args.User, ent.Comp.RepairStack, ent.Comp.RepairAmount) is not { } stack ||
            !_stack.TryUse((stack.Owner, stack.Comp), ent.Comp.RepairAmount))
        {
            return;
        }

        ent.Comp.Broken = false;
        Dirty(ent);
        _damageable.SetAllDamage(ent.Owner, FixedPoint2.Zero);
        _popup.PopupEntity(Loc.GetString("dh-poi-object-repaired", ("object", Name(ent))), ent, args.User);
        if (ent.Comp.Point is { } point)
            _controlPoints.RefreshPoint(point);
    }

    private void OnPoiExamined(Entity<DHPoiObjectComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Broken)
        {
            args.PushMarkup(Loc.GetString("dh-poi-object-examine-broken", ("amount", ent.Comp.RepairAmount),
                ("material", Loc.GetString("dh-poi-material-" + ent.Comp.RepairStack.ToLowerInvariant()))));
        }
    }
}
