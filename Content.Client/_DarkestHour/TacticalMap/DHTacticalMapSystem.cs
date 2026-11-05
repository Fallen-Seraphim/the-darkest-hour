using Content.Shared._DarkestHour.ControlPoints;
using Content.Shared._DarkestHour.Overwatch;
using Robust.Client.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._DarkestHour.TacticalMap;

/// <summary>Слой местности: один Z-уровень.</summary>
public sealed class DHTacticalLayer
{
    public int Version;
    public int Level;
    public Texture Texture = default!;
    public int Left;
    public int Bottom;
    public int Width;
    public int Height;
}

/// <summary>
/// Местность тактической карты: принимает её с сервера, собирает текстуры и держит по карте и версии (у каждого
/// Z-уровня — своя; на этажах пустота прозрачна).
/// </summary>
public sealed class DHTacticalMapSystem : EntitySystem
{
    public event Action? TerrainUpdated;

    private readonly Dictionary<NetEntity, DHTacticalLayer> _layers = new();

    public static readonly Color[] Palette =
    {
        Color.FromHex("#0b0d0f"), // Empty
        Color.FromHex("#a88c5a"), // Ground
        Color.FromHex("#7d7f80"), // Floor
        Color.FromHex("#3d3f42"), // Road
        Color.FromHex("#d8dadc"), // Wall
        Color.FromHex("#5c4630"), // Rock
        Color.FromHex("#4f86b8"), // Water
        Color.FromHex("#244c78"), // DeepWater
        Color.FromHex("#4a3a24"), // Trench
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<DHTacticalMapTerrainEvent>(OnTerrain);
    }

    public bool Has(NetEntity map, int version)
    {
        return _layers.TryGetValue(map, out var layer) && layer.Version == version;
    }

    public DHTacticalLayer? Get(NetEntity? map)
    {
        return map is { } key && _layers.TryGetValue(key, out var layer) ? layer : null;
    }

    private void OnTerrain(DHTacticalMapTerrainEvent ev)
    {
        if (ev.Width <= 0 || ev.Height <= 0)
            return;

        var image = new Image<Rgba32>(ev.Width, ev.Height);
        var index = 0;
        for (var i = 0; i + 1 < ev.Data.Length; i += 2)
        {
            var run = ev.Data[i];
            var kind = ev.Data[i + 1];
            var color = kind < Palette.Length ? Palette[kind] : Palette[0];
            if (kind == 0 && ev.Level != 0)
                color = Color.Transparent;
            var pixel = new Rgba32(color.RByte, color.GByte, color.BByte, color.AByte);
            for (var k = 0; k < run && index < ev.Width * ev.Height; k++, index++)
            {
                // Строки идут снизу вверх, в картинке — сверху вниз.
                var x = index % ev.Width;
                var y = ev.Height - 1 - index / ev.Width;
                image[x, y] = pixel;
            }
        }

        _layers[ev.Map] = new DHTacticalLayer
        {
            Texture = Robust.Client.Graphics.Texture.LoadFromImage(image, "dh-tactical-map"),
            Version = ev.Version,
            Level = ev.Level,
            Left = ev.Left,
            Bottom = ev.Bottom,
            Width = ev.Width,
            Height = ev.Height,
        };
        TerrainUpdated?.Invoke();
    }
}
