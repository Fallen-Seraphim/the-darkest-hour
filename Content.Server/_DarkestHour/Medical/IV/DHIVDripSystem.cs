using System.Diagnostics.CodeAnalysis;
using Content.Server.Chat.Systems;
using Content.Shared._DarkestHour.Medical.IV;
using Content.Shared.Body.Components;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Medical.IV;

/// <summary>DarkestHour: переливание через капельницу и пакеты крови (порт RMC-14 IVDripSystem).</summary>
public sealed partial class DHIVDripSystem : SharedDHIVDripSystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private IGameTiming _timing = default!;

    private bool TryGetBloodstream(EntityUid attachedTo,
        [NotNullWhen(true)] out Entity<SolutionComponent>? solEnt,
        [NotNullWhen(true)] out Solution? solution)
    {
        solEnt = default;
        solution = default;
        return TryComp(attachedTo, out BloodstreamComponent? stream) &&
               _solutionContainer.TryGetSolution(attachedTo, stream.BloodSolutionName, out solEnt, out solution);
    }

    protected override void DoRip(DamageSpecifier? damage, EntityUid attached, EntityUid? user,
        ProtoId<EmotePrototype> ripEmote, bool predict)
    {
        base.DoRip(damage, attached, user, ripEmote, predict);
        _chat.TryEmoteWithoutChat(attached, ripEmote);
    }

    /// <summary>Перелить порцию: в кровь — только кровь из пакета, из крови — в пакет.</summary>
    private void Transfer(EntityUid pack, DHBloodPackComponent packComp, EntityUid attachedTo, bool injecting,
        FixedPoint2 amount)
    {
        if (!_solutionContainer.TryGetSolution(pack, packComp.Solution, out var packSolEnt, out var packSol) ||
            !TryGetBloodstream(attachedTo, out var streamSolEnt, out var streamSol))
        {
            return;
        }

        if (injecting)
        {
            if (streamSol.Volume >= streamSol.MaxVolume)
                return;

            // Не кровь (лекарства и т. п.) не переливается — остаётся в пакете.
            var excluded = packSol.SplitSolutionWithout(packSol.MaxVolume, packComp.TransferableReagents);
            _solutionContainer.TryTransferSolution(streamSolEnt.Value, packSol, amount);
            _solutionContainer.TryAddSolution(packSolEnt.Value, excluded);
            Dirty(packSolEnt.Value);
        }
        else if (packSol.Volume < packSol.MaxVolume)
        {
            _solutionContainer.TryTransferSolution(packSolEnt.Value, streamSol, amount);
            Dirty(streamSolEnt.Value);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        var ivs = EntityQueryEnumerator<DHIVDripComponent>();
        while (ivs.MoveNext(out var ivId, out var ivComp))
        {
            if (ivComp.AttachedTo is not { } attachedTo)
                continue;

            if (!Exists(attachedTo) || !InRange(ivId, attachedTo, ivComp.Range))
            {
                DetachIV((ivId, ivComp), null, Exists(attachedTo), false);
                continue;
            }

            if (time < ivComp.TransferAt)
                continue;

            ivComp.TransferAt = time + ivComp.TransferDelay;

            if (_itemSlots.GetItemOrNull(ivId, ivComp.Slot) is not { } pack ||
                !TryComp(pack, out DHBloodPackComponent? packComp))
            {
                continue;
            }

            Transfer(pack, packComp, attachedTo, ivComp.Injecting, ivComp.TransferAmount);
            Dirty(ivId, ivComp);
            UpdateIVVisuals((ivId, ivComp));
            UpdatePackVisuals((pack, packComp));
        }

        var packs = EntityQueryEnumerator<DHBloodPackComponent>();
        while (packs.MoveNext(out var packId, out var packComp))
        {
            if (packComp.AttachedTo is not { } attachedTo)
                continue;

            if (!Exists(attachedTo) || !InRange(packId, attachedTo, packComp.Range))
            {
                DetachPack((packId, packComp), null, Exists(attachedTo), false);
                continue;
            }

            if (time < packComp.TransferAt)
                continue;

            packComp.TransferAt = time + packComp.TransferDelay;
            Transfer(packId, packComp, attachedTo, packComp.Injecting, packComp.TransferAmount);
            Dirty(packId, packComp);
            UpdatePackVisuals((packId, packComp));
        }
    }
}
