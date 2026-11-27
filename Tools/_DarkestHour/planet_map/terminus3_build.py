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
    'к базе': [(546, 286), (546, 262)],
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
    'место падения': (60, 166, 146, 236),
    'терминал-пирс-танкер': (306, 62, 312, 98),
    'дамба': DAM,
    'деревня': (6, 378, 96, 490),
    'база': (505, 178, 628, 262),
    'ПВО': (514, 50, 560, 84),
    'ПКО': (446, 64, 490, 98),
    'РЛС': (584, 126, 630, 166),
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
        # мелководье у терминала и танкера (эскиз автора: «мелко вода»; напоминание автора 2026-11-27) — к танкеру
        # и пирсам можно подойти вброд
        tu0, tv0, tu1, tv1 = SITES['танкер']
        for (u, v), deep in list(self.water.items()):
            if not deep:
                continue
            wob = (tb.noise(u, v, 8, 81) - 0.5) * 6
            near_tanker = tu0 - 5 < u + wob < tu1 + 5 and tv0 - 5 < v + wob < tv1 + 5
            by_terminal = 318 < u + wob < 432 and 44 < v + wob and v < 110
            if near_tanker or by_terminal:
                self.water[(u, v)] = False
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
                self.tiles[(u, v)] = 'FloorConcrete' if not (HW_V[0] <= v <= HW_V[1]) else ASPHALT
                self.res.add((u, v))
        for name, (u0, v0, u1, v1) in [(b[0], b[1:]) for b in BRIDGES]:
            for u in range(u0, u1 + 1):
                for v in range(v0, v1 + 1):
                    self.water.pop((u, v), None)
                    self.tiles[(u, v)] = 'FloorConcrete'
                    self.res.add((u, v))
        for pts in ROADS.values():
            self.road(pts, 2.6, ASPHALT)
        for pts in DIRT_ROADS.values():
            self.road(pts, 1.6, 'FloorDirt')

    # ---------- горы, лес, поля ----------

    def mountain(self, u, v):
        n = tb.noise(u, v, 17, 31) - 0.5
        # горы только севернее и западнее базы, юг и восток базы открыты (решение автора 2026-11-27)
        bu0, bv0, bu1, bv1 = SITES['база']
        if u > bu0 - 14 + n * 8 and v > bv0 - 12 + n * 8:
            return 0.0
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


def load_shuttle(path):
    """Тайлы и сущности грида-шаттла из карты (координаты клеток грида)."""
    import base64
    import struct
    import yaml

    class L(yaml.CSafeLoader):
        pass
    L.add_multi_constructor('!', lambda loader, suffix, node: None)
    d = yaml.load(open(path, encoding='utf-8'), Loader=L)
    tm = d['tilemap']
    grid = [e for p in d['entities'] for e in p['entities'] if e['uid'] == 1][0]
    tiles = {}
    for c in grid['components']:
        if c['type'] == 'MapGrid':
            for ch in c['chunks'].values():
                cx, cy = map(int, ch['ind'].split(','))
                b = base64.b64decode(ch['tiles'])
                n = len(b) // 256
                for i in range(256):
                    t = struct.unpack_from('<i', b, i * n)[0]
                    if t:
                        tiles[(cx * 16 + i % 16, cy * 16 + i // 16)] = tm[t]
    ents = []
    for p in d['entities']:
        if not p['proto']:
            continue
        for e in p['entities']:
            tr = [c for c in e.get('components', []) if c['type'] == 'Transform']
            if not tr or 'pos' not in tr[0] or tr[0].get('parent') != 1:
                continue
            x, y = map(float, tr[0]['pos'].split(','))
            rot = 0.0
            if 'rot' in tr[0]:
                rot = float(str(tr[0]['rot']).split()[0])
            ents.append((p['proto'], x, y, rot))
    return tiles, ents


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


# ------------------------------------------------------------------ запись в карту

def gx(u):
    return u - W // 2


def gy(v):
    return H // 2 - 1 - v


def setup_bounds():
    """Границы карты для общего кода terminus_build (World.inside, encode_chunks)."""
    tb.X0, tb.X1, tb.Y0, tb.Y1 = -W // 2, W // 2 - 1, -H // 2, H // 2 - 1


class Map3(tb.World):
    """Большой Терминус: рельеф из Terrain, объекты — в координатах карты (x на восток, y на север)."""

    def __init__(self, known):
        super().__init__(known)
        self.rng = random.Random(20261127)
        self.temperate_cells = set()
        # Z-уровни (решения автора 2026-11-27): −2, −1 — подземные, +1, +2 — этажи, площадки, крыши.
        self.levels = {-2: tb.Level(), -1: tb.Level(), 1: self.p1, 2: tb.Level()}

    # ---------- уровни ----------

    def lv_tile(self, z, x, y, name):
        self.levels[z].tiles[(x, y)] = name

    def lv_rect(self, z, x0, y0, x1, y1, name):
        for x in range(min(x0, x1), max(x0, x1) + 1):
            for y in range(min(y0, y1), max(y0, y1) + 1):
                self.levels[z].tiles[(x, y)] = name

    def lv_ent(self, z, proto, x, y, rot=0.0, name=None, comps=None):
        if proto not in self.known:
            self.missing.add(proto)
            return
        self.levels[z].ents.append((proto, x + 0.5, y + 0.5, rot, name, comps))

    def lv_building(self, z, x0, y0, x1, y1, wall, floor, doors=(), windows=(), window='ReinforcedWindow',
                    door='Airlock', lights='AlwaysPoweredWallLight'):
        self.lv_rect(z, x0, y0, x1, y1, floor)
        doors = {d[:2]: (d[2] if len(d) > 2 else door) for d in doors}
        windows = set(windows)
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                if x not in (x0, x1) and y not in (y0, y1):
                    continue
                if (x, y) in doors:
                    self.lv_ent(z, doors[(x, y)], x, y)
                elif (x, y) in windows:
                    self.lv_ent(z, window, x, y)
                else:
                    self.lv_ent(z, wall, x, y)
        if lights and x1 - x0 >= 2:
            for x in range(x0 + 2, x1, 7):
                self.lv_ent(z, lights, x, y1 - 1, tb.LIGHT_ROT['N'])

    def ladder(self, x, y, z0, z1):
        """Универсальная лестница с уровня z0 на z1 (на каждом уровне — в той же клетке)."""
        for z in range(min(z0, z1), max(z0, z1) + 1):
            if z == 0:
                self.ent('DHZLadder', x, y, free=True)
            else:
                self.lv_ent(z, 'DHZLadder', x, y)

    def railing_edges(self, cells, z=0):
        """Перила по внешнему краю площадки (пирс, гребень, палуба): сторона → поворот."""
        rot = {(0, -1): 0.0, (0, 1): tb.PI, (-1, 0): tb.R270, (1, 0): tb.R90}
        cells = set(cells)
        for (x, y) in cells:
            for d, r in rot.items():
                if (x + d[0], y + d[1]) not in cells:
                    if z == 0:
                        self.ent('Railing', x, y, r, free=True)
                    else:
                        self.lv_ent(z, 'Railing', x, y, r)

    def load_terrain(self, t):
        for (u, v), tile in t.tiles.items():
            self.tiles[(gx(u), gy(v))] = tile
        for c in t.res:
            self.res.add((gx(c[0]), gy(c[1])))
        for (u, v), deep in t.water.items():
            if (u, v) in t.deep_tiles:
                self.tiles[(gx(u), gy(v))] = DEEP_TILE
                continue
            self.water[(gx(u), gy(v))] = deep
        self.rock_cells = {(gx(u), gy(v)) for (u, v) in t.rock}
        for (u, v) in t.rock:
            c = (gx(u), gy(v))
            if (u, v) in t.rock_inner:
                self.tiles[c] = ROCK_TILE
            else:
                self.ent(tb.ROCK, *c)
        for (u0, v0, u1, v1) in t.rock_blocks:
            # прямоугольник u0..u1 × v0..v1 → в карте x0..x1 × y1..y0 (v растёт к югу)
            x0, x1, y0, y1 = gx(u0), gx(u1) + 1, gy(v1), gy(v0) + 1
            cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
            hw, hh = (x1 - x0) / 2, (y1 - y0) / 2
            comps = ['    - type: Fixtures', '      fixtures:', '        fix1:', '          shape:',
                     '            !type:PhysShapeAabb', f'            bounds: "{-hw},{-hh},{hw},{hh}"',
                     '          mask:', '          - FullTileMask', '          layer:', '          - WallLayer',
                     '          density: 1000',
                     '    - type: Occluder', '      polygon:', f'      - "{-hw},{-hh}"', f'      - "{hw},{-hh}"',
                     f'      - "{hw},{hh}"', f'      - "{-hw},{hh}"']
            self.ents.append(('DHRockMass', cx, cy, 0.0, None, comps))
        for (u, v) in t.trees:
            self.ent(self.rng.choice(FOREST if not Terrain.desert(u, v) else DESERT_FLORA), gx(u), gy(v))
        for (u, v) in t.grass_tall:
            self.ent('RMCGrassTallJungle', gx(u), gy(v))
        for u in range(W):
            for v in range(H):
                if not Terrain.desert(u, v):
                    self.temperate_cells.add((gx(u), gy(v)))

    def temperate(self, x, y):
        return (x, y) in self.temperate_cells

    def weather_free_areas(self):
        """Умеренная зона (без песчаных бурь): строки с одинаковой восточной границей сливаются."""
        rects = []
        cur = None
        for v in range(H - 1, -1, -1):
            e = max(u for u in range(W) if not Terrain.desert(u, v))
            y = gy(v)
            if cur and cur[2] == gx(e) and cur[3] == y - 1:
                cur[3] = y
            else:
                if cur:
                    rects.append(cur)
                cur = [tb.X0, y, gx(e), y]
        rects.append(cur)
        return rects

    # ================================================================== север

    def tanker(self):
        """Танкер (решения автора 2026-11-27): −1 трюм (танки, насосная, машинное), 0 палуба с надстройкой на корме,
        +1 мостик; ЗП-2 на палубе у носа (кабина «Мидуэя» встаёт на уровне 1). Вокруг — мелководье."""
        u0, v0, u1, v1 = SITES['танкер']
        x0, x1, y0, y1 = gx(u0), gx(u1), gy(v1), gy(v0)
        cy = (y0 + y1) // 2
        half = (y1 - y0) / 2
        hull = set()
        for x in range(x0, x1 + 1):
            nose = x - (x1 - 12)
            for y in range(y0, y1 + 1):
                if nose > 0 and abs(y - cy) > half - nose * 0.9:
                    continue
                hull.add((x, y))
        edge = {c for c in hull if any((c[0] + i, c[1] + j) not in hull for i in (-1, 0, 1) for j in (-1, 0, 1))}
        wall = 'WallReinforcedRust'
        gang_x = gx(SITES['терминал-пирс-танкер'][0]) + 3   # трап с пирса в южный борт
        for c in hull:
            self.water.pop(c, None)
            self.tile(*c, 'FloorSteelDirty')
            self.lv_tile(-1, *c, 'Plating')
        for c in edge:
            if c[1] == y0 and gang_x - 1 <= c[0] <= gang_x + 1:
                self.ent('AirlockGlass', *c)
                self.lv_ent(-1, wall, *c)
                continue
            self.ent(wall, *c)
            self.lv_ent(-1, wall, *c)
        # --- трюм (−1): поперечные переборки, танки, насосная, машинное
        holds = list(range(x0 + 22, x1 - 14, 26))
        for bx in holds:
            for y in range(y0 + 1, y1):
                if abs(y - cy) <= 1:
                    self.lv_ent(-1, 'AirlockEngineering' if y == cy else wall, bx, y)
                else:
                    self.lv_ent(-1, wall, bx, y)
        for i, bx in enumerate(holds[:-1]):
            for tx in range(bx + 4, holds[i + 1] - 2, 5):
                for ty in (cy - 5, cy + 5):
                    self.lv_ent(-1, 'RMCFiltrationMachineSoloTankEmpty', tx, ty)
            for tx in range(bx + 2, holds[i + 1] - 1, 3):
                self.lv_ent(-1, 'RMCBarrelBlack' if tx % 2 else 'RMCBarrelYellow', tx, cy + 8)
        for x in range(x0 + 2, holds[0] - 1, 4):
            self.lv_ent(-1, 'MachineFrame', x, cy + 3)
            self.lv_ent(-1, 'MachineFrame', x, cy - 3)
        self.lv_ent(-1, 'ComputerBroken', x0 + 3, cy)
        for x in range(x0 + 3, x1 - 12, 9):
            self.lv_ent(-1, 'AlwaysPoweredWallLight', x, y1 - 1, tb.LIGHT_ROT['N'])
            self.lv_ent(-1, 'AlwaysPoweredWallLight', x, y0 + 1, tb.LIGHT_ROT['S'])
        # --- палуба (0): надстройка на корме, люки танков, трубопровод
        sx0, sx1 = x0 + 1, x0 + 18
        self.building(sx0, y0 + 1, sx1, y1 - 1, wall, 'FloorDark',
                      doors=[(sx1, cy, 'AirlockGlass'), (sx1, cy + 5, 'AirlockGlass'), (sx1, cy - 5, 'AirlockGlass')],
                      windows=[(sx0 + 4, y1 - 1), (sx0 + 9, y1 - 1), (sx0 + 4, y0 + 1), (sx0 + 9, y0 + 1)],
                      window='ReinforcedWindow', lights='AlwaysPoweredWallLight')
        self.wall_line(sx0 + 1, cy - 2, sx1 - 1, cy - 2, wall, gaps=[(sx0 + 8, cy - 2)])
        self.ent('AirlockGlass', sx0 + 8, cy - 2)
        for y in range(y0 + 3, cy - 3, 2):
            self.ent('RMCBedBunk' if 'RMCBedBunk' in self.known else 'Bed', sx0 + 1, y)
            self.ent('RMCBedBunk' if 'RMCBedBunk' in self.known else 'Bed', sx0 + 5, y)
        self.ent('TableReinforced', sx0 + 10, cy + 3)
        self.ent('TableReinforced', sx0 + 11, cy + 3)
        self.ent('LockerFreezer', sx0 + 1, y1 - 3)
        self.ent('ClosetEmergencyFilledRandom', sx0 + 15, y0 + 3)
        self.ladder(sx0 + 3, cy + 2, -1, 1)        # с мостика — в машинное
        for bx in holds[:-1]:
            hx = bx + 13
            self.ladder(hx, cy, -1, 0)
            self.decal('WarnBox', hx, cy, '#FFFFFFFF')
        for x in range(sx1 + 3, x1 - 30, 6):
            self.ent('RMCFiltrationMachineSoloTankEmpty', x, cy + 4)
            self.ent('RMCFiltrationMachineSoloTankEmpty', x, cy - 4)
        for x in range(sx1 + 1, x1 - 10):
            if x % 3 == 0:
                self.decal('RMCDecalOilFloor' + str(1 + x % 7), x, cy + 1, '#FFFFFFC0')
        self.lz(x1 - 24, cy, 'ЗП-2 Танкер', clear=7)
        # --- мостик (+1) над надстройкой
        self.lv_building(1, sx0, y0 + 1, sx1, y1 - 1, wall, 'FloorDark',
                         windows=[(sx1, y) for y in range(y0 + 3, y1 - 2, 2)] + [(x, y1 - 1) for x in range(sx0 + 3, sx1, 3)])
        for i, proto in enumerate(['ComputerBroken', 'ComputerRadar', 'ComputerBroken', 'ComputerRadar']):
            self.lv_ent(1, proto, sx1 - 2, cy - 3 + i * 2)
            self.lv_ent(1, 'ChairOfficeDark', sx1 - 3, cy - 3 + i * 2)
        self.lv_ent(1, 'TableReinforced', sx0 + 4, cy)

    def terminal(self):
        """Нефтяной терминал (точка захвата вместе с танкером, решение автора 2026-11-27): два пирса, три резервуара с
        эстакадой на уровне +1, диспетчерская в 2 этажа (флагшток у входа), насосная, склады, забор."""
        u0, v0, u1, v1 = SITES['нефтяной терминал']
        x0, x1 = gx(u0), gx(u1)
        shore = gy(97)            # кромка суши у терминала
        top = gy(v1)              # южный край площадки
        # пирс к танкеру и длинный пирс
        pu0, pv0, pu1, pv1 = SITES['терминал-пирс-танкер']
        piers = [(gx(pu0), gx(pu1), gy(SITES['танкер'][3]) - 1, shore + 2), (x0 + 42, x0 + 47, gy(28), shore + 2)]
        for (px0, px1, py_top, py_bot) in piers:
            cells = []
            for x in range(px0, px1 + 1):
                for y in range(py_bot, py_top + 1):
                    self.water.pop((x, y), None)
                    self.tile(x, y, 'FloorWood')
                    cells.append((x, y))
            for y in range(py_bot + 3, py_top, 8):
                self.ent('AlwaysPoweredWallLight', px1, y, tb.LIGHT_ROT['E'], free=True)
            for y in range(py_bot, py_top, 3):
                self.decal('RMCDecalOilFloor' + str(1 + y % 7), (px0 + px1) // 2, y, '#FFFFFF90')
        lx0, lx1 = x0 + 36, x0 + 54            # Т-образная голова длинного пирса
        for x in range(lx0, lx1 + 1):
            for y in range(gy(28) - 4, gy(28) + 1):
                self.water.pop((x, y), None)
                self.tile(x, y, 'FloorWood')
        for x in range(lx0 + 2, lx1, 5):
            self.ent('RMCBarrelYellow', x, gy(28))
        # площадка
        self.rect(x0, top, x1, shore + 1, 'FloorConcrete')
        self.fence_rect(x0 - 1, top - 1, x1 + 1, shore - 1,
                        gates=[(gx(384) + i, top - 1) for i in range(-2, 3)], skip=[(x, shore - 1) for x in range(x0, x1 + 1)])
        # три резервуара с запада, на +1 — их крыши и эстакада вдоль
        cxs = x0 + 12
        for i, cyy in enumerate((shore - 14, shore - 32, shore - 50)):
            r = 7
            for x in range(cxs - r, cxs + r + 1):
                for y in range(cyy - r, cyy + r + 1):
                    d = math.hypot(x - cxs, y - cyy)
                    if d <= r + 0.3:
                        self.tile(x, y, 'FloorSteelDirty')
                        self.lv_tile(1, x, y, 'FloorSteelDirty')
                        self.roof.add((x, y))
                        if d >= r - 0.7:
                            self.ent('WallReinforcedRust', x, y)
                        else:
                            self.res.add((x, y))
                            self.occ.add((x, y))
            self.decal('RMCDecalOilBase', cxs, cyy, '#FFFFFFC0')
        ew = cxs + 9
        for y in range(shore - 58, shore - 5):
            self.lv_tile(1, ew, y, 'FloorSteel')
            self.lv_tile(1, ew - 1, y, 'FloorSteel')
        for cyy in (shore - 14, shore - 32, shore - 50):
            for x in range(cxs + 6, ew):
                self.lv_tile(1, x, cyy, 'FloorSteel')
        self.ladder(ew, shore - 6, 0, 1)
        self.ladder(ew, shore - 57, 0, 1)
        for y in range(shore - 58, shore - 5):
            self.lv_ent(1, 'Railing', ew, y, tb.R90)
        # диспетчерская (2 этажа): флагшток у входа
        dx0, dy0, dx1, dy1 = x0 + 30, shore - 24, x0 + 52, shore - 8
        self.building(dx0, dy0, dx1, dy1, 'WallSolid', 'FloorSteel',
                      doors=[((dx0 + dx1) // 2, dy0, 'AirlockGlass'), (dx1, dy1 - 4, 'AirlockGlass')],
                      windows=[(x, dy1) for x in range(dx0 + 2, dx1 - 1, 3)] + [(dx0, y) for y in range(dy0 + 3, dy1 - 1, 3)],
                      window='Window')
        self.wall_line(dx0 + 1, dy0 + 8, dx1 - 1, dy0 + 8, 'WallSolid', gaps=[(dx0 + 6, dy0 + 8)])
        self.ent('Airlock', dx0 + 6, dy0 + 8)
        for i, p in enumerate(['ComputerPowerMonitoring', 'ComputerAlert', 'ComputerBroken']):
            self.grid(p, dx0 + 4 + i * 3, dy1 - 1)
            self.ent('ChairOfficeLight', dx0 + 4 + i * 3, dy1 - 2)
        self.ent('ClosetToolFilled', dx1 - 1, dy0 + 1)
        self.ladder(dx1 - 2, dy0 + 10, 0, 1)
        self.lv_building(1, dx0, dy0, dx1, dy1, 'WallSolid', 'FloorSteel',
                         windows=[(x, dy0) for x in range(dx0 + 2, dx1 - 1, 3)] + [(x, dy1) for x in range(dx0 + 2, dx1 - 1, 3)],
                         window='Window')
        self.lv_ent(1, 'TableReinforced', dx0 + 4, dy0 + 4)
        self.lv_ent(1, 'TableReinforced', dx0 + 5, dy0 + 4)
        self.ent('DHControlPointTerminal', (dx0 + dx1) // 2, dy0 - 3, free=True)
        # насосная и склады
        self.building(x0 + 58, shore - 20, x0 + 70, shore - 8, 'WallSolid', 'FloorTechMaint2',
                      doors=[(x0 + 58, shore - 14, 'AirlockEngineering')])
        for y in range(shore - 18, shore - 9, 3):
            self.ent('MachineFrame', x0 + 66, y)
        for wx0, wy0, wx1, wy1 in ((x0 + 74, top + 4, x0 + 84, shore - 30), (x0 + 86, top + 4, x0 + 94, shore - 40)):
            self.building(wx0, wy0, wx1, wy1, 'WallSolid', 'FloorSteelDirty',
                          doors=[((wx0 + wx1) // 2, wy0, 'AirlockCargoGlass'), ((wx0 + wx1) // 2, wy1, 'AirlockCargoGlass')])
            self.scatter(['CrateGenericSteel', 'RMCBarrelBlack', 'RMCBarrelYellow', 'RMCBarrelRed'],
                         wx0 + 1, wy0 + 1, wx1 - 1, wy1 - 1, (wx1 - wx0) * (wy1 - wy0) // 18)
        self.scatter(['RMCBarrelRed', 'RMCBarrelYellow', 'RMCBarrelBlack', 'RMCBarrelBrown'],
                     x0 + 24, top + 2, x0 + 70, shore - 28, 24)
        for x in range(x0 + 2, x1, 14):
            self.post(x, shore - 2)
            self.post(x, top + 1)

    def dam(self):
        """Дамба (точка захвата): гребень с шоссе Н-14, турбинный зал со стороны реки (север), пульт, водосброс."""
        u0, v0, u1, v1 = DAM
        x0, x1, y0, y1 = gx(u0), gx(u1), gy(v1), gy(v0)
        cells = [(x, y) for x in range(x0, x1 + 1) for y in range(y0, y1 + 1)]
        self.railing_edges(cells)
        for x in range(x0 + 6, x1 - 4, 10):
            self.ent('RMCPropTurbine', x, y1 + 3, foot=[(x, y1 + 2), (x, y1 + 3), (x, y1 + 4)])
        hx0, hx1 = x0 + 4, x1 - 4
        self.building(hx0, y1 + 1, hx1, y1 + 8, 'WallReinforced', 'FloorSteel',
                      doors=[((hx0 + hx1) // 2, y1 + 1, 'AirlockEngineeringGlass')],
                      windows=[(x, y1 + 8) for x in range(hx0 + 3, hx1 - 2, 6)], window='ReinforcedWindow')
        for x in range(hx0 + 3, hx1 - 2, 8):
            self.ent('MachineFrame', x, y1 + 5)
        self.grid('ComputerPowerMonitoring', hx0 + 2, y1 + 7)
        self.grid('ComputerAlert', hx0 + 4, y1 + 7)
        self.ent('ChairOfficeDark', hx0 + 3, y1 + 6)
        self.ent('DHControlPointDam', (x0 + x1) // 2, y0 + 1, free=True)
        for x in range(x0 + 3, x1, 10):
            self.post(x, y0)
        self.reserve(x0 - 3, y0 - 3, x1 + 3, y1 + 10)

    def crash_site(self):
        """Место падения (решения автора 2026-11-27): разбитый республиканский транспорт — шаттл «Deadend» разломан на
        две части, борозда с выжженной травой позади, несколько ящиков снабжения. Не точка захвата."""
        tiles, ents = load_shuttle(os.path.join(tb.ROOT, 'Resources/Maps/_DarkestHour/Shuttles/republic_deadend.yml'))
        xs = [c[0] for c in tiles]
        ys = [c[1] for c in tiles]
        mx, my = (min(xs) + max(xs)) // 2, (min(ys) + max(ys)) // 2
        u0, v0, u1, v1 = SITES['место падения']
        ox, oy = gx((u0 + u1) // 2) - mx, gy((v0 + v1) // 2) - my
        cut = mx                                            # разлом поперёк корпуса
        shift = (5, -3)                                     # задняя часть отлетела
        rng = random.Random(77)

        def place(x, y):
            return (x + ox + (shift[0] if x < cut else 0), y + oy + (shift[1] if x < cut else 0))

        for (x, y), t in tiles.items():
            if x == cut or x == cut - 1:
                continue                                    # линия разлома
            if t in ('Lattice', 'Space'):
                continue
            c = place(x, y)
            self.tile(*c, t if rng.random() > 0.15 else 'Plating')
            self.roof.add(c)
        skip = ('Cable', 'GasPipe', 'GasVent', 'GasPort', 'GasPassive', 'APC', 'SMES', 'Substation', 'Generator',
                'Vending', 'Poweredlight', 'DHSpawner', 'DHMagazine', 'Weapon', 'DHFlamer', 'AtmosDevice',
                'Canister', 'Gravity', 'RCD', 'Stimpack', 'Handheld', 'Food', 'Drink', 'Box', 'Wrench', 'Spawn')
        for proto, x, y, rot in ents:
            tx, ty = math.floor(x), math.floor(y)
            if tx in (cut, cut - 1) or any(k in proto for k in skip):
                continue
            if proto.startswith('Computer'):
                proto = 'ComputerBroken'
            if ('Wall' in proto or 'Window' in proto or 'Grille' in proto) and rng.random() < 0.22:
                continue                                    # пробоины
            c = place(tx, ty)
            self.ent(proto, c[0], c[1], rot, free=True)
        # ящики снабжения в обломках и рядом
        for i, crate in enumerate(['DHSupplyCrateMedical', 'DHSupplyCrateBarbedWire', 'CrateFoodMRE',
                                   'DHCrateSecurityAmmo556x45']):
            self.ent(crate, gx(u0) + 6 + i * 3, gy(v1) + 3, free=True)
        # борозда с востока (шёл на запад) и гарь
        fx0 = gx((u0 + u1) // 2) + (max(xs) - mx) + 2
        for x in range(fx0, fx0 + 46):
            w = 3 if x < fx0 + 30 else 2
            for y in range(gy((v0 + v1) // 2) - w, gy((v0 + v1) // 2) + w + 1):
                self.tile(x, y, 'FloorPlanetDirt')
                self.res.add((x, y))
                if rng.random() < 0.3:
                    self.decal('burnt' + str(rng.randint(1, 4)), x, y, '#FFFFFFFF')
        for _ in range(60):
            x = gx(u0) + rng.randint(-6, u1 - u0 + 6)
            y = gy(v1) + rng.randint(-6, v1 - v0 + 6)
            self.decal('burnt' + str(rng.randint(1, 4)), x, y, '#FFFFFFC0')
        for _ in range(5):
            self.ent('RMCBarrelFire', gx(u0) + rng.randint(0, u1 - u0), gy(v1) + rng.randint(0, v1 - v0))
        self.reserve(gx(u0) - 4, gy(v1) - 4, gx(u1) + 4, gy(v0) + 4)

    # ================================================================== восток

    def site_xy(self, name):
        u0, v0, u1, v1 = SITES[name]
        return gx(u0), gy(v1), gx(u1), gy(v0)

    def base3(self):
        """Военная база (решения автора 2026-11-27): тот же состав, что был (комендатура и штаб, арсенал, гараж с двумя
        лифтами техники, карго, энергоблок, павильон спуска; под ней бункер −1 со штабом, казармами и медблоком),
        новая планировка: блок прежней базы в южной половине, на севере — казармы и полевой госпиталь, вокруг всего —
        бетонная стена с КПП на юге (с Н-14) и востоке и вышками по углам; −2 — хранилище образца."""
        X0, Y0, X1, Y1 = self.site_xy('база')
        # прежний блок базы (80×43) — сдвиг старых констант генератора v2
        dx, dy = X0 + 14 - tb.BASE[0], Y0 + 6 - tb.BASE[1]
        for name in ('BASE', 'CARGO', 'HANGARS', 'POWER', 'KOMEND', 'ARSENAL', 'BLOCKHOUSE', 'BUNKER'):
            r = getattr(tb, name)
            setattr(tb, name, (r[0] + dx, r[1] + dy, r[2] + dx, r[3] + dy))
        tb.BASE_ROAD_X = (tb.BASE_ROAD_X[0] + dx, tb.BASE_ROAD_X[1] + dx)
        tb.BASE_ROAD_Y = (tb.BASE_ROAD_Y[0] + dy, tb.BASE_ROAD_Y[1] + dy)
        tb.BUNKER_CORRIDOR = (tb.BUNKER_CORRIDOR[0] + dy, tb.BUNKER_CORRIDOR[1] + dy)
        tb.World.base(self)
        # бункер −1 — уровень −1; образец — на −2 под хранилищем
        b = self.bunker
        sample = [e for e in b.ents if e[0] == 'DHExperimentalSample']
        b.ents = [e for e in b.ents if e[0] != 'DHExperimentalSample']
        self.levels[-1].tiles.update(b.tiles)
        self.levels[-1].ents.extend(b.ents)
        if sample:
            sx, sy = math.floor(sample[0][1]), math.floor(sample[0][2])
            vx0, vy0, vx1, vy1 = sx - 7, sy - 10, sx + 7, sy + 2
            self.lv_building(-2, vx0, vy0, vx1, vy1, 'WallReinforced', 'FloorDark', lights='Poweredlight')
            self.lv_ent(-2, 'DHExperimentalSample', sx, sy - 4)
            self.lv_building(-2, vx0 + 3, vy0 - 8, vx1 - 3, vy0, 'WallReinforced', 'FloorSteel',
                             doors=[(sx, vy0, 'DHAirlockRepublicCommandLocked')], lights='Poweredlight')
            self.levels[-2].ents = [e for e in self.levels[-2].ents if not (e[0] == 'WallReinforced' and
                                                                           math.floor(e[1]) == sx and math.floor(e[2]) == vy0)]
            self.ladder(sx - 3, vy0 - 4, -2, -1)
            for x, y in ((vx0 + 2, sy - 2), (vx1 - 2, sy - 2)):
                self.lv_ent(-2, 'DHSandbagBarricade', x, y)
        # север площадки: казармы и полевой госпиталь
        ny0 = tb.BASE[3] + 4
        self.rect(X0 + 2, Y0 + 2, X1 - 2, Y1 - 2, 'FloorConcrete', reserve=False)
        for i, bx in enumerate(range(X0 + 6, X0 + 70, 22)):
            self.building(bx, ny0, bx + 18, ny0 + 12, 'WallSolid', 'FloorSteel',
                          doors=[(bx + 9, ny0, 'DHAirlockRepublic')],
                          windows=[(bx + 4, ny0), (bx + 14, ny0), (bx + 4, ny0 + 12), (bx + 14, ny0 + 12)])
            for x in range(bx + 2, bx + 17, 3):
                self.ent('RMCBedBunk' if 'RMCBedBunk' in self.known else 'Bed', x, ny0 + 10)
                self.ent('RMCBedBunk' if 'RMCBedBunk' in self.known else 'Bed', x, ny0 + 7)
        hx0 = X0 + 74
        self.building(hx0, ny0, hx0 + 26, ny0 + 16, 'WallSolid', 'FloorWhite',
                      doors=[(hx0 + 13, ny0, 'AirlockGlass')], windows=[(hx0 + 5, ny0), (hx0 + 21, ny0)])
        for x in range(hx0 + 3, hx0 + 24, 4):
            self.ent('RMCMedicalBed' if 'RMCMedicalBed' in self.known else 'MedicalBed', x, ny0 + 13)
        self.ent('CrateMedicalSupplies', hx0 + 2, ny0 + 2)
        # стена и КПП
        gate_s = [(x, Y0) for x in range(tb.BASE_ROAD_X[0], tb.BASE_ROAD_X[1] + 1)]
        gate_e = [(X1, y) for y in range(tb.BASE_ROAD_Y[0], tb.BASE_ROAD_Y[1] + 1)]
        for x in range(X0, X1 + 1):
            for y in (Y0, Y1):
                if (x, y) not in gate_s:
                    self.ent('WallReinforced', x, y, free=True)
        for y in range(Y0 + 1, Y1):
            for x in (X0, X1):
                if (x, y) not in gate_e:
                    self.ent('WallReinforced', x, y, free=True)
        self.rect(tb.BASE_ROAD_X[0], Y0 - 6, tb.BASE_ROAD_X[1], Y0 + 6, ASPHALT)
        self.rect(tb.BASE[2], tb.BASE_ROAD_Y[0], X1 + 4, tb.BASE_ROAD_Y[1], ASPHALT)
        for x in list(range(tb.BASE_ROAD_X[0] - 6, tb.BASE_ROAD_X[0] - 1)) + list(range(tb.BASE_ROAD_X[1] + 2, tb.BASE_ROAD_X[1] + 7)):
            self.ent('DHSandbagBarricade', x, Y0 - 1, free=True)
        for x, y in ((X0 + 1, Y0 + 1), (X1 - 3, Y0 + 1), (X0 + 1, Y1 - 3), (X1 - 3, Y1 - 3)):
            self.tower(x, y)
        self.reserve(X0 - 3, Y0 - 8, X1 + 6, Y1 + 3)

    def military(self):
        """ПКО, ПВО, РЛС — прежние объекты в горных карманах (решение автора: горы — скалы, у объектов площадки),
        блокпост на Н-14 между дамбой и базой."""
        def put(name, attr, size):
            x0, y0, x1, y1 = self.site_xy(name)
            setattr(tb, attr, (x0, y0, x0 + size[0], y0 + size[1]))
        put('ПКО', 'PKO', (44, 28))
        put('ПВО', 'PVO', (40, 24))
        put('РЛС', 'RLS', (40, 34))
        tb.NORTH_ROAD_X = (tb.RLS[2] + 1, tb.RLS[2] + 4)
        # ПКО, ПВО и РЛС — на горе, на уровне +1 (решение автора 2026-11-27); под ними — скала, подъём — лестницы
        # у подножия, где выходят дороги и ущелья.
        for attr, fn, site in (('PKO', tb.World.pko, 'ПКО'), ('PVO', tb.World.pvo, 'ПВО'), ('RLS', tb.World.rls, 'РЛС')):
            self.raised(fn, getattr(tb, attr), self.site_xy(site))
            self.plateau(getattr(tb, attr))
        x0, y0, x1, y1 = self.site_xy('блокпост')
        hy = gy(HW_V[1])
        for x in range(x0, x1 + 1):
            if x % 5:
                self.ent('DHSandbagBarricade', x, hy + 4, free=True)
                self.ent('DHSandbagBarricade', x, gy(HW_V[0]) - 4, free=True)
        self.building(x0, hy + 6, x0 + 8, hy + 12, 'WallSolid', 'FloorSteel', doors=[(x0 + 4, hy + 6, 'Airlock')],
                      windows=[(x0 + 2, hy + 6), (x0 + 6, hy + 6)])
        self.tower(x1 - 4, hy + 6)
        self.ent('DHFieldPhoneRepublicGate', x0 + 1, hy + 7)
        self.car(x0 + 12, hy + 2, wreck=True)

    def is_rock(self, x, y):
        return self.tiles.get((x, y)) == ROCK_TILE or (x, y) in self.rock_cells

    def plateau(self, box, width=5):
        """Горная площадка на +1 вокруг военного объекта (решение автора 2026-11-27): кольцо над скалами шириной
        width, по внешнему краю мешки, две лестницы с площадки объекта наверх."""
        x0, y0, x1, y1 = box
        ring = set()
        for x in range(x0 - width, x1 + width + 1):
            for y in range(y0 - width, y1 + width + 1):
                if x0 <= x <= x1 and y0 <= y <= y1:
                    continue
                if self.is_rock(x, y):
                    ring.add((x, y))
        if not ring:
            return
        for c in ring:
            self.lv_tile(1, *c, 'FloorDesert')
        rot = {(0, -1): 0.0, (0, 1): tb.PI, (-1, 0): tb.R270, (1, 0): tb.R90}
        for (x, y) in ring:
            outer = [d for d in rot if (x + d[0], y + d[1]) not in ring and not (x0 <= x + d[0] <= x1 and y0 <= y + d[1] <= y1)]
            if outer and (x + y) % 2 == 0:
                self.lv_ent(1, 'DHSandbagBarricade', x, y, rot[outer[0]])
        placed = 2      # объект сам на +1 — отдельные лестницы на кольцо не нужны
        for (lx, ly) in ((x0 + 1, y1 - 1), (x1 - 1, y1 - 1), (x0 + 1, y0 + 1), (x1 - 1, y0 + 1)):
            if placed >= 2 or (lx, ly) in self.occ:
                continue
            near = [(lx + i, ly + j) for i in (-2, -1, 0, 1, 2) for j in (-2, -1, 0, 1, 2) if (lx + i, ly + j) in ring]
            if not near:
                continue
            # мостик от лестницы до кольца
            self.lv_tile(1, lx, ly, 'FloorSteel')
            nx, ny = near[0]
            for x in range(min(lx, nx), max(lx, nx) + 1):
                self.lv_tile(1, x, ly, 'FloorSteel')
            for y in range(min(ly, ny), max(ly, ny) + 1):
                self.lv_tile(1, nx, y, 'FloorSteel')
            self.ladder(lx, ly, 0, 1)
            placed += 1

    def raised(self, fn, box, outer):
        """Собрать объект старым кодом v2 во временном мире и поставить его на +1 (его вышки — на +2); под площадкой на
        уровне 0 — скальный массив; две лестницы с земли у края площадки."""
        w = tb.World(self.known)
        w.missing = self.missing
        w.rng = random.Random(sum(map(ord, fn.__name__)))
        fn(w)
        up, top = self.levels[1], self.levels[2]
        up.tiles.update(w.tiles)
        up.roof.update(w.roof)
        up.ents.extend((e[0], e[1], e[2], e[3], e[4] if len(e) > 4 else None, e[5] if len(e) > 5 else None)
                       for e in w.ents)
        top.tiles.update(w.p1.tiles)
        top.roof.update(w.p1.roof)
        top.ents.extend(w.p1.ents)
        bx0, by0, bx1, by1 = box
        x0, y0, x1, y1 = (min(outer[0], bx0), min(outer[1], by0), max(outer[2], bx1), max(outer[3], by1))
        cells = [(x, y) for x in range(x0, x1 + 1) for y in range(y0, y1 + 1)]
        # под объектом и всей его площадкой — скала, а на +1 вся площадка — грунт вершины
        for c in cells:
            up.tiles.setdefault(c, 'FloorDesert')
        for c in cells:
            self.tiles[c] = ROCK_TILE
            self.rock_cells.add(c)
            self.res.add(c)
            self.occ.add(c)
        hw, hh = (x1 - x0 + 1) / 2, (y1 - y0 + 1) / 2
        self.ents.append(('DHRockMass', x0 + hw, y0 + hh, 0.0, None,
                          ['    - type: Fixtures', '      fixtures:', '        fix1:', '          shape:',
                           '            !type:PhysShapeAabb', f'            bounds: "{-hw},{-hh},{hw},{hh}"',
                           '          mask:', '          - FullTileMask', '          layer:', '          - WallLayer',
                           '          density: 1000',
                           '    - type: Occluder', '      polygon:', f'      - "{-hw},{-hh}"', f'      - "{hw},{-hh}"',
                           f'      - "{hw},{hh}"', f'      - "{-hw},{hh}"']))
        # подъём: свободные клетки у края площадки (дорога, ущелье), ближе к южной середине
        cx = (x0 + x1) / 2
        around = [(x, y0 - 1) for x in range(x0, x1 + 1)] + [(x, y1 + 1) for x in range(x0, x1 + 1)] + \
                 [(x0 - 1, y) for y in range(y0, y1 + 1)] + [(x1 + 1, y) for y in range(y0, y1 + 1)]
        free = [c for c in around if not self.is_rock(*c) and c not in self.water and c not in self.occ and
                self.inside(*c)]
        free.sort(key=lambda c: (c[0] - cx) ** 2 + (c[1] - y0) ** 2)
        chosen = []
        for c in free:
            if all(abs(c[0] - o[0]) + abs(c[1] - o[1]) >= 10 for o in chosen):
                chosen.append(c)
            if len(chosen) == 2:
                break
        if not chosen:
            print('нет подъёма к', fn.__name__)
        for (lx, ly) in chosen:
            self.lv_tile(1, lx, ly, 'FloorSteel')
            ix = min(max(lx, bx0), bx1)
            iy = min(max(ly, by0), by1)
            # дорожка по площадке от лестницы до объекта
            for x in range(min(lx, ix), max(lx, ix) + 1):
                up.tiles[(x, ly if y0 <= ly <= y1 else iy)] = up.tiles.get((x, ly), 'FloorDirt')
            for y in range(min(ly, iy), max(ly, iy) + 1):
                up.tiles[(ix if x0 <= ix <= x1 else lx, y)] = up.tiles.get((ix, y), 'FloorDirt')
            # проход в ограде/стене объекта на +1 напротив лестницы
            up.ents[:] = [e for e in up.ents if not ((math.floor(e[1]), math.floor(e[2])) == (ix, iy) and
                                                      ('Fence' in e[0] or 'Wall' in e[0] or 'Barbed' in e[0] or
                                                       'Sandbag' in e[0]))]
            up.tiles.setdefault((ix, iy), 'FloorSteel')
            self.ladder(lx, ly, 0, 1)
            self.ent('DHSandbagBarricade', lx + (1 if lx < cx else -1), ly, free=True)

    def village_houses(self):
        """Деревня крупнее (решение автора: чердаки и вторые этажи): к прежней деревне — двухэтажные дома вокруг."""
        X0, Y0, X1, Y1 = self.site_xy('деревня')
        spots = [(X0 + 4, Y0 + 4), (X0 + 20, Y0 + 4), (X0 + 36, Y0 + 4), (X0 + 52, Y0 + 4), (X0 + 70, Y0 + 4),
                 (X0 + 4, Y1 - 14), (X0 + 22, Y1 - 14), (X0 + 40, Y1 - 14)]
        for i, (hx, hy) in enumerate(spots):
            w, h = 11, 9
            if hx + w > X1 or hy + h > Y1 or any((x, y) in self.water for x in range(hx - 2, hx + w + 3)
                                                for y in range(hy - 2, hy + h + 3)) or any((x, y) in self.occ for x in range(hx - 1, hx + w + 2)
                                                for y in range(hy - 1, hy + h + 2)):
                continue
            door = (hx + 5, hy, 'Airlock') if i % 2 else (hx + 5, hy + h, 'Airlock')
            self.building(hx, hy, hx + w, hy + h, 'WallSolid', 'FloorWood', doors=[door],
                          windows=[(hx + 2, hy), (hx + 9, hy), (hx + 2, hy + h), (hx + 9, hy + h), (hx, hy + 4),
                                   (hx + w, hy + 4)], lights=None)
            self.ent('Bed', hx + 1, hy + 1)
            self.ent('TableWood' if 'TableWood' in self.known else 'Table', hx + 8, hy + 6)
            self.lv_building(1, hx, hy, hx + w, hy + h, 'WallSolid', 'FloorWood',
                             windows=[(hx + 3, hy), (hx + 8, hy), (hx + 3, hy + h), (hx + 8, hy + h)], window='Window',
                             lights=None)
            self.levels[1].roof.update((x, y) for x in range(hx, hx + w + 1) for y in range(hy, hy + h + 1))
            self.ladder(hx + w - 2, hy + h - 2, 0, 1)
            self.lv_ent(1, 'Bed', hx + 2, hy + h - 2)
            self.lv_ent(1, 'Bed', hx + 5, hy + h - 2)

    def gorodok(self):
        """Заброшенный шахтёрский городок (решение автора 2026-11-27, как был): бараки (часть двухэтажные, часть
        разрушена), копёр со спуском в штольню на −1, бочки с огнём, брошенная техника, пруд."""
        X0, Y0, X1, Y1 = self.site_xy('городок')
        self.reserve(X0, Y0, X1, Y1)
        street_y = (Y0 + Y1) // 2
        self.road_h(X0, X1, street_y - 1, street_y + 2, tile='FloorDirt', posts=0, line=False)
        blocks = []
        for row, by in enumerate((street_y + 6, street_y - 16)):
            for j, bx in enumerate(range(X0 + 26, X1 - 16, 20)):
                blocks.append((bx, by, (row + j) % 2 == 0))
        for i, (bx, by, two) in enumerate(blocks):
            self.barrack(bx, by, two, i)
        # копёр и штольня на −1
        kx, ky = X1 - 14, Y0 + 8
        self.building(kx - 5, ky - 5, kx + 5, ky + 5, 'WallSolidRust' if 'WallSolidRust' in self.known else 'WallSolid',
                      'FloorTechMaint2', doors=[(kx, ky + 5, 'Airlock')], lights=None)
        self.ent('Girder', kx - 2, ky)
        self.ent('Girder', kx + 2, ky)
        self.ladder(kx, ky, -1, 0)
        tun = [(kx + i, ky) for i in range(-60, 1)] + [(kx - 60, ky + j) for j in range(0, 30)]
        for (x, y) in tun:
            for dx2 in (-1, 0, 1):
                for dy2 in (-1, 0, 1):
                    self.lv_tile(-1, x + dx2, y + dy2, 'FloorDirt')
        for i, (x, y) in enumerate(tun):
            if i % 9 == 0:
                self.lv_ent(-1, 'RMCBarrelFire', x, y + 1 if (x, y + 1) in self.levels[-1].tiles else y)
        self.lv_ent(-1, 'RMCPropVehicleCrawlerMining', kx - 30, ky)
        self.ladder(kx - 60, ky + 29, -1, 0)                 # второй выход — на западе городка
        for x, y in ((X0 + 20, street_y), (X0 + 60, street_y + 3), (X0 + 100, street_y - 3), (X1 - 20, street_y)):
            self.ent('RMCBarrelFire', x, y)
        self.car(X0 + 40, street_y, wreck=True)
        self.car(X0 + 80, street_y + 1, wreck=True)

    def npp3(self):
        """АЭС (решения автора 2026-11-27): два реакторных купола (энергоблоки внутри, подвалы на −1, кольцевые балконы
        на +1), вентиляционная труба до +2, машзал и пульт подковой, подстанция. Точка захвата и источник сети."""
        X0, Y0, X1, Y1 = self.site_xy('АЭС')
        self.reserve(X0 - 2, Y0 - 2, X1 + 2, Y1 + 2)
        self.rect(X0, Y0, X1, Y1, 'FloorConcrete')
        self.fence_rect(X0 - 1, Y0 - 1, X1 + 1, Y1 + 1, gates=[(gx(452) + i, Y1 + 1) for i in range(-2, 3)] +
                        [(gx(470) + i, Y0 - 1) for i in range(-2, 3)])
        cy = (Y0 + Y1) // 2 - 4
        for k, cx in enumerate((X0 + 34, X0 + 72)):
            r = 15
            for x in range(cx - r - 1, cx + r + 2):
                for y in range(cy - r - 1, cy + r + 2):
                    d = math.hypot(x - cx, y - cy)
                    if d > r + 0.4:
                        continue
                    self.tile(x, y, 'FloorSteel')
                    self.roof.add((x, y))
                    self.lv_tile(-1, x, y, 'Plating')
                    if d >= r - 0.6:
                        if (x, y) == (cx, cy - r):
                            self.ent('AirlockEngineering', x, y)
                        else:
                            self.ent('WallReinforced', x, y)
                        self.lv_ent(-1, 'WallReinforced', x, y)
                    elif d > r - 3.2:
                        self.lv_tile(1, x, y, 'FloorSteel')       # кольцевой балкон
                        self.levels[1].roof.add((x, y))
            self.ent('DHFusionReactor', cx, cy, foot=[(cx - 1 + i, cy - 1 + j) for i in range(3) for j in range(3)],
                     comps=['    - type: Damageable', '      damageContainer: StructuralInorganic',
                            '    - type: DHPoiObject', '      breakDamage: 600', '      point: npp',
                            '      contamination: DHNppContamination', '      repairStack: Plasteel'])
            self.ent('SignRadiation' if 'SignRadiation' in self.known else 'SignDanger', cx, cy - 4)
            self.ladder(cx + r - 2, cy, -1, 1)
            for a in range(0, 360, 45):
                self.lv_ent(-1, 'MachineFrame', cx + int(round(9 * math.cos(math.radians(a)))),
                            cy + int(round(9 * math.sin(math.radians(a)))))
        # труба
        tx, ty = X0 + 53, cy + 18
        for x in range(tx - 2, tx + 3):
            for y in range(ty - 2, ty + 3):
                self.ent('WallReinforced', x, y)
                self.lv_tile(1, x, y, 'Plating')
                self.lv_ent(1, 'WallReinforced', x, y)
                self.lv_tile(2, x, y, 'Plating')
                self.lv_ent(2, 'WallReinforced', x, y)
        # машзал и пульт подковой по южной стороне
        self.building(X0 + 8, Y0 + 2, X1 - 8, Y0 + 12, 'WallReinforced', 'FloorTechMaint2',
                      doors=[(x, Y0 + 12, 'AirlockEngineering') for x in (X0 + 20, X1 - 20)],
                      windows=[(x, Y0 + 12) for x in range(X0 + 26, X1 - 26, 6)], window='ReinforcedWindow')
        for x in range(X0 + 12, X1 - 10, 7):
            self.ent('DHGasTurbine', x, Y0 + 6, foot=[(x + i, Y0 + 4 + j) for i in range(2) for j in range(5)])
        px0 = X1 - 26
        self.building(px0, Y1 - 14, X1 - 4, Y1 - 2, 'WallReinforced', 'FloorDark',
                      doors=[(px0, Y1 - 8, 'AirlockEngineeringGlass')], windows=[(px0 + 6, Y1 - 2), (px0 + 14, Y1 - 2)],
                      window='ReinforcedWindow')
        for i, p in enumerate(['ComputerPowerMonitoring', 'ComputerAlert', 'ComputerPowerMonitoring']):
            self.grid(p, px0 + 4 + i * 5, Y1 - 3)
            self.ent('ChairOfficeDark', px0 + 4 + i * 5, Y1 - 4)
        self.ent('DHControlPointPowerPlant', px0 - 4, Y1 - 6, free=True)
        for x in range(X0 + 2, X1, 12):
            self.post(x, Y1 - 1)

    # ================================================================== юг и запад

    def transplant(self, fn, dx, dy):
        """Собрать объект старым кодом генератора v2 во временном мире и перенести со сдвигом (dx, dy)."""
        w = tb.World(self.known)
        w.missing = self.missing
        w.rng = random.Random(sum(map(ord, fn.__name__)))
        fn(w)
        for (x, y), t in w.tiles.items():
            if not self.inside(x + dx, y + dy):
                continue
            self.tiles[(x + dx, y + dy)] = t
            self.water.pop((x + dx, y + dy), None)
        for c in w.res | w.occ:
            self.res.add((c[0] + dx, c[1] + dy))
        for c in w.occ:
            self.occ.add((c[0] + dx, c[1] + dy))
        for c in w.roof:
            self.roof.add((c[0] + dx, c[1] + dy))
        for e in w.ents:
            self.ents.append((e[0], e[1] + dx, e[2] + dy) + tuple(e[3:]))
        for did, x, y, col, ang in w.decals:
            self.decals.append((did, x + dx, y + dy, col, ang))
        for (x, y), t in w.p1.tiles.items():
            self.levels[1].tiles[(x + dx, y + dy)] = t
        for (x, y) in w.p1.roof:
            self.levels[1].roof.add((x + dx, y + dy))
        for e in w.p1.ents:
            self.levels[1].ents.append((e[0], e[1] + dx, e[2] + dy) + tuple(e[3:]))
        return w

    def village3(self):
        """Деревня (точка захвата, 0/1): прежняя деревня v2 — дома, площадь с водонапорной башней, фельдшерский пункт."""
        X0, Y0, X1, Y1 = self.site_xy('деревня')
        self.transplant(tb.World.village, X0 + 10 - tb.VILLAGE[0], Y0 + 20 - tb.VILLAGE[1])

    def spaceport3(self):
        """Космодром (точка захвата, −1…2): прежний космодром v2 с ЗП-3 и терминалом, подковой вокруг — ангары, на −1 —
        подземный топливный склад с тоннелем, диспетчерская башня до +2."""
        X0, Y0, X1, Y1 = self.site_xy('космодром')
        old = tb.SPACEPORT
        dx, dy = X0 + 40 - old[0], Y0 + 20 - old[1]
        tb.ZP3 = tb.ZP3
        self.transplant(tb.World.spaceport, dx, dy)
        zx, zy = tb.ZP3[0] + dx, tb.ZP3[1] + dy
        # подкова ангаров вокруг ЗП-3
        for k in range(9):
            a = math.radians(200 + k * 20)
            hx, hy = zx + int(round(62 * math.cos(a))), zy + int(round(48 * math.sin(a)))
            if not (X0 + 2 < hx - 6 and hx + 6 < X1 - 2 and Y0 + 2 < hy - 5 and hy + 5 < Y1 - 2):
                continue
            if any((x, y) in self.occ for x in range(hx - 6, hx + 7) for y in range(hy - 5, hy + 6)):
                continue
            self.building(hx - 6, hy - 5, hx + 6, hy + 5, 'WallSolid', 'FloorSteelDirty',
                          doors=[(hx, hy + (5 if hy < zy else -5), 'AirlockCargoGlass')])
            self.scatter(['CrateGenericSteel', 'RMCBarrelBlack', 'RMCBarrelYellow'], hx - 5, hy - 4, hx + 5, hy + 4, 5)
        # подземный топливный склад (−1) с двумя спусками
        fx0, fy0 = X1 - 40, Y0 + 6
        self.lv_building(-1, fx0, fy0, fx0 + 30, fy0 + 16, 'WallReinforced', 'Plating', lights='Poweredlight')
        for x in range(fx0 + 3, fx0 + 28, 4):
            self.lv_ent(-1, 'RMCFiltrationMachineSoloTankWaste', x, fy0 + 12)
            self.lv_ent(-1, 'RMCBarrelYellow', x, fy0 + 4)
        self.ladder(fx0 + 2, fy0 + 2, -1, 0)
        self.ladder(fx0 + 28, fy0 + 14, -1, 0)
        # диспетчерская башня 7×7 до +2
        tx0, ty0 = X1 - 22, Y1 - 18
        self.building(tx0, ty0, tx0 + 7, ty0 + 7, 'WallReinforced', 'FloorDark', doors=[(tx0 + 3, ty0, 'AirlockGlass')])
        self.ladder(tx0 + 2, ty0 + 5, 0, 2)
        for z in (1, 2):
            self.lv_building(z, tx0, ty0, tx0 + 7, ty0 + 7, 'WallReinforced', 'FloorDark',
                             windows=[(tx0 + i, ty0 + 7) for i in range(1, 7)] + [(tx0 + 7, ty0 + i) for i in range(1, 7)])
        for i, p in enumerate(['ComputerRadar', 'ComputerBroken', 'ComputerRadar']):
            self.lv_ent(2, p, tx0 + 6, ty0 + 2 + i * 2)
        self.reserve(X0, Y0, X1, Y1)

    def factory(self):
        """Завод (точка захвата, металлургический, решения автора 2026-11-27): три плавильные печи, цеха, краны, ж/д тупик,
        проходная; −1 и −2 — подземные цеха и коллекторы, +1 — мостки над цехами."""
        X0, Y0, X1, Y1 = self.site_xy('завод')
        self.rect(X0, Y0, X1, Y1, 'FloorConcrete')
        self.fence_rect(X0 - 1, Y0 - 1, X1 + 1, Y1 + 1, gates=[(X1 + 1, gy(655) + i) for i in range(-2, 3)] +
                        [(X0 - 1, gy(690) + i) for i in range(-2, 3)])
        wall = 'WallSolidRust' if 'WallSolidRust' in self.known else 'WallSolid'
        # плавильный цех с тремя печами (север, запад)
        sx0, sy0, sx1, sy1 = X0 + 6, Y1 - 50, X0 + 40, Y1 - 6
        self.building(sx0, sy0, sx1, sy1, wall, 'FloorSteelDirty',
                      doors=[(sx1, sy0 + 8, 'AirlockCargoGlass'), (sx1, sy1 - 8, 'AirlockCargoGlass')])
        for k in range(3):
            fy = sy0 + 8 + k * 14
            for x in range(sx0 + 4, sx0 + 13):
                for y in range(fy - 4, fy + 5):
                    if math.hypot(x - (sx0 + 8), y - fy) >= 4.2 and math.hypot(x - (sx0 + 8), y - fy) < 5:
                        self.ent('WallReinforced', x, y)
            self.ent('RMCBarrelFire', sx0 + 8, fy)
            self.decal('burnt' + str(1 + k), sx0 + 8, fy - 1)
        # прокатный цех и склад (север, восток)
        rx0 = X0 + 60
        self.building(rx0, Y1 - 30, X1 - 30, Y1 - 6, wall, 'FloorSteelDirty',
                      doors=[(rx0, Y1 - 18, 'AirlockCargoGlass'), ((rx0 + X1 - 30) // 2, Y1 - 30, 'AirlockCargoGlass')])
        for x in range(rx0 + 4, X1 - 34, 6):
            self.ent('MachineFrame', x, Y1 - 18)
        self.building(X1 - 22, Y1 - 30, X1 - 6, Y1 - 6, wall, 'FloorSteelDirty', doors=[(X1 - 14, Y1 - 30, 'AirlockCargoGlass')])
        self.scatter(['CrateGenericSteel', 'RMCBarrelBrown', 'RMCBarrelBlack'], X1 - 21, Y1 - 29, X1 - 7, Y1 - 7, 14)
        # мостки и краны над цехами (+1)
        for y in (sy0 + 15, sy0 + 29):
            for x in range(sx0, X1 - 6):
                self.lv_tile(1, x, y, 'FloorSteel')
                self.lv_tile(1, x, y + 1, 'FloorSteel')
        self.ladder(sx1 + 2, sy0 + 15, 0, 1)
        self.ladder(X1 - 8, sy0 + 29, 0, 1)
        # южная часть: корпуса (на эскизе — прямоугольники), проходная, ж/д тупик
        for i, bx in enumerate((X0 + 8, X0 + 50, X0 + 80, X0 + 110)):
            w2 = 30 if i == 0 else 16
            self.building(bx, Y0 + 6, bx + w2, Y0 + 50, wall, 'FloorSteel',
                          doors=[(bx + w2 // 2, Y0 + 50, 'Airlock'), (bx + w2 // 2, Y0 + 6, 'Airlock')])
            self.scatter(['MachineFrame', 'CrateGenericSteel', 'Girder'], bx + 1, Y0 + 7, bx + w2 - 1, Y0 + 49, 8)
        for x in range(X0, X1 + 1):
            self.decal('RMCDecalRoadTiremark' + str(1 + x % 8), x, Y0 + 55, '#5A5048FF')
        self.building(X1 - 14, gy(655) - 6, X1 - 2, gy(655) + 6, 'WallSolid', 'FloorSteel',
                      doors=[(X1 - 2, gy(655), 'AirlockGlass'), (X1 - 14, gy(655), 'AirlockGlass')])
        self.ent('DHControlPointFactory', (X0 + X1) // 2, Y0 + 58, free=True)
        # подземные цеха и коллекторы
        cx, cy = (X0 + X1) // 2, Y0 + 30
        self.lv_building(-1, cx - 30, cy - 14, cx + 30, cy + 14, 'WallReinforced', 'Plating', lights='Poweredlight')
        for x in range(cx - 26, cx + 27, 8):
            self.lv_ent(-1, 'MachineFrame', x, cy)
        self.lv_building(-2, cx - 50, cy - 3, cx + 50, cy + 3, 'WallReinforced', 'FloorDirt', lights='Poweredlight')
        self.ladder(cx - 28, cy - 12, -1, 0)
        self.ladder(cx + 28, cy + 1, -2, -1)
        self.ladder(cx - 48, cy, -2, -1)
        self.lv_building(-1, cx - 52, cy - 3, cx - 44, cy + 3, 'WallReinforced', 'Plating', lights=None)
        self.ladder(cx - 50, cy - 1, -1, 0)
        for x in range(X0 + 4, X1, 14):
            self.post(x, Y0 + 2)
        self.reserve(X0 - 2, Y0 - 2, X1 + 2, Y1 + 2)

    def zp1(self):
        x0, y0, x1, y1 = self.site_xy('ЗП-1')
        self.lz((x0 + x1) // 2, (y0 + y1) // 2, 'ЗП-1 Поля', clear=10)


    # ================================================================== привязка систем

    def wiring(self):
        """Въезды грузовиков Республики (концы Н-14: запад закрыт, пока Империя держит дамбу или деревню), метка
        телепорта призраков у павильона спуска, тяжёлые лодки на озере и реке."""
        hy = gy((HW_V[0] + HW_V[1]) // 2)
        self.ent('DHReinforcementEntryRepublicWest', tb.X0 + 4, hy, tb.R90, foot=[], free=True, off=(0.5, 1.0))
        self.ent('DHReinforcementEntryRepublicEast', tb.X1 - 4, hy, tb.R270, foot=[], free=True)
        self.ent('DHWarpPointTerminus', (tb.BLOCKHOUSE[0] + tb.BLOCKHOUSE[2]) // 2, tb.BLOCKHOUSE[1] - 2, free=True)

        def boat(tu, tv, radius=16):
            tx, ty = gx(tu), gy(tv)
            best = None
            for x in range(tx - radius, tx + radius + 1):
                for y in range(ty - radius, ty + radius + 1):
                    cells = [(x + i, y + j) for i in (-1, 0, 1) for j in (-2, -1, 0, 1, 2)]
                    if all(c in self.water and c not in self.occ for c in cells):
                        d = (x - tx) ** 2 + (y - ty) ** 2
                        if best is None or d < best[0]:
                            best = (d, x, y)
            if best:
                self.ent('DHBoatHeavy', best[1], best[2], 0.0, foot=[], free=True)
            else:
                print('нет воды под лодку у', tu, tv)
        boat(221, 200)          # река севернее дамбы
        boat(250, 306)          # озеро у дамбы
        boat(330, 590)          # озеро у завода


FOREST = ['FloraTreeConifer', 'FloraTreeConifer', 'FloraTree', 'FloraTreeLarge', 'FloraTreeStumpConifer',
          'RMCBushJungle1', 'RMCBushJungle2']
DESERT_FLORA = ['RMCGrassDesert', 'RMCGrassDesertHeavy', 'RMCBoulderLarge', 'RMCCacti1', 'RMCCactus', 'RMCTreeJoshua']


COLORS = {
    SAND: (214, 190, 140), DESERT: (226, 200, 150), GRASS: (96, 140, 70), GRASS2: (110, 152, 80),
    DIRT: (128, 104, 74), ASPHALT: (70, 70, 74), DEEP_TILE: (24, 52, 96), ROCK_TILE: (70, 60, 52), 'FloorDirt': (150, 120, 84), 'FloorConcrete': (150, 150, 150),
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


LEVEL_FILES = {-2: ('terminus3_m2.yml', 'Terminus −2'), -1: ('terminus3_m1.yml', 'Terminus −1'),
               1: ('terminus3_p1.yml', 'Terminus +1'), 2: ('terminus3_p2.yml', 'Terminus +2')}


def write_level(lv, path, name, underground):
    """Грид Z-уровня: свои тайлы и сущности (с компонентами). Под землёй крыша везде, наверху — по lv.roof."""
    tb.TILE_IDS.clear()
    tb.tile_id('Space')
    ch = tb.encode_chunks(lv.tiles)
    L = ['meta:', '  format: 7', '  category: Grid', '  engineVersion: 293.0.1', '  forkId: ""', '  forkVersion: ""',
         '  time: 11/27/2026 12:00:00', '  entityCount: ENTITY_COUNT', 'maps: []', 'grids:', '- 1', 'orphans:', '- 1',
         'nullspace: []', 'tilemap:']
    for tname, idx in sorted(tb.TILE_IDS.items(), key=lambda kv: kv[1]):
        L.append(f'  {idx}: {tname}')
    L += ['entities:', '- proto: ""', '  entities:', '  - uid: 1', '    components:', '    - type: MetaData',
          f'      name: {name}', '    - type: Transform', '      parent: invalid', '    - type: MapGrid',
          '      canSplit: False', '      chunks:']
    for cx, cy, data in ch:
        L += [f'        {cx},{cy}:', f'          ind: {cx},{cy}', f'          tiles: {data}', '          version: 7']
    L += ['    - type: Broadphase', '    - type: Physics', '      bodyStatus: InAir', '      angularDamping: 0.05',
          '      linearDamping: 0.05', '      fixedRotation: False', '      bodyType: Dynamic', '    - type: Fixtures',
          '      fixtures: {}', '    - type: OccluderTree', '    - type: GridPathfinding', '    - type: DecalGrid',
          '      chunkCollection:', '        version: 2', '        nodes: []', '    - type: SpreaderGrid']
    L += tb.roof_data(set(lv.tiles) if underground else lv.roof)
    groups = {}
    for e in tb.rmc_ents(lv.ents):
        proto, x, y, rot = e[:4]
        name, comps = (e[4], e[5]) if len(e) > 5 else (None, None)
        if (math.floor(x), math.floor(y)) in lv.tiles:
            groups.setdefault(proto, []).append((x, y, rot, name, comps))
    uid = 2
    for proto in sorted(groups):
        L += [f'- proto: {proto}', '  entities:']
        for x, y, rot, name, comps in groups[proto]:
            L += [f'  - uid: {uid}', '    components:']
            uid += 1
            if name:
                L += ['    - type: MetaData', f'      name: {name}']
            L += ['    - type: Transform', '      parent: 1', f'      pos: {tb.fmt(x)},{tb.fmt(y)}']
            if rot:
                L.append(f'      rot: {rot!r} rad')
            L += comps or []
    L.append('...')
    open(path, 'w', encoding='utf-8', newline='\n').write('\n'.join(L).replace('ENTITY_COUNT', str(uid - 1)) + '\n')
    print(f'{os.path.relpath(path, tb.ROOT)}: тайлов {len(lv.tiles)}, сущностей {uid - 1}')
    return set(groups), set(tb.TILE_IDS)


def map_preview(m, path, z=0, box=None, scale=2):
    """Превью собранного уровня: тайлы цветом, стены тёмным, двери зелёным, прочие сущности точкой."""
    from PIL import Image
    if z == 0:
        tiles, ents = m.tiles, m.ents
    else:
        tiles, ents = m.levels[z].tiles, m.levels[z].ents
    x0, y0, x1, y1 = box or (tb.X0, tb.Y0, tb.X1, tb.Y1)
    img = Image.new('RGB', ((x1 - x0 + 1) * scale, (y1 - y0 + 1) * scale), (10, 10, 14))
    px = img.load()

    def put(x, y, col, full=True):
        if not (x0 <= x <= x1 and y0 <= y <= y1):
            return
        bx, by = (x - x0) * scale, (y1 - y) * scale
        for i in range(scale):
            for j in range(scale):
                if full or (i == scale // 2 and j == scale // 2):
                    px[bx + i, by + j] = col

    for (x, y), t in tiles.items():
        put(x, y, COLORS.get(t, (120, 120, 128) if 'Floor' in t or 'Plating' in t else (200, 0, 200)))
    water = getattr(m, 'water', {}) if z == 0 else {}
    for (x, y), deep in water.items():
        put(x, y, (40, 80, 140) if deep else (80, 140, 190))
    for e in ents:
        proto, x, y = e[0], math.floor(e[1]), math.floor(e[2])
        if proto in ('DHRockMass',):
            continue
        if 'Wall' in proto or 'Rock' in proto and 'Wall' in proto or proto.startswith('DHWorldEdge'):
            put(x, y, (40, 40, 44))
        elif 'Airlock' in proto or 'Gate' in proto:
            put(x, y, (60, 200, 90))
        elif 'Window' in proto:
            put(x, y, (110, 170, 220))
        elif 'Ladder' in proto:
            put(x, y, (255, 230, 0))
        elif 'ControlPoint' in proto or 'LandingZone' in proto:
            put(x, y, (255, 40, 40))
        elif proto.startswith('DHWater') or proto.startswith('Flora') or 'Grass' in proto or 'Bush' in proto:
            continue
        else:
            put(x, y, (230, 110, 60), full=False)
    img.save(path)
    print('превью:', path, img.size)


def build(t, known):
    """Собрать карту целиком из готового рельефа t (Terrain после split)."""
    setup_bounds()
    m = Map3(known)
    m.load_terrain(t)
    for step in ('tanker', 'terminal', 'dam', 'crash_site', 'base3', 'military', 'gorodok', 'npp3', 'village3', 'village_houses',
                 'spaceport3', 'factory', 'zp1', 'wiring', 'place_water', 'edge'):
        getattr(m, step)()
    return m


def terrain():
    t = Terrain()
    t.geography()
    t.roads_all()
    t.sites()
    t.ground()
    t.nature()
    t.split()
    return t


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--preview', help='PNG превью каркаса')
    ap.add_argument('--map-preview', help='префикс PNG превью собранной карты по уровням (с --write)')
    ap.add_argument('--box', help='u0,v0,u1,v1 — участок для --map-preview')
    ap.add_argument('--write', action='store_true',
                    help='записать Resources/Maps/_DarkestHour/Assault/terminus3*.yml (пока не подключены к игре)')
    args = ap.parse_args()

    t = terrain()
    t.stats()
    if args.preview:
        preview(t, args.preview)
    if args.write:
        known, tiles = tb.known_protos()
        tb.KNOWN_TILES.update(tiles)
        m = build(t, known)
        if m.missing:
            print('НЕТ ПРОТОТИПОВ (пропущены):', sorted(m.missing))
        protos, used = tb.write_surface(m, os.path.join(tb.OUT, 'terminus3.yml'))
        for z, (fname, title) in LEVEL_FILES.items():
            pz, tz = write_level(m.levels[z], os.path.join(tb.OUT, fname), title, z < 0)
            protos |= pz
            used |= tz
        if args.map_preview:
            box = None
            if args.box:
                bu0, bv0, bu1, bv1 = map(int, args.box.split(','))
                box = (gx(bu0), gy(bv1), gx(bu1), gy(bv0))
            for z in (-2, -1, 0, 1, 2):
                map_preview(m, f'{args.map_preview}_{z}.png', z, box)
        bad = sorted(p for p in protos if p not in known)
        badt = sorted(x for x in used if x not in tiles and x != 'Space')
        if bad or badt:
            print('ОШИБКИ: прототипы', bad, 'тайлы', badt)


if __name__ == '__main__':
    main()
