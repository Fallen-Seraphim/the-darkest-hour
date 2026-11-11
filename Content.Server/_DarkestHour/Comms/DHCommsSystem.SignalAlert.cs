using Content.Shared._DarkestHour.Comms;
using Content.Shared.Alert;
using Content.Shared.Inventory;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Comms;

/// <summary>
/// Значок «Нет связи» (решение автора 2026-11-11): у игрока гарнитура с каналом, который идёт через ретрансляторы,
/// а сам он вне зоны работающего ретранслятора этого канала — рация молчит не просто так.
/// </summary>
public sealed partial class DHCommsSystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private InventorySystem _inventory = default!;

    private static readonly ProtoId<AlertPrototype> NoSignalAlert = "DHNoSignal";

    /// <summary>Канал → зоны, где он сейчас ловит (пересчёт раз в секунду).</summary>
    private readonly Dictionary<ProtoId<RadioChannelPrototype>, HashSet<EntityUid>> _channelZones = new();

    private void UpdateSignalAlerts()
    {
        foreach (var zones in _channelZones.Values)
        {
            zones.Clear();
        }

        var relays = EntityQueryEnumerator<DHCommsRelayComponent>();
        while (relays.MoveNext(out var relayUid, out var relay))
        {
            var working = relay.Active && RelayWorking(relayUid) && Zone(relayUid) is not null;
            foreach (var channel in relay.Channels)
            {
                if (!_channelZones.TryGetValue(channel, out var zones))
                {
                    zones = new HashSet<EntityUid>();
                    _channelZones[channel] = zones;
                }

                if (working && Zone(relayUid) is { } zone)
                    zones.Add(zone);
            }
        }

        var players = EntityQueryEnumerator<ActorComponent, AlertsComponent>();
        while (players.MoveNext(out var uid, out _, out _))
        {
            var noSignal = false;
            if (_inventory.TryGetSlotEntity(uid, "ears", out var headset) &&
                TryComp<EncryptionKeyHolderComponent>(headset, out var keys))
            {
                var zone = Zone(uid);
                var governed = false;
                var covered = false;
                foreach (var channel in keys.Channels)
                {
                    if (!_channelZones.TryGetValue(channel, out var zones))
                        continue;

                    governed = true;
                    if (zone is { } z && zones.Contains(z))
                    {
                        covered = true;
                        break;
                    }
                }

                noSignal = governed && !covered;
            }

            if (noSignal)
                _alerts.ShowAlert(uid, NoSignalAlert);
            else
                _alerts.ClearAlert(uid, NoSignalAlert);
        }
    }
}
