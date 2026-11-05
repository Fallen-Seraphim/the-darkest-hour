using System.Collections.Generic;
using Content.Client._DarkestHour.Levels;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client.Viewport;

// DarkestHour: отрисовка нижних Z-уровней под своим (по образцу CMU / CrystallEdge / TTMC14, своя упрощённая версия).
public sealed partial class ScalingViewport
{
    private readonly DHZPassEye _dhZPassEye = new();
    private readonly List<(MapId Map, int Depth)> _dhZPasses = new();

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
            viewport.Eye = originalEye;
            viewport.ClearColor = null;
            viewport.Render();
        }
        finally
        {
            viewport.Eye = originalEye;
            viewport.ClearColor = originalClear;
        }
    }
}
