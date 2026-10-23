using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._DarkestHour.Shuttles.Rift;

/// <summary>
/// Рисование небесных тел и орбит в пиксельных координатах контрола. Огромные окружности рисуются только видимой
/// дугой, иначе многоугольник заметно отходит от настоящей окружности.
/// </summary>
public static class DHCelestialDraw
{
    private const int ArcSegments = 96;

    public static void Circle(DrawingHandleScreen handle, Vector2 center, float radius, Vector2 viewSize,
        Color outline, Color? fill, List<Vector2> buffer)
    {
        var mid = viewSize / 2f;
        var viewRadius = viewSize.Length() / 2f;
        var toMid = mid - center;
        var distance = toMid.Length();

        // Целиком вне вида.
        if (distance - radius > viewRadius || radius < 0.5f)
            return;

        // Вид целиком внутри окружности: заливаем весь вид.
        if (distance + viewRadius < radius)
        {
            if (fill != null)
                handle.DrawRect(new UIBox2(Vector2.Zero, viewSize), fill.Value);

            return;
        }

        float from, to;
        bool closed;
        if (radius < viewRadius * 2f || distance < 1f)
        {
            from = 0f;
            to = MathF.Tau;
            closed = true;
        }
        else
        {
            // Видимая дуга: угол, под которым окружность пересекает круг обзора, с запасом.
            var cos = (radius * radius + distance * distance - viewRadius * viewRadius) / (2f * radius * distance);
            var half = MathF.Min(MathF.PI, MathF.Acos(Math.Clamp(cos, -1f, 1f)) * 1.15f + 0.002f);
            var dir = MathF.Atan2(toMid.Y, toMid.X);
            from = dir - half;
            to = dir + half;
            closed = half >= MathF.PI;
        }

        buffer.Clear();
        var segments = closed ? Math.Clamp((int) (radius / 3f), 24, ArcSegments * 2) : ArcSegments;
        for (var i = 0; i <= segments; i++)
        {
            var angle = from + (to - from) * i / segments;
            buffer.Add(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
        }

        if (fill != null)
        {
            buffer.Insert(0, center);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, buffer, fill.Value);
            buffer.RemoveAt(0);
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, buffer, outline);
    }
}
