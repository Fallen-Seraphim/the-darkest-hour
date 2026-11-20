#!/usr/bin/env python3
"""Фон лобби «Терминус» (план осады п. 23, решение автора 2026-11-20): тактическая карта REP-27-7 на янтарном
терминале — как на экране штаба. Сама карта берётся из генератора Терминуса (terminus_build.py), поэтому фон
совпадает с игровой картой: вода, горы, дороги, стены построек, точки захвата с подписями, сетка квадратов.
Строки развёртки и бегущую полосу поверх рисует шейдер старого монитора в лобби, здесь их нет.

Запуск из корня репозитория:  python3 Tools/_DarkestHour/ui/lobby_terminus.py
Выход: Resources/Textures/_DarkestHour/LobbyScreens/terminus_tactical.webp
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', '_DarkestHour', 'planet_map'))
os.chdir(ROOT)
import terminus_build as T  # noqa: E402

OUT = os.path.join(ROOT, 'Resources/Textures/_DarkestHour/LobbyScreens/terminus_tactical.webp')
FONT = os.path.join(ROOT, 'Resources/Fonts/_DarkestHour/IBMPlexMono/IBMPlexMono-Regular.ttf')
FONT_BOLD = os.path.join(ROOT, 'Resources/Fonts/_DarkestHour/IBMPlexMono/IBMPlexMono-Bold.ttf')

W, H = 1920, 1080
BG = (11, 10, 7)
AMBER = (227, 164, 64)
DIM = (156, 122, 69)
GREEN = (127, 174, 70)     # Республика
YELLOW = (242, 194, 48)    # Империя

# Классы клеток → цвет (янтарная монохромная палитра).
GROUND = {
    'FloorAsteroidSand': (22, 18, 12), 'FloorDesert': (24, 20, 13), 'FloorPlanetDirt': (20, 17, 11),
    'FloorDirt': (27, 21, 13), 'FloorPlanetGrass': (19, 19, 11), 'FloorGrass': (19, 20, 11),
}
ROAD = (54, 41, 20)
FLOOR = (40, 31, 17)       # полы построек
ROCK = (46, 34, 18)
WATER_DEEP = (9, 12, 13)
WATER_SHALLOW = (14, 17, 16)
WALL = (190, 136, 54)


def build_world():
    known, tiles = T.known_protos()
    T.KNOWN_TILES.update(tiles)
    w = T.World(known)
    for step in ('geography', 'base', 'dam', 'tanker', 'roads', 'village', 'field', 'construction', 'pko', 'pvo',
                 'rls', 'spaceport', 'mines', 'npp', 'power', 'ground', 'boats', 'place_water', 'nature', 'edge'):
        getattr(w, step)()
    return w


def render_map(w, scale):
    mw, mh = T.X1 - T.X0 + 1, T.Y1 - T.Y0 + 1
    img = Image.new('RGB', (mw, mh), BG)
    px = img.load()
    for (x, y), tile in w.tiles.items():
        if not (T.X0 <= x <= T.X1 and T.Y0 <= y <= T.Y1):
            continue
        if tile in GROUND:
            c = GROUND[tile]
        elif tile in ('RMCFloorAsphalt',):
            c = ROAD
        else:
            c = FLOOR
        px[x - T.X0, T.Y1 - y] = c

    walls = set()
    for proto, fx, fy in ((e[0], e[1], e[2]) for e in w.ents):
        x, y = int(math.floor(fx)), int(math.floor(fy))
        if not (T.X0 <= x <= T.X1 and T.Y0 <= y <= T.Y1):
            continue
        ix, iy = x - T.X0, T.Y1 - y
        if proto == 'DHWaterDeep':
            px[ix, iy] = WATER_DEEP
        elif proto == 'DHWaterShallow':
            px[ix, iy] = WATER_SHALLOW
        elif proto.startswith('WallRock'):
            px[ix, iy] = ROCK
        elif proto.startswith(('Wall', 'DHGridWall', 'DHBunkerWall', 'Window', 'ReinforcedWindow')):
            walls.add((ix, iy))

    big = img.resize((mw * scale, mh * scale), Image.NEAREST)
    d = ImageDraw.Draw(big)
    # штриховка воды: тонкие горизонтальные линии, как на штабном планшете
    wpx = img.load()
    for iy in range(mh):
        for ix in range(mw):
            if wpx[ix, iy] == WATER_DEEP and (iy % 3 == 0):
                d.line([(ix * scale, iy * scale + scale // 2), (ix * scale + scale - 1, iy * scale + scale // 2)],
                       fill=(30, 34, 30))
    # горы — светлая кромка по краю скал
    for iy in range(1, mh - 1):
        for ix in range(1, mw - 1):
            if wpx[ix, iy] != ROCK:
                continue
            if any(wpx[ix + dx, iy + dy] != ROCK for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                d.rectangle([ix * scale, iy * scale, ix * scale + scale - 1, iy * scale + scale - 1], fill=(88, 64, 30))
    for ix, iy in walls:
        d.rectangle([ix * scale, iy * scale, ix * scale + scale - 1, iy * scale + scale - 1], fill=WALL)
    return big


def grid(d, ox, oy, scale, font):
    """Сетка квадратов по 32 клетки с подписями, как на тактической карте."""
    step = 32
    for i, x in enumerate(range(T.X0, T.X1 + 2, step)):
        sx = ox + (x - T.X0) * scale
        d.line([(sx, oy), (sx, oy + (T.Y1 - T.Y0 + 1) * scale)], fill=(48, 38, 20), width=1)
        if x <= T.X1 - step // 2:
            d.text((sx + 4, oy - 22), chr(ord('A') + i), font=font, fill=DIM)
    for j, y in enumerate(range(T.Y1 + 1, T.Y0 - 1, -step)):
        sy = oy + (T.Y1 + 1 - y) * scale
        d.line([(ox, sy), (ox + (T.X1 - T.X0 + 1) * scale, sy)], fill=(48, 38, 20), width=1)
        if y >= T.Y0 + step // 2:
            d.text((ox - 30, sy + 4), f'{j + 1:02d}', font=font, fill=DIM)


def marker(d, ox, oy, scale, box, label, color, font):
    x0, y0, x1, y1 = box
    sx0, sx1 = ox + (x0 - T.X0) * scale, ox + (x1 - T.X0 + 1) * scale
    sy0, sy1 = oy + (T.Y1 - y1) * scale, oy + (T.Y1 - y0 + 1) * scale
    k = 10
    for (cx, cy, dx, dy) in ((sx0, sy0, 1, 1), (sx1, sy0, -1, 1), (sx0, sy1, 1, -1), (sx1, sy1, -1, -1)):
        d.line([(cx, cy), (cx + dx * k, cy)], fill=color, width=2)
        d.line([(cx, cy), (cx, cy + dy * k)], fill=color, width=2)
    tw = d.textlength(label, font=font)
    d.rectangle([sx0, sy0 - 22, sx0 + tw + 10, sy0 - 3], fill=BG)
    d.text((sx0 + 5, sy0 - 21), label, font=font, fill=color)


def main():
    random.seed(2318)
    w = build_world()
    scale = 3
    tmap = render_map(w, scale)
    canvas = Image.new('RGB', (W, H), BG)
    # Карта крупнее экрана по высоте. Сдвинута влево, чтобы база и РЛС не уходили под личное дело справа.
    ox = 48
    oy = (H - tmap.height) // 2 + 40
    canvas.paste(tmap, (ox, oy))

    d = ImageDraw.Draw(canvas)
    font = ImageFont.truetype(FONT, 15)
    font_b = ImageFont.truetype(FONT_BOLD, 16)
    grid(d, ox, oy, scale, font)

    poi = [
        (T.BASE, 'БАЗА «ТЕРМИНУС»', GREEN),
        (T.PKO, 'ПКО', GREEN), (T.PVO, 'ПВО', GREEN), (T.RLS, 'РЛС/СВЯЗЬ', GREEN),
        (T.NPP, 'АЭС', AMBER), (T.SPACEPORT, 'КОСМОДРОМ', AMBER), (T.CONSTRUCTION, 'СТРОЙКА', AMBER),
        (T.DAM, 'ДАМБА', AMBER), (T.VILLAGE, 'ДЕРЕВНЯ', AMBER), (T.MINES, 'ГОРОДОК', AMBER),
        (T.TANKER, 'ТАНКЕР', DIM),
    ]
    for box, label, color in poi:
        marker(d, ox, oy, scale, box, label, color, font_b)

    # Затемнение к краям: интерфейс лобби читается поверх.
    shade = Image.new('L', (W, H), 0)
    sd = ImageDraw.Draw(shade)
    for i in range(240):
        a = int(200 * (1 - i / 240) ** 1.6)
        sd.rectangle([i, i, W - 1 - i, H - 1 - i], outline=a)
    canvas = Image.composite(Image.new('RGB', (W, H), BG), canvas, shade)

    d = ImageDraw.Draw(canvas)
    d.text((32, H - 40), 'REP-27-7 · ВОЕННАЯ БАЗА «ТЕРМИНУС» · ТАКТИЧЕСКАЯ КАРТА', font=font, fill=DIM)
    canvas = canvas.filter(ImageFilter.GaussianBlur(0.4))
    canvas.save(OUT, 'WEBP', quality=88, method=6)
    print('->', OUT, canvas.size)


if __name__ == '__main__':
    main()
