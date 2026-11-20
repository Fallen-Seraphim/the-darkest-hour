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

    /// <summary>Реплики над персонажем появляются посимвольно (терминальная строка, решение автора 2026-11-11).</summary>
    public static readonly CVarDef<bool> SpeechTypewriter =
        CVarDef.Create("dh.speech_typewriter", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Строки развёртки поверх окон (общий стиль TDH, выбор автора 2026-11-11).</summary>
    public static readonly CVarDef<bool> UiScanlines =
        CVarDef.Create("dh.ui_scanlines", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Надписи окон — моноширинным шрифтом (общий стиль TDH).</summary>
    public static readonly CVarDef<bool> UiMonoWindows =
        CVarDef.Create("dh.ui_mono_windows", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Голосования за режим и карту запускаются сами при входе в лобби (план осады п. 23, решение автора 2026-11-18),
    /// если есть из чего выбирать (больше одного режима в голосовании / больше одной подходящей карты).
    /// </summary>
    public static readonly CVarDef<bool> LobbyAutoVote =
        CVarDef.Create("dh.lobby_auto_vote", true, CVar.SERVERONLY);

    /// <summary>Через сколько секунд после открытия лобби запускать эти голосования (чтобы игроки успели зайти).</summary>
    public static readonly CVarDef<float> LobbyAutoVoteDelay =
        CVarDef.Create("dh.lobby_auto_vote_delay", 20f, CVar.SERVERONLY);
}
