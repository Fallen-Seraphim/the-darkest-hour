using System.Numerics;
using Content.Shared._DarkestHour.CCVar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client._DarkestHour.UI;

/// <summary>
/// «Старый монитор» поверх контрола (план осады п. 23, решение автора 2026-11-20 — на всём лобби и в меню входа):
/// строки развёртки, зерно, медленно бегущая светлая полоса и затемнение к краям. Мышь пропускает насквозь.
/// Шейдер — <c>DHCrtLobby</c> (Textures/_DarkestHour/Shaders/crt_lobby.swsl). Выключается вместе со строками
/// развёртки окон — настройкой <c>dh.ui_scanlines</c>.
/// </summary>
public sealed partial class DHCrtOverlay : Control
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<ShaderPrototype> Shader = "DHCrtLobby";

    /// <summary>Цвет «люминофора» — подсветка полосы и зерна (янтарный, как у терминалов TDH).</summary>
    public Color Phosphor = Color.FromHex("#E3A440");

    /// <summary>Шаг строк развёртки в пикселях.</summary>
    public float Pitch = 3f;

    /// <summary>Общая сила эффекта 0..1.</summary>
    public float Strength = 1f;

    /// <summary>Секунд на один проход полосы сверху вниз.</summary>
    public float BandPeriod = 7f;

    private readonly ShaderInstance _shader;

    public DHCrtOverlay()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        _shader = _proto.Index(Shader).InstanceUnique();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (!_cfg.GetCVar(DHCVars.UiScanlines))
            return;

        _shader.SetParameter("height", (float) PixelHeight);
        _shader.SetParameter("pitch", Pitch * UIScale);
        _shader.SetParameter("strength", Strength);
        _shader.SetParameter("grain", 1f);
        _shader.SetParameter("bandPeriod", BandPeriod);
        _shader.SetParameter("phosphor", new Vector3(Phosphor.R, Phosphor.G, Phosphor.B));

        handle.UseShader(_shader);
        handle.DrawTextureRect(Texture.White, new UIBox2(Vector2.Zero, PixelSize));
        handle.UseShader(null);
    }
}
