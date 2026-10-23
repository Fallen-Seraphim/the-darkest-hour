using System.Numerics;
using Content.Client._DarkestHour.Shuttles.Rift;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Shuttles.UI;

// DarkestHour: небесные тела звёздной системы на сканере масс — диски тел, линии орбит, кольца радиации и метки
// с расстоянием до поверхности. Тела огромные, поэтому рисуется только видимая дуга.
public sealed partial class ShuttleNavControl
{
    private static readonly Color DHOrbitColor = Color.FromHex("#8FA6C8").WithAlpha(0.35f);
    private static readonly Color DHRadiationColor = Color.FromHex("#E8D44D").WithAlpha(0.45f);

    private readonly List<Vector2> _dhArc = new();

    private void DHDrawCelestial(DrawingHandleScreen handle, EntityUid? map, Matrix3x2 worldToView, Vector2 ourWorldPos)
    {
        if (map == null ||
            !EntManager.TryGetComponent<DHStarSystemMapComponent>(map, out var comp) ||
            comp.System == null ||
            !IoCManager.Resolve<IPrototypeManager>().TryIndex(comp.System.Value, out var system))
        {
            return;
        }

        var origin = comp.Origin;

        // Орбиты под телами.
        foreach (var body in system.Bodies)
        {
            if (body.OrbitParent == null)
                continue;

            var parent = system.Bodies.Find(b => b.Id == body.OrbitParent);
            if (parent == null)
                continue;

            var center = origin + parent.Position;
            var radius = Vector2.Distance(parent.Position, body.Position);
            DHDrawCircleWorld(handle, worldToView, center, radius, DHOrbitColor, null);
        }

        foreach (var body in system.Bodies)
        {
            var center = origin + body.Position;

            if (body.Radiation > 0f && body.RadiationFalloff > 0f)
                DHDrawCircleWorld(handle, worldToView, center, body.Radius + body.RadiationFalloff, DHRadiationColor, null);

            var fill = body.Color.WithAlpha(body.Kind == DHCelestialKind.Star ? 0.55f : 0.3f);
            DHDrawCircleWorld(handle, worldToView, center, body.Radius, body.Color, fill);

            // Граница, ниже которой не опуститься (верхние слои газового гиганта).
            if (body.EnterableDepth > 0f)
                DHDrawCircleWorld(handle, worldToView, center, DHCelestial.SolidRadius(body), body.Color.WithAlpha(0.5f), null);
        }

        foreach (var body in system.Bodies)
        {
            DHDrawCelestialLabel(handle, worldToView, origin + body.Position, body, ourWorldPos);
        }
    }

    private void DHDrawCircleWorld(DrawingHandleScreen handle, Matrix3x2 worldToView, Vector2 worldCenter, float worldRadius,
        Color outline, Color? fill)
    {
        DHCelestialDraw.Circle(handle, Vector2.Transform(worldCenter, worldToView), worldRadius * MinimapScale,
            (Vector2) PixelSize, outline, fill, _dhArc);
    }

    /// <summary>Подпись у ближайшей к центру сканера точки поверхности; за краем — прижата к краю.</summary>
    private void DHDrawCelestialLabel(DrawingHandleScreen handle, Matrix3x2 worldToView, Vector2 worldCenter,
        DHCelestialBody body, Vector2 ourWorldPos)
    {
        var center = Vector2.Transform(worldCenter, worldToView);
        var size = (Vector2) PixelSize;
        var mid = size / 2f;
        var toMid = mid - center;
        var distance = toMid.Length();
        var radius = body.Radius * MinimapScale;

        var anchor = distance > radius && distance > 0.01f
            ? center + toMid / distance * radius
            : mid;

        var surface = Vector2.Distance(ourWorldPos, worldCenter) - body.Radius;
        var text = surface > 0f
            ? Loc.GetString("dh-celestial-label", ("name", Loc.GetString(body.Name)), ("distance", DHCelestial.FormatDistance(surface)))
            : Loc.GetString("dh-celestial-label-inside", ("name", Loc.GetString(body.Name)));

        var dimensions = handle.GetDimensions(Font, text, 1f);
        var position = anchor - new Vector2(dimensions.X / 2f, dimensions.Y + 2f);
        position = Vector2.Clamp(position, Vector2.Zero, Vector2.Max(Vector2.Zero, size - dimensions));

        handle.DrawString(Font, position, text, body.Color);
    }
}
