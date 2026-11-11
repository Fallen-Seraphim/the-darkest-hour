using Content.Shared._DarkestHour.Chat;

namespace Content.Server._DarkestHour.Chat;

/// <summary>Выставляет цвет реплик над головой по стороне бойца (<see cref="DHSpeechColorComponent"/>).</summary>
public sealed partial class DHSpeechColorSystem : EntitySystem
{
    public void SetColor(EntityUid uid, Color color)
    {
        var comp = EnsureComp<DHSpeechColorComponent>(uid);
        if (comp.Color == color)
            return;

        comp.Color = color;
        Dirty(uid, comp);
    }
}
