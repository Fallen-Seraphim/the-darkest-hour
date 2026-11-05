using System.Collections.Generic;
using Content.Client._DarkestHour.Levels;
using Robust.Client.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client.Viewport;

// DarkestHour: отрисовка нижних Z-уровней под своим (по образцу CMU / CrystallEdge / TTMC14, своя упрощённая версия).
public sealed partial class ScalingViewport
{
    private readonly DHZPassEye _dhZPassEye = new();
    private readonly List<(MapId Map, int Depth)> _dhZPasses = new();
    private readonly List<Overlay> _dhZHiddenOverlays = new();

    /// <summary>
    /// Оверлеи, которые нельзя рисовать в проходах нижних уровней: призрак размещения (спавн-меню) считает клетки
    /// по сетке под курсором, а она на другой карте, — движок падает с «Grid is on map X, but coords are on map Y».
    /// </summary>
    private void DHHideOverlaysForLowerPasses()
    {
        var overlays = IoCManager.Resolve<IOverlayManager>();
        _dhZHiddenOverlays.Clear();
        foreach (var overlay in overlays.AllOverlays)
        {
            if (overlay.GetType().Namespace?.StartsWith("Robust.Client.Placement") == true)
                _dhZHiddenOverlays.Add(overlay);
        }

        foreach (var overlay in _dhZHiddenOverlays)
        {
            overlays.RemoveOverlay(overlay);
        }
    }

    private void DHRestoreOverlays()
    {
        if (_dhZHiddenOverlays.Count == 0)
            return;

        var overlays = IoCManager.Resolve<IOverlayManager>();
        foreach (var overlay in _dhZHiddenOverlays)
        {
            overlays.AddOverlay(overlay);
        }

        _dhZHiddenOverlays.Clear();
    }

    private void RenderWithZLevels(IClydeViewport viewport)
    {
        if (_eye is not { } eye ||
            !_entityManager.EntitySysManager.TryGetEntitySystem<DHZLevelRenderSystem>(out var zRender) ||
            !zRender.TryGetLowerPasses(eye, _dhZPasses))
        {
            viewport.Render();
            return;
        }

        var originalEye = viewport.Eye;
        var originalClear = viewport.ClearColor;
        try
        {
            DHHideOverlaysForLowerPasses();
            var first = true;
            foreach (var (map, depth) in _dhZPasses)
            {
                _dhZPassEye.Position = new MapCoordinates(eye.Position.Position, map);
                _dhZPassEye.Offset = eye.Offset;
                _dhZPassEye.Rotation = eye.Rotation;
                _dhZPassEye.Zoom = eye.Zoom;
                _dhZPassEye.DrawLight = eye.DrawLight;
                _dhZPassEye.DrawFov = false;
                _dhZPassEye.Depth = depth;

                viewport.Eye = _dhZPassEye;
                viewport.ClearColor = first ? Color.Black : null;
                viewport.Render();
                first = false;
            }

            // Свой уровень — поверх, без очистки: в проёмах остаётся то, что нарисовано снизу.
            DHRestoreOverlays();
            viewport.Eye = originalEye;
            viewport.ClearColor = null;
            viewport.Render();
        }
        finally
        {
            DHRestoreOverlays();
            viewport.Eye = originalEye;
            viewport.ClearColor = originalClear;
        }
    }
}
