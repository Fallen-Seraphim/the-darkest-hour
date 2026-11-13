#!/usr/bin/env python3
"""
DarkestHour: генератор карты «Терминус» 448×384 по плану автора (2026-11-12).

Север сверху. Северо-запад — море с танкером у нефтяного пирса (ЗП-2); под ним побережье. С моря на юг течёт река к
дамбе, дамба запирает большое озеро. Шоссе А-131 идёт от дамбы на восток через стройку к военной базе и упирается в
горы; от дамбы ответвление на юго-восток к космодрому (ЗП-3), на запад — к деревне. На юго-западе поле и ЗП-1.
Север и восток — горный массив: бункер ПКО у побережья, бункер ПВО, на северо-востоке РЛС и связь гарнизона.
Восточнее космодрома — заброшенный шахтёрский городок (здания до +1 уровня). Из озера на восток течёт река, за ней
АЭС — источник сети: фонари, свет и машины локаций (DHControlPointPowered point: npp), у военных объектов —
резервные дизели. Умеренный климат (трава, лес, без песчаных бурь) — только за рекой и дамбой: деревня, поле, ЗП-1.

База гарнизона (нарисована вручную) переносится из снимка terminus_base.snapshot со смещением BASE_SHIFT; бункер
под базой сдвигается тем же смещением (--bunker).

Запуск из корня репозитория:
  python3 Tools/_DarkestHour/planet_map/terminus_build.py \
      --bunker-in OLD_terminus_bunker.yml
Пишет Resources/Maps/_DarkestHour/Assault/terminus.yml, terminus_p1.yml (+1 уровень) и terminus_bunker.yml.
Детерминирован: повторный запуск даёт ту же карту. Бункер сдвигается, только если передан --bunker-in.
"""
import argparse
import base64
import glob
import json
import math
import os
import random
import re
import struct
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
OUT = os.path.join(ROOT, 'Resources/Maps/_DarkestHour/Assault')
SNAPSHOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'terminus_base.snapshot')

X0, X1, Y0, Y1 = -224, 223, -192, 191          # включительно; кратно чанкам 16×16
BASE_SHIFT = (128, 80)                          # база x −17…18, y −26…9 → x 111…146, y 54…89

PI = math.pi
# Поворот настенной лампы: стена с этой стороны (как на нарисованной базе).
LIGHT_ROT = {'N': 0.0, 'S': PI, 'E': -PI / 2, 'W': PI / 2}
R90, R180, R270 = PI / 2, PI, -PI / 2

SAND = 'FloorAsteroidSand'
GRASS = 'FloorPlanetGrass'
DIRT = 'FloorPlanetDirt'
ROCK = 'WallRockSand'
EDGE = 'DHWorldEdgeSand'

LIGHT = 'DHGridWallLight'          # свет от сети АЭС
LIGHT_MIL = 'DHBunkerWallLight'    # от сети АЭС с резервным дизелем

GRID = ['    - type: ApcPowerReceiver', '      needsPower: False',
        '    - type: DHControlPointPowered', '      point: npp']
GRID_BACKUP = GRID + ['      backup: 600']


def lerp_path(pts, t):
    """Точка ломаной pts по координате y (pts упорядочены по y)."""
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        lo, hi = min(ay, by), max(ay, by)
        if lo <= t <= hi and ay != by:
            return ax + (bx - ax) * (t - ay) / (by - ay)
    return None


def poly_x(pts, x):
    """y ломаной pts по x (pts упорядочены по x)."""
    if x <= pts[0][0]:
        return pts[0][1]
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        if ax <= x <= bx:
            return ay + (by - ay) * (x - ax) / (bx - ax) if bx != ax else ay
    return pts[-1][1]


def in_poly(pts, x, y):
    inside = False
    j = len(pts) - 1
    for i in range(len(pts)):
        xi, yi = pts[i]
        xj, yj = pts[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
            inside = not inside
        j = i
    return inside


def seg_dist(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy or 1)))
    return math.hypot(px - ax - t * dx, py - ay - t * dy)


def path_dist(pts, x, y):
    return min(seg_dist(x, y, *a, *b) for a, b in zip(pts, pts[1:]))


_NOISE = {}


def noise(x, y, scale, seed):
    gx, gy = math.floor(x / scale), math.floor(y / scale)
    fx, fy = x / scale - gx, y / scale - gy

    def h(i, j):
        k = (i, j, seed)
        v = _NOISE.get(k)
        if v is None:
            v = _NOISE[k] = random.Random((i * 92837111) ^ (j * 689287499) ^ (seed * 283923481)).random()
        return v

    sx, sy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a = h(gx, gy) + (h(gx + 1, gy) - h(gx, gy)) * sx
    b = h(gx, gy + 1) + (h(gx + 1, gy + 1) - h(gx, gy + 1)) * sx
    return a + (b - a) * sy


def fbm(x, y, seed):
    return noise(x, y, 24, seed) * 0.55 + noise(x, y, 9, seed + 1) * 0.3 + noise(x, y, 4, seed + 2) * 0.15


# ------------------------------------------------------------------ география

# Берег моря: y по x (море севернее), до x 30.
COAST = [(-224, 92), (-180, 94), (-150, 98), (-134, 100), (-112, 106), (-90, 113), (-60, 121), (-30, 131),
         (-4, 141), (14, 152), (24, 166), (30, 192)]
# Река с моря к дамбе (по y).
RIVER_N = [(-118, 38), (-121, 52), (-126, 66), (-131, 82), (-134, 101)]
DAM = (-140, 24, -96, 36)                     # x0, y0, x1, y1
LAKE = [(-138, 23), (-98, 23), (-62, 8), (-32, -12), (-8, -32), (2, -54), (0, -84), (-6, -104), (-30, -126),
        (-78, -136), (-116, -126), (-136, -102), (-146, -72), (-146, -40), (-140, -10)]
RIVER_S = [(-6, -104), (12, -114), (40, -122), (72, -130), (110, -140), (150, -148), (190, -152), (224, -156)]
ARM = [(40, -122), (64, -108), (92, -98), (118, -92), (134, -90)]   # старица севернее АЭС
TANKER = (-122, 144, -58, 156)                # корпус: x0, y0, x1, y1
PIER_X = (-82, -79)                           # пирс к танкеру

HIGHWAY_Y = (44, 48)
HW_WEST = -96                                 # шоссе начинается у восточного края дамбы
HW_EAST = 198
NORTH_ROAD_X = (194, 198)                     # от конца шоссе на север к РЛС

VILLAGE = (-222, -46, -164, 22)
FIELD = (-222, -144, -160, -58)
ZP1 = (-178, -168)
CONSTRUCTION = (20, 52, 86, 84)
BASE = (106, 50, 152, 92)                     # с запасом вокруг забора
PKO = (-20, 98, 24, 126)
PVO = (58, 106, 98, 130)
RLS = (140, 112, 186, 146)
SPACEPORT = (14, -86, 100, -2)
ZP3 = (58, -46)
MINES = (112, -76, 178, -12)
NPP = (140, -142, 212, -98)


class Level:
    """Отдельный Z-уровень (здесь — +1 шахтёрского городка)."""

    def __init__(self):
        self.tiles = {}
        self.ents = []
        self.roof = set()


class World:
    def __init__(self, known):
        self.known = known
        self.tiles = {}
        self.ents = []          # (proto, fx, fy, rot, name, comps)
        self.occ = set()        # клетки с постройками/предметами
        self.res = set()        # где не ставить скалы, флору, воду
        self.water = {}         # клетка → глубокая?
        self.roof = set()
        self.decals = []        # (id, x, y, color, angle)
        self.rng = random.Random(20261112)
        self.temperate_edge = {}
        self.p1 = Level()
        self.missing = set()

    # ---------- примитивы ----------

    def inside(self, x, y):
        return X0 < x < X1 and Y0 < y < Y1

    def tile(self, x, y, name, reserve=True):
        if self.inside(x, y):
            self.tiles[(x, y)] = name
            if reserve:
                self.res.add((x, y))

    def rect(self, x0, y0, x1, y1, name, reserve=True):
        for x in range(min(x0, x1), max(x0, x1) + 1):
            for y in range(min(y0, y1), max(y0, y1) + 1):
                self.tile(x, y, name, reserve)

    def reserve(self, x0, y0, x1, y1):
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                self.res.add((x, y))

    def dry(self, x0, y0, x1, y1):
        """Убрать воду (мост, пирс, дамба)."""
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                self.water.pop((x, y), None)

    def ent(self, proto, x, y, rot=0.0, name=None, comps=None, foot=None, free=False, off=(0.5, 0.5)):
        if proto not in self.known:
            self.missing.add(proto)
            return False
        cells = foot if foot is not None else [(x, y)]
        if not free and any(c in self.occ for c in cells):
            return False
        self.ents.append((proto, x + off[0], y + off[1], rot, name, comps))
        for c in cells:
            self.occ.add(c)
            self.res.add(c)
        return True

    def item(self, proto, x, y, rot=0.0, comps=None):
        """Предмет поверх мебели (стол, стеллаж): клетка может быть занята."""
        return self.ent(proto, x, y, rot, comps=comps, free=True)

    def grid(self, proto, x, y, rot=0.0, backup=False, **kw):
        """Машина, которая работает от сети АЭС (без проводки)."""
        return self.ent(proto, x, y, rot, comps=GRID_BACKUP if backup else GRID, **kw)

    def decal(self, did, x, y, color='#FFFFFFFF', angle=0.0):
        self.decals.append((did, x, y, color, angle))

    def free(self, x, y):
        return (x, y) not in self.occ and (x, y) not in self.water

    def light(self, x, y, wall, proto=LIGHT):
        self.ent(proto, x, y, LIGHT_ROT[wall], free=True)

    def building(self, x0, y0, x1, y1, wall, floor, doors=(), windows=(), window='Window', door='Airlock',
                 skip=(), roof=True, lights=LIGHT, light_step=7):
        """Прямоугольное здание: пол, стены по периметру, двери/окна на периметре, крыша и свет по стенам."""
        self.rect(x0, y0, x1, y1, floor)
        self.reserve(x0 - 1, y0 - 1, x1 + 1, y1 + 1)
        doors = {d[:2]: (d[2] if len(d) > 2 else door) for d in doors}
        windows = set(windows)
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                if roof:
                    self.roof.add((x, y))
                if x not in (x0, x1) and y not in (y0, y1) or (x, y) in skip:
                    continue
                if (x, y) in doors:
                    self.ent(doors[(x, y)], x, y)
                elif (x, y) in windows:
                    self.ent(window, x, y)
                else:
                    self.ent(wall, x, y)
        if lights:
            self.room_lights(x0 + 1, y0 + 1, x1 - 1, y1 - 1, lights, light_step)

    def room_lights(self, x0, y0, x1, y1, proto=LIGHT, step=7):
        """Свет по северной и южной стене комнаты (внутренние клетки x0…x1, y0…y1)."""
        if x1 - x0 < 1 or y1 - y0 < 0:
            return
        xs = list(range(x0 + min(2, (x1 - x0) // 2), x1 + 1, step)) or [x0]
        for x in xs:
            self.light(x, y1, 'N', proto)
            if y1 - y0 >= 7:
                self.light(x, y0, 'S', proto)

    def wall_line(self, x0, y0, x1, y1, wall, gaps=()):
        for x in range(min(x0, x1), max(x0, x1) + 1):
            for y in range(min(y0, y1), max(y0, y1) + 1):
                if (x, y) not in gaps:
                    self.ent(wall, x, y)

    def fence_rect(self, x0, y0, x1, y1, gates=(), proto='FenceMetal', skip=()):
        gates = set(gates)
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                if x not in (x0, x1) and y not in (y0, y1) or (x, y) in skip:
                    continue
                corner = {(x1, y0): 0.0, (x0, y0): R270, (x1, y1): R90, (x0, y1): R180}.get((x, y))
                horizontal = y in (y0, y1)
                if corner is not None:
                    self.ent(proto + 'Corner', x, y, corner)
                elif (x, y) in gates:
                    self.ent(proto + 'Gate', x, y, R180 if horizontal else R90)
                else:
                    self.ent(proto + 'Straight', x, y, R270 if horizontal else 0.0)
        self.reserve(x0, y0, x1, y1)

    def scatter(self, protos, x0, y0, x1, y1, n, foot=1, test=None):
        placed = 0
        for _ in range(n * 30):
            if placed >= n:
                break
            x, y = self.rng.randint(x0, x1), self.rng.randint(y0, y1)
            cells = [(x + i, y + j) for i in range(foot) for j in range(foot)]
            if all(self.free(*c) for c in cells) and (test is None or test(x, y)):
                if self.ent(self.rng.choice(protos), x, y, foot=cells):
                    placed += 1
        return placed

    def car(self, x, y, wreck=False):
        colors = ['Black', 'Blue', 'Brown', 'Green', 'LightBlue', 'Red', 'Turquoise', 'Purple', 'Orange']
        c = self.rng.choice(colors)
        if wreck:
            proto = f'RMCPropVehicleMonoSupron{c}' + self.rng.choice(['Damaged1', 'Damaged3', 'Damaged4', 'Damaged5',
                                                                        'Destroyed'])
        else:
            proto = f'RMCPropVehicleMonoSupron{c}'
        if proto not in self.known:
            proto = 'RMCPropVehicleMonoSupronBlack'
        if self.rng.random() < 0.5 and proto + 'Flipped' in self.known:
            proto += 'Flipped'
        self.ent(proto, x, y, foot=[(x, y), (x + 1, y)])

    def container(self, x, y, brand, length=4):
        cells = [(x + i, y + j) for i in range(length) for j in range(2)]
        if not all(self.free(*c) for c in cells):
            return
        for i in range(length):
            part = 'Left' if i == 0 else 'Right' if i == length - 1 else 'Middle'
            self.ent(f'RMCContainer{brand}{part}', x + i, y, foot=[(x + i, y), (x + i, y + 1)])

    def lz(self, x, y, name, clear=12):
        self.ent('DHLandingZonePad', x, y, name=name)
        self.reserve(x - clear, y - clear, x + clear, y + clear)

    def post(self, x, y):
        """Фонарь сети АЭС."""
        self.ent('DHLightPostGrid', x, y)

    # ---------- рельеф и вода ----------

    def coast_y(self, x):
        if x > 30:
            return 10 ** 6
        return poly_x(COAST, x) + (noise(x, 0, 11, 7) - 0.5) * 6

    def river_n_x(self, y):
        x = lerp_path(RIVER_N, y)
        return None if x is None else x + (noise(0, y, 9, 8) - 0.5) * 3

    def geography(self):
        lake_edge = {}
        for x in range(X0 + 1, X1):
            cy = self.coast_y(x)
            for y in range(Y0 + 1, Y1):
                # море
                if y >= cy:
                    self.water[(x, y)] = y >= cy + 5
                    self.tiles[(x, y)] = SAND
                elif y >= cy - 6:
                    self.tiles[(x, y)] = 'FloorDesert'      # пляж
                    self.res.add((x, y))
        # река с моря к дамбе
        for y in range(DAM[3], 106):
            cx = self.river_n_x(y)
            if cx is None:
                continue
            for x in range(int(cx) - 9, int(cx) + 10):
                d = abs(x - cx)
                if d <= 4.5:
                    self.water[(x, y)] = d <= 2.5
                    self.tiles[(x, y)] = DIRT
                elif d <= 7 and (x, y) not in self.water:
                    self.tiles[(x, y)] = GRASS if x < cx else 'FloorDirt'
                    self.res.add((x, y))
        # озеро
        lake = []
        for (x, y) in [(x, y) for x in range(-160, 20) for y in range(-150, 30)]:
            wob = (noise(x, y, 13, 9) - 0.5) * 5
            if in_poly(LAKE, x + wob, y + wob):
                lake.append((x, y))
                lake_edge[y] = min(lake_edge.get(y, x), x)
        lake_set = set(lake)
        for (x, y) in lake:
            deep = all((x + i, y + j) in lake_set for i in (-4, 0, 4) for j in (-4, 0, 4))
            self.water[(x, y)] = deep
            self.tiles[(x, y)] = DIRT
        # берег озера
        for (x, y) in lake:
            for i in (-2, -1, 1, 2):
                for j in (-2, -1, 1, 2):
                    c = (x + i, y + j)
                    if c not in lake_set and c not in self.water:
                        self.tiles[c] = GRASS if x < -60 else 'FloorDirt'
                        self.res.add(c)
        # река из озера на восток и старица
        for pts, half, deep_half in ((RIVER_S, 5.5, 3.5), (ARM, 3.5, 1.5)):
            xs = range(min(p[0] for p in pts) - 8, max(p[0] for p in pts) + 1)
            for x in xs:
                for y in range(min(p[1] for p in pts) - 10, max(p[1] for p in pts) + 10):
                    if not self.inside(x, y):
                        continue
                    d = path_dist(pts, x, y) + (noise(x, y, 10, 11) - 0.5) * 2
                    if d <= half:
                        self.water[(x, y)] = d <= deep_half
                        self.tiles[(x, y)] = DIRT
                    elif d <= half + 3 and (x, y) not in self.water:
                        self.tiles[(x, y)] = 'FloorDirt'
                        self.res.add((x, y))
        # граница умеренного климата: западнее реки, дамбы и озера
        for y in range(Y0, Y1 + 1):
            if y > DAM[3]:
                cx = self.river_n_x(y)
                edge = cx if cx is not None else -134
            elif y >= DAM[1]:
                edge = DAM[0]
            else:
                # западнее озера; ниже, где озеро сужается к востоку, граница не уходит за x −140
                edge = min(lake_edge.get(y, -150), -140)
            self.temperate_edge[y] = int(edge)

    def temperate(self, x, y):
        return x < self.temperate_edge.get(y, -150)

    def ground(self):
        """Грунт: трава и земля за рекой, песок и пустыня — остальное."""
        for x in range(X0, X1 + 1):
            for y in range(Y0, Y1 + 1):
                if (x, y) in self.tiles:
                    continue
                if self.temperate(x, y):
                    n = noise(x, y, 7, 21)
                    self.tiles[(x, y)] = DIRT if n > 0.78 else 'FloorGrass' if n < 0.18 else GRASS
                else:
                    n = noise(x, y, 9, 22)
                    self.tiles[(x, y)] = 'FloorDesert' if n > 0.8 else 'FloorDesert' if n < 0.12 else SAND

    def mountain(self, x, y):
        """0…1: насколько клетка — горы."""
        n = noise(x, y, 17, 31) - 0.5
        v = 0.0
        # северо-восточный массив (база — у его подножия)
        if x > 26 + n * 10 and y > 94 + n * 8:
            v = 1.0
        # прибрежные холмы у ПКО и между побережьем и стройкой
        elif x > -40 and y > 86 + n * 10 and y < self.coast_y(x) - 8:
            v = 0.55
        # восточная гряда
        if x > 200 + n * 8 and y < 92 and not (NPP[1] - 6 < y < NPP[3] + 6) and y > -150:
            v = 1.0
        # гряда между шоссе и космодромом / городком
        if 6 + n * 6 < y < 36 + n * 4 and 24 < x < 196:
            v = max(v, 0.85)
        # южная гряда под озером и за рекой
        if y < -170 + n * 8 and x > -150:
            v = 1.0
        if y < -162 + n * 8 and x > 30:
            v = 1.0
        # лесистые холмы на северо-западе (умеренная зона)
        if x < -140 and 32 < y < self.coast_y(x) - 9:
            v = max(v, 0.5)
        return v

    # ---------- дороги ----------

    def road_h(self, x0, x1, y0, y1, tile='RMCFloorAsphalt', posts=16, line=True):
        for x in range(min(x0, x1), max(x0, x1) + 1):
            for y in range(y0, y1 + 1):
                if (x, y) in self.water:
                    continue
                self.tile(x, y, tile)
            if line and (x % 4) < 2 and (x, (y0 + y1) // 2) not in self.water:
                self.decal('WarnLineGreyscaleN', x, (y0 + y1) // 2, '#C8C8C8A0')
            if posts and x % posts == 0 and (x, y1 + 1) not in self.water:
                self.post(x, y1 + 1)

    def road_v(self, x0, x1, y0, y1, tile='RMCFloorAsphalt', posts=16, line=True):
        for y in range(min(y0, y1), max(y0, y1) + 1):
            for x in range(x0, x1 + 1):
                if (x, y) in self.water:
                    continue
                self.tile(x, y, tile)
            if line and (y % 4) < 2:
                self.decal('WarnLineGreyscaleE', (x0 + x1) // 2, y, '#C8C8C8A0')
            if posts and y % posts == 0:
                self.post(x1 + 1, y)

    def road_path(self, pts, width=5, tile='RMCFloorAsphalt', posts=18):
        """Дорога по ломаной (любые углы)."""
        half = width / 2
        xs = range(int(min(p[0] for p in pts)) - width, int(max(p[0] for p in pts)) + width + 2)
        ys = range(int(min(p[1] for p in pts)) - width, int(max(p[1] for p in pts)) + width + 2)
        for x in xs:
            for y in ys:
                if path_dist(pts, x + 0.5, y + 0.5) <= half and (x, y) not in self.water:
                    self.tile(x, y, tile)
        # фонари вдоль
        length = 0.0
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            seg = math.hypot(bx - ax, by - ay)
            k = 0.0
            while k < seg:
                t = k / seg
                if posts and int(length + k) % posts == 0:
                    px, py = ax + (bx - ax) * t, ay + (by - ay) * t
                    nx, ny = -(by - ay) / seg, (bx - ax) / seg
                    cx, cy = int(px + nx * (half + 1)), int(py + ny * (half + 1))
                    if self.free(cx, cy):
                        self.post(cx, cy)
                k += 1
            length += seg

    def roads(self):
        h0, h1 = HIGHWAY_Y
        # шоссе А-131: восточный край дамбы → на восток до гор
        self.road_path([(DAM[2], 33), (-80, 34), (-56, h0 + 2), (-40, (h0 + h1) / 2 + 0.5)], width=5, posts=16)
        self.road_h(-40, HW_EAST, h0, h1)
        # брошенные и сгоревшие машины вдоль шоссе
        for x in range(-30, HW_EAST - 10, 19):
            if self.rng.random() < 0.45:
                self.car(x, self.rng.choice((h0, h1)), wreck=self.rng.random() < 0.6)
        for x in range(-30, HW_EAST, 7):
            if self.rng.random() < 0.35:
                self.decal('RMCDecalRoadTiremark' + str(self.rng.randint(1, 8)), x, self.rng.randint(h0, h1), '#2A2A2AC0')
        # блокпост гарнизона на шоссе западнее стройки: мешки, шлагбаум, будка
        cx = 2
        for y in (h0 - 2, h1 + 2):
            for x in range(cx - 3, cx + 4):
                self.ent('DHSandbagBarricade', x, y)
        self.ent('DHBarbedWireObstacle', cx - 6, h0)
        self.ent('DHBarbedWireObstacle', cx - 6, h1)
        self.building(cx - 2, h1 + 4, cx + 2, h1 + 8, 'WallSolid', 'FloorSteel', doors=[(cx, h1 + 4, 'Airlock')],
                      windows=[(cx - 2, h1 + 6), (cx + 2, h1 + 6)], light_step=3)
        self.ent('TableReinforced', cx, h1 + 7)
        self.ent('Chair', cx, h1 + 6, R180)
        # конец шоссе — на север к РЛС
        self.road_v(*NORTH_ROAD_X, h0, RLS[1] + 6)
        # ответвление на юго-восток вдоль озера к космодрому
        self.road_path([(-82, 32), (-60, 16), (-34, 0), (-12, -14), (6, -10), (10, 0), (SPACEPORT[0] + 18, 0)],
                       width=5, posts=18)
        # запад: дамба → деревня
        self.road_path([(DAM[0], 33), (-152, 30), (-166, 18), (-186, 12), (-186, 2)],
                       width=4, tile='FloorDirt', posts=20)
        # грунтовка: деревня → поле → ЗП-1
        self.road_path([(-186, 8), (-186, -52), (-190, -60), (-190, -150), (ZP1[0], ZP1[1] + 10)],
                       width=3, tile='FloorDirt', posts=0)
        # от шоссе к стройке и к воротам базы
        self.road_v(52, 55, CONSTRUCTION[1], h1, posts=0, line=False)
        self.road_v(120, 124, h1 + 1, 54, posts=0, line=False)
        # горная дорога: северные ворота базы → ПВО → ПКО
        self.road_v(120, 123, 90, 102, tile='FloorDirt', posts=0, line=False)
        self.road_v(132, 135, 90, 102, tile='FloorDirt', posts=0, line=False)
        self.road_h(PVO[2] - 2, 135, 100, 103, tile='FloorDirt', posts=24, line=False)
        self.road_path([(PVO[0] + 2, 101), (40, 100), (30, 96), (PKO[2] - 2, 96)], width=4, tile='FloorDirt',
                       posts=24)
        # горная тропа: РЛС ↔ ПВО
        self.road_path([(RLS[0] + 2, 120), (120, 124), (PVO[2] - 2, 120)], width=3, tile='FloorDirt', posts=0)
        # перевал: стройка → на юг к городку (через гряду)
        self.road_v(127, 130, -10, h0 - 1, tile='FloorDirt', posts=20, line=False)
        # тропа: космодром ↔ стройка (через гряду западнее)
        self.road_v(60, 62, 0, h0 - 1, tile='FloorDirt', posts=0, line=False)
        # от городка к АЭС — по восточному берегу старицы
        self.road_v(196, 199, NPP[3] + 1, -44, tile='FloorDirt', posts=20, line=False)
        # космодром ↔ АЭС: мост через старицу
        self.road_v(96, 99, -120, -86, posts=0, line=False)
        self.road_h(96, NPP[0], -124, -121, posts=16)
        for y in range(-120, -86):
            for x in range(96, 100):
                if (x, y) in self.water:
                    self.water.pop((x, y))
                    self.tile(x, y, 'FloorReinforced')
                    if x in (96, 99):
                        self.ent('Railing', x, y, R90 if x == 99 else R270)

    # ---------- дамба ----------

    def dam(self):
        x0, y0, x1, y1 = DAM
        self.dry(x0, y0, x1, y1)
        self.rect(x0, y0, x1, y1, 'FloorConcrete')
        self.rect(x0, y0 + 7, x1, y1, 'RMCFloorAsphalt')        # дорога по гребню
        for x in range(x0, x1 + 1):
            self.ent('Railing', x, y1, R180)                # над рекой
            if x % 11 not in (4, 5, 6):
                self.ent('Railing', x, y0, 0.0)             # над озером
            if x % 4 < 2:
                self.decal('WarnLineGreyscaleN', x, y0 + 9, '#C8C8C8A0')
        # турбинный ряд
        for x in range(x0 + 6, x1 - 4, 11):
            self.ent('RMCPropTurbine', x, y0 + 3, foot=[(x, y0 + 2), (x, y0 + 3), (x, y0 + 4)])
            self.ent('RMCPropTurbineStrutsBorder', x, y0 + 3, free=True)
            self.ent('RMCPropTurbineStrutsLeft', x - 1, y0 + 3)
            self.ent('RMCPropTurbineStrutsRight', x + 1, y0 + 3)
        # служебное здание с пультом (на гребне, запад)
        bx = x0 + 2
        self.building(bx, y0 + 1, bx + 8, y0 + 6, 'WallReinforced', 'FloorSteel',
                      doors=[(bx + 4, y0 + 6, 'AirlockEngineeringGlass')],
                      windows=[(bx + 2, y0 + 1), (bx + 6, y0 + 1)], window='ReinforcedWindow')
        self.grid('ComputerPowerMonitoring', bx + 2, y0 + 5)
        self.ent('ChairOfficeDark', bx + 2, y0 + 4)
        self.grid('ComputerAlert', bx + 5, y0 + 5)
        self.ent('Rack', bx + 7, y0 + 2)
        self.ent('ClosetToolFilled', bx + 1, y0 + 2)
        self.ent('DHControlPointDam', (x0 + x1) // 2, y0 + 5)
        for x in range(x0 + 3, x1, 9):
            self.post(x, y1 - 1)
        self.reserve(x0 - 3, y0 - 3, x1 + 3, y1 + 3)
        # водосброс: пена у подножия со стороны реки
        for x in range((x0 + x1) // 2 - 6, (x0 + x1) // 2 + 6):
            self.decal('DirtLight', x, y1 + 1, '#FFFFFF80')

    # ---------- танкер и нефтяной пирс (ЗП-2) ----------

    def tanker(self):
        x0, y0, x1, y1 = TANKER
        # корпус: борт — укреплённая стена, нос на восток
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                nose = x - (x1 - 7)
                if nose > 0 and abs(y - (y0 + y1) / 2) > (y1 - y0) / 2 - nose * 0.8:
                    continue
                self.tile(x, y, 'FloorSteel')
                self.water.pop((x, y), None)
                edge = x == x0 or y in (y0, y1) or (nose > 0 and abs(y - (y0 + y1) / 2) > (y1 - y0) / 2 - (nose + 1) * 0.8)
                if edge:
                    self.ent('WallReinforcedRust' if 'WallReinforcedRust' in self.known else 'WallReinforced', x, y)
        # палуба: открытая, люки танков, трубопровод
        cy = (y0 + y1) // 2
        for x in range(x0 + 4, x1 - 9, 6):
            self.ent('RMCFiltrationMachineSoloTankEmpty', x, cy + 3, foot=[(x, cy + 3)])
            self.ent('RMCFiltrationMachineSoloTankEmpty', x, cy - 3, foot=[(x, cy - 3)])
        for x in range(x0 + 2, x1 - 8):
            if x % 3 == 0:
                self.decal('RMCDecalOilFloor' + str(1 + x % 7), x, cy, '#FFFFFFC0')
        # надстройка на корме (запад): мостик и каюты
        sx = x0 + 1
        self.building(sx, y0 + 1, sx + 9, y1 - 1, 'WallReinforced', 'FloorDark',
                      doors=[(sx + 9, cy, 'AirlockGlass'), (sx + 9, cy - 3, 'AirlockGlass')],
                      windows=[(sx + 3, y1 - 1), (sx + 6, y1 - 1), (sx + 3, y0 + 1), (sx + 6, y0 + 1)],
                      window='ReinforcedWindow', lights='AlwaysPoweredWallLight')
        self.wall_line(sx + 1, cy - 1, sx + 8, cy - 1, 'WallReinforced', gaps=[(sx + 5, cy - 1)])
        self.ent('AirlockGlass', sx + 5, cy - 1)
        for x, p in ((sx + 2, 'ComputerBroken'), (sx + 4, 'ComputerRadar'), (sx + 6, 'ComputerBroken')):
            self.ent(p, x, y1 - 2)
            self.ent('ChairOfficeDark', x, y1 - 3)
        for y in range(y0 + 2, cy - 2, 2):
            self.ent('RMCBedBunk' if 'RMCBedBunk' in self.known else 'Bed', sx + 1, y)
        self.ent('ClosetEmergencyFilledRandom', sx + 8, y0 + 2)
        self.ent('LockerFreezer', sx + 7, y0 + 2)
        self.ent('TableReinforced', sx + 4, y0 + 3)
        # ЗП-2 на палубе
        self.lz((x0 + x1) // 2 + 4, cy, 'ЗП-2 Танкер', clear=5)
        # пирс от берега к борту
        px0, px1 = PIER_X
        shore = int(self.coast_y((px0 + px1) // 2)) - 4
        self.dry(px0 - 1, shore, px1 + 1, y0)
        for y in range(shore, y0):
            for x in range(px0 - 1, px1 + 2):
                self.tile(x, y, 'FloorWood')
            self.ent('Railing', px0 - 1, y, R270)
            self.ent('Railing', px1 + 1, y, R90)
        for y in range(shore + 4, y0, 9):
            self.ent('AlwaysPoweredWallLight', px1, y, LIGHT_ROT['E'], free=True)
        # трап в борт
        for x in range(px0, px1 + 1):
            self.ent('AirlockGlass', x, y0)
        # нефтяной терминал на берегу
        tx = px0 - 14
        self.fence_rect(tx, shore - 22, px1 + 14, shore - 2, gates=[(px0, shore - 2), (px0 + 1, shore - 2),
                                                                   (px1, shore - 2)])
        self.rect(tx + 1, shore - 21, px1 + 13, shore - 3, 'FloorConcrete')
        for x in range(tx + 3, px1 + 12, 5):
            self.ent('RMCFiltrationMachineSoloTankWaste', x, shore - 18)
            self.decal('RMCDecalOilBase', x, shore - 19, '#FFFFFFC0')
        self.scatter(['RMCBarrelRed', 'RMCBarrelYellow', 'RMCBarrelBlack', 'RMCBarrelBrown'],
                     tx + 2, shore - 14, px1 + 12, shore - 6, 16)
        self.building(px1 + 4, shore - 12, px1 + 12, shore - 5, 'WallSolid', 'FloorSteel',
                      doors=[(px1 + 4, shore - 8, 'AirlockGlass')], windows=[(px1 + 8, shore - 5)])
        self.grid('ComputerPowerMonitoring', px1 + 7, shore - 6)
        self.ent('ChairOfficeLight', px1 + 7, shore - 7)
        self.ent('ClosetToolFilled', px1 + 11, shore - 11)
        self.post(tx + 1, shore - 3)
        self.post(px1 + 13, shore - 3)
        # дорога от терминала к шоссе
        self.road_path([(px0 + 1, shore - 22), (-76, 70), (-64, h_mid()), ], width=4, tile='FloorDirt', posts=20)

    # ---------- деревня ----------

    def village(self):
        x0, y0, x1, y1 = VILLAGE
        self.reserve(x0, y0, x1, y1)
        street_y = (-14, -11)
        self.road_h(x0 + 1, x1 - 2, *street_y, tile='FloorDirt', posts=14, line=False)
        self.road_v(-188, -185, y0 + 2, y1 - 2, tile='FloorDirt', posts=14, line=False)
        # площадь с колодцем и точкой захвата
        self.rect(-196, -20, -176, -6, 'FloorDirt')
        self.ent('DHControlPointVillage', -186, -18)
        for x, y in ((-195, -7), (-177, -7), (-195, -19), (-177, -19)):
            self.ent('FloraTreeLarge', x, y)
        self.ent('RMCBarrelBlue', -183, -9)
        self.ent('RMCBarrelGreen', -182, -9)
        self.ent('RMCBenchLeft', -190, -8) if 'RMCBenchLeft' in self.known else None
        houses = [(-218, 4), (-206, 4), (-174, 4), (-218, -36), (-206, -36), (-174, -36), (-162 - 10, -28)]
        houses = [(-218, 4), (-206, 4), (-174, 6), (-218, -38), (-206, -38), (-174, -38)]
        for i, (hx, hy) in enumerate(houses):
            north = hy > -11
            self.house(hx, hy, north, i)
        # сельсовет / фельдшерский пункт
        self.building(-206, -30, -194, -22, 'WallSolid', 'FloorWhite', doors=[(-200, -22, 'AirlockGlass')],
                      windows=[(-204, -30), (-198, -30), (-206, -26), (-194, -26)])
        for y in (-29, -27):
            self.ent('MedicalBed', -205, y)
        self.ent('LockerMedicalFilled', -195, -29)
        self.ent('SinkWide', -197, -29)
        self.ent('TableReinforced', -202, -25)
        self.ent('ChairWood', -202, -26)
        # лавка
        self.building(-172, -30, -162, -22, 'WallWood', 'FloorWood', doors=[(-167, -22, 'WoodDoor')],
                      windows=[(-170, -22), (-164, -22)])
        for x in range(-171, -163, 2):
            self.ent('RMCPropSupermartShelf1' if x % 4 else 'RMCPropSupermartShelf2', x, -29)
        self.ent('TableCounterMetal', -170, -25)
        self.ent('TableCounterMetal', -169, -25)
        self.ent('RMCPropSupermartBasketApples', -165, -25)
        # амбар и сеновал
        self.building(-220, -30, -212, -20, 'WallWood', 'FloorWood', doors=[(-212, -25, 'WoodDoor')],
                      windows=[], lights=None)
        self.scatter(['CrateGenericSteel', 'RMCBarrelBrown'], -219, -29, -213, -21, 6)
        # машины
        self.car(-180, -12)
        self.car(-200, -13)
        self.ent('RMCPropVehicleSmallTruckBrownCargo', -192, -4, foot=[(-192, -4), (-191, -4)])
        # фонари на площади
        for x, y in ((-196, -6), (-176, -6)):
            self.post(x, y)

    def house(self, hx, hy, north, i):
        """Деревенский дом 9×7 с огородом; дверь к улице."""
        w, h = 9, 7
        dy = hy if north else hy + h
        door = (hx + 4, hy if north else hy + h, 'WoodDoor')
        self.building(hx, hy, hx + w, hy + h, 'WallWood', 'FloorWood', doors=[door],
                      windows=[(hx + 2, hy + (h if north else 0)), (hx + 7, hy + (h if north else 0)),
                               (hx, hy + 3), (hx + w, hy + 3)], light_step=6)
        iy = hy + h - 1 if north else hy + 1
        self.ent('Bed', hx + 1, iy)
        self.ent('Bed', hx + 2, iy)
        self.ent('TableWood', hx + 6, hy + 3)
        self.ent('ChairWood', hx + 5, hy + 3, R90)
        self.ent('ChairWood', hx + 7, hy + 3, R270)
        self.ent('Dresser', hx + 8, iy)
        if i % 2 == 0:
            self.ent('Fireplace', hx + 4, iy)
        else:
            self.ent('BookshelfFilled', hx + 4, iy)
        self.ent('Sink', hx + 1, hy + 3 if north else hy + 4)
        # огород позади дома
        gy0 = hy + h + 2 if north else hy - 7
        self.fence_rect(hx, gy0, hx + w, gy0 + 5, proto='FenceWoodSmall', gates=[(hx + 4, gy0 if north else gy0 + 5)])
        self.rect(hx + 1, gy0 + 1, hx + w - 1, gy0 + 4, DIRT)
        for x in range(hx + 1, hx + w):
            for y in range(gy0 + 1, gy0 + 5):
                if (x + y) % 2 == 0:
                    self.decal(self.rng.choice(['Bushb1', 'Bushb2', 'Bushc1', 'Flowersy1', 'Flowerspv1']), x, y)

    # ---------- поле и ЗП-1 ----------

    def field(self):
        x0, y0, x1, y1 = FIELD
        self.reserve(x0, y0, x1, y1)
        # участки поля: распаханная земля и высокая трава
        for x in range(x0 + 1, x1):
            for y in range(y0 + 1, y1):
                if (x, y) in self.tiles and self.tiles[(x, y)] == 'FloorDirt':
                    continue
                strip = ((x - x0) // 14) % 2
                if strip == 0:
                    self.tile(x, y, DIRT)
                    if y % 2 == 0:
                        self.decal(self.rng.choice(['Grassa1', 'Grassa2', 'Grassb1', 'Grassc2']), x, y)
                else:
                    self.tile(x, y, GRASS)
                    if self.rng.random() < 0.35:
                        self.decal(self.rng.choice(['Grassd1', 'Grassd2', 'Grasse1', 'Grasse2', 'Grasse3',
                                                    'Flowersy2', 'Flowersy3', 'Flowersbr1']), x, y)
        # высокая трава (укрытие)
        tall = 'RMCGrassTallJungle' if 'RMCGrassTallJungle' in self.known else None
        if tall:
            for x in range(x0 + 1, x1):
                for y in range(y0 + 1, y1):
                    if ((x - x0) // 14) % 2 == 1 and noise(x, y, 5, 41) > 0.55 and self.free(x, y):
                        self.ent(tall, x, y)
        # стога, трактор, сарай, пугало-ветряк
        self.scatter(['CrateGenericSteel'], x0 + 2, y0 + 2, x1 - 2, y1 - 2, 5)
        self.ent('RMCPropVehicleCrawler', -176, -100, foot=[(-176 + i, -100 + j) for i in range(3) for j in range(2)])
        self.building(-212, -66, -202, -60, 'WallWood', 'FloorWood', doors=[(-207, -60, 'WoodDoor')],
                      windows=[(-210, -60)], lights=None)
        self.ent('ClosetToolFilled', -211, -65)
        self.scatter(['RMCBarrelBrown', 'CrateGenericSteel'], -210, -65, -204, -62, 4)
        # ЗП-1 «Поля»
        zx, zy = ZP1
        self.rect(zx - 7, zy - 7, zx + 7, zy + 7, DIRT)
        for i in range(-7, 8, 2):
            self.decal('WarnLineGreyscaleN', zx + i, zy - 7, '#E0C060C0')
            self.decal('WarnLineGreyscaleS', zx + i, zy + 7, '#E0C060C0')
        self.lz(zx, zy, 'ЗП-1 Поля')

    # ---------- стройка ----------

    def construction(self):
        x0, y0, x1, y1 = CONSTRUCTION
        self.rect(x0, y0, x1, y1, 'FloorDirt')
        self.fence_rect(x0, y0, x1, y1, gates=[(52, y0), (53, y0), (54, y0), (55, y0)])
        # каркас многоэтажки: бетонный пол, часть стен, балки
        bx0, by0, bx1, by1 = x0 + 6, y0 + 10, x0 + 36, y1 - 6
        self.rect(bx0, by0, bx1, by1, 'FloorConcrete')
        for x in range(bx0, bx1 + 1):
            for y in range(by0, by1 + 1):
                edge = x in (bx0, bx1) or y in (by0, by1)
                inner = (x - bx0) % 10 == 0 and (y - by0) % 6 == 0
                if not (edge or inner):
                    continue
                r = self.rng.random()
                if edge and x < bx0 + 14 and r < 0.75:
                    self.ent('WallSolid', x, y)
                elif r < 0.55:
                    self.ent('Girder', x, y)
        self.rect(bx0 + 1, by0 + 1, bx0 + 12, by0 + 8, 'Plating')
        self.roof.update((x, y) for x in range(bx0, bx0 + 14) for y in range(by0, by0 + 10))
        self.room_lights(bx0 + 1, by0 + 1, bx0 + 12, by0 + 8, LIGHT, 6)
        # кран, экскаватор, грузовики
        self.ent('RMCPropVehicleCargoCrane', x1 - 8, y1 - 10, foot=[(x1 - 9 + i, y1 - 11 + j) for i in range(3) for j in range(3)])
        self.ent('RMCPropDiggerOrange', x1 - 12, y0 + 6, foot=[(x1 - 13 + i, y0 + 5 + j) for i in range(4) for j in range(3)])
        for x, y in ((x0 + 3, y0 + 3), (x0 + 3, y0 + 6)):
            self.ent('RMCPropVehicleSmallTruckBrownCargo', x, y, foot=[(x, y), (x + 1, y)])
        # склад материалов (ресурс зоны — мешки и стержни)
        for x in range(x1 - 20, x1 - 4, 3):
            self.ent('CrateGenericSteel', x, y1 - 3)
            self.ent('DHSandbagsFull', x + 1, y1 - 3)
        for x in range(x1 - 20, x1 - 4, 4):
            self.ent('DHSandbagsEmpty25', x, y1 - 5)
            self.item('PartRodMetal10', x + 2, y1 - 5)
            self.item('SheetSteel10', x + 1, y1 - 7)
        self.container(x0 + 2, y1 - 4, 'Trijent')
        self.container(x0 + 2, y1 - 7, 'WeYa')
        # бытовки
        for i, bx in enumerate((x1 - 26, x1 - 17)):
            self.building(bx, y0 + 2, bx + 7, y0 + 7, 'WallSolid', 'FloorSteel',
                          doors=[(bx + 3, y0 + 7, 'Airlock')], windows=[(bx + 6, y0 + 7)], light_step=5)
            self.ent('TableReinforced', bx + 2, y0 + 4)
            self.ent('ChairOfficeLight', bx + 2, y0 + 3)
            self.grid('ComputerAlert' if i == 0 else 'VendingMachineCoffee', bx + 6, y0 + 3)
            self.ent('ClosetToolFilled', bx + 1, y0 + 3)
        self.ent('DHControlPointConstruction', 50, y0 + 4)
        for x in range(x0 + 4, x1, 12):
            self.post(x, y1 - 1)

    # ---------- перенос базы ----------

    def base(self):
        snap = json.load(open(SNAPSHOT, encoding='utf-8'))
        dx, dy = BASE_SHIFT
        x0, y0, x1, y1 = BASE
        self.reserve(x0, y0, x1, y1)
        for key, name in snap['tiles'].items():
            x, y = (int(v) for v in key.split(','))
            self.tile(x + dx, y + dy, name)
            if name in INDOOR:
                self.roof.add((x + dx, y + dy))
        self.base_ents = []
        for e in snap['entities']:
            lines = []
            for l in e['lines']:
                m = re.match(r'^(\s+pos: )(-?[\d.]+),(-?[\d.]+)$', l)
                if m:
                    px, py = float(m.group(2)) + dx, float(m.group(3)) + dy
                    l = f'{m.group(1)}{fmt(px)},{fmt(py)}'
                    self.occ.add((math.floor(px), math.floor(py)))
                lines.append(l)
            self.base_ents.append((e['proto'], lines))
        # бетон стоянки техники у западного забора (танк и грузовики стоят за забором)
        self.rect(x0 - 5, 58, x0 + 3, 76, 'FloorConcrete')
        self.reserve(x0 - 7, 56, x0, 78)

    # ---------- бункер ПКО ----------

    def pko(self):
        x0, y0, x1, y1 = PKO
        self.reserve(x0 - 2, y0 - 6, x1 + 2, y1 + 2)
        self.rect(x0 - 2, y0 - 5, x1 + 2, y0 - 1, 'FloorConcrete')     # площадка у входа
        # корпус в скале
        self.building(x0, y0, x1, y1, 'WallPlastitanium', 'FloorSteel',
                      doors=[(x0 + 6, y0, 'DHAirlockRepublicGlassLocked'), (x0 + 7, y0, 'DHAirlockRepublicGlassLocked')],
                      windows=[], lights=None)
        # коридор посередине, две орудийные галереи (700 мм — запад, 400 мм — восток)
        cx0, cx1 = x0 + 5, x0 + 8
        self.wall_line(cx0, y0 + 1, cx0, y1 - 1, 'WallPlastitanium', gaps=[(cx0, y0 + 8), (cx0, y1 - 6)])
        self.wall_line(cx1, y0 + 1, cx1, y1 - 1, 'WallPlastitanium',
                       gaps=[(cx1, y0 + 8), (cx1, y0 + 16), (cx1, y1 - 6)])
        self.ent('DHAirlockRepublicGlassLocked', cx0, y0 + 8)
        self.ent('DHAirlockRepublicGlassLocked', cx0, y1 - 6)
        self.ent('DHAirlockRepublicGlassLocked', cx1, y0 + 8)
        self.ent('DHAirlockRepublicGlassLocked', cx1, y0 + 16)
        self.ent('DHAirlockRepublicGlassLocked', cx1, y1 - 6)
        # перегородки восточной части: склад снарядов (юг), галерея 400 (середина), галерея 700 (север)
        self.wall_line(cx1 + 1, y0 + 12, x1 - 1, y0 + 12, 'WallPlastitanium', gaps=[(cx1 + 12, y0 + 12)])
        self.ent('DHAirlockRepublicGlassLocked', cx1 + 12, y0 + 12)
        self.wall_line(cx1 + 1, y0 + 20, x1 - 1, y0 + 20, 'WallPlastitanium', gaps=[(cx1 + 12, y0 + 20)])
        self.ent('DHAirlockRepublicGlassLocked', cx1 + 12, y0 + 20)
        # галерея 700 мм (север): железнодорожная «Дора» 4×11 стволом на запад — казённик на cx1 + 7, ствол над
        # cx1 + 1 … cx1 + 6, тележки и площадка до cx1 + 11; консоль со стулом за площадкой
        gy = y0 + 24
        self.ent('DHHeavyGunBarrel700', cx1 + 6, gy, R90)
        self.ent('DHHeavyGunBreech700', cx1 + 7, gy, R90)
        self.ent('Chair', cx1 + 14, gy)
        self.ent('DHComputerAntiOrbital', cx1 + 14, gy - 1, R180, comps=['    - type: ApcPowerReceiver', '      powerLoad: 50'])
        for i, s in enumerate(['HighExplosive', 'HighExplosive', 'ArmorPiercing', 'Incendiary', 'Cluster']):
            self.ent('DHHeavyShell700' + s, cx1 + 9 + i, y1 - 1)
        self.ent('AlwaysPoweredlightRed', cx1 + 2, gy, R90, free=True)
        # галерея 400 мм (середина): «Дора» 3×8 — казённик на cx1 + 7, площадка до cx1 + 10
        gy = y0 + 16
        self.ent('DHHeavyGunBarrel400', cx1 + 6, gy, R90)
        self.ent('DHHeavyGunBreech400', cx1 + 7, gy, R90)
        self.ent('Chair', cx1 + 13, gy)
        self.ent('DHComputerAntiOrbital', cx1 + 13, gy - 1, R180, comps=['    - type: ApcPowerReceiver', '      powerLoad: 50'])
        for i, s in enumerate(['HighExplosive', 'HighExplosive', 'ArmorPiercing', 'Incendiary', 'Cluster']):
            self.ent('DHHeavyShell400' + s, cx1 + 9 + i, y0 + 19)
        # склад снарядов и мастерская (юг)
        for x in range(cx1 + 2, x1 - 1, 2):
            self.ent('Rack', x, y0 + 11)
        self.ent('DHHeavyShell700HighExplosive', cx1 + 3, y0 + 3)
        self.ent('DHHeavyShell700ArmorPiercing', cx1 + 4, y0 + 3)
        self.ent('DHHeavyShell400HighExplosive', cx1 + 6, y0 + 3)
        self.ent('DHHeavyShell400Cluster', cx1 + 7, y0 + 3)
        self.ent('DHFieldLathe', x1 - 2, y0 + 3)
        self.ent('ClosetToolFilled', x1 - 2, y0 + 6)
        self.ent('VendingMachineYouTool', x1 - 2, y0 + 8) if 'VendingMachineYouTool' in self.known else None
        # западная часть: пост управления, связь, казарма расчёта, резервный дизель
        self.wall_line(x0 + 1, y0 + 9, cx0 - 1, y0 + 9, 'WallPlastitanium', gaps=[(x0 + 3, y0 + 9)])
        self.ent('DHAirlockRepublicGlassLocked', x0 + 3, y0 + 9)
        self.wall_line(x0 + 1, y0 + 18, cx0 - 1, y0 + 18, 'WallPlastitanium', gaps=[(x0 + 3, y0 + 18)])
        self.ent('DHAirlockRepublicGlassLocked', x0 + 3, y0 + 18)
        self.ent('DHFieldPhoneRepublicBattery', x0 + 2, y1 - 2)
        self.ent('TableReinforced', x0 + 3, y1 - 2)
        self.ent('Chair', x0 + 3, y1 - 3, R180)
        self.ent('RMCBedBunk' if 'RMCBedBunk' in self.known else 'Bed', x0 + 1, y0 + 11)
        self.ent('RMCBedBunk' if 'RMCBedBunk' in self.known else 'Bed', x0 + 1, y0 + 13)
        self.ent('RMCBedBunk' if 'RMCBedBunk' in self.known else 'Bed', x0 + 1, y0 + 15)
        self.ent('LockerFreezer', x0 + 4, y0 + 16)
        self.diesel(x0 + 1, y0 + 2)
        self.ent('WeldingFuelTankFull', x0 + 3, y0 + 2)
        # свет (с резервом)
        for (lx0, ly0, lx1, ly1) in ((cx0 + 1, y0 + 1, cx1 - 1, y1 - 1), (cx1 + 1, y0 + 1, x1 - 1, y0 + 11),
                                     (cx1 + 1, y0 + 13, x1 - 1, y0 + 19), (cx1 + 1, y0 + 21, x1 - 1, y1 - 1),
                                     (x0 + 1, y0 + 1, cx0 - 1, y0 + 8), (x0 + 1, y0 + 10, cx0 - 1, y0 + 17),
                                     (x0 + 1, y0 + 19, cx0 - 1, y1 - 1)):
            self.room_lights(lx0, ly0, lx1, ly1, LIGHT_MIL, 6)
        self.post(x0 - 2, y0 - 2)
        self.post(x1 + 2, y0 - 2)
        self.sandbags(x0 + 2, y0 - 5, x1 - 2)

    def sandbags(self, x0, y, x1):
        for x in range(x0, x1 + 1):
            if x % 7 not in (3, 4):
                self.ent('DHSandbagBarricade', x, y)

    def diesel(self, x, y):
        """Резервный дизель-генератор военного объекта (декор: питание — через DHControlPointPowered backup)."""
        proto = 'DHBackupDiesel'
        self.ent(proto, x, y)
        self.decal('RMCDecalOilFloor2', x, y - 1, '#FFFFFF90') if y - 1 > Y0 else None

    # ---------- бункер ПВО ----------

    def pvo(self):
        x0, y0, x1, y1 = PVO
        self.reserve(x0 - 2, y0 - 6, x1 + 2, y1 + 2)
        self.rect(x0 - 2, y0 - 5, x1 + 2, y0 - 1, 'FloorConcrete')
        self.building(x0 + 14, y0, x1, y1 - 10, 'WallPlastitanium', 'FloorSteel',
                      doors=[(x0 + 20, y0, 'DHAirlockRepublicGlassLocked'), (x0 + 21, y0, 'DHAirlockRepublicGlassLocked')],
                      lights=None)
        # открытые позиции: ракетные шахты и спаренные автопушки на бетоне (небо должно быть открыто)
        self.rect(x0, y0, x0 + 13, y1, 'FloorConcrete')
        self.rect(x0 + 14, y1 - 9, x1, y1, 'FloorConcrete')
        for y in (y0 + 4, y0 + 12):
            self.ent('DHMissileSilo', x0 + 4, y, comps=['    - type: DHControlPointPowered', '      point: npp',
                                                        '      backup: 600'])
            self.decal('WarningLine', x0 + 3, y - 2, '#E0C060C0')
        for x in (x0 + 20, x0 + 30):
            self.ent('DHAutocannonTwin30', x, y1 - 4, R90, comps=['    - type: DHControlPointPowered', '      point: npp',
                                                                  '      backup: 600'])
            self.sandbags(x - 2, y1 - 7, x + 2)
        self.sandbags(x0, y1, x0 + 12)
        # склад перехватчиков и снарядов, пост
        cy = y0 + 8
        self.wall_line(x0 + 15, cy, x1 - 1, cy, 'WallPlastitanium', gaps=[(x0 + 24, cy)])
        self.ent('DHAirlockRepublicGlassLocked', x0 + 24, cy)
        for x in range(x0 + 16, x0 + 23, 2):
            self.ent('Rack', x, y0 + 7)
            self.item('DHInterceptorMissile', x, y0 + 7)
        self.ent('Rack', x0 + 27, y0 + 7)
        self.item('DHAutocannonAmmoBox30', x0 + 27, y0 + 7)
        self.item('DHAutocannonAmmoBox30', x0 + 28, y0 + 7) if self.ent('Rack', x0 + 28, y0 + 7) else None
        self.ent('DHFieldPhoneRepublic', x1 - 2, y1 - 11)
        self.ent('TableReinforced', x1 - 3, y1 - 11)
        self.ent('Chair', x1 - 3, y1 - 12, R180)
        self.diesel(x1 - 1, y0 + 2)
        self.ent('RMCBedBunk' if 'RMCBedBunk' in self.known else 'Bed', x0 + 15, cy + 2)
        self.ent('RMCBedBunk' if 'RMCBedBunk' in self.known else 'Bed', x0 + 15, cy + 4)
        self.ent('ClosetToolFilled', x0 + 15, y0 + 2)
        self.room_lights(x0 + 15, y0 + 1, x1 - 1, cy - 1, LIGHT_MIL, 6)
        self.room_lights(x0 + 15, cy + 1, x1 - 1, y1 - 11, LIGHT_MIL, 6)
        for x, y in ((x0 - 1, y0 - 1), (x0 + 13, y1 + 1), (x1 + 1, y0 - 1)):
            self.post(x, y)

    # ---------- РЛС и связь гарнизона ----------

    def rls(self):
        x0, y0, x1, y1 = RLS
        self.reserve(x0 - 2, y0 - 2, x1 + 2, y1 + 2)
        self.rect(x0, y0, x1, y1, 'FloorConcrete')
        self.fence_rect(x0, y0, x1, y1, gates=[(x1, y0 + 6), (x1, y0 + 7), (x1, y0 + 8)])
        self.road_h(x1 + 1, NORTH_ROAD_X[0] - 1, y0 + 6, y0 + 8, tile='FloorDirt', posts=0, line=False)
        # антенны и РЛС
        self.ent('DHRadarRepublic', x0 + 10, y1 - 8, comps=['    - type: DHControlPointPowered', '      point: npp',
                                                            '      backup: 600'])
        self.ent('DHCommsRelayRepublic', x0 + 24, y1 - 8)
        self.sandbags(x0 + 6, y1 - 13, x0 + 14)
        self.sandbags(x0 + 20, y1 - 13, x0 + 28)
        # здание поста
        bx0, by0, bx1, by1 = x0 + 4, y0 + 3, x0 + 26, y0 + 13
        self.building(bx0, by0, bx1, by1, 'WallReinforced', 'FloorDark',
                      doors=[(bx1, by0 + 4, 'DHAirlockRepublicGlassLocked'), (bx0 + 6, by1, 'DHAirlockRepublicGlassLocked')],
                      windows=[(bx0 + 3, by1), (bx0 + 10, by1), (bx0 + 14, by1), (bx0 + 18, by1)],
                      window='ReinforcedWindow', lights=LIGHT_MIL, light_step=6)
        self.wall_line(bx0 + 12, by0 + 1, bx0 + 12, by1 - 1, 'WallReinforced', gaps=[(bx0 + 12, by0 + 4)])
        self.ent('DHAirlockRepublicGlassLocked', bx0 + 12, by0 + 4)
        for i, p in enumerate(('DHComputerOverwatchRepublic', 'ComputerRadar', 'ComputerComms')):
            self.grid(p, bx0 + 14 + i * 3, by1 - 1, backup=True)
            self.ent('ChairOfficeDark', bx0 + 14 + i * 3, by1 - 2)
        self.ent('DHFieldPhoneRepublic', bx1 - 1, by0 + 1)
        self.ent('TableReinforced', bx1 - 2, by0 + 1)
        self.diesel(bx0 + 1, by0 + 1)
        self.ent('WeldingFuelTankFull', bx0 + 2, by0 + 1)
        self.ent('RMCBedBunk' if 'RMCBedBunk' in self.known else 'Bed', bx0 + 1, by1 - 1)
        self.ent('RMCBedBunk' if 'RMCBedBunk' in self.known else 'Bed', bx0 + 3, by1 - 1)
        self.ent('LockerFreezer', bx0 + 5, by1 - 1)
        self.ent('ClosetToolFilled', bx0 + 7, by0 + 1)
        for x, y in ((x0 + 1, y0 + 1), (x1 - 1, y0 + 1), (x0 + 1, y1 - 1), (x1 - 1, y1 - 1)):
            self.post(x, y)

    # ---------- космодром (ЗП-3) ----------

    def spaceport(self):
        x0, y0, x1, y1 = SPACEPORT
        self.reserve(x0 - 3, y0 - 3, x1 + 3, y1 + 3)
        # поле
        fx0, fy0, fx1, fy1 = x0 + 6, y0 + 4, x1 - 2, y1 - 28
        self.rect(fx0, fy0, fx1, fy1, 'FloorConcreteMono')
        zx, zy = ZP3
        self.rect(zx - 8, zy - 8, zx + 8, zy + 8, 'FloorDark')
        self.rect(zx - 6, zy - 6, zx + 6, zy + 6, 'FloorSteelMono')
        self.lz(zx, zy, 'ЗП-3 Космодром')
        for cx, cy in ((fx0 + 10, fy1 - 10), (fx1 - 10, fy1 - 10), (fx0 + 10, fy0 + 10), (fx1 - 10, fy0 + 10)):
            self.rect(cx - 5, cy - 5, cx + 5, cy + 5, 'FloorDark')
            for i in range(-5, 6, 2):
                self.decal('WarnLineGreyscaleN', cx + i, cy - 5, '#E0C060C0')
        for x in range(fx0, fx1 + 1, 10):
            self.post(x, fy1 + 1)
            self.post(x, fy0 - 1)
        # повреждённый шаттл на поле
        self.decal('burnt1', fx0 + 12, fy0 + 14)
        self.decal('burnt3', fx0 + 13, fy0 + 15)
        # терминал (север поля)
        ty0 = fy1 + 3
        self.building(x0 + 2, ty0, x0 + 30, y1, 'WallReinforced', 'FloorSteel',
                      doors=[(x0 + 15, ty0, 'AirlockGlass'), (x0 + 16, ty0, 'AirlockGlass'), (x0 + 30, ty0 + 10, 'AirlockGlass'),
                             (x0 + 8, y1, 'AirlockGlass')],
                      windows=[(x, ty0) for x in range(x0 + 4, x0 + 29, 3) if x not in (x0 + 15, x0 + 16)] +
                              [(x, y1) for x in range(x0 + 4, x0 + 29, 3) if x != x0 + 8],
                      window='ReinforcedWindow')
        for x in range(x0 + 5, x0 + 27, 4):
            for y in (ty0 + 3, ty0 + 6, ty0 + 9):
                self.ent('Chair', x, y, R180)
                self.ent('Chair', x + 1, y, R180)
        for x in range(x0 + 6, x0 + 22, 2):
            self.ent('TableCounterMetal', x, y1 - 7)
        for i, p in enumerate(('VendingMachineCoffee', 'VendingMachineSnack', 'VendingMachineCola')):
            self.grid(p, x0 + 3 + i, y1 - 1)
        self.ent('PottedPlantRandom', x0 + 3, ty0 + 1)
        self.ent('PottedPlantRandom', x0 + 29, ty0 + 1)
        # диспетчерская (контрольная точка)
        self.building(x0 + 32, ty0, x0 + 42, y1 - 6, 'WallReinforced', 'FloorDark',
                      doors=[(x0 + 37, ty0, 'AirlockGlass')],
                      windows=[(x0 + 34, y1 - 6), (x0 + 37, y1 - 6), (x0 + 40, y1 - 6), (x0 + 42, ty0 + 5),
                               (x0 + 32, ty0 + 5)], window='ReinforcedWindow')
        for x, p in ((x0 + 34, 'ComputerAlert'), (x0 + 37, 'ComputerPowerMonitoring'), (x0 + 40, 'ComputerRadar')):
            self.grid(p, x, y1 - 7)
            self.ent('ChairOfficeDark', x, y1 - 8)
        self.ent('DHControlPointSpaceport', x0 + 37, ty0 - 2)
        # ангары (открыты к полю)
        for hx in (x0 + 46, x0 + 62):
            self.building(hx, ty0, hx + 13, y1, 'WallSolid', 'FloorSteel',
                          skip=[(x, ty0) for x in range(hx + 2, hx + 12)], light_step=6)
            self.ent('RMCPropVehicleLoaderTruckWhiteFilled', hx + 3, ty0 + 6,
                     foot=[(hx + 3 + i, ty0 + 6 + j) for i in range(3) for j in range(2)])
            self.ent('RMCPropVehicleSmallTruckBlueCargo', hx + 3, ty0 + 12, foot=[(hx + 3, ty0 + 12), (hx + 4, ty0 + 12)])
            for y in range(ty0 + 3, y1 - 1, 3):
                self.ent('Rack', hx + 12, y)
                self.ent('CrateGenericSteel', hx + 11, y)
        # топливный склад (юго-запад поля)
        self.fence_rect(x0, y0, x0 + 4, y0 + 30, gates=[(x0 + 4, y0 + 14), (x0 + 4, y0 + 15)])
        for y in range(y0 + 3, y0 + 28, 4):
            self.ent('RMCFiltrationMachineSoloTankWaste', x0 + 2, y)
        # стоянка у терминала
        for x in range(x0 + 4, x0 + 28, 3):
            if self.rng.random() < 0.6:
                self.car(x, y1 + 2)
        self.rect(x0 + 2, y1 + 1, x0 + 30, y1 + 3, 'RMCFloorAsphalt')

    # ---------- заброшенный шахтёрский городок (+1 уровень) ----------

    def mines(self):
        x0, y0, x1, y1 = MINES
        self.reserve(x0, y0, x1, y1)
        street = (-46, -43)
        self.road_h(x0, 199, *street, tile='FloorDirt', posts=0, line=False)
        self.road_v(127, 130, y0 + 2, y1, tile='FloorDirt', posts=0, line=False)
        # бараки: двухэтажные (лестница внутри), часть разрушена
        blocks = [(x0, -38, True), (x0 + 19, -38, False), (x0 + 35, -38, True), (x0 + 51, -38, False),
                  (x0, -64, False), (x0 + 19, -64, True), (x0 + 35, -64, False), (x0 + 51, -64, True)]
        for i, (bx, by, two) in enumerate(blocks):
            self.barrack(bx, by, two, i)
        # штольня: короткий тоннель в скальном выступе на северо-востоке городка
        self.rect(x1 - 20, -20, x1 + 2, -16, 'FloorDirt')
        for x in range(x1 - 20, x1 + 12):
            self.decal('RMCDecalRoadTiremark' + str(1 + x % 8), x, -18, '#6A5A4AFF')
        self.building(x1 + 2, -26, x1 + 14, -10, ROCK, 'FloorDirt', skip=[(x1 + 2, y) for y in (-19, -18, -17)],
                      lights=None)
        self.ent('RMCPropVehicleCrawlerMining', x1 - 8, -15, foot=[(x1 - 8 + i, -15) for i in range(3)])
        self.scatter(['CrateGenericSteel', 'RMCBarrelBrown', 'RMCBarrelBlack'], x1 + 4, -24, x1 + 12, -12, 8)
        self.ent('RMCBarrelFire', x1 + 8, -12)
        # пожары в бочках — единственный свет
        for x, y in ((x0 + 18, -44), (x0 + 50, -44), (127, -20), (127, -70), (x0 + 34, -56)):
            self.ent('RMCBarrelFire', x, y)
        # брошенная техника
        self.ent('RMCPropVehicleLongTruckMining' if 'RMCPropVehicleLongTruckMining' in self.known else 'RMCPropVehicleBoxVanMining',
                 x0 + 6, -50, foot=[(x0 + 6 + i, -50 + j) for i in range(4) for j in range(2)])
        self.car(x0 + 30, -45, wreck=True)
        self.car(x0 + 44, -44, wreck=True)
        self.decal('burnt2', x0 + 44, -45)
        # мусор и обломки
        for _ in range(120):
            x, y = self.rng.randint(x0, x1), self.rng.randint(y0, y1)
            if self.free(x, y):
                self.decal(self.rng.choice(['DirtHeavy', 'DirtMedium', 'Rock01', 'Rock03', 'Rock05', 'burnt4']), x, y)
        self.scatter(['Girder', 'GrilleBroken'], x0, y0, x1, y1, 25,
                     test=lambda x, y: self.tiles.get((x, y)) not in ('FloorDirt', 'RMCFloorAsphalt'))

    def barrack(self, bx, by, two, i):
        """Барак 14×10; двухэтажный — с лестницей и вторым этажом на +1."""
        w, h = 14, 10
        broken = i % 3 == 2
        wall = 'WallSolidRust' if 'WallSolidRust' in self.known else 'WallSolid'
        north_door = by < -50
        door_y = by + h if north_door else by
        self.building(bx, by, bx + w, by + h, wall, 'FloorSteel',
                      doors=[(bx + 3, door_y, 'Airlock')],
                      windows=[(bx + 7, door_y), (bx + 11, door_y), (bx, by + 5), (bx + w, by + 5)],
                      window='GrilleBroken' if broken else 'Window', lights=None)
        if broken:
            # обрушенный угол
            for x in range(bx + w - 4, bx + w + 1):
                for y in range(by, by + 4):
                    self.ents = [e for e in self.ents if not (math.floor(e[1]) == x and math.floor(e[2]) == y)]
                    self.occ.discard((x, y))
                    self.roof.discard((x, y))
                    self.tile(x, y, 'FloorDirt')
            self.ent('Girder', bx + w - 4, by + 4)
        # нижний этаж: нары, столы, шкафчики
        for x in range(bx + 6, bx + w - 1, 2):
            self.ent('RMCBedBunkNoSheets' if 'RMCBedBunkNoSheets' in self.known else 'Bed',
                     x, by + h - 1 if not north_door else by + 1)
        self.ent('Table', bx + 9, by + 5)
        self.ent('Chair', bx + 8, by + 5, R90)
        self.ent('ClosetMaintenanceFilledRandom', bx + w - 1, by + 5)
        self.ent('PoweredlightEmpty', bx + 7, by + h - 1, LIGHT_ROT['N'], free=True)
        if not two:
            return
        # лестница (ступени RMC): марш на север у западной стены
        sx, sy = bx + 1, by + 2
        for k in range(2):
            piece = 'Left' if k == 0 else 'Right'
            for r in range(3):
                top = r == 2
                self.ent(('DHZStairsTop' if top else 'DHZStairStep') + piece, sx + k, sy + r, R180)
                self.p1.ents.append(('DHZStairwell' if top else 'DHZStairShaft', sx + k, sy + r, 0.0))
        # второй этаж: пол, стены, окна; проём над маршем
        lv = self.p1
        for x in range(bx, bx + w + 1):
            for y in range(by, by + h + 1):
                shaft = sx <= x <= sx + 1 and sy <= y <= sy + 1
                if not shaft:
                    lv.tiles[(x, y)] = 'FloorSteel'
                lv.roof.add((x, y))
                if x in (bx, bx + w) or y in (by, by + h):
                    win = (x in (bx + 4, bx + 9) and y in (by, by + h)) or (y == by + 5 and x in (bx, bx + w))
                    lv.ents.append(('GrilleBroken' if broken and win else 'Window' if win else wall, x, y, 0.0))
        for x in range(bx + 5, bx + w - 1, 3):
            lv.ents.append(('RMCBedBunkNoSheets' if 'RMCBedBunkNoSheets' in self.known else 'Bed', x, by + h - 1, 0.0))
        lv.ents.append(('ClosetMaintenanceFilledRandom', bx + w - 1, by + 1, 0.0))
        lv.ents.append(('Table', bx + 8, by + 4, 0.0))

    # ---------- АЭС ----------

    def npp(self):
        x0, y0, x1, y1 = NPP
        self.reserve(x0 - 2, y0 - 2, x1 + 2, y1 + 2)
        self.rect(x0, y0, x1, y1, 'FloorConcrete')
        self.fence_rect(x0, y0, x1, y1, gates=[(x0, -123), (x0, -122), (x0 + 56, y1), (x0 + 57, y1), (x0 + 58, y1),
                                               (x0 + 59, y1)])
        # два энергоблока
        for n, bx in enumerate((x0 + 4, x0 + 30)):
            by0, by1 = y0 + 4, y0 + 30
            self.building(bx, by0, bx + 22, by1, 'WallReinforced', 'FloorSteel',
                          doors=[(bx + 11, by1, 'AirlockEngineering'), (bx + 11, by0 + 14, 'AirlockEngineering')],
                          windows=[(bx, by0 + 20), (bx + 22, by0 + 20)], window='ReinforcedWindow', lights=None)
            self.wall_line(bx + 1, by0 + 14, bx + 21, by0 + 14, 'WallReinforced', gaps=[(bx + 11, by0 + 14)])
            self.rect(bx + 6, by0 + 2, bx + 16, by0 + 12, 'FloorTechMaint2')
            self.ent('DHFusionReactor', bx + 11, by0 + 7, foot=[(bx + 10 + i, by0 + 6 + j) for i in range(3) for j in range(3)])
            self.decal('WarningLine', bx + 9, by0 + 4, '#E0C060C0')
            for tx in (bx + 4, bx + 15):
                self.ent('DHGasTurbine', tx, by0 + 20, foot=[(tx + i, by0 + 18 + j) for i in range(2) for j in range(5)])
            self.ent('SMESBasic', bx + 2, by1 - 2)
            self.ent('SMESBasic', bx + 20, by1 - 2)
            self.ent('SignRadiation' if 'SignRadiation' in self.known else 'SignDanger', bx + 10, by0 + 15)
            self.room_lights(bx + 1, by0 + 1, bx + 21, by0 + 13, LIGHT, 6)
            self.room_lights(bx + 1, by0 + 15, bx + 21, by1 - 1, LIGHT, 6)
        # пульт управления (точка захвата рядом)
        cx0, cy0 = x0 + 4, y1 - 9
        self.building(cx0, cy0, cx0 + 18, y1 - 2, 'WallReinforced', 'FloorDark',
                      doors=[(cx0 + 9, cy0, 'AirlockEngineeringGlass')],
                      windows=[(cx0 + 4, y1 - 2), (cx0 + 9, y1 - 2), (cx0 + 14, y1 - 2)], window='ReinforcedWindow')
        for i, p in enumerate(('ComputerPowerMonitoring', 'ComputerAlert', 'ComputerPowerMonitoring', 'ComputerAlert')):
            self.grid(p, cx0 + 3 + i * 4, y1 - 3)
            self.ent('ChairOfficeLight', cx0 + 3 + i * 4, y1 - 4)
        self.ent('DHControlPointPowerPlant', cx0 + 9, cy0 - 2)
        # подстанция и градирни
        sx = x1 - 12
        self.fence_rect(sx, y1 - 14, x1 - 2, y1 - 2, gates=[(sx, y1 - 8)])
        self.ent('DHPowerTransformer', sx + 5, y1 - 8, foot=[(sx + 4 + i, y1 - 9 + j) for i in range(3) for j in range(3)])
        self.ent('SubstationBasic', sx + 2, y1 - 4)
        self.ent('SignDanger', sx + 8, y1 - 13)
        for x in (x0 + 58, x0 + 66):
            self.ent('RMCFiltration', x, y0 + 6, foot=[(x - 1 + i, y0 + 5 + j) for i in range(3) for j in range(3)])
        # водозабор из реки
        for x in range(x0 + 4, x1 - 4, 6):
            self.ent('RMCFiltrationCollectionsPipe1', x, y0 + 1, foot=[(x, y0 + 1), (x + 1, y0 + 1)])
        for x in range(x0 + 4, x1, 10):
            self.post(x, y1 - 1)

    # ---------- ЛЭП ----------

    def pylon_line(self, pts):
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            n = max(abs(bx - ax), abs(by - ay))
            k = max(1, round(n / 12))
            towers = [(round(ax + (bx - ax) * i / k), round(ay + (by - ay) * i / k)) for i in range(k + 1)]
            for t in towers:
                if self.free(*t):
                    self.ent('DHPowerPylon', *t)
                    self.reserve(t[0] - 1, t[1] - 1, t[0] + 1, t[1] + 1)
            vertical = ax == bx
            for i in range(len(towers) - 1):
                (x0, y0), (x1, y1) = towers[i], towers[i + 1]
                if vertical:
                    lo, hi = sorted((y0, y1))
                    for y in range(lo + 2, hi + 2):
                        self.ents.append(('DHPowerLineWire', x0 + 0.5, y + 0.5, 0.0, None, None))
                else:
                    lo, hi = sorted((x0, x1))
                    for x in range(lo + 1, hi):
                        self.ents.append(('DHPowerLineWire', x + 0.5, y0 + 2.5, R90, None, None))

    def power(self):
        # АЭС → на север по восточному берегу старицы → шоссе → на запад вдоль шоссе (к базе, стройке, дамбе)
        self.pylon_line([(NPP[0] + 50, NPP[3] + 4), (192, NPP[3] + 4), (192, 51)])
        self.pylon_line([(192, 51), (100, 51)])
        self.pylon_line([(100, 51), (-90, 51)])
        # ветки: к РЛС, к ПВО и ПКО
        self.pylon_line([(200, 51), (200, RLS[1] + 4)])
        self.pylon_line([(104, 51), (104, 104), (60, 104)])
        self.pylon_line([(56, 104), (28, 104)])

    # ---------- природа ----------

    def nature(self):
        rocks = 0
        res = set(self.res)
        temperate_flora = ['FloraTree', 'FloraTree', 'FloraTreeLarge', 'FloraTreeConifer', 'FloraTreeStump',
                           'RMCBushJungle1', 'RMCBushJungle2', 'RMCBushJungle3']
        forest = ['FloraTreeConifer', 'FloraTreeConifer', 'FloraTree', 'FloraTreeStumpConifer', 'RMCBoulderLarge']
        desert = ['RMCGrassDesert', 'RMCGrassDesertHeavy', 'RMCBoulderLarge', 'RMCCacti1', 'RMCCactus', 'RMCTreeJoshua']
        temperate_flora = [f for f in temperate_flora if f in self.known]
        forest = [f for f in forest if f in self.known]
        desert = [f for f in desert if f in self.known]
        for x in range(X0 + 1, X1):
            for y in range(Y0 + 1, Y1):
                c = (x, y)
                if c in res or c in self.water or c in self.occ:
                    continue
                m = self.mountain(x, y)
                f = fbm(x, y, 3)
                t = self.temperate(x, y)
                if m >= 0.8:
                    # сплошной массив с редкими карманами
                    if f < 0.34 and m < 1:
                        pass
                    elif not (f < 0.3):
                        self.ent(ROCK, x, y)
                        rocks += 1
                        continue
                elif m >= 0.5:
                    if f > 0.56:
                        self.ent(ROCK, x, y)
                        rocks += 1
                        continue
                    if t and forest and self.rng.random() < 0.08:
                        self.ent(self.rng.choice(forest), x, y)
                        continue
                else:
                    if not t and f > 0.71 and not any((x + i, y + j) in res for i in (-2, 0, 2) for j in (-2, 0, 2)):
                        self.ent(ROCK, x, y)
                        rocks += 1
                        continue
                if t:
                    if temperate_flora and self.rng.random() < (0.05 if noise(x, y, 20, 51) > 0.6 else 0.012):
                        self.ent(self.rng.choice(temperate_flora), x, y)
                    elif self.rng.random() < 0.06:
                        self.decal(self.rng.choice(['Grassa1', 'Grassa3', 'Grassb2', 'Grassc1', 'Grassd3', 'Grasse2',
                                                    'Flowersy1', 'Flowerspv2', 'Flowersbr2', 'Bushd1', 'Bushe2']), x, y)
                else:
                    if desert and self.rng.random() < 0.005:
                        self.ent(self.rng.choice(desert), x, y)
                    elif self.rng.random() < 0.012:
                        self.decal(self.rng.choice(['Rock01', 'Rock02', 'Rock04', 'Rock06', 'RMCDecalRock3',
                                                    'RMCDecalRock7']), x, y)
        print('Скал:', rocks)

    def edge(self):
        for x in range(X0, X1 + 1):
            for y in (Y0, Y1):
                self.ents.append((EDGE, x + 0.5, y + 0.5, 0.0, None, None))
        for y in range(Y0 + 1, Y1):
            for x in (X0, X1):
                self.ents.append((EDGE, x + 0.5, y + 0.5, 0.0, None, None))

    def place_water(self):
        n = 0
        for (x, y), deep in self.water.items():
            if not self.inside(x, y) or (x, y) in self.occ or self.tiles.get((x, y)) not in (DIRT, SAND):
                continue
            self.ents.append(('DHWaterDeep' if deep else 'DHWaterShallow', x + 0.5, y + 0.5, 0.0, None, None))
            n += 1
        print('Воды:', n)

    # ---------- климат ----------

    def weather_free_areas(self):
        """Прямоугольники умеренной зоны (без песчаных бурь): строки с одинаковой границей сливаются."""
        rects = []
        cur = None
        for y in range(Y0, Y1 + 1):
            e = self.temperate_edge[y]
            if cur and cur[2] == e and cur[3] == y - 1:
                cur[3] = y
            else:
                if cur:
                    rects.append(cur)
                cur = [X0, y, e, y]
        rects.append(cur)
        return rects


def h_mid():
    return (HIGHWAY_Y[0] + HIGHWAY_Y[1]) / 2


INDOOR = {'FloorSteel', 'FloorDark', 'FloorWhite', 'FloorWood', 'FloorTechMaint2', 'Plating', 'FloorSteelMono',
          'FloorReinforced', 'FloorDarkMono', 'FloorSteelDirty', 'FloorFreezer', 'FloorMono'}
KNOWN_TILES = set()


def fmt(v):
    s = repr(float(v))
    return s[:-2] if s.endswith('.0') else s


# ------------------------------------------------------------------ запись

TILE_IDS = {}


def tile_id(name):
    if name not in TILE_IDS:
        TILE_IDS[name] = len(TILE_IDS)
    return TILE_IDS[name]


def encode_chunks(tiles, fill=None):
    space = tile_id('Space')
    out = {}
    for (x, y), name in tiles.items():
        out.setdefault((x // 16, y // 16), {})[(x % 16, y % 16)] = name
    if fill:
        for cx in range(X0 // 16, X1 // 16 + 1):
            for cy in range(Y0 // 16, Y1 // 16 + 1):
                out.setdefault((cx, cy), {})
    res = []
    for (cx, cy), cells in sorted(out.items()):
        data = bytearray()
        for i in range(256):
            lx, ly = i % 16, i // 16
            name = cells.get((lx, ly))
            if name is None and fill:
                name = fill
            tid = tile_id(name) if name else space
            x, y = cx * 16 + lx, cy * 16 + ly
            variant = random.Random(x * 73856093 ^ y * 19349663).randrange(7) if name and 'Sand' in name else 0
            data += struct.pack('<iBBB', tid, 0, variant, 0)
        res.append((cx, cy, base64.b64encode(bytes(data)).decode()))
    return res


def roof_data(cells):
    out = {}
    for x, y in cells:
        cx, cy = x // 8, y // 8
        out[(cx, cy)] = out.get((cx, cy), 0) | (1 << ((x % 8) + (y % 8) * 8))
    lines = ['    - type: Roof']
    if not out:
        return lines + ['      data: {}']
    lines.append('      data:')
    for (cx, cy), mask in sorted(out.items()):
        lines.append(f'        {cx},{cy}: {mask}')
    return lines


def decal_grid(decals):
    lines = ['    - type: DecalGrid', '      chunkCollection:', '        version: 2', '        nodes:']
    by = {}
    for did, x, y, color, angle in decals:
        by.setdefault((did, color, angle), []).append((x, y))
    i = 0
    for (did, color, angle), pts in sorted(by.items()):
        lines += ['        - node:']
        if angle:
            lines.append(f'            angle: {angle} rad')
        lines += [f"            color: '{color}'", f'            id: {did}', '          decals:']
        for x, y in pts:
            lines.append(f'            {i}: {x},{y}')
            i += 1
    if not by:
        lines[-1] = '        nodes: []'
    return lines


def write_surface(w, path):
    TILE_IDS.clear()
    tile_id('Space')
    ch = encode_chunks(w.tiles, fill=SAND)
    head = ['meta:', '  format: 7', '  category: Map', '  engineVersion: 293.0.1', '  forkId: ""', '  forkVersion: ""',
            '  time: 11/12/2026 12:00:00', '  entityCount: ENTITY_COUNT', 'maps:', '- 1', 'grids:', '- 1',
            'orphans: []', 'nullspace: []', 'tilemap:']
    for name, idx in sorted(TILE_IDS.items(), key=lambda kv: kv[1]):
        head.append(f'  {idx}: {name}')
    L = head + ['entities:', '- proto: ""', '  entities:', '  - uid: 1', '    components:', '    - type: MetaData',
                '      name: Map Entity', '    - type: Transform', '    - type: Map', '      mapPaused: True',
                '    - type: GridTree', '    - type: Broadphase', '    - type: ChunkContainer',
                '      chunkEntities: []', '    - type: OccluderTree', '    - type: MapGrid', '      chunks:']
    for cx, cy, data in ch:
        L += [f'        {cx},{cy}:', f'          ind: {cx},{cy}', f'          tiles: {data}', '          version: 7']
    L += ['    - type: SpreaderGrid', '    - type: GridPathfinding', '    - type: Gravity', '      inherent: True',
          '      gravityShakeSound: !type:SoundPathSpecifier', '        path: /Audio/Effects/alert.ogg',
          '      enabled: True', '    - type: MapLight', "      ambientLightColor: '#D8B059FF'"]
    L += roof_data(w.roof)
    L += ['    - type: LightCycle', '    - type: SunShadow', '    - type: SunShadowCycle', '      directions:',
          '      - direction: 0,3', '      - ratio: 0.25', '        direction: -3,-0.1', '        alpha: 0.5',
          '      - ratio: 0.5', '        direction: 0,-3', '        alpha: 0.8', '      - ratio: 0.75',
          '        direction: 3,-0.1', '        alpha: 0.5', '    - type: MapAtmosphere', '      space: False',
          '      mixture:', '        temperature: 293.15', '        volume: 2500', '        moles:',
          '          Oxygen: 21.82478', '          Nitrogen: 82.10312', '        immutable: True',
          '    - type: ExplosionAirtightGrid', '    - type: RadiationGridResistance']
    L += decal_grid(w.decals)
    L += ['    - type: DHWeatherFreeArea', '      areas:']
    for x0, y0, x1, y1 in w.weather_free_areas():
        L.append(f'      - {x0},{y0},{x1},{y1 + 1}')
    L += ['    - type: BecomesStation', '      id: Terminus']

    groups = {}
    for proto, x, y, rot, name, comps in w.ents:
        groups.setdefault(proto, []).append(('gen', x, y, rot, name, comps))
    for proto, lines in w.base_ents:
        groups.setdefault(proto, []).append(('base', lines))
    uid = 2
    for proto in sorted(groups):
        L += [f'- proto: {proto}', '  entities:']
        for g in groups[proto]:
            L += [f'  - uid: {uid}', '    components:']
            uid += 1
            if g[0] == 'base':
                L += g[1]
                continue
            _, x, y, rot, name, comps = g
            if name:
                L += ['    - type: MetaData', f'      name: {name}']
            L += ['    - type: Transform', '      parent: 1', f'      pos: {fmt(x)},{fmt(y)}']
            if rot:
                L.append(f'      rot: {rot!r} rad')
            L += comps or []
    L.append('...')
    text = '\n'.join(L).replace('ENTITY_COUNT', str(uid - 1)) + '\n'
    open(path, 'w', encoding='utf-8', newline='\n').write(text)
    print(f'{os.path.relpath(path, ROOT)}: тайлов {len(w.tiles)}, сущностей {uid - 1}, тайлмап {len(TILE_IDS)}')
    return set(groups), set(TILE_IDS)


def write_level(lv, path, name):
    TILE_IDS.clear()
    tile_id('Space')
    ch = encode_chunks(lv.tiles)
    L = ['meta:', '  format: 7', '  category: Grid', '  engineVersion: 293.0.1', '  forkId: ""', '  forkVersion: ""',
         '  time: 11/12/2026 12:00:00', '  entityCount: ENTITY_COUNT', 'maps: []', 'grids:', '- 1', 'orphans:', '- 1',
         'nullspace: []', 'tilemap:']
    for tname, idx in sorted(TILE_IDS.items(), key=lambda kv: kv[1]):
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
    L += roof_data(lv.roof)
    groups = {}
    for proto, x, y, rot in lv.ents:
        if (x, y) in lv.tiles or proto == 'DHZStairShaft' or proto == 'DHZStairwell':
            groups.setdefault(proto, []).append((x, y, rot))
    uid = 2
    for proto in sorted(groups):
        L += [f'- proto: {proto}', '  entities:']
        for x, y, rot in groups[proto]:
            L += [f'  - uid: {uid}', '    components:', '    - type: Transform']
            if rot:
                L.append(f'      rot: {rot!r} rad')
            L += [f'      pos: {x + 0.5},{y + 0.5}', '      parent: 1']
            uid += 1
    L.append('...')
    open(path, 'w', encoding='utf-8', newline='\n').write('\n'.join(L).replace('ENTITY_COUNT', str(uid - 1)) + '\n')
    print(f'{os.path.relpath(path, ROOT)}: тайлов {len(lv.tiles)}, сущностей {uid - 1}')
    return set(groups), set(TILE_IDS)


def shift_bunker(src, dst):
    """Сдвинуть бункер под базой на BASE_SHIFT (тайлы и сущности на гриде)."""
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    dx, dy = BASE_SHIFT
    text = open(src, encoding='utf-8-sig').read()
    lines = text.split('\n')
    out = []
    i = 0
    in_chunks = False
    chunks = {}
    order = []
    while i < len(lines):
        l = lines[i]
        m = re.match(r'^        (-?\d+),(-?\d+):$', l)
        if m and i + 1 < len(lines) and lines[i + 1].startswith('          ind:'):
            cx, cy = int(m.group(1)), int(m.group(2))
            data = lines[i + 2].strip()[len('tiles: '):]
            ver = int(lines[i + 3].strip()[len('version: '):])
            chunks[(cx, cy)] = (base64.b64decode(data), ver)
            if not in_chunks:
                in_chunks = True
                out.append('@@CHUNKS@@')
            i += 4
            continue
        m = re.match(r'^(\s+pos: )(-?[\d.]+),(-?[\d.]+)$', l)
        if m:
            l = f'{m.group(1)}{fmt(float(m.group(2)) + dx)},{fmt(float(m.group(3)) + dy)}'
        out.append(l)
        i += 1
    # тайлы: пересобрать чанки со сдвигом
    cells = {}
    for (cx, cy), (data, ver) in chunks.items():
        size = len(data) // 256
        for k in range(256):
            if size >= 7:
                tid, flags, var, rot = struct.unpack_from('<iBBB', data, k * size)
            else:
                tid, flags, var = struct.unpack_from('<iBB', data, k * size)
                rot = 0
            x, y = cx * 16 + k % 16 + dx, cy * 16 + k // 16 + dy
            cells[(x, y)] = (tid, flags, var, rot)
    tm = re.search(r'^tilemap:\n((?:  .*\n)+)', text, re.M)
    space = None
    for l in tm.group(1).splitlines():
        k, v = l.strip().split(': ', 1)
        if v == 'Space':
            space = int(k)
    new = {}
    for (x, y), v in cells.items():
        new.setdefault((x // 16, y // 16), {})[(x % 16, y % 16)] = v
    block = []
    for (cx, cy), cc in sorted(new.items()):
        data = bytearray()
        for k in range(256):
            v = cc.get((k % 16, k // 16), (space, 0, 0, 0))
            data += struct.pack('<iBBB', *v)
        block += [f'        {cx},{cy}:', f'          ind: {cx},{cy}',
                  f'          tiles: {base64.b64encode(bytes(data)).decode()}', '          version: 7']
    idx = out.index('@@CHUNKS@@')
    out[idx:idx + 1] = block
    open(dst, 'w', encoding='utf-8', newline='\n').write('\n'.join(out))
    print(f'Бункер сдвинут на {BASE_SHIFT}: {os.path.relpath(dst, ROOT)}')


def known_protos():
    ids, abstract, tiles = set(), set(), set()
    for f in glob.glob(ROOT + '/Resources/Prototypes/**/*.yml', recursive=True):
        kind, cur, is_abs = None, None, False
        for line in open(f, encoding='utf-8-sig', errors='ignore'):
            m = re.match(r'^- type: (\w+)', line)
            if m:
                if kind == 'entity' and cur:
                    (abstract if is_abs else ids).add(cur)
                if kind in ('tile', 'tileAlias') and cur and not is_abs:
                    tiles.add(cur)
                kind, cur, is_abs = m.group(1), None, False
                continue
            m = re.match(r'^  id: (\S+)', line)
            if m:
                cur = m.group(1)
            if re.match(r'^  abstract: true', line):
                is_abs = True
        if kind == 'entity' and cur:
            (abstract if is_abs else ids).add(cur)
        if kind in ('tile', 'tileAlias') and cur and not is_abs:
            tiles.add(cur)
    return ids, tiles


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--bunker-in', help='исходный бункер (на старом месте) — сдвинуть под новую базу')
    args = ap.parse_args()

    known, tiles = known_protos()
    KNOWN_TILES.update(tiles)
    w = World(known)
    w.geography()
    w.base()
    w.dam()
    w.tanker()
    w.roads()
    w.village()
    w.field()
    w.construction()
    w.pko()
    w.pvo()
    w.rls()
    w.spaceport()
    w.mines()
    w.npp()
    w.power()
    w.ground()
    w.place_water()
    w.nature()
    w.edge()
    # Метка телепорта призраков у штаба (палатка командования базы).
    w.ent('DHWarpPointTerminus', 133, 62, free=True)
    if w.missing:
        print('НЕТ ПРОТОТИПОВ (пропущены):', sorted(w.missing))

    protos, used_tiles = write_surface(w, os.path.join(OUT, 'terminus.yml'))
    p1, t1 = write_level(w.p1, os.path.join(OUT, 'terminus_p1.yml'), 'Terminus +1')
    bad = sorted(p for p in (protos | p1) if p not in known)
    badt = sorted(t for t in (used_tiles | t1) if t not in tiles and t != 'Space')
    if bad or badt:
        print('ОШИБКИ: прототипы', bad, 'тайлы', badt)
    if args.bunker_in:
        shift_bunker(args.bunker_in, os.path.join(OUT, 'terminus_bunker.yml'))


if __name__ == '__main__':
    main()
