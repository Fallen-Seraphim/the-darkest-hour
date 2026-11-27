#!/usr/bin/env python3
"""
DarkestHour: генератор большого «Терминуса» 640×768 по эскизу автора (2026-11-27, `claude/terminus-v3.md`).

Север сверху. Координаты в коде — «по листу»: u — от западного края (0…639), v — от северного (0…767); в карту
пишется x = u − 320, y = 383 − v. Этап 1 — каркас: море, река к дамбе, большое озеро, реки у космодрома (брод и мост),
горы, лес, поля, климат (пустыня — весь восточный берег), шоссе Н-14 и дороги, места под объекты. Пока карта не
готова целиком, генератор пишет только превью (`--preview путь.png`) и счёт сущностей.

Вода и скалы тайлами (решение автора 2026-11-27): глубокая вода дальше 2 клеток от берега — тайл DHFloorSeaDeep без
сущности; нутро горного массива — тайл DHFloorRockMass под прямоугольными блоками DHRockMass; сущности воды и скал —
только у кромок.

Запуск из корня репозитория:
  python3 Tools/_DarkestHour/planet_map/terminus3_build.py --preview out.png
"""
import argparse
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import terminus_build as tb  # noqa: E402  (шум, многоугольники, запись карт)

W, H = 640, 768

SAND = 'FloorAsteroidSand'
DESERT = 'FloorDesert'
GRASS = 'FloorPlanetGrass'
GRASS2 = 'FloorGrass'
DIRT = 'FloorPlanetDirt'
ASPHALT = 'RMCFloorAsphalt'
DEEP_TILE = 'DHFloorSeaDeep'
ROCK_TILE = 'DHFloorRockMass'

# ------------------------------------------------------------------ эскиз (u, v)

# Берег моря: v по u (море севернее).
COAST = [(0, 96), (80, 92), (160, 97), (230, 101), (300, 98), (345, 94), (430, 90), (470, 72), (520, 52),
         (580, 32), (639, 22)]
RIVER_N = [(223, 92), (219, 140), (226, 190), (218, 240), (220, 284)]
DAM = (194, 280, 246, 294)                      # u0, v0, u1, v1: гребень с шоссе
HW_V = (283, 289)                               # шоссе Н-14
LAKE = [(84, 300), (250, 298), (330, 306), (370, 340), (398, 380), (372, 420), (356, 470), (352, 530),
        (366, 584), (300, 596), (220, 592), (140, 580), (88, 562), (78, 500), (80, 420), (82, 350)]
RIVER_SP = [(360, 586), (420, 570), (470, 562), (520, 564), (580, 558), (639, 562)]   # между АЭС и космодромом
FORD_U = (494, 508)
RIVER_S = [(358, 588), (370, 622), (366, 662), (374, 704), (370, 767)]                # завод | космодром
POND = (470, 322, 7)
# Островки на озере (решение автора 2026-11-27): укрытие для лодок и место, куда может упасть парашютист.
ISLANDS = [(176, 420, 11), (292, 492, 8), (214, 548, 7)]
# Ущелья в северо-восточном массиве (решение автора 2026-11-27): обходные пути пехоты к базе, ПКО и ПВО.
GORGES = [
    [(376, 214), (420, 206), (470, 214), (506, 220)],          # от дороги к терминалу — к западной стене базы
    [(470, 272), (482, 220), (500, 168), (520, 120), (534, 86)],  # от шоссе на север — к ПВО
    [(560, 178), (548, 140), (540, 104), (510, 92), (488, 86)],   # от базы к ПВО и ПКО
]
# Скалистая гряда восточнее космодрома (решение автора 2026-11-27: космодром на плато, входы — брод и мост).
SP_RIDGE = [(616, 572), (622, 620), (618, 680), (624, 767)]

ROADS = {
    'Н-14': [(0, 286), (639, 286)],
    'к деревне': [(124, 288), (100, 310), (76, 340), (60, 378)],
    'к терминалу': [(384, 168), (372, 210), (352, 250), (340, 286)],
    'Н-14 (ветка)': [(338, 288), (380, 306), (420, 338), (446, 372), (452, 418)],
    'к базе': [(562, 286), (562, 262)],
    'к городку': [(430, 350), (470, 356), (500, 356)],
    'АЭС — брод — космодром': [(452, 520), (480, 540), (500, 560), (502, 600)],
    'завод — мост — космодром': [(292, 655), (340, 655), (396, 655), (420, 655)],
    'ЗП-1 — завод': [(64, 704), (90, 694), (116, 682)],
}
DIRT_ROADS = {
    'деревня — ЗП-1': [(56, 492), (54, 560), (58, 640), (62, 690)],
    'к ПВО и ПКО': [(430, 168), (452, 130), (480, 100), (520, 84)],
    'к РЛС': [(600, 178), (606, 160)],
}
BRIDGES = [('завод — космодром', 340, 650, 396, 660)]

# Места под объекты (u0, v0, u1, v1) — пока только размечены.
SITES = {
    'танкер': (140, 36, 322, 62),
    'нефтяной терминал': (330, 20, 426, 166),
    'место падения': (80, 170, 140, 232),
    'дамба': DAM,
    'деревня': (6, 378, 96, 490),
    'база': (505, 178, 628, 262),
    'ПВО': (514, 50, 560, 84),
    'ПКО': (446, 64, 490, 98),
    'РЛС': (588, 128, 630, 166),
    'блокпост': (398, 278, 424, 296),
    'городок': (456, 300, 628, 414),
    'АЭС': (372, 424, 506, 530),
    'космодром': (404, 590, 608, 716),
    'завод': (116, 614, 294, 744),
    'ЗП-1': (36, 684, 84, 724),
}
FIELDS = [(8, 104, 150, 262), (0, 500, 84, 767), (84, 680, 120, 767)]
FORESTS = [(140, 104, 212, 278), (232, 104, 304, 274)]

NE_MASSIF = [(370, 176), (428, 132), (440, 96), (470, 72), (520, 52), (580, 32), (639, 22), (639, 272),
             (360, 272), (356, 226)]


def poly_v(pts, u):
    for (a, va), (b, vb) in zip(pts, pts[1:]):
        if a <= u <= b:
            t = 0 if b == a else (u - a) / (b - a)
            return va + (vb - va) * t
    return pts[-1][1]


def inside(u, v):
    return 0 <= u < W and 0 <= v < H


class Terrain:
    def __init__(self):
        self.tiles = {}       # (u, v) → тайл
        self.water = {}       # (u, v) → глубокая?
        self.rock = set()     # клетки скалы
        self.res = set()      # клетки, где не ставить скалы и флору
        self.roads = set()
        self.trees = set()
        self.grass_tall = set()
        self.rng = random.Random(20261127)

    # ---------- вода ----------

    def river(self, pts, half, deep_half, tag=None):
        us = [p[0] for p in pts]
        vs = [p[1] for p in pts]
        for u in range(max(0, min(us) - 12), min(W, max(us) + 13)):
            for v in range(max(0, min(vs) - 12), min(H, max(vs) + 13)):
                d = tb.path_dist(pts, u, v) + (tb.noise(u, v, 10, 11) - 0.5) * 2
                if d <= half:
                    self.water[(u, v)] = self.water.get((u, v), False) or d <= deep_half
                elif d <= half + 3 and (u, v) not in self.water:
                    self.res.add((u, v))

    def geography(self):
        for u in range(W):
            cv = poly_v(COAST, u) + (tb.noise(u, 0, 9, 3) - 0.5) * 4
            for v in range(H):
                if v < cv:
                    self.water[(u, v)] = v < cv - 5
                elif v < cv + 6:
                    self.tiles[(u, v)] = SAND
                    self.res.add((u, v))
        self.river(RIVER_N, 6, 3.5)
        lake = [(u, v) for u in range(60, 420) for v in range(280, 620)
                if tb.in_poly(LAKE, u + (tb.noise(u, v, 13, 9) - 0.5) * 6, v + (tb.noise(u, v, 13, 19) - 0.5) * 6)]
        lake_set = {c for c in lake
                    if not any((c[0] - iu) ** 2 + (c[1] - iv) ** 2 <= (r + (tb.noise(c[0], c[1], 5, 61) - 0.5) * 4) ** 2
                               for iu, iv, r in ISLANDS)}
        lake = [c for c in lake if c in lake_set]
        for c in lake:
            u, v = c
            self.water[c] = all((u + i, v + j) in lake_set for i in (-3, 0, 3) for j in (-3, 0, 3))
        for (u, v) in lake:
            for i in (-2, -1, 1, 2):
                for j in (-2, -1, 1, 2):
                    if (u + i, v + j) not in lake_set:
                        self.res.add((u + i, v + j))
        self.river(RIVER_SP, 5, 3)
        self.river(RIVER_S, 6, 3.5)
        # пруд у городка
        pu, pv, r = POND
        for u in range(pu - r - 1, pu + r + 2):
            for v in range(pv - r - 1, pv + r + 2):
                if (u - pu) ** 2 + (v - pv) ** 2 <= r * r:
                    self.water[(u, v)] = (u - pu) ** 2 + (v - pv) ** 2 <= (r - 3) ** 2
        # брод: мелко, без глубины
        for (u, v), deep in list(self.water.items()):
            if FORD_U[0] <= u <= FORD_U[1] and 540 <= v <= 590:
                self.water[(u, v)] = False

    # ---------- климат и грунт ----------

    @staticmethod
    def desert(u, v):
        """Весь восточный берег (решение автора 2026-11-27): восточнее реки к дамбе выше шоссе — побережье с
        терминалом, горы, база; ниже шоссе — восточнее озера и южной реки (городок, АЭС, космодром)."""
        if v < HW_V[1] + 8:
            return u > 306
        return u > 362

    def ground(self):
        for u in range(W):
            for v in range(H):
                c = (u, v)
                if c in self.tiles or c in self.water:
                    continue
                if self.desert(u, v):
                    n = tb.noise(u, v, 9, 22)
                    self.tiles[c] = DESERT if n > 0.78 or n < 0.14 else SAND
                else:
                    n = tb.noise(u, v, 7, 21)
                    self.tiles[c] = DIRT if n > 0.8 else GRASS2 if n < 0.18 else GRASS
        for c in self.water:
            self.tiles.setdefault(c, DIRT if not self.desert(*c) else SAND)

    # ---------- дороги ----------

    def road(self, pts, half, tile):
        us = [p[0] for p in pts]
        vs = [p[1] for p in pts]
        for u in range(max(0, min(us) - 4), min(W, max(us) + 5)):
            for v in range(max(0, min(vs) - 4), min(H, max(vs) + 5)):
                if tb.path_dist(pts, u, v) <= half:
                    c = (u, v)
                    if c in self.water:
                        continue
                    self.tiles[c] = tile
                    self.roads.add(c)
                    self.res.add(c)

    def roads_all(self):
        # дамба: гребень сухой
        for u in range(DAM[0], DAM[2] + 1):
            for v in range(DAM[1], DAM[3] + 1):
                self.water.pop((u, v), None)
                self.tiles[(u, v)] = 'RMCFloorConcrete' if not (HW_V[0] <= v <= HW_V[1]) else ASPHALT
                self.res.add((u, v))
        for name, (u0, v0, u1, v1) in [(b[0], b[1:]) for b in BRIDGES]:
            for u in range(u0, u1 + 1):
                for v in range(v0, v1 + 1):
                    self.water.pop((u, v), None)
                    self.tiles[(u, v)] = 'RMCFloorConcrete'
                    self.res.add((u, v))
        for pts in ROADS.values():
            self.road(pts, 2.6, ASPHALT)
        for pts in DIRT_ROADS.values():
            self.road(pts, 1.6, 'FloorDirt')

    # ---------- горы, лес, поля ----------

    def mountain(self, u, v):
        n = tb.noise(u, v, 17, 31) - 0.5
        if tb.in_poly(NE_MASSIF, u + n * 12, v + n * 12):
            if any(tb.path_dist(g, u, v) + (tb.noise(u, v, 6, 71) - 0.5) * 3 < 3.5 for g in GORGES):
                return 0.0
            return 1.0
        if tb.path_dist(SP_RIDGE, u, v) + n * 6 < 9 and v > 566:
            return 1.0
        # каменистые холмы пустыни на востоке (на эскизе «^» у АЭС и космодрома)
        if u > 520 + n * 14 and v > 420:
            return 0.55
        return 0.0

    def sites(self):
        for (u0, v0, u1, v1) in SITES.values():
            for u in range(u0, u1 + 1):
                for v in range(v0, v1 + 1):
                    if (u, v) not in self.water:
                        self.res.add((u, v))

    def nature(self):
        for u in range(W):
            for v in range(H):
                c = (u, v)
                if c in self.res or c in self.water:
                    continue
                m = self.mountain(u, v)
                f = tb.fbm(u, v, 3)
                if m >= 0.8:
                    if not (f < 0.3 and m < 1):
                        self.rock.add(c)
                        continue
                elif m >= 0.5 and f > 0.6:
                    self.rock.add(c)
                    continue
                if any(f0[0] <= u <= f0[2] and f0[1] <= v <= f0[3] for f0 in FORESTS):
                    if self.rng.random() < 0.16:
                        self.trees.add(c)
                elif any(f0[0] <= u <= f0[2] and f0[1] <= v <= f0[3] for f0 in FIELDS):
                    if tb.noise(u, v, 6, 41) > 0.55:
                        self.grass_tall.add(c)
                elif not self.desert(u, v) and self.rng.random() < (
                        0.12 if any((u - iu) ** 2 + (v - iv) ** 2 <= r * r for iu, iv, r in ISLANDS) else 0.01):
                    self.trees.add(c)

    # ---------- вода и скалы тайлами ----------

    def split(self):
        """Глубина дальше 2 клеток от не-глубины — тайл; скала, у которой все соседи в радиусе 1 — скала, — нутро."""
        deep = {c for c, d in self.water.items() if d}
        self.deep_tiles = set()
        for (u, v) in deep:
            if all((u + i, v + j) in deep or not inside(u + i, v + j)
                   for i in range(-2, 3) for j in range(-2, 3)):
                self.deep_tiles.add((u, v))
        self.rock_inner = set()
        for (u, v) in self.rock:
            if all((u + i, v + j) in self.rock or not inside(u + i, v + j)
                   for i in (-1, 0, 1) for j in (-1, 0, 1)):
                self.rock_inner.add((u, v))
        self.rock_blocks = rectangles(self.rock_inner)

    def stats(self):
        water_ents = len(self.water) - len(self.deep_tiles)
        rock_ents = len(self.rock) - len(self.rock_inner)
        trees = len(self.trees)
        grass = len(self.grass_tall)
        edge = 2 * (W + H)
        total = water_ents + rock_ents + len(self.rock_blocks) + trees + grass + edge
        print(f'Вода: {len(self.water)} клеток, сущностей {water_ents}, тайлом {len(self.deep_tiles)}')
        print(f'Скалы: {len(self.rock)} клеток, сущностей {rock_ents}, нутро {len(self.rock_inner)} '
              f'в {len(self.rock_blocks)} блоках')
        print(f'Деревья {trees}, высокая трава {grass}, край {edge}; всего рельефа ≈ {total}')


def rectangles(cells):
    """Жадно режет множество клеток на прямоугольники (строки слева направо, вниз пока полная ширина)."""
    left = set(cells)
    rects = []
    for (u, v) in sorted(cells, key=lambda c: (c[1], c[0])):
        if (u, v) not in left:
            continue
        u1 = u
        while (u1 + 1, v) in left and u1 - u < 63:
            u1 += 1
        v1 = v
        while v1 - v < 63 and all((x, v1 + 1) in left for x in range(u, u1 + 1)):
            v1 += 1
        for x in range(u, u1 + 1):
            for y in range(v, v1 + 1):
                left.discard((x, y))
        rects.append((u, v, u1, v1))
    return rects


COLORS = {
    SAND: (214, 190, 140), DESERT: (226, 200, 150), GRASS: (96, 140, 70), GRASS2: (110, 152, 80),
    DIRT: (128, 104, 74), ASPHALT: (70, 70, 74), 'FloorDirt': (150, 120, 84), 'RMCFloorConcrete': (150, 150, 150),
}


def preview(t, path, scale=2):
    from PIL import Image, ImageDraw, ImageFont
    img = Image.new('RGB', (W * scale, H * scale))
    px = img.load()

    def put(c, col):
        u, v = c
        for i in range(scale):
            for j in range(scale):
                px[u * scale + i, v * scale + j] = col

    for c, tile in t.tiles.items():
        put(c, COLORS.get(tile, (200, 0, 200)))
    for c in t.grass_tall:
        put(c, (150, 170, 70))
    for c, deep in t.water.items():
        put(c, (24, 52, 96) if c in t.deep_tiles else (40, 80, 140) if deep else (80, 140, 190))
    for c in t.rock:
        put(c, (70, 60, 52) if c in t.rock_inner else (110, 96, 82))
    for c in t.trees:
        put(c, (30, 80, 30))
    d = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 13)
    except OSError:
        font = ImageFont.load_default()
    for name, (u0, v0, u1, v1) in SITES.items():
        d.rectangle([u0 * scale, v0 * scale, u1 * scale, v1 * scale], outline=(240, 60, 40), width=2)
        d.text((u0 * scale + 3, v0 * scale + 2), name, fill=(255, 255, 255), font=font, stroke_width=2,
               stroke_fill=(0, 0, 0))
    for name, u0, v0, u1, v1 in BRIDGES:
        d.rectangle([u0 * scale, v0 * scale, u1 * scale, v1 * scale], outline=(255, 220, 0), width=2)
    d.text((FORD_U[0] * scale - 10, 572 * scale), 'брод', fill=(255, 255, 0), font=font, stroke_width=2,
           stroke_fill=(0, 0, 0))
    for name, pts in ROADS.items():
        u, v = pts[len(pts) // 2]
        if name.startswith('Н-14'):
            d.text((u * scale - 30, (v - 8) * scale), name, fill=(255, 255, 255), font=font, stroke_width=2,
                   stroke_fill=(0, 0, 0))
    for v in range(0, H, 64):
        d.line([(0, v * scale), (W * scale, v * scale)], fill=(255, 255, 255), width=1)
    for u in range(0, W, 64):
        d.line([(u * scale, 0), (u * scale, H * scale)], fill=(255, 255, 255), width=1)
    img.save(path)
    print('превью:', path, img.size)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--preview', help='PNG превью каркаса')
    args = ap.parse_args()

    t = Terrain()
    t.geography()
    t.roads_all()
    t.sites()
    t.ground()
    t.nature()
    t.split()
    t.stats()
    if args.preview:
        preview(t, args.preview)


if __name__ == '__main__':
    main()
