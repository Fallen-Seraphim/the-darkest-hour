using Content.Shared._DarkestHour.Assault;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Announcements;

/// <summary>
/// Компьютер или планшет объявлений стороны (решение автора 2026-11-26): объявление слышит только своя сторона — в чат
/// цветом стороны, со звуком и крупно наверху экрана. Заменяет ванильную консоль связи «Осады», чьи объявления слышали
/// все, включая противника.
/// </summary>
[RegisterComponent]
public sealed partial class DHFactionAnnouncerComponent : Component
{
    [DataField(required: true)]
    public DHAssaultFaction Faction;

    /// <summary>Подпись: { $name } — кто объявляет.</summary>
    [DataField(required: true)]
    public LocId Sender;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    [DataField]
    public int MaxLength = 500;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_DarkestHour/Announcements/command_report.ogg"); // решение автора 2026-11-26

    [ViewVariables]
    public TimeSpan NextAnnounce;
}

[Serializable, NetSerializable]
public enum DHFactionAnnouncerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHFactionAnnouncerState(DHAssaultFaction faction, int secondsLeft, int maxLength) : BoundUserInterfaceState
{
    public readonly DHAssaultFaction Faction = faction;

    /// <summary>Сколько ждать до следующего объявления (0 — можно).</summary>
    public readonly int SecondsLeft = secondsLeft;

    public readonly int MaxLength = maxLength;
}

[Serializable, NetSerializable]
public sealed class DHFactionAnnounceMessage(string text) : BoundUserInterfaceMessage
{
    public readonly string Text = text;
}
