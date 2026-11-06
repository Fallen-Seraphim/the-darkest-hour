using Robust.Shared.Configuration;

namespace Content.Shared._DarkestHour.CCVar;

[CVarDefs]
public sealed class DHCVars
{
    /// <summary>
    /// Сколько нижних Z-уровней клиент рисует сквозь проёмы (не больше 3; 0 — не рисовать, глубже — дымка). Для слабых ПК.
    /// </summary>
    public static readonly CVarDef<int> ZLevelsRenderDepth =
        CVarDef.Create("dh.zlevels_render_depth", 3, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Сколько нижних Z-уровней сервер присылает игроку сквозь проёмы (зонды видимости).</summary>
    public static readonly CVarDef<int> ZLevelsProbeDepth =
        CVarDef.Create("dh.zlevels_probe_depth", 3, CVar.SERVERONLY);
}
