#!/usr/bin/env python3
"""Тестовая карта «Восстания» — квартал Лост-Энджелса (схема автора 2026-11-05), все Z-уровни −3..+8.

Схема (север сверху):
  * ТЦ/БЦ «Ворота» (северо-запад) — две башни по 7 этажей (уровни 0..7, крыша +8), под ними парковка −1/−2;
    между башнями — переход на 3-м и 4-м уровне. На 7-м уровне и на крыше (+8) второй башни — офис LA-V.
    Управа района — 1-й и 2-й уровни первой башни.
  * Площадь (между «Воротами» и панельками) со станцией метро: павильон на 0, станция −1, техэтаж −2.
    С площади на восток идёт проспект — до военпола (север) и склада (юг).
  * Военпол (северо-восток) — 4 этажа вверх (0..3, крыша +4) и 3 вниз (−1..−3), со своим лифтом.
  * Панельки (запад) — 4 «хрущёвки» на 5 этажей (0..4, крыша +5) с техподвалом −1; бетонные стены (×15).
    Западнее — подстанция района.
  * Завод (центр, буквой Т) — 2 этажа (0..1, крыша +2) и подвал −1. Склад (восток) — 2 этажа и техэтаж −1.
  * Четыре башни (юго-восток) — 4 жилых этажа; в двух ещё 2 недостроенных (без крыши), две достроены (6 этажей,
    крыша +6). Техэтаж −1.
  * ЗП-1 «Аэронавтика» (юг) — посадочная площадка, на неё садится борт (DHUprisingFlightPad).
  * Под землёй: −1 — коллектор и канализация с водой (люки на улицах); −2 — заброшенные тоннели и катакомбы;
    −3 — грязные катакомбы и подземный резервуар. На −2/−3 скрытые проходы во все здания, кроме военпола.

Выход: Resources/Maps/_DarkestHour/Uprising/lost_angels.yml (уровень 0 — карта-поверхность, станция LostAngels)
и lost_angels_{m3,m2,m1,p1..p8}.yml (гриды уровней). Уровни грузит DHPlanetBase (Uprising/uprising.yml).

Запуск из корня репозитория:  python3 Tools/_DarkestHour/uprising/build_map.py
"""
import base64
import glob
import math
import os
import random
import re
import struct

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..')
OUT = ROOT + '/Resources/Maps/_DarkestHour/Uprising'
RND = random.Random(2318)

X0, X1, Y0, Y1 = -102, 90, -90, 80   # границы квартала (включительно)
PI = math.pi
ROT_N, ROT_S, ROT_E, ROT_W = PI, 0.0, PI / 2, -PI / 2  # куда «смотрит» сущность (ToWorldVec)

# ------------------------------------------------------------------ тайлы

T_ASPHALT = 'RMCPlanetHybrisaAsphalt'
T_LINE = 'RMCPlanetHybrisaAsphaltLinesYellow'
T_WALK = 'RMCPlanetHybrisaSidewalk'
T_CEMENT = 'RMCPlanetHybrisaCement'
T_PLAZA = 'RMCFloorHybrisaCementFlat'
T_DIRT = 'RMCPlanetHybrisaDirt'
T_UNDER = 'RMCPlanetHybrisaUnderground'
T_PAD = 'RMCFloorHybrisaBlueSpacePort1'
T_PAD2 = 'RMCFloorHybrisaBlueSpacePort2'
T_OFFICE = 'RMCFloorHybrisaGrey'
T_OFFICE_CARPET = 'RMCFloorHybrisaBlackCarpet'
T_LAV = 'RMCFloorHybrisaBlackShiny'
T_LAV_CARPET = 'RMCFloorHybrisaBlueCarpet'
T_LOBBY = 'RMCFloorHybrisaWhiteCheck'
T_WOOD = 'RMCFloorHybrisaWood'
T_BEIGE = 'RMCFloorHybrisaBeige'
T_SHOP = 'RMCFloorHybrisaBeigeSupermart'
T_FACTORY = 'RMCFloorHybrisaGreenMetal'
T_CONCRETE = 'RMCFloorCement1'
T_POLICE = 'RMCFloorHybrisaBlueTile'
T_DARK = 'FloorDark'
T_STEEL = 'FloorSteel'
T_TECH = 'FloorTechMaint2'
T_PLATING = 'Plating'
T_METRO = 'RMCFloorHybrisaWhite'
T_PLATFORM = 'RMCFloorHybrisaBlackShiny'
T_ROOF = 'RMCFloorCementSunBleached1'

# ------------------------------------------------------------------ сущности

W_OFFICE = 'RMCWallHybrisaOffice'
W_CONCRETE = 'WallConcrete'
W_FACTORY = 'RMCWallHybrisaEngiReinforced'
W_STORE = 'RMCWallHybrisa'
W_POLICE = 'WallReinforced'
W_METRO = 'RMCWallHybrisaSpacePort'
W_ROCK = 'RMCWallHybrisaRock'
W_EDGE = 'RMCWallHybrisaHull'
W_INNER = 'WallSolid'
WINDOW = 'ReinforcedWindow'
WINDOW_PLAIN = 'Window'
GIRDER = 'Girder'

LIGHT = 'AlwaysPoweredLightLED'
LIGHT_DIM = 'AlwaysPoweredLightSodium'
# точки появления ОФСК (кроме глав отрядов) — по кругу: повстанцы чаще, остальные роли по одной.
# ОФСК появляется только на −3, в катакомбах (решение автора 2026-11-05).
_FRONT = ['Rebel', 'Saboteur', 'Rebel', 'Specialist', 'Rebel', 'Machinegunner']
_front_i = [0]


def front_spawn():
    job = _FRONT[_front_i[0] % len(_FRONT)]
    _front_i[0] += 1
    return 'SpawnPointDHLAFront' + job


VENDORS = ['DHCityVendorSnacks', 'DHCityVendorDrinks', 'DHCityVendorCoffee', 'DHCityVendorCigarettes']
POST = 'RMCStreetlight'           # уличные фонари RMC (колонии)
TRAFFIC = 'RMCTrafficLight'


class Level:
    def __init__(self, z):
        self.z = z
        self.tiles = {}
        self.walls = {}      # (x, y) -> proto
        self.ents = []       # (proto, x, y, rot)

    # тайлы
    def floor(self, x0, y0, x1, y1, tile):
        for x in range(min(x0, x1), max(x0, x1) + 1):
            for y in range(min(y0, y1), max(y0, y1) + 1):
                self.tiles[(x, y)] = tile

    # стены
    def wall(self, x, y, proto):
        self.walls[(x, y)] = proto

    def unwall(self, x, y):
        self.walls.pop((x, y), None)

    def box(self, x0, y0, x1, y1, wall, tile=None):
        if tile:
            self.floor(x0, y0, x1, y1, tile)
        for x in range(x0, x1 + 1):
            self.wall(x, y0, wall)
            self.wall(x, y1, wall)
        for y in range(y0, y1 + 1):
            self.wall(x0, y, wall)
            self.wall(x1, y, wall)

    def hline(self, x0, x1, y, wall):
        for x in range(min(x0, x1), max(x0, x1) + 1):
            self.wall(x, y, wall)

    def vline(self, x, y0, y1, wall):
        for y in range(min(y0, y1), max(y0, y1) + 1):
            self.wall(x, y, wall)

    def door(self, x, y, proto='Airlock'):
        self.unwall(x, y)
        self.put(proto, x, y)

    def window(self, x, y, proto=WINDOW):
        if (x, y) in self.walls:
            self.walls[(x, y)] = proto

    def put(self, proto, x, y, rot=None):
        self.ents.append((proto, x, y, rot))

    def clear_ents(self, x0, y0, x1, y1):
        self.ents = [e for e in self.ents if not (x0 <= e[1] <= x1 and y0 <= e[2] <= y1)]
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                self.unwall(x, y)


LEVELS = {z: Level(z) for z in range(-3, 9)}


def L(z):
    return LEVELS[z]


# ------------------------------------------------------------------ общие узлы зданий

STAIR_CELLS = {}


def stairwell(levels, sx, sy):
    """Лестничный пролёт 2×3 (sx..sx+1, sy−1..sy+1) через уровни levels (по возрастанию): с уровня z — ступени
    вверх в столбце sx+(z%2) (смотрят на север), на уровне z+1 в том же тайле — проём вниз (смотрит на юг)."""
    for i, z in enumerate(levels[:-1]):
        col = sx + (z % 2)
        L(z).put('DHZStairs', col, sy, ROT_N)
        L(levels[i + 1]).put('DHZStairwell', col, sy, ROT_S)
    for z in levels:
        lv = L(z)
        for x in (sx, sx + 1):
            for y in (sy - 1, sy, sy + 1):
                lv.unwall(x, y)
                STAIR_CELLS.setdefault(z, set()).add((x, y))


def lift(levels, lx, ly, panel_dx=2):
    """Лифт: платформа 3×3 с центром (lx, ly) на каждом уровне и пульт рядом."""
    for z in levels:
        lv = L(z)
        for x in range(lx - 1, lx + 2):
            for y in range(ly - 1, ly + 2):
                lv.unwall(x, y)
        for x in range(lx - 1, lx + 2):
            for y in range(ly - 1, ly + 2):
                STAIR_CELLS.setdefault(z, set()).add((x, y))
        lv.put('DHZLiftPlatform', lx, ly)
        lv.put('DHZLiftPanel', lx + panel_dx, ly + 1)


def lights_in(lv, x0, y0, x1, y1, step=6, proto=LIGHT):
    """Свет в помещении: по северной стене, через step тайлов."""
    for x in range(x0 + 2, x1 - 1, step):
        lv.put(proto, x, y1 - 1, ROT_N)


def roof(lv, x0, y0, x1, y1, rails=True):
    """Крыша: бетонный пол и ограждение по краю."""
    lv.floor(x0, y0, x1, y1, T_ROOF)
    if not rails:
        return
    for x in range(x0, x1 + 1):
        lv.put('Railing', x, y1, ROT_N)
        lv.put('Railing', x, y0, ROT_S)
    for y in range(y0, y1 + 1):
        lv.put('Railing', x1, y, ROT_E)
        lv.put('Railing', x0, y, ROT_W)
    lv.put(LIGHT_DIM, x0 + 2, y0 + 1)


def exterior(lv, x0, y0, x1, y1, wall, tile, window=WINDOW, every=3):
    """Этаж здания: пол, наружные стены, окна через every тайлов."""
    lv.box(x0, y0, x1, y1, wall, tile)
    if window:
        for x in range(x0 + 2, x1 - 1, every):
            lv.window(x, y0, window)
            lv.window(x, y1, window)
        for y in range(y0 + 2, y1 - 1, every):
            lv.window(x0, y, window)
            lv.window(x1, y, window)


def furnish_apartment(lv, x0, y0, x1, y1):
    """Квартира: кровать, стол, стул, шкаф, свет."""
    lv.put(RND.choice(['CMBed', 'RMCBedAlt', 'CMBed']), x0 + 1, y1 - 1)
    lv.put(RND.choice(['CMTableWooden', 'CMTableWoodenPoor', 'CMTable']), x1 - 1, y1 - 1)
    lv.put(RND.choice(['CMChair', 'CMChairComfyBeige', 'CMChair']), x1 - 1, y1 - 2)
    lv.put(RND.choice(['CMWardrobeWhite', 'CMWardrobeBlack', 'CMWardrobeGreen', 'CMWardrobeBlue']), x0 + 1, y0 + 1)
    lv.put(LIGHT_DIM, (x0 + x1) // 2, y1 - 1, ROT_N)


def furnish_office(lv, x0, y0, x1, y1, fancy=False):
    """Офис: ряды столов со стульями, шкафы, растения."""
    table = 'CMTableReinforcedBlack' if fancy else 'CMTable'
    chair = 'CMChairComfyBlack' if fancy else 'CMChairComfy'
    for x in range(x0 + 2, x1 - 1, 3):
        for y in range(y0 + 2, y1 - 1, 3):
            lv.put(table, x, y)
            lv.put(chair, x, y - 1, ROT_N)
    lv.put('CMFilingCabinetTall', x0 + 1, y1 - 1)
    lv.put('CMPottedPlant%d' % RND.randint(0, 20), x1 - 1, y1 - 1)
    lights_in(lv, x0, y0, x1, y1)


# ------------------------------------------------------------------ улицы (уровень 0)

def streets():
    g = L(0)
    # всё — бетонная мостовая, по краю — стена квартала
    g.floor(X0, Y0, X1, Y1, T_CEMENT)
    for x in range(X0, X1 + 1):
        g.wall(x, Y0, W_EDGE)
        g.wall(x, Y1, W_EDGE)
    for y in range(Y0, Y1 + 1):
        g.wall(X0, y, W_EDGE)
        g.wall(X1, y, W_EDGE)

    def road(x0, y0, x1, y1, horizontal):
        g.floor(x0 - (0 if horizontal else 1), y0 - (1 if horizontal else 0),
                x1 + (0 if horizontal else 1), y1 + (1 if horizontal else 0), T_WALK)
        g.floor(x0, y0, x1, y1, T_ASPHALT)
        if horizontal:
            mid = (y0 + y1) // 2
            for x in range(x0, x1 + 1, 2):
                g.tiles[(x, mid)] = T_LINE
            for x in range(x0 + 3, x1, 14):
                g.put(POST, x, y1 + 1)
                g.put(POST, x + 7, y0 - 1)
        else:
            mid = (x0 + x1) // 2
            for y in range(y0, y1 + 1, 2):
                g.tiles[(mid, y)] = T_LINE
            for y in range(y0 + 3, y1, 14):
                g.put(POST, x1 + 1, y)
                g.put(POST, x0 - 1, y + 7)

    # проспект (с площади на восток), бульвар к ЗП-1, западная и южная улицы
    road(-39, 38, X1 - 1, 45, True)
    road(8, -88, 15, 37, False)
    road(-38, -88, -33, 37, False)
    road(-32, -46, X1 - 1, -42, True)

    # светофоры на перекрёстках (по углам тротуаров)
    for (x0, x1, y0, y1) in ((8, 15, 38, 45), (-38, -33, 38, 45), (8, 15, -46, -42), (-38, -33, -46, -42)):
        g.put(TRAFFIC, x0 - 1, y1 + 1)
        g.put(TRAFFIC, x1 + 1, y0 - 1)

    # машины на дорогах и у тротуаров
    cars = ['RMCPropVehicleMonoSupronBlack', 'RMCPropVehicleBoxVanBlueGrey', 'RMCPropVehicleBoxVanMaintWhite',
            'RMCPropVehicleMonoSupronBlackDamaged3', 'RMCPropVehicleBoxVanPizza', 'RMCPropVehicleAmbulance']
    for x in range(-30, X1 - 6, 17):
        g.put(RND.choice(cars), x, 39 if RND.random() < 0.5 else 44)
    for y in range(-80, 30, 19):
        g.put(RND.choice(cars), 9, y)
    # мелочи улиц: гидранты, урны, скамейки, клумбы
    for x in range(-35, X1 - 4, 11):
        g.put(RND.choice(['RMCPropFireHydrantRed', 'RMCTrashBinGreen', 'RMCPlanter', 'RMCBenchLeft']), x, 47)
        g.put(RND.choice(['RMCTrashBinBlue', 'RMCPlanter', 'RMCPropFireHydrantGrey']), x + 5, 36)
    # уличные автоматы (только республиканские кредиты)
    for i, x in enumerate(range(-27, X1 - 6, 22)):
        g.put(VENDORS[i % len(VENDORS)], x, 36)


def square():
    """Площадь между «Воротами» и панельками, со станцией метро."""
    g = L(0)
    g.floor(-100, 29, -40, 48, T_PLAZA)
    for x in range(-96, -42, 8):
        g.put(POST, x, 30)
        g.put(POST, x, 47)
        g.put('RMCPlanter', x + 3, 47)
    for x in range(-94, -44, 10):
        g.put('RMCBenchLeft', x, 33)
        g.put('RMCBenchRight', x + 1, 33)
    for i, x in enumerate((-91, -79, -63, -51)):
        g.put(VENDORS[i % len(VENDORS)], x, 31)
    g.put('SpawnPointLatejoin', -70, 40)
    for dx in (-3, 0, 3):
        g.put('SpawnPointLatejoin', -70 + dx, 44)
        g.put('SpawnPointDHLAResident', -82 + dx, 35)


def substation():
    """Подстанция района (запад панелек): трансформаторы за забором, будка дежурного."""
    g = L(0)
    g.floor(-100, -8, -90, 14, T_CONCRETE)
    for x in range(-100, -89):
        g.put('RMCFenceHybrisa', x, -8)
        g.put('RMCFenceHybrisa', x, 14)
    for y in range(-8, 15):
        g.put('RMCFenceHybrisa', -89, y, ROT_E)
    g.ents = [e for e in g.ents if not (e[0] == 'RMCFenceHybrisa' and e[1] == -89 and e[2] in (2, 3))]
    for y in (-4, 1, 6):
        g.put('DHPowerTransformer', -97, y)
        g.put('RMCPropPowerTransformer', -93, y)
    exterior(g, -100, 9, -94, 13, W_STORE, T_TECH, WINDOW_PLAIN)
    g.door(-94, 11, 'DHLAAirlockUtilityLocked')
    g.put('CMTable', -98, 11)
    g.put('CMChair', -98, 10, ROT_N)
    g.put(LIGHT, -97, 12, ROT_N)
    for i in range(4):
        g.put('SpawnPointDHLAUtilityWorker', -97 + (i % 2), 10 + (i // 2) * 0)
    roof(L(1), -100, 9, -94, 13, rails=False)


# ------------------------------------------------------------------ «Ворота»

GATES_A = (-90, 52, -71, 71)
GATES_B = (-60, 50, -41, 69)
BRIDGE_Y = (59, 63)


def gates():
    ax0, ay0, ax1, ay1 = GATES_A
    bx0, by0, bx1, by1 = GATES_B
    # парковка −1/−2 под обеими башнями и между ними
    for z in (-2, -1):
        lv = L(z)
        lv.box(ax0, 50, bx1, ay1, W_CONCRETE, T_ASPHALT)
        for x in range(ax0 + 3, bx1 - 2, 4):
            lv.tiles[(x, 60)] = T_LINE
        for x in range(ax0 + 3, bx1 - 3, 7):
            lv.put(RND.choice(['RMCPropVehicleMonoSupronBlack', 'RMCPropVehicleBoxVanBlueGrey']), x, 55)
            lv.put(RND.choice(['RMCPropVehicleMonoSupronBlack', 'RMCPropVehicleMonoSupronBlackDamaged1']), x + 2, 66)
        for x in range(ax0 + 4, bx1 - 3, 8):
            lv.put(LIGHT_DIM, x, ay1 - 1, ROT_N)
    # въезд на парковку с площади (лестница на −1) и съезд −1 → −2
    stairwell([-2, -1, 0], -66, 53)
    L(0).floor(-67, 51, -64, 54, T_CONCRETE)

    # башни: уровни 0..7, крыша +8
    for (x0, y0, x1, y1), name in ((GATES_A, 'A'), (GATES_B, 'B')):
        for z in range(0, 8):
            lv = L(z)
            exterior(lv, x0, y0, x1, y1, W_OFFICE, T_LOBBY if z == 0 else T_OFFICE)
        # лестница и лифт у западной/восточной стены, с −2 до крыши
        sx = x0 + 2
        stairwell(list(range(-2, 9)), sx, y0 + 4)
        lift(list(range(-2, 8)), x1 - 3, y0 + 4, panel_dx=-2)
        roof(L(8), x0, y0, x1, y1)

    # первые этажи: вход с площади, магазины и кафе по фасаду
    g = L(0)
    for (x0, y0, x1, y1) in (GATES_A, GATES_B):
        mid = (x0 + x1) // 2
        g.door(mid, y0, 'AirlockGlass')
        g.door(mid + 1, y0, 'AirlockGlass')
        # лавки вдоль южной стены
        g.hline(x0 + 1, x1 - 1, y0 + 7, W_INNER)
        for sx in range(x0, x1 - 3, 6):
            g.vline(sx, y0 + 1, y0 + 6, W_INNER)
            g.door(sx + 3, y0 + 7, 'AirlockGlass')
            g.floor(sx + 1, y0 + 1, sx + 5, y0 + 6, T_SHOP)
            g.put(RND.choice(['RMCPropSupermartShelf1', 'RMCPropSupermartShelf2', 'RMCPropSupermartSodaRack']), sx + 1, y0 + 5)
            g.put('CMTable', sx + 4, y0 + 2)
            g.put('SpawnPointDHLAEntrepreneur', sx + 3, y0 + 3)
            g.put(LIGHT, sx + 3, y0 + 6, ROT_N)
        g.unwall(mid, y0 + 7)
        g.unwall(mid + 1, y0 + 7)
        lights_in(g, x0, y0 + 8, x1, y1)
        g.put('RMCOverheadSignNeonCafe', x0 + 3, y0 - 1)

    # этажи 1..6 первой башни: управа (1–2), офисы (3–6); второй — офисы (1–6)
    ax0, ay0, ax1, ay1 = GATES_A
    for z in range(1, 8):
        lv = L(z)
        # коридор с севера на юг посредине, кабинеты по сторонам
        mid = (ax0 + ax1) // 2
        lv.vline(mid - 2, ay0 + 8, ay1 - 1, W_INNER)
        lv.vline(mid + 2, ay0 + 8, ay1 - 1, W_INNER)
        for y in (ay0 + 12, ay0 + 16):
            lv.door(mid - 2, y, 'DHLAAirlockDistrictLocked' if z <= 2 else 'Airlock')
            lv.door(mid + 2, y, 'DHLAAirlockDistrictLocked' if z <= 2 else 'Airlock')
        furnish_office(lv, ax0, ay0 + 8, mid - 2, ay1, fancy=z <= 2)
        furnish_office(lv, mid + 2, ay0 + 8, ax1, ay1, fancy=z <= 2)
        if z == 1:
            lv.put('SpawnPointDHLADistrictHead', ax0 + 3, ay1 - 3)
            for i in range(3):
                lv.put('SpawnPointDHLADistrictStaff', mid + 4 + i * 2, ay1 - 3)
        elif z in (3, 4):
            for i in range(3):
                lv.put('SpawnPointDHLAOfficeWorker', ax0 + 3 + i * 2, ay1 - 4)

    bx0, by0, bx1, by1 = GATES_B
    for z in range(1, 7):
        lv = L(z)
        furnish_office(lv, bx0, by0 + 8, bx1, by1)
        if z in (3, 4):
            for i in range(3):
                lv.put('SpawnPointDHLAOfficeWorker', bx0 + 6 + i * 2, by1 - 4)

    # переход между башнями на 3-м и 4-м уровнях (крыша перехода — на 5-м)
    for z in (3, 4):
        lv = L(z)
        exterior(lv, ax1, BRIDGE_Y[0], bx0, BRIDGE_Y[1], W_OFFICE, T_OFFICE_CARPET, every=2)
        for y in range(BRIDGE_Y[0] + 1, BRIDGE_Y[1]):
            lv.door(ax1, y, 'AirlockGlass') if y == (BRIDGE_Y[0] + BRIDGE_Y[1]) // 2 else lv.unwall(ax1, y)
            lv.door(bx0, y, 'AirlockGlass') if y == (BRIDGE_Y[0] + BRIDGE_Y[1]) // 2 else lv.unwall(bx0, y)
        lv.put(LIGHT, (ax1 + bx0) // 2, BRIDGE_Y[1] - 1, ROT_N)
        lv.put('CMPottedPlant3', ax1 + 2, BRIDGE_Y[0] + 1)
    roof(L(5), ax1 + 1, BRIDGE_Y[0], bx0 - 1, BRIDGE_Y[1], rails=False)

    # офис LA-V: 7-й уровень второй башни и пентхаус на крыше (+8)
    lv = L(7)
    lv.floor(bx0 + 1, by0 + 1, bx1 - 1, by1 - 1, T_LAV)
    lv.hline(bx0 + 1, bx1 - 1, by0 + 8, W_OFFICE)
    lv.door((bx0 + bx1) // 2, by0 + 8, 'DHLAAirlockVanguardOfficeLocked')
    lv.door(bx0 + 3, by0 + 8, 'DHLAAirlockVanguardLocked')
    furnish_office(lv, bx0, by0 + 8, bx1, by1, fancy=True)
    lv.put('SpawnPointDHLAVEmployee', bx0 + 4, by1 - 3)
    lv.put('SpawnPointDHLAVEmployee', bx0 + 6, by1 - 3)
    lv.put('SpawnPointDHLAVSecurity', (bx0 + bx1) // 2, by0 + 6)
    lv.put('SpawnPointDHLAVSecurity', (bx0 + bx1) // 2 + 1, by0 + 6)
    for x in range(bx0 + 2, bx1 - 1, 4):
        lv.put('RMCCouchMidBlack', x, by0 + 2)
    lv.put('LockerSecurityFilled', bx1 - 2, by0 + 2)
    pent = L(8)
    px0, py0, px1, py1 = bx0 + 4, by0 + 6, bx1 - 1, by1 - 1
    exterior(pent, px0, py0, px1, py1, W_OFFICE, T_LAV_CARPET, every=2)
    pent.door(px0, py0 + 4, 'DHLAAirlockVanguardOfficeLocked')
    pent.put('CMTableWoodenFancy', (px0 + px1) // 2, py1 - 3)
    pent.put('CMChairComfyBlack', (px0 + px1) // 2, py1 - 2, ROT_S)
    pent.put('RMCBookcase', px0 + 1, py1 - 1)
    pent.put('DHLAVSafe', px0 + 2, py1 - 1)
    pent.put('CMPottedPlant7', px1 - 1, py1 - 1)
    pent.put('CMPottedPlant8', px1 - 1, py0 + 1)
    pent.put('SpawnPointDHLAVRepresentative', (px0 + px1) // 2, py1 - 4)
    pent.put('SpawnPointDHLAVAssistant', (px0 + px1) // 2 - 2, py1 - 4)
    pent.put(LIGHT, (px0 + px1) // 2, py1 - 1, ROT_N)
    # лестница выходит на крышу внутри пентхауса? нет — на открытую часть; пентхаус над офисом
    # переход «Ворота» → площадь: тротуар у входов
    L(0).floor(GATES_A[0], 48, GATES_B[2], 51, T_WALK)


# ------------------------------------------------------------------ военпол

MP = (10, 52, 60, 72)


def police():
    x0, y0, x1, y1 = MP
    for z in range(-3, 4):
        lv = L(z)
        exterior(lv, x0, y0, x1, y1, W_POLICE, T_POLICE if z >= 0 else T_DARK, WINDOW if z >= 0 else None)
        lights_in(lv, x0, y0, x1, y1)
    stairwell(list(range(-3, 5)), x0 + 2, y0 + 4)
    lift(list(range(-3, 4)), x1 - 3, y0 + 4, panel_dx=-2)
    roof(L(4), x0, y0, x1, y1)

    g = L(0)
    mid = (x0 + x1) // 2
    g.door(mid, y0, 'DHLAAirlockPoliceGlassLocked')
    g.door(mid + 1, y0, 'DHLAAirlockPoliceGlassLocked')
    # дежурная часть, камеры, оружейная
    g.hline(x0 + 1, x1 - 1, y0 + 8, W_POLICE)
    g.door(mid, y0 + 8, 'DHLAAirlockPoliceLocked')
    for cx in range(x0 + 6, x0 + 22, 4):
        g.box(cx, y1 - 6, cx + 4, y1 - 1, W_POLICE)
        g.door(cx + 2, y1 - 6, 'DHLAAirlockPoliceGlassLocked')
        g.put('CMBed', cx + 2, y1 - 3)
    g.box(x1 - 12, y1 - 7, x1 - 1, y1 - 1, W_POLICE)
    g.door(x1 - 6, y1 - 7, 'DHLAAirlockPoliceCommandLocked')
    for x in range(x1 - 10, x1 - 2, 2):
        g.put('LockerSecurityFilled', x, y1 - 2)
    for x in range(x0 + 4, x1 - 4, 4):
        g.put('CMTableReinforced', x, y0 + 3)
        g.put('CMChairComfyBlue', x, y0 + 4, ROT_S)
    # 1-й уровень — кабинеты и командир, 2–3 — казарма
    lv = L(1)
    furnish_office(lv, x0, y0, x1, y1)
    lv.put('SpawnPointDHLAMPChief', mid, y1 - 3)
    lv.put('SpawnPointDHLAMPDeputyChief', mid - 2, y1 - 3)
    lv.put('SpawnPointDHLAMPInvestigator', mid + 2, y1 - 3)
    lv.put('SpawnPointDHLAMPInvestigator', mid + 4, y1 - 3)
    for z in (2, 3):
        lv = L(z)
        for x in range(x0 + 6, x1 - 2, 3):
            lv.put('RMCBedBunkBlue', x, y1 - 2)
            lv.put('CMWardrobeBlue', x, y0 + 2)
        if z == 2:
            for i in range(12):
                job = 'SpawnPointDHLAMPOfficer' if i < 8 else 'SpawnPointDHLAMPCadet'
                lv.put(job, x0 + 6 + (i % 6) * 3, y1 - 4 - (i // 6) * 2)
    # подземные: архив (−1), тир и склад (−2), изолятор (−3)
    for x in range(x0 + 6, x1 - 2, 4):
        L(-1).put('CMFilingCabinetTall', x, y1 - 2)
        L(-2).put('CMRack', x, y1 - 2)
        L(-2).put('CrateGenericSteel', x, y0 + 2)
        L(-3).box(x, y1 - 5, x + 3, y1 - 1, W_POLICE)
        L(-3).door(x + 1, y1 - 5, 'DHLAAirlockPoliceLocked')
    # тюремные надзиратели — у изолятора
    L(-3).put('SpawnPointDHLAMPWarden', x0 + 4, y1 - 7)
    L(-3).put('SpawnPointDHLAMPWarden', x0 + 6, y1 - 7)


# ------------------------------------------------------------------ панельки

PANELS = [(-90, -8, -77, 26, 'v'), (-66, -18, -53, 24, 'v'), (-90, -40, -44, -29, 'h'), (-90, -71, -44, -60, 'h')]


def panels():
    for idx, (x0, y0, x1, y1, ori) in enumerate(PANELS):
        for z in range(-1, 5):
            lv = L(z)
            exterior(lv, x0, y0, x1, y1, W_CONCRETE, T_TECH if z < 0 else T_WOOD, WINDOW_PLAIN if z >= 0 else None)
        roof(L(5), x0, y0, x1, y1)
        if ori == 'v':
            cx = (x0 + x1) // 2
            stairwell(list(range(-1, 6)), cx, y0 + 3)
            for z in range(0, 5):
                lv = L(z)
                lv.vline(cx - 1, y0 + 6, y1 - 1, W_CONCRETE)
                lv.vline(cx + 2, y0 + 6, y1 - 1, W_CONCRETE)
                for ry in range(y0 + 6, y1 - 3, 6):
                    lv.hline(x0 + 1, cx - 1, ry, W_CONCRETE)
                    lv.hline(cx + 2, x1 - 1, ry, W_CONCRETE)
                    lv.door(cx - 1, ry + 3, 'WoodDoor')
                    lv.door(cx + 2, ry + 3, 'WoodDoor')
                    furnish_apartment(lv, x0 + 1, ry + 1, cx - 2, ry + 5)
                    furnish_apartment(lv, cx + 3, ry + 1, x1 - 1, ry + 5)
                    if z in (1, 3):
                        lv.put('SpawnPointDHLAResident', x0 + 3, ry + 3)
                lights_in(lv, cx - 1, y0, cx + 2, y1, step=8)
            L(0).door(cx, y0, 'AirlockGlass')
            L(0).door(cx + 1, y0, 'AirlockGlass')
        else:
            cy = (y0 + y1) // 2
            stairwell(list(range(-1, 6)), x0 + 3, cy)
            for z in range(0, 5):
                lv = L(z)
                lv.hline(x0 + 6, x1 - 1, cy - 1, W_CONCRETE)
                lv.hline(x0 + 6, x1 - 1, cy + 2, W_CONCRETE)
                for rx in range(x0 + 6, x1 - 3, 6):
                    lv.vline(rx, y0 + 1, cy - 1, W_CONCRETE)
                    lv.vline(rx, cy + 2, y1 - 1, W_CONCRETE)
                    lv.door(rx + 3, cy - 1, 'WoodDoor')
                    lv.door(rx + 3, cy + 2, 'WoodDoor')
                    furnish_apartment(lv, rx + 1, y0 + 1, rx + 5, cy - 2)
                    furnish_apartment(lv, rx + 1, cy + 3, rx + 5, y1 - 1)
                    if z in (1, 3):
                        lv.put('SpawnPointDHLAResident', rx + 3, y0 + 2)
                for x in range(x0 + 8, x1 - 2, 8):
                    lv.put(LIGHT_DIM, x, cy + 1, ROT_N)
            L(0).door(x0, cy, 'AirlockGlass')
            L(0).door(x0, cy + 1, 'AirlockGlass')
        # подвал: трубы и хлам, скрытый люк в тоннели −2
        b = L(-1)
        b.put('CMRack', x0 + 2, y1 - 2)
        b.put('CrateGenericSteel', x1 - 2, y0 + 2)
        b.put(LIGHT_DIM, (x0 + x1) // 2, y1 - 1, ROT_N)
        hx, hy = PANEL_HATCHES[idx]
        b.put('DHZHatchDown', hx, hy)
        L(-2).put('DHZLadderUp', hx, hy)
    # дворы между панельками
    L(0).floor(-76, -27, -67, 26, T_DIRT)
    L(0).floor(-52, -27, -41, 26, T_DIRT)
    for y in range(-24, 24, 7):
        L(0).put('RMCPlanter', -72, y)
        L(0).put(RND.choice(['RMCBenchLeft', 'RMCTrashBinGreen']), -47, y)


PANEL_HATCHES = [(-80, 0), (-56, -10), (-50, -35), (-50, -69)]


# ------------------------------------------------------------------ завод и склад

FACTORY = [(-25, 12, 2, 25), (-12, -25, -4, 12), (-15, -38, 0, -25)]
WAREHOUSE = (22, -15, 62, 25)


def factory():
    for z in (-1, 0, 1):
        lv = L(z)
        for (x0, y0, x1, y1) in FACTORY:
            lv.floor(x0, y0, x1, y1, T_FACTORY if z >= 0 else T_TECH)
        cells = set()
        for (x0, y0, x1, y1) in FACTORY:
            for x in range(x0, x1 + 1):
                for y in range(y0, y1 + 1):
                    cells.add((x, y))
        for (x, y) in cells:
            if any((x + dx, y + dy) not in cells for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                lv.wall(x, y, W_FACTORY)
        for (x0, y0, x1, y1) in FACTORY:
            lights_in(lv, x0, y0, x1, y1)
    roof(L(2), -25, 12, 2, 25, rails=False)
    roof(L(2), -12, -25, -4, 12, rails=False)
    roof(L(2), -15, -38, 0, -25, rails=False)
    stairwell([-1, 0, 1, 2], -23, 16)
    stairwell([-1, 0, 1, 2], -13, -34)
    g = L(0)
    g.door(2, 18, 'DHLAAirlockFactoryLocked')
    g.door(2, 19, 'DHLAAirlockFactoryLocked')
    g.door(-8, -38, 'DHLAAirlockFactoryLocked')
    # цех: конвейеры, станки, роботы
    for x in range(-20, 0, 4):
        g.put('RMCPropFactoryConveyer', x, 20)
        g.put(RND.choice(['RMCPropFactoryRoboticArm', 'RMCPropFactoryRoboticArm2']), x, 22)
    for y in range(-20, 10, 5):
        g.put('RMCMachinePropBig1', -9, y)
    for i in range(6):
        g.put('SpawnPointDHLAFactoryWorker', -18 + i * 2, 15)
    lv = L(1)
    for x in range(-22, 0, 4):
        lv.put('CMTable', x, 22)
        lv.put('CMChair', x, 21, ROT_N)
    L(-1).put('DHZHatchDown', -8, 0)
    L(-2).put('DHZLadderUp', -8, 0)


def warehouse():
    x0, y0, x1, y1 = WAREHOUSE
    for z in (-1, 0, 1):
        lv = L(z)
        exterior(lv, x0, y0, x1, y1, W_STORE, T_CONCRETE if z >= 0 else T_TECH, WINDOW_PLAIN if z >= 0 else None, every=6)
        lights_in(lv, x0, y0, x1, y1, step=8)
    roof(L(2), x0, y0, x1, y1)
    stairwell([-1, 0, 1, 2], x0 + 2, y0 + 4)
    lift([-1, 0, 1], x1 - 4, y0 + 5, panel_dx=-2)
    g = L(0)
    g.door(x0, 4, 'DHLAAirlockFactoryLocked')
    g.door(x0, 5, 'DHLAAirlockFactoryLocked')
    g.door((x0 + x1) // 2, y1, 'DHLAAirlockFactoryLocked')
    for z in (0, 1):
        lv = L(z)
        for x in range(x0 + 5, x1 - 4, 3):
            for y in range(y0 + 9, y1 - 2, 5):
                lv.put('CMRack', x, y)
                if RND.random() < 0.5:
                    lv.put('CrateGenericSteel', x, y + 2)
    L(-1).put('DHZHatchDown', x0 + 10, 0)
    L(-2).put('DHZLadderUp', x0 + 10, 0)


# ------------------------------------------------------------------ четыре башни

TOWER_HATCHES = [(30, -37), (56, -37), (30, -59), (56, -59)]
TOWERS = [(22, -40, 36, -27, True), (48, -40, 62, -27, True), (22, -62, 36, -49, False), (48, -62, 62, -49, False)]


def towers():
    for idx, (x0, y0, x1, y1, finished) in enumerate(TOWERS):
        top = 5
        for z in range(-1, top + 1):
            lv = L(z)
            skeleton = not finished and z >= 4
            if skeleton:
                # недостроенный этаж: плиты, балки по краю, без окон и стен
                lv.floor(x0, y0, x1, y1, T_PLATING)
                for x in range(x0, x1 + 1, 3):
                    lv.put(GIRDER, x, y0)
                    lv.put(GIRDER, x, y1)
                for y in range(y0, y1 + 1, 3):
                    lv.put(GIRDER, x0, y)
                    lv.put(GIRDER, x1, y)
                continue
            exterior(lv, x0, y0, x1, y1, W_CONCRETE, T_BEIGE if z >= 0 else T_TECH, WINDOW_PLAIN if z >= 0 else None)
            if z >= 0:
                cx = (x0 + x1) // 2
                mid_y = (y0 + y1) // 2
                lv.vline(cx, y0 + 1, y1 - 1, W_INNER)
                lv.hline(x0 + 1, x1 - 1, mid_y, W_INNER)
                for (ax0, ay0, ax1, ay1) in ((x0, y0, cx, mid_y), (cx, y0, x1, mid_y), (x0, mid_y, cx, y1), (cx, mid_y, x1, y1)):
                    furnish_apartment(L(z), ax0 + 1, ay0 + 1, ax1 - 1, ay1 - 1)
                lv.door(cx, mid_y - 2, 'WoodDoor')
                lv.door(cx, mid_y + 2, 'WoodDoor')
                lv.door(cx - 2, mid_y, 'WoodDoor')
                lv.door(cx + 2, mid_y, 'WoodDoor')
                if z in (1, 2):
                    L(z).put('SpawnPointDHLAResident', x0 + 3, y0 + 3)
        stairwell(list(range(-1, top + (2 if finished else 1))), x0 + 2, (y0 + y1) // 2 - 3)
        for z in range(-1, top + 1):
            for x in range(x0 + 1, x0 + 5):
                for y in range((y0 + y1) // 2 - 5, (y0 + y1) // 2 - 1):
                    L(z).unwall(x, y)
        if finished:
            roof(L(top + 1), x0, y0, x1, y1)
        L(0).door((x0 + x1) // 2, y1 if y1 < -40 else y0, 'AirlockGlass')
        hx, hy = TOWER_HATCHES[idx]
        L(-1).put('DHZHatchDown', hx, hy)
        L(-2).put('DHZLadderUp', hx, hy)


# ------------------------------------------------------------------ ЗП-1, метро

ZP1 = (-28, -88, 4, -52)


def landing_zone():
    g = L(0)
    x0, y0, x1, y1 = ZP1
    g.floor(x0, y0, x1, y1, T_PAD)
    g.floor(x0 + 4, y0 + 4, x1 - 4, y1 - 4, T_PAD2)
    for x in range(x0, x1 + 1, 4):
        g.put(POST, x, y1 + 1)
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    g.put('DHUprisingFlightPad', cx, cy)
    # будка «Аэронавтики»
    exterior(g, x1 + 1, y1 - 8, x1 + 3, y1 - 2, W_METRO, T_METRO, WINDOW_PLAIN)
    g.door(x1 + 1, y1 - 5, 'Airlock')
    g.put('RMCOverheadSignNeonHotel', cx, y1 + 2)
    # посадочные огни по краю и прожекторы по углам
    for x in range(x0, x1 + 1, 3):
        g.put('LandingZoneLightOne', x, y0)
        g.put('LandingZoneLightOne', x, y1)
    for y in range(y0, y1 + 1, 3):
        g.put('LandingZoneLightOne', x0, y)
        g.put('LandingZoneLightOne', x1, y)
    for (x, y) in ((x0 + 1, y0 + 1), (x1 - 1, y0 + 1), (x0 + 1, y1 - 1), (x1 - 1, y1 - 1)):
        g.put('RMCLZFloodlight', x, y)


METRO = (-84, 30, -44, 46)
PAVILION = (-66, 34, -56, 42)


def metro():
    g = L(0)
    x0, y0, x1, y1 = PAVILION
    exterior(g, x0, y0, x1, y1, W_METRO, T_METRO)
    g.door(x0, 38, 'AirlockGlass')
    g.door(x1, 38, 'AirlockGlass')
    g.put(LIGHT, (x0 + x1) // 2, y1 - 1, ROT_N)
    roof(L(1), x0, y0, x1, y1, rails=False)
    stairwell([-2, -1, 0], x0 + 3, 37)
    # станция −1: зал и платформа с путями
    mx0, my0, mx1, my1 = METRO
    st = L(-1)
    st.box(mx0, my0, mx1, my1, W_METRO, T_METRO)
    st.floor(mx0 + 1, my0 + 1, mx1 - 1, my0 + 4, T_PLATFORM)
    for x in range(mx0 + 1, mx1):
        st.tiles[(x, my0 + 2)] = T_STEEL
    for x in range(mx0 + 3, mx1 - 2, 6):
        st.put('RMCBenchLeft', x, my0 + 6)
        st.put(LIGHT, x, my1 - 1, ROT_N)
    for i in range(2):
        st.put('SpawnPointDHLAMetroWorker', mx0 + 4 + i * 2, my1 - 3)
    # техэтаж −2
    tech = L(-2)
    tech.box(-80, 32, -60, 44, W_METRO, T_TECH)
    for x in range(-77, -62, 4):
        tech.put('RMCMachinePropBig1', x, 42)
        tech.put(LIGHT_DIM, x, 43, ROT_N)


# ------------------------------------------------------------------ подземелье

def sewers():
    """−1: коллекторы под улицами (вода посредине), люки на улицах."""
    lv = L(-1)
    runs = [(-32, 39, X1 - 2, 44, True), (9, -86, 14, 37, False), (-37, -86, -34, 37, False),
            (-31, -45, X1 - 2, -43, True)]
    for x0, y0, x1, y1, horizontal in runs:
        lv.floor(x0, y0, x1, y1, T_UNDER)
        if horizontal:
            for x in range(x0, x1 + 1):
                lv.put('DHWaterShallow', x, (y0 + y1) // 2)
            for x in range(x0 + 4, x1, 12):
                lv.put(LIGHT_DIM, x, y1, ROT_N)
        else:
            for y in range(y0, y1 + 1):
                lv.put('DHWaterShallow', (x0 + x1) // 2, y)
            for y in range(y0 + 4, y1, 12):
                lv.put(LIGHT_DIM, x1, y, ROT_E)
    # люки на улицах ↔ лестницы в коллекторе
    manholes = [(-20, 41), (20, 42), (50, 41), (75, 42), (11, 0), (11, -60), (-36, 10), (-36, -40), (30, -44), (70, -44)]
    for x, y in manholes:
        L(0).put('DHZManhole', x, y)
        lv.put('DHZLadderUp', x, y)
    # спуски в тоннели −2
    for x, y in [(60, 40), (12, -80), (-35, -80)]:
        lv.put('DHZHatchDown', x, y)
        L(-2).put('DHZLadderUp', x, y)


def tunnels():
    """−2: заброшенные тоннели и катакомбы, проходы под все здания, кроме военпола."""
    lv = L(-2)
    corridors = [
        (-95, -2, 80, 1),        # главный тоннель запад—восток
        (-30, -86, -28, 28),     # под западной улицей
        (40, -70, 42, 30),       # под складом и башнями
        (-82, 2, -80, 28),       # под первой панелькой к площади
        (-51, 28, -48, 49),      # к парковке «Ворот» (−2)
        (-70, 28, -68, 31),      # к техэтажу метро
        (-90, -70, -30, -68),    # под южной панелькой
        (-90, -36, -30, -34),    # под панелькой −40..−29
        (-58, -16, -56, -2),     # под второй панелькой
        (-10, -2, -6, 0),        # к заводу
        (24, -38, 60, -36),      # под северными башнями
        (24, -60, 60, -58),      # под южными башнями
        (52, -64, 54, -2),
        (12, -82, 40, -80),      # к коллектору у ЗП-1
        (-37, -82, -30, -80),
        (58, 1, 61, 40),         # к коллектору у проспекта
    ]
    for x0, y0, x1, y1 in corridors:
        lv.floor(x0, y0, x1, y1, T_UNDER)
    # катакомбы: ниши с костями и мусором
    for cx, cy in [(-70, -20), (-20, -55), (30, -20), (70, -55), (-88, 15)]:
        lv.floor(cx - 4, cy - 3, cx + 4, cy + 3, T_DIRT)
        lv.floor(cx, cy + 3, cx, cy + 10 if cy < 0 else cy + 3, T_UNDER)
        lv.put(LIGHT_DIM, cx, cy + 2, ROT_N)
        lv.put('CrateGenericSteel', cx - 3, cy - 2)
    # соединить ниши с главным тоннелем
    for cx, cy in [(-70, -20), (30, -20)]:
        lv.floor(cx, cy, cx + 1, -2, T_UNDER)
    for cx, cy in [(-20, -55), (70, -55)]:
        lv.floor(cx, cy, cx + 1, -36 if cx < 0 else -2, T_UNDER)
    lv.floor(-90, 10, -83, 13, T_UNDER)
    # проход в парковку «Ворот» (−2) и в техэтаж метро
    L(-2).floor(-51, 49, -48, 51, T_UNDER)
    L(-2).unwall(-50, 50)
    L(-2).unwall(-49, 50)
    L(-2).put('Airlock', -50, 50)
    L(-2).unwall(-69, 32)
    L(-2).put('DHLAAirlockMetroLocked', -69, 32)
    # спуски в катакомбы −3
    for x, y in [(-29, -60), (41, -40), (0, 0)]:
        lv.put('DHZHatchDown', x, y)
        L(-3).put('DHZLadderUp', x, y)


def catacombs():
    """−3: грязные катакомбы и подземный резервуар с водой."""
    lv = L(-3)
    lv.floor(-24, -86, 4, -56, T_UNDER)
    for x in range(-22, 3):
        for y in range(-84, -58):
            lv.put('DHWaterDeep' if -80 < y < -62 and -20 < x < 0 else 'DHWaterShallow', x, y)
    corridors = [(-29, -60, -27, -2), (-27, -2, 41, 0), (39, -40, 41, -2), (-1, -2, 1, 0), (-27, -60, -24, -58)]
    for x0, y0, x1, y1 in corridors:
        lv.floor(x0, y0, x1, y1, T_DIRT)
    for i, (cx, cy) in enumerate([(-40, -1), (20, -10), (60, -25)]):
        lv.floor(cx - 5, cy - 4, cx + 5, cy + 4, T_DIRT)
        lv.put(LIGHT_DIM, cx, cy + 3, ROT_N)
        # логово ОФСК: глава отряда и бойцы
        if i < 2:
            lv.put('SpawnPointDHLAFrontLeader', cx, cy + 1)
        for dx in (-3, 0, 3):
            for dy in (-2, -1):
                lv.put(front_spawn(), cx + dx, cy + dy)
        lv.put('CrateGenericSteel', cx - 4, cy + 3)
        lv.put('RMCBedBunkBlue', cx + 4, cy + 3)
    lv.floor(41, -26, 55, -24, T_DIRT)
    lv.floor(-34, -1, -27, 0, T_DIRT)
    lv.floor(15, -6, 16, -1, T_DIRT)
    lv.put(LIGHT_DIM, -10, -57, ROT_N)
    # скрытый проход в парковку «Ворот» (−2) и в техэтаж метро
    lv.floor(-50, -1, -45, 52, T_DIRT)
    lv.put('DHZLadderUp', -48, 52)
    L(-2).put('DHZHatchDown', -48, 52)
    lv.floor(-71, 33, -50, 35, T_DIRT)
    lv.floor(-72, 33, -72, 35, T_DIRT)
    lv.put('DHZLadderUp', -72, 34)
    L(-2).put('DHZHatchDown', -72, 34)


KEEP = {'DHZStairs', 'DHZStairwell', 'DHZLiftPlatform', 'DHZLiftPanel'}


def clean_passages():
    """С лестничных пролётов и лифтовых площадок — убрать мебель, стены и прочее (кроме самих переходов)."""
    for z, cells in STAIR_CELLS.items():
        lv = L(z)
        for c in cells:
            lv.walls.pop(c, None)
        lv.ents = [e for e in lv.ents if e[0] in KEEP or (e[1], e[2]) not in cells]


def rock_walls():
    """Подземные уровни: вокруг всех пустот — неразрушимая скала (на своём тайле)."""
    for z in (-3, -2, -1):
        lv = L(z)
        cells = set(lv.tiles)
        for (x, y) in list(cells):
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    p = (x + dx, y + dy)
                    if p not in cells and p not in lv.walls:
                        lv.tiles[p] = T_UNDER
                        lv.walls[p] = W_ROCK


# ------------------------------------------------------------------ запись

TILE_IDS = {}


def tile_id(name):
    if name not in TILE_IDS:
        TILE_IDS[name] = len(TILE_IDS)
    return TILE_IDS[name]


def chunks(tiles):
    space = tile_id('Space')
    out = {}
    for (x, y), name in tiles.items():
        out.setdefault((x // 16, y // 16), {})[(x % 16, y % 16)] = tile_id(name)
    res = []
    for (cx, cy), cells in sorted(out.items()):
        data = bytearray()
        for i in range(256):
            data += struct.pack('<iBBB', cells.get((i % 16, i // 16), space), 0, 0, 0)
        res.append((cx, cy, base64.b64encode(bytes(data)).decode()))
    return res


def entities(lv):
    """Сущности уровня: стены (на месте окон — окна) + остальное; сущности не на полу отбрасываются."""
    ents = [(p, x, y, None) for (x, y), p in lv.walls.items()]
    ents += lv.ents
    return [e for e in ents if (e[1], e[2]) in lv.tiles]


GRID_COMPONENTS = """    - type: Broadphase
    - type: Physics
      bodyStatus: InAir
      angularDamping: 0.05
      linearDamping: 0.05
      fixedRotation: False
      bodyType: Dynamic
    - type: Fixtures
      fixtures: {}
    - type: OccluderTree
    - type: GridPathfinding
    - type: DecalGrid
      chunkCollection:
        version: 2
        nodes: []
    - type: SpreaderGrid"""


def write(lv, path, name, surface):
    TILE_IDS.clear()
    tile_id('Space')
    ch = chunks(lv.tiles)
    lines = ['meta:', '  format: 7', '  category: %s' % ('Map' if surface else 'Grid'), '  engineVersion: 293.0.1',
             '  forkId: ""', '  forkVersion: ""', '  time: 11/05/2026 12:00:00', '  entityCount: ENTITY_COUNT']
    if surface:
        lines += ['maps:', '- 1', 'grids:', '- 1', 'orphans: []']
    else:
        lines += ['maps: []', 'grids:', '- 1', 'orphans:', '- 1']
    lines += ['nullspace: []', 'tilemap:']
    for tname, idx in sorted(TILE_IDS.items(), key=lambda kv: kv[1]):
        lines.append('  %d: %s' % (idx, tname))
    lines += ['entities:', '- proto: ""', '  entities:', '  - uid: 1', '    components:', '    - type: MetaData',
              '      name: %s' % name, '    - type: Transform']
    if surface:
        lines += ['    - type: Map', '      mapPaused: True', '    - type: GridTree']
    else:
        lines += ['      parent: invalid']
    lines += ['    - type: MapGrid', '      chunks:']
    for cx, cy, data in ch:
        lines += ['        %d,%d:' % (cx, cy), '          ind: %d,%d' % (cx, cy), '          tiles: %s' % data,
                  '          version: 7']
    lines.append(GRID_COMPONENTS)
    if surface:
        lines += ['    - type: Gravity', '      inherent: True', '      enabled: True',
                  '    - type: MapLight', "      ambientLightColor: '#AEB7C4FF'",
                  '    - type: Roof', '      data: {}',
                  '    - type: MapAtmosphere', '      space: False', '      mixture:', '        temperature: 291',
                  '        volume: 2500', '        moles:', '          Oxygen: 21.82478', '          Nitrogen: 82.10312',
                  '        immutable: True',
                  '    - type: BecomesStation', '      id: LostAngels']
    uid = 2
    by_proto = {}
    for proto, x, y, rot in entities(lv):
        by_proto.setdefault(proto, []).append((x, y, rot))
    for proto in sorted(by_proto):
        lines += ['- proto: %s' % proto, '  entities:']
        for x, y, rot in by_proto[proto]:
            lines += ['  - uid: %d' % uid, '    components:', '    - type: Transform']
            if rot:
                lines.append('      rot: %s rad' % rot)
            lines += ['      pos: %s,%s' % (x + 0.5, y + 0.5), '      parent: 1']
            uid += 1
    lines.append('...')
    text = '\n'.join(lines) + '\n'
    text = text.replace('ENTITY_COUNT', str(uid - 1))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, 'w', encoding='utf-8').write(text)
    print('%-60s level %+d tiles %6d entities %6d' % (os.path.relpath(path, ROOT), lv.z, len(lv.tiles), uid - 1))
    return by_proto, set(TILE_IDS)


def check(protos, tiles):
    """Все прототипы и тайлы есть и не абстрактные."""
    ids, abstract, tile_ids = set(), set(), set()
    for f in glob.glob(ROOT + '/Resources/Prototypes/**/*.yml', recursive=True):
        kind, cur, is_abs = None, None, False
        for line in open(f, encoding='utf-8-sig', errors='ignore'):
            m = re.match(r'^- type: (\w+)', line)
            if m:
                if kind == 'entity' and cur:
                    (abstract if is_abs else ids).add(cur)
                if kind == 'tile' and cur and not is_abs:
                    tile_ids.add(cur)
                kind, cur, is_abs = m.group(1), None, False
                continue
            m = re.match(r'^  id: (\S+)', line)
            if m:
                cur = m.group(1)
            if re.match(r'^  abstract: true', line):
                is_abs = True
        if kind == 'entity' and cur:
            (abstract if is_abs else ids).add(cur)
        if kind == 'tile' and cur and not is_abs:
            tile_ids.add(cur)
    bad = sorted(p for p in protos if p not in ids)
    badt = sorted(t for t in tiles if t not in tile_ids and t != 'Space')
    if bad:
        print('MISSING/ABSTRACT protos:', bad)
    if badt:
        print('MISSING tiles:', badt)
    return not bad and not badt


def main():
    streets()
    square()
    substation()
    gates()
    police()
    panels()
    factory()
    warehouse()
    towers()
    landing_zone()
    metro()
    sewers()
    tunnels()
    catacombs()
    clean_passages()
    rock_walls()

    names = {-3: 'm3', -2: 'm2', -1: 'm1'}
    protos, tiles = set(), set()
    for z, lv in sorted(LEVELS.items()):
        if z == 0:
            path, title = OUT + '/lost_angels.yml', 'Lost Angels'
        else:
            path = OUT + '/lost_angels_%s.yml' % (names.get(z) or 'p%d' % z)
            title = 'Lost Angels %+d' % z
        p, t = write(lv, path, title, z == 0)
        protos |= set(p)
        tiles |= t
    print('OK' if check(protos, tiles) else 'ERRORS')


if __name__ == '__main__':
    main()
