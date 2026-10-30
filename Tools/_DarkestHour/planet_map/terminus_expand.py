#!/usr/bin/env python3
"""
DarkestHour: расширение карты «Терминус» до 384×384 с локациями (решение автора 2026-10-30).

Берёт текущую карту (нарисованная база и окрестности остаются как есть) и дорисовывает вокруг:
  * море на севере с пляжем; дамба с турбинами отделяет море от озера; из озера на юг течёт река;
  * шоссе с запада на восток, мост через реку обрушен; брод на юге;
  * деревню (запад), стройку (юго-запад), космодром (восток, за рекой), АЭС с двумя энергоблоками (север),
    подстанцию с трансформатором и ЛЭП до базы;
  * терминалы зон захвата, новые маяки ЗП (старые убираются), скалы и пустынную флору на новой земле;
  * ПВО и фонари базы питаются от сети АЭС (DHControlPointPowered).

Запуск (из корня репозитория), потом обязательно prepare.py — он ставит край карты и застилает дыры:
  python3 Tools/_DarkestHour/planet_map/terminus_expand.py Resources/Maps/_DarkestHour/Assault/terminus.yml -o /tmp/t.yml
  python3 Tools/_DarkestHour/planet_map/prepare.py /tmp/t.yml -o Resources/Maps/_DarkestHour/Assault/terminus.yml

Повторно на уже расширенной карте не запускать — скрипт рассчитан на исходную (x -112..63, y -80..79).
"""
import argparse
import math
import os
import random
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from prepare import CHUNK, TILE_BYTES, MapFile, fmt, pos_of  # noqa: E402

# Прямоугольник новой карты (кратен чанкам 16×16)
X0, X1, Y0, Y1 = -192, 191, -176, 207
GROUND = 'FloorAsteroidSand'
ROCK = 'WallRockSand'

R90, R180, R270 = math.pi / 2, math.pi, -math.pi / 2

# --- география ---
SEA_SHALLOW = 186          # y ≥ — прибой
SEA_DEEP = 190             # y ≥ — глубоко
BEACH = 172                # y ≥ — пляж
BAY = (78, 142)            # залив у дамбы: море подходит до y 172
DAM = (74, 146, 164, 171)  # x0, x1, y0, y1
LAKE_C, LAKE_R = (110, 140), (36, 37)   # верх эллипса срезан дамбой
RIVER_TOP = 104
HIGHWAY = (-103, -98)      # асфальт
FORD = (-152, -146)        # брод: река мелкая


def river_x(y):
    return 110 + 8 * math.sin(y / 37) + 4 * math.sin(y / 13 + 1)


def in_lake(x, y, grow=0.0):
    return ((x - LAKE_C[0]) / (LAKE_R[0] + grow)) ** 2 + ((y - LAKE_C[1]) / (LAKE_R[1] + grow)) ** 2 <= 1


class Gen:
    def __init__(self, mf):
        self.mf = mf
        self.tiles = {}
        self.ents = []          # (proto, x, y, rot, name, comps)
        self.occ = set()        # занятые клетки новыми сущностями
        self.reserved = set()   # клетки, где не ставить скалы и флору
        self.rng = random.Random(20261030)
        self.old_ents = mf.entities()
        self.old_at = {}
        for e in self.old_ents:
            p = pos_of(e)
            if p is None or e['parent'] != str(mf.root):
                continue
            self.old_at.setdefault((math.floor(p[0]), math.floor(p[1])), []).append(e)
        self.remove = set()
        self.add_comps = {}     # uid -> [строки компонентов]
        self.skipped = []       # не поставлено: клетка занята старой постройкой

    # ---------- примитивы ----------

    def tile(self, x, y, name):
        if X0 < x < X1 and Y0 < y < Y1:
            self.tiles[(x, y)] = name
            self.reserved.add((x, y))

    def rect(self, x0, y0, x1, y1, name):
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                self.tile(x, y, name)

    def reserve(self, x0, y0, x1, y1):
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                self.reserved.add((x, y))

    def clear(self, x, y):
        """Убрать старые скалы с клетки (новое строение важнее)."""
        for e in self.old_at.get((x, y), []):
            if e['proto'] and ('Rock' in e['proto'] or e['proto'] == 'DHWorldEdgeSand'):
                self.remove.add(e['uid'])

    def blocked_old(self, x, y):
        return any(e['proto'] and 'Rock' not in e['proto'] and e['proto'] != 'DHWorldEdgeSand'
                   for e in self.old_at.get((x, y), []))

    def ent(self, proto, x, y, rot=0.0, name=None, comps=None, center=True, foot=None):
        """Сущность на клетке (x, y); foot — занимаемые клетки (по умолчанию одна)."""
        px, py = (x + 0.5, y + 0.5) if center else (x, y)
        if any(self.blocked_old(*c) for c in foot or [(x, y)]) and not proto.startswith('FenceMetalGate'):
            self.skipped.append((proto, x, y))
            return None
        self.ents.append((proto, px, py, rot, name, comps))
        for c in foot or [(x, y)]:
            self.occ.add(c)
            self.reserved.add(c)
            self.clear(*c)
        return len(self.ents) - 1

    def free(self, x, y):
        return (x, y) not in self.occ and not self.blocked_old(x, y)

    def building(self, x0, y0, x1, y1, wall, floor, doors=(), windows=(), window='Window', door='Airlock',
                 skip=()):
        """Прямоугольное здание: пол везде, стены по периметру; doors/windows — клетки на периметре."""
        self.rect(x0, y0, x1, y1, floor)
        self.reserve(x0 - 1, y0 - 1, x1 + 1, y1 + 1)
        doors = {d[:2]: (d[2] if len(d) > 2 else door) for d in doors}
        windows = set(windows)
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                if x not in (x0, x1) and y not in (y0, y1):
                    continue
                if (x, y) in skip:
                    continue
                if (x, y) in doors:
                    self.ent(doors[(x, y)], x, y)
                elif (x, y) in windows:
                    self.ent(window, x, y)
                else:
                    self.ent(wall, x, y)

    def fence_rect(self, x0, y0, x1, y1, gates=(), proto='FenceMetal', skip=()):
        """Забор по периметру: Straight/Corner/Gate с поворотами как на исходной карте."""
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

    def scatter(self, protos, x0, y0, x1, y1, n, foot=1):
        """n случайных сущностей в прямоугольнике на свободных клетках."""
        placed = 0
        for _ in range(n * 20):
            if placed >= n:
                break
            x, y = self.rng.randint(x0, x1), self.rng.randint(y0, y1)
            cells = [(x + i, y + j) for i in range(foot) for j in range(foot)]
            if all(self.free(*c) for c in cells):
                self.ent(self.rng.choice(protos), x, y, foot=cells)
                placed += 1

    def car(self, x, y, proto=None, flipped=None):
        """Легковушка Моно-Супрон: две клетки на восток от якоря."""
        colors = ['Orange', 'Black', 'Blue', 'Brown', 'DesatBlue', 'Green', 'LightBlue', 'Red', 'Turquoise', 'Purple']
        if proto is None:
            proto = 'RMCPropVehicleMonoSupron' + self.rng.choice(colors)
            if flipped if flipped is not None else self.rng.random() < 0.5:
                proto += 'Flipped'
        cells = [(x, y), (x + 1, y)]
        if all(self.free(*c) for c in cells):
            self.ent(proto, x, y, foot=cells)

    def container(self, x, y, brand, length=4):
        """Контейнер из сегментов Left/Middle…/Right, высота две клетки."""
        cells = [(x + i, y + j) for i in range(length) for j in range(2)]
        if not all(self.free(*c) for c in cells):
            return
        for i in range(length):
            part = 'Left' if i == 0 else 'Right' if i == length - 1 else 'Middle'
            self.ent(f'RMCContainer{brand}{part}', x + i, y, foot=[(x + i, y), (x + i, y + 1)])

    def powered(self, proto_names, point='npp'):
        """Добавить DHControlPointPowered существующим сущностям старой карты."""
        for e in self.old_ents:
            if e['proto'] in proto_names:
                self.add_comps[e['uid']] = ['    - type: DHControlPointPowered', f'      point: {point}']

    # ---------- география ----------

    def geography(self):
        for x in range(X0 + 1, X1):
            # море и пляж
            sea_from = BEACH if BAY[0] <= x <= BAY[1] else SEA_SHALLOW
            for y in range(BEACH - 6, Y1):
                if y >= sea_from:
                    deep = y >= (sea_from + 4 if sea_from == BEACH else SEA_DEEP)
                    self.water(x, y, deep)
                elif y >= BEACH - 4 and not (DAM[0] <= x <= DAM[1]):
                    self.tile(x, y, 'FloorDesert')
            # река
            for y in range(Y0 + 1, RIVER_TOP + 6):
                cx = river_x(y)
                d = abs(x - cx)
                if d <= 5.5:
                    deep = d <= 3.5 and not (FORD[0] <= y <= FORD[1])
                    self.water(x, y, deep)
                elif d <= 9:
                    self.tile(x, y, 'FloorPlanetGrass')
                    self.reserved.add((x, y))
        # озеро
        for x in range(LAKE_C[0] - LAKE_R[0] - 8, LAKE_C[0] + LAKE_R[0] + 9):
            for y in range(LAKE_C[1] - LAKE_R[1] - 8, DAM[2]):
                if in_lake(x, y):
                    self.water(x, y, in_lake(x, y, -3))
                elif in_lake(x, y, 6):
                    self.tile(x, y, 'FloorPlanetGrass')
                    self.reserved.add((x, y))

    def water(self, x, y, deep):
        if not (X0 < x < X1 and Y0 < y < Y1) or (x, y) in self.occ:
            return
        self.tile(x, y, 'FloorPlanetDirt')
        self.ent('DHWaterDeep' if deep else 'DHWaterShallow', x, y)

    def dam(self):
        x0, x1, y0, y1 = DAM
        # тело дамбы: южная половина — машинный ряд турбин, северная — дорога
        self.rect(x0, y0, x1, y1, 'FloorConcrete')
        self.rect(x0, y0 + 4, x1, y1, 'FloorAsphalt')
        # убрать воду под дамбой
        self.ents = [e for e in self.ents if not (x0 <= math.floor(e[1]) <= x1 and y0 <= math.floor(e[2]) <= y1)]
        self.occ = {c for c in self.occ if not (x0 <= c[0] <= x1 and y0 <= c[1] <= y1)}
        for x in range(x0, x1 + 1):
            self.ent('Railing', x, y1, R180)        # над морем
            if x % 12 not in (4, 5, 6):
                self.ent('Railing', x, y0, 0.0)     # над озером
        # турбины (декор RMC) и опоры
        for x in range(x0 + 8, x1 - 6, 12):
            if abs(x - 110) <= 3:
                continue
            self.ent('RMCPropTurbine', x, y0 + 2, foot=[(x, y0 + 1), (x, y0 + 2), (x, y0 + 3)])
            self.ent('RMCPropTurbineStrutsBorder', x, y0 + 2, foot=[])
            self.ent('RMCPropTurbineStrutsLeft', x - 1, y0 + 2, foot=[(x - 1, y0 + 2)])
            self.ent('RMCPropTurbineStrutsRight', x + 1, y0 + 2, foot=[(x + 1, y0 + 2)])
        # фонари вдоль дороги
        for x in range(x0 + 2, x1, 10):
            self.ent('DHLightPostGrid', x, y1 - 1)
        self.ent('DHControlPointDam', 110, y0 + 2)
        self.reserve(x0 - 2, y0 - 2, x1 + 2, y1 + 2)

    def roads(self):
        # шоссе запад → восток с обрушенным мостом
        h0, h1 = HIGHWAY
        for x in range(X0 + 1, X1):
            cx = river_x((h0 + h1) / 2)
            d = abs(x - cx)
            if d <= 7.5:
                if d <= 2.5:
                    continue                      # пролёт обрушен — вода
                # мост поверх воды
                self.ents = [e for e in self.ents if not (math.floor(e[1]) == x and h0 - 1 <= math.floor(e[2]) <= h1 + 1)]
                self.occ = {c for c in self.occ if not (c[0] == x and h0 - 1 <= c[1] <= h1 + 1)}
                self.rect(x, h0 - 1, x, h1 + 1, 'FloorReinforced')
                self.ent('Railing', x, h0 - 1, 0.0)
                self.ent('Railing', x, h1 + 1, R180)
                if 2.5 < d <= 3.5:
                    for y in range(h0, h1 + 1, 2):
                        self.ent('Girder', x, y)
                continue
            for y in range(h0 - 1, h1 + 2):
                self.tile(x, y, 'FloorAsphalt' if h0 <= y <= h1 else 'FloorConcrete')
                self.clear(x, y)
            if x % 16 == 0:
                self.ent('DHLightPostGrid', x, h1 + 2)
        # пробка из сгоревших машин у моста (с обеих сторон)
        cx = round(river_x((h0 + h1) / 2))
        self.ent('RMCCarPileup', cx - 14, h0, foot=[(cx - 14 + i, h0 + j) for i in range(4) for j in range(1, 3)])
        self.ent('RMCCarPileup', cx + 9, h0 + 2, foot=[(cx + 9 + i, h0 + 2 + j) for i in range(4) for j in range(1, 3)])
        for x in (cx - 24, cx - 19, cx + 16, cx + 22):
            self.car(x, h0 + self.rng.choice((0, 2, 4)))
        # брошенные машины вдоль шоссе
        for x in range(X0 + 12, X1 - 12, 23):
            if abs(x - cx) > 30 and self.rng.random() < 0.5:
                self.car(x, self.rng.choice((h0, h1)))

        # дорога с базы на север — к АЭС, подстанции и дамбе
        for y in range(10, DAM[3] + 1):
            for x in range(-8, -3):
                self.tile(x, y, 'FloorAsphalt')
                self.clear(x, y)
        for x in range(-8, DAM[0]):
            for y in range(DAM[2] + 4, DAM[3] + 1):
                self.tile(x, y, 'FloorAsphalt')
                self.clear(x, y)
        # с базы на юг до шоссе (в южном заборе базы — ворота)
        for y in range(h1 + 1, -26):
            for x in range(-8, -3):
                self.tile(x, y, 'FloorAsphalt')
                self.clear(x, y)
        for e in self.old_ents:
            p = pos_of(e)
            if p and e['proto'] == 'FenceMetalStraight' and p[1] == -25.5 and p[0] in (-6.5, -5.5, -4.5):
                self.remove.add(e['uid'])
        for x in (-7, -6, -5):
            self.ent('FenceMetalGate', x, -26, R180)
        # от восточного конца дамбы к космодрому
        for x in range(DAM[1], 161):
            for y in range(DAM[2] + 4, DAM[3] + 1):
                self.tile(x, y, 'FloorAsphalt')
        for y in range(60, DAM[3] + 1):
            for x in range(157, 161):
                self.tile(x, y, 'FloorAsphalt')
        # к деревне и стройке от шоссе
        for y in range(h1 + 1, 30):
            for x in range(-153, -149):
                self.tile(x, y, 'FloorDirt')
        for y in range(-118, h0):
            for x in range(-143, -139):
                self.tile(x, y, 'FloorAsphalt')
        # фонари вдоль северной дороги
        for y in range(16, DAM[2], 14):
            self.ent('DHLightPostGrid', -9, y)

    # ---------- локации ----------

    def village(self):
        X, Y = -184, 30
        self.rect(X, Y, -120, 96, 'FloorPlanetGrass')
        self.reserve(X - 3, Y - 3, -117, 99)
        for y in range(Y, 97):
            for x in range(-153, -149):
                self.tile(x, y, 'FloorDirt')
        # площадь
        self.rect(-160, 58, -142, 70, 'FloorDirt')
        self.ent('DHControlPointVillage', -151, 64)
        for x, y in ((-158, 60), (-144, 60), (-158, 68), (-144, 68)):
            self.ent('FloraTree', x, y)
        # дома по обе стороны улицы
        houses = [(-172, 34), (-172, 46), (-172, 74), (-172, 86), (-145, 34), (-145, 46), (-145, 74), (-145, 86)]
        for i, (hx, hy) in enumerate(houses):
            west = hx < -153
            door = (hx + 9, hy + 3) if west else (hx, hy + 3)
            self.building(hx, hy, hx + 9, hy + 7, 'WallWood', 'FloorWood', doors=[(*door, 'WoodDoor')],
                          windows=[(hx + 3, hy), (hx + 6, hy), (hx + 3, hy + 7), (hx + 6, hy + 7)])
            ix = hx + 2 if west else hx + 7
            self.ent('Bed', hx + 1 if west else hx + 8, hy + 6)
            self.ent('Bed', hx + 1 if west else hx + 8, hy + 5)
            self.ent('TableWood', ix, hy + 2)
            self.ent('ChairWood', ix + (1 if west else -1), hy + 2, R90 if west else R270)
            self.ent('Dresser', hx + 4, hy + 6)
            if i % 3 == 0:
                self.ent('Fireplace', hx + 6, hy + 6)
            # огород
            gx = hx - 6 if west else hx + 11
            self.fence_rect(gx, hy + 1, gx + 4, hy + 6, proto='FenceWoodSmall', gates=[(gx + 4 if west else gx, hy + 3)])
            self.rect(gx + 1, hy + 2, gx + 3, hy + 5, 'FloorPlanetDirt')
        # фельдшерский пункт
        self.building(-172, 58, -162, 69, 'WallSolid', 'FloorWhite', doors=[(-162, 64, 'AirlockGlass')],
                      windows=[(-169, 69), (-166, 69), (-169, 58), (-166, 58)])
        for y in (60, 62, 66):
            self.ent('MedicalBed', -170, y)
        self.ent('LockerMedicalFilled', -164, 67)
        self.ent('SinkWide', -166, 67)
        self.ent('TableReinforced', -164, 60)
        # колодец и бочки
        self.ent('RMCBarrelBlue', -146, 58)
        self.ent('RMCBarrelGreen', -145, 58)
        for x in (-155, -146):
            for y in (40, 52, 80, 92):
                self.ent('DHLightPostGrid', x, y)
        self.car(-148, 50)
        self.car(-157, 84)

    def construction(self):
        x0, y0, x1, y1 = -176, -172, -108, -118
        self.rect(x0, y0, x1, y1, 'FloorDirt')
        self.fence_rect(x0, y0, x1, y1, gates=[(-142, y1), (-141, y1), (-140, y1), (-139, y1)])
        # каркас здания: бетонный пол, стены частично, балки
        bx0, by0, bx1, by1 = -166, -162, -132, -138
        self.rect(bx0, by0, bx1, by1, 'FloorConcrete')
        for x in range(bx0, bx1 + 1):
            for y in range(by0, by1 + 1):
                edge = x in (bx0, bx1) or y in (by0, by1)
                inner = (x - bx0) % 11 == 0 and (y - by0) % 8 == 0
                if not (edge or inner):
                    continue
                r = self.rng.random()
                if edge and x < -150 and r < 0.75:
                    self.ent('WallSolid', x, y)
                elif r < 0.55:
                    self.ent('Girder', x, y)
        self.rect(bx0 + 1, by0 + 1, bx0 + 14, by0 + 9, 'Plating')
        # кран, экскаватор, грузовики
        self.ent('RMCPropVehicleCargoCrane', -125, -150, foot=[(-126 + i, -151 + j) for i in range(3) for j in range(3)])
        self.ent('RMCPropDiggerOrange', -122, -132, foot=[(-123 + i, -133 + j) for i in range(4) for j in range(3)])
        for x, y in ((-170, -128), (-170, -122)):
            self.ent('RMCPropVehicleSmallTruckBrownCargo', x, y, foot=[(x, y), (x + 1, y)])
        # склад материалов: мешки, стержни, сталь
        for x in range(-128, -112, 3):
            self.ent('CrateGenericSteel', x, -166)
            self.ent('DHSandbagsFull', x + 1, -166)
        for x in range(-128, -112, 4):
            self.ent('DHSandbagsEmpty25', x, -163)
            self.ent('PartRodMetal10', x + 2, -163)
            self.ent('SheetSteel10', x + 1, -160)
        self.container(-174, -170, 'Trijent')
        self.container(-174, -167, 'WeYa')
        self.container(-118, -170, 'Red')
        self.ent('DHControlPointConstruction', -141, -128)
        for x in range(x0 + 4, x1, 12):
            self.ent('DHLightPostGrid', x, y1 - 1)

    def spaceport(self):
        x0, y0, x1, y1 = 118, -84, 188, 62
        self.reserve(x0 - 3, y0 - 3, x1, y1 + 3)
        # посадочное поле
        self.rect(126, -48, 186, 30, 'FloorConcreteMono')
        for cx, cy, name in ((156, -10, 'ЗП-3 Космодром'),):
            self.rect(cx - 8, cy - 8, cx + 8, cy + 8, 'FloorDark')
            self.rect(cx - 6, cy - 6, cx + 6, cy + 6, 'FloorSteelMono')
            self.lz(cx, cy, name)
        for cx, cy in ((138, 16), (174, 16), (138, -36), (174, -36)):
            self.rect(cx - 5, cy - 5, cx + 5, cy + 5, 'FloorDark')
        for x in range(126, 187, 10):
            self.ent('DHLightPostGrid', x, 31)
            self.ent('DHLightPostGrid', x, -49)
        # терминал
        self.building(120, 36, 146, 58, 'WallReinforced', 'FloorSteel',
                      doors=[(133, 36, 'AirlockGlass'), (134, 36, 'AirlockGlass'), (146, 47, 'AirlockGlass')],
                      windows=[(x, 36) for x in range(122, 145, 3) if x not in (133, 134)] +
                              [(x, 58) for x in range(122, 145, 3)], window='ReinforcedWindow')
        for x in range(123, 144, 4):
            for y in (40, 43, 46):
                self.ent('Chair', x, y, R180)
                self.ent('Chair', x + 1, y, R180)
        for x in range(124, 140, 2):
            self.ent('TableCounterMetal', x, 52)
        for x, p in ((122, 'VendingMachineCoffee'), (123, 'VendingMachineSnack'), (124, 'VendingMachineCola')):
            self.ent(p, x, 57)
        for x in (121, 145):
            self.ent('PottedPlantRandom', x, 37)
        # диспетчерская вышка
        self.building(148, 36, 156, 46, 'WallReinforced', 'FloorDark', doors=[(152, 36, 'AirlockGlass')],
                      windows=[(150, 46), (152, 46), (154, 46), (156, 40), (156, 42), (148, 40), (148, 42)],
                      window='ReinforcedWindow')
        for x, p in ((150, 'ComputerAlert'), (152, 'ComputerPowerMonitoring'), (154, 'ComputerAlert')):
            self.ent(p, x, 45)
            self.ent('ChairOfficeDark', x, 44)
        self.ent('DHControlPointSpaceport', 152, 33)
        # ангары: открыты к полю
        for hx in (160, 175):
            self.building(hx, 36, hx + 12, 58, 'WallSolid', 'FloorSteel',
                          skip=[(x, 36) for x in range(hx + 2, hx + 11)])
            self.ent('RMCPropVehicleLoaderTruckWhiteFilled', hx + 3, 44,
                     foot=[(hx + 3 + i, 44 + j) for i in range(3) for j in range(2)])
            self.ent('RMCPropVehicleSmallTruckBlueCargo', hx + 3, 50, foot=[(hx + 3, 50), (hx + 4, 50)])
            for y in range(40, 57, 3):
                self.ent('Rack', hx + 11, y)
                self.ent('CrateGenericSteel', hx + 10, y)
        # топливный склад
        self.fence_rect(122, -82, 150, -60, gates=[(136, -60), (137, -60)])
        self.rect(123, -81, 149, -61, 'FloorConcrete')
        for x in range(125, 148, 4):
            self.ent('RMCFiltrationMachineSoloTankWaste', x, -78)
            self.ent('RMCFiltrationMachineSoloTankWater', x, -72)
        self.scatter(['RMCBarrelRed', 'RMCBarrelYellow', 'RMCBarrelBlack', 'RMCBarrelFire'], 124, -68, 148, -62, 14)
        # грузовой двор
        self.rect(156, -82, 186, -56, 'FloorConcrete')
        brands = ['WeYa', 'Trijent', 'Novaris', 'Blue', 'Green', 'Red', 'Ferret', 'SPP', 'Avant']
        for y in range(-80, -58, 3):
            for x in range(158, 182, 6):
                if self.rng.random() < 0.8:
                    self.container(x, y, self.rng.choice(brands))
        self.ent('RMCPropVehicleCargoCrane', 184, -70, foot=[(183 + i, -71 + j) for i in range(3) for j in range(3)])
        # дорога вокруг поля
        for x in range(120, 187):
            for y in (32, 33, 34):
                self.tile(x, y, 'FloorAsphalt')
        for y in range(-84, 35):
            for x in (120, 121, 122):
                self.tile(x, y, 'FloorAsphalt')
        # стоянка у терминала
        for x in range(126, 146, 3):
            if self.rng.random() < 0.6:
                self.car(x, 60)

    def npp(self):
        x0, y0, x1, y1 = -72, 108, -12, 162
        self.rect(x0, y0, x1, y1, 'FloorConcrete')
        self.fence_rect(x0, y0, x1, y1, gates=[(x1, 133), (x1, 134), (x1, 135), (x1, 136)])
        # подъезд к северной дороге
        for x in range(x1, -8):
            for y in range(133, 137):
                self.tile(x, y, 'FloorAsphalt')
        # два энергоблока: реакторный зал (север) и машинный зал (юг)
        for n, bx in enumerate((-68, -42)):
            self.building(bx, 126, bx + 22, 158, 'WallReinforced', 'FloorSteel',
                          doors=[(bx + 11, 126, 'AirlockEngineering'), (bx + 11, 142, 'AirlockEngineering')],
                          windows=[(bx, 132), (bx, 136), (bx + 22, 132), (bx + 22, 136)],
                          window='ReinforcedWindow')
            for x in range(bx + 1, bx + 22):
                if x != bx + 11:
                    self.ent('WallReinforced', x, 142)
            self.rect(bx + 6, 146, bx + 16, 156, 'FloorTechMaint')
            self.ent('DHFusionReactor', bx + 11, 151, foot=[(bx + 10 + i, 150 + j) for i in range(3) for j in range(3)])
            for tx in (bx + 4, bx + 15):
                self.ent('DHGasTurbine', tx, 134,
                         foot=[(tx + i, 132 + j) for i in range(2) for j in range(5)])
            self.ent('SMESBasic', bx + 2, 140)
            self.ent('SMESBasic', bx + 20, 140)
            self.ent('SignDanger', bx + 10, 143)
        # пульт управления
        self.building(-68, 110, -50, 122, 'WallReinforced', 'FloorDark', doors=[(-50, 116, 'AirlockEngineeringGlass')],
                      windows=[(-64, 122), (-60, 122), (-56, 122)], window='ReinforcedWindow')
        for x, p in ((-66, 'ComputerPowerMonitoring'), (-63, 'ComputerAlert'), (-60, 'ComputerPowerMonitoring'),
                     (-57, 'ComputerAlert')):
            self.ent(p, x, 121)
            self.ent('ChairOfficeLight', x, 120)
        for y in (112, 114, 116):
            self.ent('Rack', -67, y)
        self.ent('DHControlPointPowerPlant', -36, 117)
        # водозабор с моря и градирни (декор RMC)
        for x in range(-66, -18, 6):
            self.ent('RMCFiltrationCollectionsPipe1', x, SEA_SHALLOW - 1,
                     foot=[(x, SEA_SHALLOW - 1), (x + 1, SEA_SHALLOW - 1)])
        for x in (-46, -28):
            self.ent('RMCFiltration', x, 117, foot=[(x - 1 + i, 116 + j) for i in range(3) for j in range(3)])
        for x in range(x0 + 4, x1, 10):
            self.ent('DHLightPostGrid', x, y0 + 1)

    def substation(self):
        # линия идёт по x = 15, в обход поста связи у базы (x 3..9, y 18..28)
        lx = 15
        x0, y0, x1, y1 = lx - 7, 122, lx + 7, 136
        self.rect(x0, y0, x1, y1, 'FloorConcrete')
        self.fence_rect(x0, y0, x1, y1, gates=[(x0, 128), (x0, 129)])
        self.ent('DHPowerTransformer', lx, 129, foot=[(lx - 1 + i, 128 + j) for i in range(3) for j in range(3)])
        self.ent('SubstationBasic', x0 + 3, 134)
        self.ent('SignDanger', lx + 3, 124)
        # ЛЭП: АЭС → подстанция (на восток), подстанция → база (на юг)
        line_y = 139
        self.pylon_line([(-14, line_y), (lx, line_y)])
        self.pylon_line([(lx, 119), (lx, 14)])
        # спуски проводов к вводам трансформатора
        for y in list(range(132, line_y + 2)) + list(range(121, 128)):
            self.ents.append(('DHPowerLineWire', lx + 0.5, y + 0.5, 0.0, None, None))

    def pylon_line(self, pts):
        (ax, ay), (bx, by) = pts
        n = max(abs(bx - ax), abs(by - ay))
        step = 12
        k = max(1, round(n / step))
        towers = [(round(ax + (bx - ax) * i / k), round(ay + (by - ay) * i / k)) for i in range(k + 1)]
        for t in towers:
            if (t[0], t[1]) not in self.occ:
                self.ent('DHPowerPylon', *t)
        vertical = ax == bx
        for i in range(len(towers) - 1):
            (x0, y0), (x1, y1) = towers[i], towers[i + 1]
            if vertical:
                # провода висят на траверсе опоры (≈2 клетки над землёй)
                lo, hi = sorted((y0, y1))
                for y in range(lo + 2, hi + 2):
                    self.ents.append(('DHPowerLineWire', x0 + 0.5, y + 0.5, 0.0, None, None))
            else:
                lo, hi = sorted((x0, x1))
                for x in range(lo + 1, hi):
                    self.ents.append(('DHPowerLineWire', x + 0.5, y0 + 2.5, R90, None, None))
        for x, y in towers:
            self.reserve(x - 1, y - 1, x + 1, y + 1)

    def lz(self, x, y, name):
        self.ent('DHLandingZonePad', x, y, name=name)
        self.reserve(x - 12, y - 12, x + 12, y + 12)
        for i in range(x - 12, x + 13):
            for j in range(y - 12, y + 13):
                self.clear(i, j)

    def landing_zones(self):
        for e in self.old_ents:
            if e['proto'] == 'DHLandingZonePad':
                self.remove.add(e['uid'])
        self.lz(-168, -54, 'ЗП-1 Западная пустошь')
        self.lz(-24, -150, 'ЗП-2 Южное плато')
        # ЗП-3 — на поле космодрома (spaceport)

    # ---------- природа ----------

    def noise(self, x, y, scale, seed):
        gx, gy = math.floor(x / scale), math.floor(y / scale)
        fx, fy = x / scale - gx, y / scale - gy

        def h(i, j):
            return random.Random((i * 92837111) ^ (j * 689287499) ^ seed).random()

        sx, sy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
        a = h(gx, gy) + (h(gx + 1, gy) - h(gx, gy)) * sx
        b = h(gx, gy + 1) + (h(gx + 1, gy + 1) - h(gx, gy + 1)) * sx
        return a + (b - a) * sy

    def nature(self):
        old = lambda x, y: -112 <= x <= 63 and -80 <= y <= 79
        flora = ['RMCGrassDesert', 'RMCGrassDesertHeavy', 'RMCBoulderLarge', 'RMCCacti1', 'RMCCactus', 'RMCTreeJoshua']
        flora = [f for f in flora if f in self.known]
        rocks = 0
        reserved = set(self.reserved)   # снимок: свои же скалы не должны отталкивать соседние
        for x in range(X0 + 2, X1 - 1):
            for y in range(Y0 + 2, Y1 - 1):
                if old(x, y) or (x, y) in reserved or y >= BEACH - 6:
                    continue
                v = self.noise(x, y, 14, 1) * 0.7 + self.noise(x, y, 5, 2) * 0.3
                if v > 0.68:
                    near = any((x + i, y + j) in reserved for i in (-2, 0, 2) for j in (-2, 0, 2))
                    if not near:
                        self.ent(ROCK, x, y)
                        rocks += 1
                        continue
                if flora and self.rng.random() < 0.006:
                    self.ent(self.rng.choice(flora), x, y)
        print('Скал добавлено:', rocks)

    # ---------- запись ----------

    def write(self):
        mf = self.mf
        tm = mf.tilemap()
        names = {v: k for k, v in tm.items()}
        for name in sorted(set(self.tiles.values()) | {GROUND}):
            if name not in names:
                names[name] = max(tm) + 1
                tm[names[name]] = name
        mf.set_tilemap(tm)
        chunks = mf.chunks()
        space = names.get('Space')
        for cx in range(X0 // CHUNK, X1 // CHUNK + 1):
            for cy in range(Y0 // CHUNK, Y1 // CHUNK + 1):
                data = chunks.get((cx, cy))
                if data is None:
                    data = bytearray(CHUNK * CHUNK * TILE_BYTES)
                    for k in range(CHUNK * CHUNK):
                        struct.pack_into('<i', data, k * TILE_BYTES, space)
                    chunks[(cx, cy)] = data
                for k in range(CHUNK * CHUNK):
                    x, y = cx * CHUNK + k % CHUNK, cy * CHUNK + k // CHUNK
                    name = self.tiles.get((x, y))
                    if name is None:
                        continue
                    variant = random.Random(x * 73856093 ^ y * 19349663).randrange(7) if 'Sand' in name else 0
                    struct.pack_into('<iBBB', data, k * TILE_BYTES, names[name], 0, variant, 0)
        mf.set_chunks(chunks)

        # компоненты старым сущностям
        if self.add_comps:
            ranges = {e['uid']: e for e in mf.entities()}
            for uid in sorted(self.add_comps, key=lambda u: -ranges[u]['end']):
                e = ranges[uid]
                end = e['end']
                while mf.lines[end - 1] in ('', '...'):
                    end -= 1
                mf.lines[end:end] = self.add_comps[uid]
        mf.remove_entities(self.remove)

        # вода, поверх которой потом легла дорога или здание, — убрать
        solid = {(math.floor(e[1]), math.floor(e[2])) for e in self.ents
                 if not e[0].startswith('DHWater') and e[0] != 'DHPowerLineWire'}
        self.ents = [e for e in self.ents if not e[0].startswith('DHWater') or
                     (self.tiles.get((math.floor(e[1]), math.floor(e[2]))) == 'FloorPlanetDirt' and
                      (math.floor(e[1]), math.floor(e[2])) not in solid)]

        # новые сущности, сгруппированные по прототипу
        groups = {}
        for proto, x, y, rot, name, comps in self.ents:
            groups.setdefault(proto, []).append((x, y, rot, name, comps))
        uid = mf.max_uid()
        L = mf.lines
        existing = {l[len('- proto: '):]: i for i, l in enumerate(L) if l.startswith('- proto: ')}
        insert = []
        for proto in sorted(groups):
            block = []
            for x, y, rot, name, comps in groups[proto]:
                uid += 1
                block.append(f'  - uid: {uid}')
                block.append('    components:')
                if name:
                    block += ['    - type: MetaData', f'      name: {name}']
                block += ['    - type: Transform', f'      parent: {mf.root}', f'      pos: {fmt(x)},{fmt(y)}']
                if rot:
                    block.append(f'      rot: {rot!r} rad')
                block += comps or []
            insert.append((proto, block))
        for proto, block in insert:
            L = mf.lines
            if proto in existing:
                i = next(i for i, l in enumerate(L) if l == f'- proto: {proto}')
                j = i + 2
                while j < len(L) and not L[j].startswith('- proto:') and L[j] != '':
                    j += 1
                L[j:j] = block
            else:
                at = len(L)
                while at > 0 and L[at - 1] in ('', '...'):
                    at -= 1
                L[at:at] = [f'- proto: {proto}', '  entities:'] + block
        mf.update_entity_count()
        print('Новых сущностей:', len(self.ents), 'убрано старых:', len(self.remove))
        for proto, x, y in self.skipped:
            print(f'  пропущено (занято старой постройкой): {proto} {x},{y}')


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('map')
    ap.add_argument('-o', '--output', required=True)
    ap.add_argument('--protos', default='Resources/Prototypes', help='для проверки, что прототипы существуют')
    args = ap.parse_args()

    mf = MapFile(open(args.map, encoding='utf-8-sig').read())
    g = Gen(mf)
    known = set()
    for root, _, files in os.walk(args.protos):
        for f in files:
            if f.endswith('.yml'):
                kind = None
                for line in open(os.path.join(root, f), encoding='utf-8-sig', errors='ignore'):
                    if line.startswith('- type: '):
                        kind = line[8:].strip()
                    elif line.startswith('  id: ') and kind == 'entity':
                        known.add(line[6:].strip())
    g.known = known

    g.geography()
    g.dam()
    g.roads()
    g.npp()
    g.substation()
    g.village()
    g.construction()
    g.spaceport()
    g.landing_zones()
    g.powered({'DHMissileSilo', 'DHAutocannonTwin30', 'LightPostSmall'})
    g.nature()

    missing = sorted({e[0] for e in g.ents} - known)
    if missing:
        sys.exit(f'Нет прототипов: {missing}')
    g.write()
    with open(args.output, 'w', encoding='utf-8', newline='\n') as f:
        f.write(mf.text())
    print('Записано:', args.output)


if __name__ == '__main__':
    main()
