using Robust.Shared.Configuration;

namespace Content.Shared._DarkestHour.CCVar;

/// <summary>Настройки медицины DH (отдельный класс, чтобы не конфликтовать с правками DHCVars в других ветках).</summary>
[CVarDefs]
public sealed class DHMedicalCVars
{
    /// <summary>
    /// Окно реанимации, секунд: сколько тело может быть мёртвым без кровообращения, прежде чем наступит смерть мозга
    /// (решение автора 2026-11-11: «труп можно вернуть только в течение минуты после смерти»). СЛР и жизнеобеспечение
    /// останавливают отсчёт. 0 — без ограничения (как в ванили: только гниение).
    /// </summary>
    public static readonly CVarDef<float> ReviveWindow =
        CVarDef.Create("dh.revive_window", 60f, CVar.SERVERONLY);
}
