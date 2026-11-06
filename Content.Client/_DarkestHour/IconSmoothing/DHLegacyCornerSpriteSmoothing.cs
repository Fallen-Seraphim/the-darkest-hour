using Content.Client.IconSmoothing.Smoothings;

namespace Content.Client._DarkestHour.IconSmoothing;

/// <summary>
/// DarkestHour: угловое сглаживание для спрайтов, нарисованных под старый IconSmooth (mode: Corners у RMC-14 и
/// старого SS14). Там у каждого угла «против часовой» = 1, диагональ = 2, «по часовой» = 4; ванильный
/// <see cref="CornerSpriteSmoothing"/> считает биты в обратном порядке (1 и 4 переставлены), из-за чего прямые
/// участки стен RMC рисовались торцами.
/// </summary>
public sealed partial class DHLegacyCornerSpriteSmoothing : CornerSpriteSmoothing
{
    protected override byte GetAppendix(byte directions, byte offset)
    {
        var appendix = base.GetAppendix(directions, offset);
        return (byte) ((appendix & 2) | ((appendix & 1) << 2) | ((appendix & 4) >> 2));
    }
}
