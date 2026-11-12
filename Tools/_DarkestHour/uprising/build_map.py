#!/usr/bin/env python3
"""Карта «Восстания» — квартал Лост-Энджелса, все Z-уровни −3..+8. Уменьшена и уплотнена (решения автора
2026-11-06): 99×128 тайлов вместо 193×171; убраны подстанция, завод и четыре башни; ЗП-1 — на крыше «Ворот».

Схема (север сверху):
  * ТЦ/БЦ «Ворота» (север) — две башни по 7 этажей (уровни 0..7, крыша +8), под ними парковка −1/−2;
    между башнями — переход на 3-м и 4-м уровне. Вторая башня: 6-й уровень — офис LA-V, 7-й — кабинет
    представителя, крыша (+8) — ЗП-1 «Аэронавтика» (на неё садится борт, DHUprisingFlightPad).
    Управа района — 1-й и 2-й уровни первой башни. Восточнее — открытая стоянка.
  * Под «Воротами»: небольшая площадь (запад) с павильоном метро сбоку, посередине — проспект от «Ворот» на юг,
    восточнее — торговые ряды. Под площадью — станция метро (−1) и техэтаж (−2).
  * Военпол (центр) — 4 этажа вверх (0..3, крыша +4) и 3 вниз (−1..−3), со своим лифтом; главный вход на юг,
    служебный на север. По бокам — техузел района (коммунальщики) и бар.
  * Юг, плотно: две панельки на 5 этажей (0..4, крыша +5) с подвалами, тесный двор между ними; уменьшенный склад
    (2 этажа и техэтаж −1) и гаражи впритык к нему.
  * Под землёй: −1 — коллекторы под улицами (люки); −2 — тоннели и катакомбы, проходы под всё, кроме военпола;
    −3 — грязные катакомбы с логовами ОФСК и подземный резервуар под торговыми рядами.

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

X0, X1, Y0, Y1 = -50, 48, -72, 55   # границы квартала (включительно); плотная застройка (решение автора 2026-11-06)
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
SHAFT_CELLS = {}   # z -> клетки проёма над маршем: без пола, чтобы сквозь них было видно лестницу внизу


def stairwell(levels, sx, sy, w=2, l=3):
    """Лестница (ступени RMC-14): два марша шириной w и длиной l рядом — sx..sx+2w−1, ступени sy..sy+l−1, площадки
    sy−1 и sy+l. С уровня z марш вверх в своей половине (по чётности z, смотрит на север), последняя ступень поднимает
    на z+1; там над маршем — шахта и спуск над последней ступенью (смотрит на юг)."""
    for i, z in enumerate(levels[:-1]):
        x0 = sx + (z % 2) * w
        up = levels[i + 1]
        for k in range(w):
            piece = 'Left' if k == 0 else 'Right' if k == w - 1 else ''
            for r in range(l):
                top = r == l - 1
                L(z).put(('DHZStairsTop' if top else 'DHZStairStep') + piece, x0 + k, sy + r, ROT_N)
                L(up).put('DHZStairwell' if top else 'DHZStairShaft', x0 + k, sy + r, ROT_S)
                if not top:
                    SHAFT_CELLS.setdefault(up, set()).add((x0 + k, sy + r))
    for z in levels:
        lv = L(z)
        for x in range(sx, sx + 2 * w):
            for y in range(sy - 1, sy + l + 1):
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

def road(g, x0, y0, x1, y1, horizontal, posts=True):
    """Дорога: асфальт с разметкой посредине, тротуар по краям, фонари. Возвращает прямоугольник асфальта."""
    g.floor(x0 - (0 if horizontal else 1), y0 - (1 if horizontal else 0),
            x1 + (0 if horizontal else 1), y1 + (1 if horizontal else 0), T_WALK)
    g.floor(x0, y0, x1, y1, T_ASPHALT)
    if horizontal:
        mid = (y0 + y1) // 2
        for x in range(x0, x1 + 1, 2):
            g.tiles[(x, mid)] = T_LINE
        if posts:
            for x in range(x0 + 3, x1, 12):
                g.put(POST, x, y1 + 1)
                g.put(POST, x + 6, y0 - 1)
    else:
        mid = (x0 + x1) // 2
        for y in range(y0, y1 + 1, 2):
            g.tiles[(mid, y)] = T_LINE
        if posts:
            for y in range(y0 + 3, y1, 12):
                g.put(POST, x1 + 1, y)
                g.put(POST, x0 - 1, y + 6)
    return (x0, y0, x1, y1)


PROSPECT = (-3, 7, 4, 24)        # проспект: от «Ворот» на юг, между площадью и торговыми рядами
STREET_A = (X0 + 2, 1, X1 - 2, 5)     # улица перед военполом (север)
STREET_B = (X0 + 2, -33, X1 - 2, -29)  # улица за военполом (юг) — к панелькам и складу
LANES = [(-46, -28, -42, 0), (41, -28, 45, 0)]   # проезды по бокам военпола


def beacons():
    """Городские ориентиры на уровне 0 (решение автора 2026-11-06): тактическая карта и приложение «Карты»."""
    g = L(0)
    ax0, ay0, ax1, ay1 = GATES_A
    bx0, by0, bx1, by1 = GATES_B
    g.put('DHMapBeaconGates', (ax1 + bx0) // 2, (ay0 + by1) // 2)
    g.put('DHMapBeaconPolice', (MP[0] + MP[2]) // 2, (MP[1] + MP[3]) // 2 - 3)
    g.put('DHMapBeaconWarehouse', (WAREHOUSE[0] + WAREHOUSE[2]) // 2, (WAREHOUSE[1] + WAREHOUSE[3]) // 2)
    for proto, (x0, y0, x1, y1) in zip(('DHMapBeaconDana14', 'DHMapBeaconDana15'), PANELS):
        g.put(proto, (x0 + x1) // 2, (y0 + y1) // 2 - 3)


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

    asphalt = [road(g, *lane, False) for lane in LANES]
    asphalt.append(road(g, *PROSPECT, False))
    asphalt.append(road(g, *STREET_A, True))
    asphalt.append(road(g, *STREET_B, True))
    # перекрёстки — сплошной асфальт (тротуар поперечной улицы его не режет)
    for (x0, y0, x1, y1) in asphalt:
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                if g.tiles.get((x, y)) == T_WALK:
                    g.tiles[(x, y)] = T_ASPHALT

    # тротуар перед «Воротами»
    g.floor(X0 + 1, 25, X1 - 1, 28, T_WALK)

    # светофоры на перекрёстках
    for (x0, x1, y0, y1) in ((-3, 4, 1, 5), (-46, -42, 1, 5), (41, 45, 1, 5), (-46, -42, -33, -29), (41, 45, -33, -29)):
        g.put(TRAFFIC, x0 - 1, y1 + 1)
        g.put(TRAFFIC, x1 + 1, y0 - 1)

    cars = ['RMCPropVehicleMonoSupronBlack', 'RMCPropVehicleBoxVanBlueGrey', 'RMCPropVehicleBoxVanMaintWhite',
            'RMCPropVehicleMonoSupronBlackDamaged3', 'RMCPropVehicleBoxVanPizza', 'RMCPropVehicleAmbulance']
    for x in range(X0 + 6, X1 - 6, 13):
        if not -6 <= x <= 7:
            g.put(RND.choice(cars), x, 2 if RND.random() < 0.5 else 4)
            g.put(RND.choice(cars), x + 4, -32 if RND.random() < 0.5 else -30)
    for y in range(10, 42, 11):
        g.put(RND.choice(cars), -2, y)
    # мелочи улиц: гидранты, урны, скамейки, клумбы
    for x in range(X0 + 4, X1 - 3, 9):
        if -6 <= x <= 7:
            continue
        g.put(RND.choice(['RMCPropFireHydrantRed', 'RMCTrashBinGreen', 'RMCPlanter', 'RMCBenchLeft']), x, 0)
        g.put(RND.choice(['RMCTrashBinBlue', 'RMCPlanter', 'RMCPropFireHydrantGrey']), x + 4, -28)
    # уличные автоматы (только республиканские кредиты) и банкоматы — на тротуаре у военпола
    for i, x in enumerate((-38, -30, 30, 37)):
        g.put(VENDORS[i % len(VENDORS)], x, 0)
    g.put('DHCityAtm', -34, 0)
    g.put('DHCityAtm', 34, 0)

    # стоянка восточнее «Ворот»
    g.floor(9, 30, X1 - 2, Y1 - 2, T_ASPHALT)
    for x in range(10, X1 - 2, 3):
        g.tiles[(x, 41)] = T_LINE
    for x in range(10, X1 - 4, 4):
        g.put(RND.choice(cars), x, 34)
        g.put(RND.choice(cars), x + 1, 48)
    for x in range(12, X1 - 2, 10):
        g.put(POST, x, 29)
        g.put(POST, x + 5, Y1 - 1)


def square():
    """Небольшая площадь у «Ворот» (запад от проспекта) с павильоном метро сбоку."""
    g = L(0)
    g.floor(X0 + 2, 7, -5, 24, T_PLAZA)
    for x in range(X0 + 4, -6, 8):
        g.put(POST, x, 8)
        g.put(POST, x, 23)
    g.put('RMCBenchLeft', -24, 9)
    g.put('RMCBenchRight', -23, 9)
    g.put('RMCPlanter', -16, 9)
    for i, x in enumerate((-30, -26, -12)):
        g.put(VENDORS[i % len(VENDORS)], x, 22)
    g.put('DHCityAtm', -8, 22)
    g.put('SpawnPointLatejoin', -18, 15)
    for dx in (-3, 0, 3):
        g.put('SpawnPointLatejoin', -18 + dx, 18)
        g.put('SpawnPointDHLAResident', -22 + dx, 12)


def market():
    """Торговые ряды восточнее проспекта (бывшая ЗП-1): лавки впритык, проход посредине."""
    g = L(0)
    x0, y0, x1, y1 = 7, 7, 46, 24
    g.floor(x0, y0, x1, y1, T_PLAZA)
    mid = (y0 + y1) // 2
    for row_y0, row_y1, door_y in ((y0, mid - 2, mid - 2), (mid + 2, y1, mid + 2)):
        for sx in range(x0, x1 - 4, 6):
            exterior(g, sx, row_y0, sx + 6, row_y1, W_STORE, T_SHOP, WINDOW_PLAIN, every=3)
            g.door(sx + 3, door_y, 'AirlockGlass')
            g.put(RND.choice(['RMCPropSupermartShelf1', 'RMCPropSupermartShelf2', 'RMCPropSupermartSodaRack']),
                  sx + 2, (row_y0 + row_y1) // 2)
            g.put('CMTable', sx + 4, (row_y0 + row_y1) // 2)
            g.put(LIGHT, sx + 3, row_y1 - 1, ROT_N)
            roof(L(1), sx, row_y0, sx + 6, row_y1, rails=False)
    for sx in range(x0 + 3, x1 - 4, 12):
        g.put('SpawnPointDHLAEntrepreneur', sx, mid)
    for x in range(x0 + 1, x1, 8):
        g.put(POST, x, mid)


# ------------------------------------------------------------------ «Ворота» (север)

GATES_A = (-44, 31, -25, 50)
GATES_B = (-14, 29, 5, 48)
BRIDGE_Y = (38, 42)


def gates():
    ax0, ay0, ax1, ay1 = GATES_A
    bx0, by0, bx1, by1 = GATES_B
    # парковка −1/−2 под обеими башнями и между ними
    for z in (-2, -1):
        lv = L(z)
        lv.box(ax0, 29, bx1, ay1, W_CONCRETE, T_ASPHALT)
        for x in range(ax0 + 3, bx1 - 2, 4):
            lv.tiles[(x, 39)] = T_LINE
        for x in range(ax0 + 3, bx1 - 3, 7):
            lv.put(RND.choice(['RMCPropVehicleMonoSupronBlack', 'RMCPropVehicleBoxVanBlueGrey']), x, 34)
            lv.put(RND.choice(['RMCPropVehicleMonoSupronBlack', 'RMCPropVehicleMonoSupronBlackDamaged1']), x + 2, 45)
        for x in range(ax0 + 4, bx1 - 3, 8):
            lv.put(LIGHT_DIM, x, ay1 - 1, ROT_N)
    # въезд на парковку между башнями (лестница на −1) и съезд −1 → −2
    stairwell([-2, -1, 0], -20, 32, w=3, l=2)
    L(0).floor(-21, 30, -18, 33, T_CONCRETE)

    # башни: уровни 0..7, крыша +8
    for (x0, y0, x1, y1), name in ((GATES_A, 'A'), (GATES_B, 'B')):
        for z in range(0, 8):
            lv = L(z)
            exterior(lv, x0, y0, x1, y1, W_OFFICE, T_LOBBY if z == 0 else T_OFFICE)
        # лестница и лифт у западной/восточной стены, с −2 до крыши
        sx = x0 + 2
        stairwell(list(range(-2, 9)), sx, y0 + 4, w=3)
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

    # этажи 1..7 первой башни: управа (1–2), офисы (3–7); второй — офисы (1–6)
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
            lv.put('DHCityCommsDistrict', ax0 + 2, ay0 + 10)   # на первом столе кабинета управляющего
        elif z == 2:
            lv.put('DHCityNewsComputer', ax0 + 2, ay0 + 10)    # редакция района
            for i in range(3):
                lv.put('SpawnPointDHLADistrictStaff', mid + 4 + i * 2, ay1 - 3)
        elif z in (3, 4):
            for i in range(3):
                lv.put('SpawnPointDHLAOfficeWorker', ax0 + 3 + i * 2, ay1 - 4)

    # 1–2: поликлиника (clinic()), 3–5: офисы. Для 1–2 мебель «расставляется» в пустоту, чтобы не сдвигать
    # случайные выборы остальной карты.
    for z in range(1, 6):
        lv = L(z) if z >= 3 else Level(z)
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

    # офис LA-V — 6-й и 7-й уровни второй башни (решение автора 2026-11-06), на крыше (+8) — ЗП-1 (landing_zone).
    # Юг этажа — холл с лестницей и лифтом, север — за перегородкой с дверью по доступу LA-V.
    for z in (6, 7):
        lv = L(z)
        lv.floor(bx0 + 1, by0 + 1, bx1 - 1, by1 - 1, T_LAV if z == 6 else T_LAV_CARPET)
        lv.hline(bx0 + 1, bx1 - 1, by0 + 8, W_OFFICE)
        lv.door((bx0 + bx1) // 2, by0 + 8,
                'DHLAAirlockVanguardLocked' if z == 6 else 'DHLAAirlockVanguardOfficeLocked')
        lv.put('CMPottedPlant%d' % (7 if z == 6 else 8), bx0 + 9, by0 + 6)
        lv.put(LIGHT, bx0 + 9, by0 + 7, ROT_N)
    # 6: офис — сотрудники, охрана, оружейная
    lv = L(6)
    furnish_office(lv, bx0, by0 + 8, bx1, by1, fancy=True)
    lv.put('SpawnPointDHLAVEmployee', bx0 + 4, by1 - 3)
    lv.put('SpawnPointDHLAVEmployee', bx0 + 6, by1 - 3)
    lv.put('SpawnPointDHLAVSecurity', (bx0 + bx1) // 2, by0 + 6)
    lv.put('SpawnPointDHLAVSecurity', (bx0 + bx1) // 2 + 1, by0 + 6)
    lv.put('LockerSecurityFilled', bx0 + 1, by0 + 9)
    # у лифта (x1-3, y0+4) место занято шахтой — сейф и терминал у перегородки
    lv.put('DHLAVArmory', bx1 - 2, by0 + 7)
    lv.put('CMTableWooden', bx1 - 4, by0 + 7)
    lv.put('DHCityTerminal', bx1 - 4, by0 + 7)
    # 7: кабинет представителя компании
    lv = L(7)
    cx, top = (bx0 + bx1) // 2, by1 - 1
    lv.put('CMTableWoodenFancy', cx, top - 3)
    lv.put('CMChairComfyBlack', cx, top - 2, ROT_S)
    lv.put('RMCBookcase', bx0 + 1, top)
    lv.put('RMCBookcase', bx0 + 2, top)
    lv.put('DHLAVSafe', bx0 + 3, top)
    lv.put('CMTableWooden', bx1 - 2, top)
    lv.put('DHBudgetComputerVanguard', bx1 - 2, top)
    lv.put('CMPottedPlant7', bx1 - 1, top)
    lv.put('CMPottedPlant8', bx1 - 1, by0 + 9)
    for x in range(bx0 + 2, bx1 - 1, 4):
        lv.put('RMCCouchMidBlack', x, by0 + 10)
    lv.put('SpawnPointDHLAVRepresentative', cx, top - 5)
    lv.put('SpawnPointDHLAVAssistant', cx - 2, top - 5)
    lights_in(lv, bx0, by0 + 8, bx1, by1)
    # переход «Ворота» → площадь: тротуар у входов
    L(0).floor(GATES_A[0], 27, GATES_B[2], 30, T_WALK)


def clinic():
    """Поликлиника «Ворот» — 1-й и 2-й уровни второй башни (решение автора 2026-11-11). Юг этажей — холлы у лестницы
    (запад) и лифта (восток), север — за перегородкой.
      1: приёмная (стойка медсестры, ожидание, NanoMed); операционная (запад, служебная) и кабинет приёма с койками.
      2: кабинет главврача (юг посредине); химлаборатория с аптекой (запад, служебная) и стационар (восток)."""
    bx0, by0, bx1, by1 = GATES_B
    part = by0 + 9          # перегородка: юг — холл, север — помещения
    mid = bx0 + 10          # стена между западным и восточным помещениями
    for z in (1, 2):
        lv = L(z)
        lv.floor(bx0 + 1, by0 + 1, bx1 - 1, by1 - 1, T_METRO)
        lv.hline(bx0 + 1, bx1 - 1, part, W_INNER)
        lv.vline(mid, part + 1, by1 - 1, W_INNER)
        lv.door(bx0 + 6, part, 'DHLAAirlockClinicLocked')       # служебное помещение (запад)
        lv.door(bx1 - 4, part, 'AirlockMedicalGlass')           # к пациентам (восток)
        lights_in(lv, bx0, part, mid, by1)
        lights_in(lv, mid, part, bx1, by1)
        lv.put(LIGHT, mid - 1, part - 1, ROT_N)

    # ---- 1: приёмная, операционная, кабинет приёма
    lv = L(1)
    rx = mid - 2            # стойка регистратуры между лестницей и лифтом
    lv.put('CMTableReinforcedBlack', rx, by0 + 3)
    lv.put('CMTableReinforcedBlack', rx + 1, by0 + 3)
    lv.put('CMChairComfy', rx, by0 + 4, ROT_S)
    lv.put('SpawnPointDHLAClinicNurse', rx + 1, by0 + 4)
    for x in (mid - 2, mid + 1, mid + 4):
        lv.put('RMCCouchMidBlack', x, part - 2)
    lv.put('VendingMachineMedical', bx1 - 1, part - 3)
    lv.put('CMPottedPlant5', bx1 - 1, part - 1)
    # операционная (запад, служебная)
    ox = (bx0 + mid) // 2
    lv.put('OperatingTable', ox, part + 5)
    lv.put('DHIVDripFilled', ox + 1, part + 5)
    # аппараты стационара (этап 6): ИВЛ и жизнеобеспечение у операционного стола
    lv.put('DHVentilator', ox - 1, part + 6, ROT_S)
    lv.put('DHLifeSupport', ox - 1, part + 4, ROT_S)
    lv.put('CrateMedicalSurgery', bx0 + 1, by1 - 1)
    lv.put('LockerMedicalFilled', bx0 + 2, by1 - 1)
    lv.put('SinkWide', mid - 1, by1 - 1)
    lv.put('CMTable', bx0 + 1, part + 2)
    lv.put('BoxSyringe', bx0 + 1, part + 2)
    lv.put('CMTable', bx0 + 1, part + 3)
    lv.put('DHBloodPackFull', bx0 + 1, part + 3)
    lv.put('DHBloodPackFull', bx0 + 1, part + 3)
    lv.put('DHSalinePackFull', bx0 + 1, part + 3)
    # кабинет приёма: два стола врачей и койки
    for i, y in enumerate((part + 3, part + 6)):
        lv.put('CMTable', mid + 3, y)
        lv.put('CMChairComfy', mid + 3, y + 1, ROT_S)
        lv.put('CMChair', mid + 3, y - 1, ROT_N)
        lv.put('SpawnPointDHLAClinicDoctor', mid + 4, y + 1)
    lv.put('HandheldHealthAnalyzer', mid + 3, part + 3)
    for y in (part + 2, part + 5, by1 - 1):
        lv.put('MedicalBed', bx1 - 1, y)
    lv.put('LockerMedicineFilled', mid + 1, by1 - 1)

    # ---- 2: кабинет главврача, лаборатория с аптекой, стационар
    lv = L(2)
    # кабинет главврача — между лестницей (x ≤ bx0+7) и лифтом (x ≥ bx1−4, пульт на bx1−5, by0+5), у южной стены;
    # дверь на север, в коридор холла
    hx0, hx1, hy = mid - 2, mid + 4, by0 + 4
    lv.vline(hx0, by0 + 1, hy, W_INNER)
    lv.vline(hx1, by0 + 1, hy, W_INNER)
    lv.hline(hx0, hx1, hy, W_INNER)
    lv.door((hx0 + hx1) // 2, hy, 'DHLAAirlockClinicHeadLocked')
    lv.floor(hx0 + 1, by0 + 1, hx1 - 1, hy - 1, T_WOOD)
    lv.put('CMTableWooden', hx0 + 2, by0 + 2)
    lv.put('CMChairComfyBlack', hx0 + 2, by0 + 3, ROT_S)
    lv.put('CMFilingCabinetTall', hx1 - 1, by0 + 1)
    lv.put('SpawnPointDHLAClinicHead', hx0 + 3, by0 + 3)
    lv.put(LIGHT_DIM, hx0 + 2, hy - 1, ROT_N)
    # лаборатория и аптека (запад, служебная): раздатчик, ХимМастер, плитка и электролиз на столе, сырьё в ящиках
    lv.put('DHChemDispenser', bx0 + 1, by1 - 1)
    lv.put('DHChemMaster', bx0 + 2, by1 - 1)
    lv.put('SinkWide', bx0 + 3, by1 - 1)
    for x in (bx0 + 5, bx0 + 6, bx0 + 7):
        lv.put('CMTableReinforcedBlack', x, by1 - 1)
    lv.put('DHChemistryHotplate', bx0 + 5, by1 - 1)
    lv.put('DHElectrolysisUnit', bx0 + 6, by1 - 1)
    lv.put('BoxBeaker', bx0 + 7, by1 - 1)
    lv.put('LargeBeaker', bx0 + 7, by1 - 1)
    lv.put('LargeBeaker', bx0 + 7, by1 - 1)
    for x in (bx0 + 1, bx0 + 2):
        lv.put('CMTable', x, part + 4)
    lv.put('BoxPillCanister', bx0 + 1, part + 4)
    lv.put('BoxBottle', bx0 + 2, part + 4)
    lv.put('BoxSyringe', bx0 + 2, part + 4)
    lv.put('DHCrateChemistryAcids', bx0 + 1, part + 1)
    lv.put('DHCrateChemistryBases', bx0 + 2, part + 1)
    lv.put('DHCrateChemistryOrganics', bx0 + 3, part + 1)
    lv.put('LockerMedicineFilled', mid - 1, part + 1)
    lv.put('CMChair', bx0 + 5, by1 - 2, ROT_N)
    lv.put('SpawnPointDHLAClinicPharmacist', bx0 + 6, part + 5)
    # стационар (восток): койки с капельницами, пост медсестры
    for y in (part + 2, part + 5, by1 - 1):
        lv.put('MedicalBed', bx1 - 1, y)
        lv.put('DHIVDripFilled', bx1 - 2, y)
    lv.put('DHVentilator', bx1 - 1, part + 3, ROT_W)
    lv.put('DHLifeSupport', bx1 - 1, part + 4, ROT_W)
    lv.put('CMTable', mid + 2, by1 - 1)
    lv.put('CMChair', mid + 2, by1 - 2, ROT_N)
    lv.put('DHSalinePackFull', mid + 2, by1 - 1)
    lv.put('DHBloodPackFull', mid + 2, by1 - 1)


# ------------------------------------------------------------------ ЗП-1 (крыша «Ворот»), метро (под «Воротами»)

def landing_zone():
    """ЗП-1 «Аэронавтика» — на крыше второй башни «Ворот» (+8), решение автора 2026-11-06. Площадка восточнее
    выхода лестницы: мини-шаттл LA-V (7×14, build_lav_shuttle.py) садится по центру метки DHUprisingFlightPad."""
    roof_ = L(8)
    bx0, by0, bx1, by1 = GATES_B
    x0, y0, x1, y1 = bx1 - 10, by0 + 1, bx1 - 1, by1 - 1
    roof_.floor(x0, y0, x1, y1, T_PAD)
    roof_.floor(x0 + 2, y0 + 2, x1 - 2, y1 - 2, T_PAD2)
    roof_.put('DHUprisingFlightPad', x1 - 5, (y0 + y1) // 2)
    for y in range(y0, y1 + 1, 3):
        roof_.put('LandingZoneLightOne', x0 - 1, y)
    for (x, y) in ((x0 - 2, y0 + 1), (x0 - 2, y1 - 1)):
        roof_.put('RMCLZFloodlight', x, y)
    roof_.put('RMCOverheadSignNeonHotel', bx0 + 4, by1 - 2)
    # нейтральная антенна (канал ОФСК заработает, когда её взломают) — на крыше первой башни
    ax0, ay0, ax1, ay1 = GATES_A
    roof_.put('DHLANeutralAntenna', ax1 - 4, ay1 - 4)


METRO = (-48, 8, -8, 22)
PAVILION = (-44, 11, -34, 19)


def metro():
    g = L(0)
    x0, y0, x1, y1 = PAVILION
    exterior(g, x0, y0, x1, y1, W_METRO, T_METRO)
    g.door(x0, 15, 'AirlockGlass')
    g.door(x1, 15, 'AirlockGlass')
    g.put(LIGHT, (x0 + x1) // 2, y1 - 1, ROT_N)
    roof(L(1), x0, y0, x1, y1, rails=False)
    stairwell([-2, -1, 0], x0 + 3, 14, w=3, l=2)
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
    tech.box(-46, 10, -30, 22, W_METRO, T_TECH)
    for x in range(-44, -31, 4):
        tech.put('RMCMachinePropBig1', x, 20)
        tech.put(LIGHT_DIM, x, 21, ROT_N)


# ------------------------------------------------------------------ военпол (центр)

MP = (-25, -26, 25, -6)


def police():
    x0, y0, x1, y1 = MP
    for z in range(-3, 4):
        lv = L(z)
        exterior(lv, x0, y0, x1, y1, W_POLICE, T_POLICE if z >= 0 else T_DARK, WINDOW if z >= 0 else None)
        lights_in(lv, x0, y0, x1, y1)
    stairwell(list(range(-3, 5)), x0 + 2, y0 + 4, w=3)
    lift(list(range(-3, 4)), x1 - 3, y0 + 4, panel_dx=-2)
    roof(L(4), x0, y0, x1, y1)

    g = L(0)
    mid = (x0 + x1) // 2
    # главный вход — на южную улицу (к панелькам и складу), служебный — на северную
    g.door(mid, y0, 'DHLAAirlockPoliceGlassLocked')
    g.door(mid + 1, y0, 'DHLAAirlockPoliceGlassLocked')
    g.door(mid, y1, 'DHLAAirlockPoliceLocked')
    # дежурная часть, камеры, оружейная
    g.hline(x0 + 1, x1 - 1, y0 + 8, W_POLICE)
    g.door(mid, y0 + 8, 'DHLAAirlockPoliceLocked')
    for cx in range(x0 + 6, x0 + 22, 4):
        g.box(cx, y1 - 6, cx + 4, y1 - 1, W_POLICE)
        g.door(cx + 2, y1 - 6, 'DHLAAirlockPoliceGlassLocked')
        g.put('CMBed', cx + 2, y1 - 3)
        g.put('DHPoliceCell', cx + 2, y1 - 4)        # камера: арест члена ОФСК (задания, решение автора 2026-11-12)
    # оружейная (решение автора 2026-11-06): доступ «оружейная Военной Полиции» — начальник, заместитель, надзиратель
    g.box(x1 - 12, y1 - 7, x1 - 1, y1 - 1, W_POLICE)
    g.door(x1 - 6, y1 - 7, 'DHLAAirlockPoliceArmoryLocked')
    ax0, ay = x1 - 11, y1 - 2          # северный ряд стеллажей: оружие
    guns = (['DHWeaponRifleRMPR6239'] * 3 + ['DHWeaponSMGRMPR0919'] * 3   # Raptor — решение автора 2026-11-07
            + ['DHWeaponSMGRAPTORM1216'] * 2 + ['DHWeaponLMGRAMGM141'] * 2)
    for i, gun in enumerate(guns):
        g.put('CMRack', ax0 + i, ay)
        g.put(gun, ax0 + i, ay)
    ammo = [  # средний ряд столов: магазины и коробки патронов
        ['DHMagazine762x39FMG'] * 4, ['DHMagazine762x39BB'] * 2 + ['DHMagazine762x39E'] * 2,
        ['DHMagazine9x19FMG'] * 4, ['DHMagazine9x19RUB'] * 4,
        ['DHMagazineBox556x45FMG'] * 3, ['DHMagazineBox556x45BB'] * 3,
        ['DHAmmoBox762x39FMG', 'DHAmmoBox762x39BB', 'DHAmmoBox762x39E'],
        ['DHAmmoBox9x19FMG', 'DHAmmoBox9x19RUB', 'DHAmmoBox9x19RUB'],
    ]
    for i, items in enumerate(ammo):
        g.put('CMTableReinforced', ax0 + i, ay - 2)
        for item in items:
            g.put(item, ax0 + i, ay - 2)
    # южный ряд: станковый пулемёт с лентами, наплечники и наколенники
    g.put('DHWeaponHMGRSPMG1054', ax0 + 8, ay - 4)
    g.put('CMTableReinforced', ax0 + 9, ay - 2)
    for item in ['DHMagazineBelt762x51FMG'] * 3:
        g.put(item, ax0 + 9, ay - 2)
    g.put('CMTableReinforced', ax0 + 2, ay - 4)          # магазины 10×24 к Raptor
    for item in ['DHMagazine10x24SVP'] * 3 + ['DHMagazine10x24E'] * 3:
        g.put(item, ax0 + 2, ay - 4)
    for i in range(2):
        g.put('CMRack', ax0 + i, ay - 4)
        for item in ['DHLAMPShoulderPads', 'DHLAMPKneePads'] * 6:   # по одному на руку/ногу: 6 комплектов
            g.put(item, ax0 + i, ay - 4)
    g.put(LIGHT, x1 - 6, y1 - 2, ROT_N)
    for x in range(x0 + 4, x1 - 4, 4):
        g.put('CMTableReinforced', x, y0 + 3)
        g.put('CMChairComfyBlue', x, y0 + 4, ROT_S)
    # дежурная часть: компьютер заданий и терминал базы данных; сервер базы — в архиве (−1)
    g.put('DHComputerTasksPolice', x0 + 12, y0 + 3, ROT_N)
    g.put('DHComputerPoliceDatabase', x0 + 8, y0 + 3, ROT_N)
    L(-1).put('DHPoliceDatabaseServer', x0 + 4, y1 - 2)
    # 1-й уровень — кабинеты и командир, 2–3 — казарма
    lv = L(1)
    furnish_office(lv, x0, y0, x1, y1)
    lv.put('SpawnPointDHLAMPChief', mid, y1 - 3)
    lv.put('CMTable', x1 - 1, y0 + 1)
    lv.put('DHBudgetComputerPolice', x1 - 1, y0 + 1)
    lv.put('CMTable', x1 - 1, y0 + 4)
    lv.put('DHCityTerminal', x1 - 1, y0 + 4)
    lv.put('CMTable', x1 - 1, y0 + 2)
    lv.put('DHCityCommsPolice', x1 - 1, y0 + 2)
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
        L(-3).put('DHPoliceCell', x + 1, y1 - 3)
    # тюремные надзиратели — у изолятора
    L(-3).put('SpawnPointDHLAMPWarden', x0 + 4, y1 - 7)
    L(-3).put('SpawnPointDHLAMPWarden', x0 + 6, y1 - 7)


def side_buildings():
    """По бокам военпола: техузел района (запад, коммунальщики) и бар (восток)."""
    g = L(0)
    # техузел: трансформаторы и будка дежурного
    x0, y0, x1, y1 = -39, -25, -28, -8
    exterior(g, x0, y0, x1, y1, W_STORE, T_TECH, WINDOW_PLAIN, every=4)
    g.door(x0, -16, 'DHLAAirlockUtilityLocked')
    g.hline(x0 + 1, x1 - 1, -19, W_INNER)
    g.door(-33, -19, 'DHLAAirlockUtilityLocked')
    for x in (-37, -33, -30):
        g.put('DHPowerTransformer', x, -23)
    g.put('CMTable', -36, -11)
    g.put('CMChair', -36, -12, ROT_N)
    g.put(LIGHT, -34, y1 - 1, ROT_N)
    g.put(LIGHT, -34, -20, ROT_N)
    for i in range(4):
        g.put('SpawnPointDHLAUtilityWorker', -36 + i * 2, -14)
    roof(L(1), x0, y0, x1, y1, rails=False)
    # бар
    x0, y0, x1, y1 = 28, -25, 39, -8
    exterior(g, x0, y0, x1, y1, W_STORE, T_WOOD, WINDOW_PLAIN, every=3)
    g.door(x1, -16, 'WoodDoor')
    for y in range(-23, -10, 3):
        g.put('CMTableWooden', 31, y)
        g.put('CMChair', 30, y, ROT_E)
    g.put('RMCOverheadSignNeonCafe', x1 + 1, -14)
    g.put(LIGHT, 33, y1 - 1, ROT_N)
    for i in range(2):
        g.put('SpawnPointDHLAEntrepreneur', 36, -20 + i * 3)
    roof(L(1), x0, y0, x1, y1, rails=False)


# ------------------------------------------------------------------ юг: панельки и склад (плотная застройка)

PANELS = [(-47, -52, -5, -41), (-47, -69, -5, -58)]
PANEL_HATCHES = [(-9, -50), (-9, -67)]


def panels():
    for idx, (x0, y0, x1, y1) in enumerate(PANELS):
        for z in range(-1, 5):
            lv = L(z)
            exterior(lv, x0, y0, x1, y1, W_CONCRETE, T_TECH if z < 0 else T_WOOD, WINDOW_PLAIN if z >= 0 else None)
        roof(L(5), x0, y0, x1, y1)
        cy = (y0 + y1) // 2
        stairwell(list(range(-1, 6)), x0 + 1, cy + 3, l=2)
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
        # подъезды с обоих торцов: в западный переулок и в проход к складу
        for x in (x0, x1):
            L(0).door(x, cy, 'AirlockGlass')
            L(0).door(x, cy + 1, 'AirlockGlass')
        # подвал: трубы и хлам, скрытый люк в тоннели −2
        b = L(-1)
        b.put('CMRack', x0 + 8, y1 - 2)
        b.put('CrateGenericSteel', x1 - 2, y0 + 2)
        b.put(LIGHT_DIM, (x0 + x1) // 2, y1 - 1, ROT_N)
        hx, hy = PANEL_HATCHES[idx]
        b.put('DHZHatchDown', hx, hy)
        L(-2).put('DHZLadderUp', hx, hy)
    # тесный двор между панельками
    L(0).floor(-46, -57, -6, -53, T_DIRT)
    for x in range(-44, -6, 6):
        L(0).put(RND.choice(['RMCPlanter', 'RMCBenchLeft', 'RMCTrashBinGreen']), x, -55)
    for y in (-50, -46, -65, -61):
        L(0).put(RND.choice(['RMCTrashBinGreen', 'RMCTrashBinBlue']), -49, y)


WAREHOUSE = (2, -69, 33, -40)
SHED = (36, -64, 46, -42)


def warehouse():
    x0, y0, x1, y1 = WAREHOUSE
    for z in (-1, 0, 1):
        lv = L(z)
        exterior(lv, x0, y0, x1, y1, W_STORE, T_CONCRETE if z >= 0 else T_TECH, WINDOW_PLAIN if z >= 0 else None, every=6)
        lights_in(lv, x0, y0, x1, y1, step=8)
    roof(L(2), x0, y0, x1, y1)
    stairwell([-1, 0, 1, 2], x0 + 2, y0 + 4, l=2)
    lift([-1, 0, 1], x1 - 4, y0 + 5, panel_dx=-2)
    g = L(0)
    g.door(x0, -55, 'DHLAAirlockFactoryLocked')
    g.door(x0, -54, 'DHLAAirlockFactoryLocked')
    g.door((x0 + x1) // 2, y1, 'DHLAAirlockFactoryLocked')
    g.door((x0 + x1) // 2 + 1, y1, 'DHLAAirlockFactoryLocked')
    for z in (0, 1):
        lv = L(z)
        for x in range(x0 + 5, x1 - 4, 3):
            for y in range(y0 + 9, y1 - 2, 5):
                lv.put('CMRack', x, y)
                if RND.random() < 0.5:
                    lv.put('CrateGenericSteel', x, y + 2)
    for i in range(6):
        g.put('SpawnPointDHLAFactoryWorker', x0 + 6 + i * 2, y0 + 3)
    # грузовой лифт LA-V (5×5) и компьютер склада: покупка за деньги корпорации (решение автора 2026-11-12)
    g.put('DHSupplyElevator', x0 + 20, y0 + 5)
    g.put('DHComputerSupplyLAV', x0 + 16, y0 + 7, ROT_E)
    L(-1).put('DHZHatchDown', 13, -55)
    L(-2).put('DHZLadderUp', 13, -55)
    # гаражи-склады восточнее, впритык
    sx0, sy0, sx1, sy1 = SHED
    exterior(g, sx0, sy0, sx1, sy1, W_STORE, T_CONCRETE, None)
    for y in range(sy0 + 5, sy1, 6):
        g.hline(sx0 + 1, sx1 - 1, y, W_STORE)
    for y in range(sy0 + 2, sy1, 6):
        g.door(sx0, y, 'Airlock')
        g.put('CrateGenericSteel', sx0 + 4, y)
        g.put(LIGHT_DIM, sx0 + 6, y + 1, ROT_N)
    roof(L(1), sx0, sy0, sx1, sy1, rails=False)
    # переулок между панельками и складом: мусорные баки, ящики
    for y in range(-68, -42, 5):
        g.put(RND.choice(['RMCTrashBinGreen', 'CrateGenericSteel', 'RMCTrashBinBlue']), -2, y)


# ------------------------------------------------------------------ подземелье

def sewers():
    """−1: коллекторы под улицами (вода посредине), люки на улицах."""
    lv = L(-1)
    runs = [(STREET_A[0] + 1, 2, STREET_A[2] - 1, 4, True), (STREET_B[0] + 1, -32, STREET_B[2] - 1, -30, True),
            (0, 6, 1, 24, False), (-45, -28, -43, 0, False), (42, -28, 44, 0, False)]
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
    manholes = [(-30, 3), (20, 3), (-20, -31), (30, -31), (1, 15), (-44, -14), (43, -14)]
    for x, y in manholes:
        L(0).put('DHZManhole', x, y)
        lv.put('DHZLadderUp', x, y)
    # спуски в тоннели −2
    for x, y in [(0, 7), (45, 3), (-44, -27)]:
        lv.put('DHZHatchDown', x, y)
        L(-2).put('DHZLadderUp', x, y)


def tunnels():
    """−2: заброшенные тоннели и катакомбы, проходы под все здания, кроме военпола."""
    lv = L(-2)
    corridors = [
        (-49, -71, -48, 27),     # западный тоннель
        (45, -71, 46, 27),       # восточный тоннель
        (-47, 25, 44, 26),       # северный — под тротуаром «Ворот»
        (-47, 6, 44, 7),         # под улицей перед военполом
        (-47, -37, 44, -36),     # под южной улицей
        (-47, -71, 44, -70),     # южный
        (-48, 39, -45, 40),      # к парковке «Ворот»
        (-38, 8, -37, 9),        # к техэтажу метро
        (-45, -35, -44, -27),    # к коллектору западного проезда
        (-10, -69, -9, -38),     # под панельками
        (12, -69, 13, -38),      # под складом
    ]
    for x0, y0, x1, y1 in corridors:
        lv.floor(x0, y0, x1, y1, T_UNDER)
    # катакомбы: ниши с костями и мусором
    for cx, cy in [(-30, -50), (28, -52), (14, 14), (-20, 16)]:
        lv.floor(cx - 4, cy - 3, cx + 4, cy + 3, T_DIRT)
        lv.put(LIGHT_DIM, cx, cy + 2, ROT_N)
        lv.put('CrateGenericSteel', cx - 3, cy - 2)
    lv.floor(-26, -50, -11, -49, T_UNDER)      # ниша у панелек → тоннель под панельками
    lv.floor(14, -52, 24, -51, T_UNDER)        # ниша у склада → тоннель под складом
    lv.floor(14, 8, 15, 10, T_UNDER)           # ниша под ЗП → тоннель под улицей
    lv.floor(-20, 17, -19, 24, T_UNDER)        # ниша у площади → северный тоннель
    # двери в парковку «Ворот» (−2) и в техэтаж метро
    lv.unwall(-44, 39)
    lv.put('Airlock', -44, 39)
    lv.unwall(-38, 10)
    lv.put('DHLAAirlockMetroLocked', -38, 10)
    # спуски в катакомбы −3
    for x, y in [(-30, -50), (28, -52), (11, 11)]:
        lv.put('DHZHatchDown', x, y)
        L(-3).put('DHZLadderUp', x, y)


def catacombs():
    """−3: грязные катакомбы и подземный резервуар с водой (под торговыми рядами); логова ОФСК."""
    lv = L(-3)
    x0, y0, x1, y1 = 10, 8, 42, 22
    lv.floor(x0, y0, x1, y1, T_UNDER)
    for x in range(x0 + 2, x1 - 1):
        for y in range(y0 + 2, y1 - 1):
            deep = x0 + 6 < x < x1 - 6 and y0 + 6 < y < y1 - 6
            lv.put('DHWaterDeep' if deep else 'DHWaterShallow', x, y)
    corridors = [(-41, -62, -39, 32), (-39, -62, 30, -61), (29, -61, 30, -59), (-39, 14, 10, 15),
                 (-39, 19, -35, 20), (-39, 30, -2, 32)]
    for cx0, cy0, cx1, cy1 in corridors:
        lv.floor(cx0, cy0, cx1, cy1, T_DIRT)
    for i, (cx, cy) in enumerate([(-35, -50), (30, -55), (-30, 19)]):
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
    lv.put(LIGHT_DIM, 12, 21, ROT_N)
    # скрытые проходы наверх: в парковку «Ворот» (−2) и в техэтаж метро
    lv.put('DHZLadderUp', -2, 31)
    L(-2).put('DHZHatchDown', -2, 31)
    lv.put('DHZLadderUp', -40, 18)
    L(-2).put('DHZHatchDown', -40, 18)


KEEP = {'DHZStairs', 'DHZStairwell', 'DHZStairShaft', 'DHZStairStep', 'DHZStairStepLeft', 'DHZStairStepRight',
        'DHZStairsTop', 'DHZStairsTopLeft', 'DHZStairsTopRight', 'DHZLiftPlatform', 'DHZLiftPanel'}


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
    return [e for e in ents if (e[1], e[2]) in lv.tiles or e[0] == 'DHZStairShaft']


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
    # canSplit: False — иначе движок при загрузке режет грид уровня на острова (здания) в новые гриды со своим
    # смещением: стены и платформы оказываются не прикручены, лифты рвутся (ошибка, найдена автором 2026-11-06).
    lines += ['    - type: MapGrid', '      canSplit: False', '      chunks:']
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


def task_markers():
    """Объекты для подрыва (задания ОФСК) и логова ОФСК (пленные военпола) — решение автора 2026-11-12."""
    L(0).put('DHBombTargetTransformer', -33, -22)                       # техузел района
    mx0, my0, mx1, my1 = METRO
    L(-1).put('DHBombTargetMetro', (mx0 + mx1) // 2, (my0 + my1) // 2)   # станция метро
    bx0, by0, bx1, by1 = GATES_B
    cx, cy = (bx0 + bx1) // 2, (by0 + by1) // 2
    L(1).put('DHBombTargetClinic', cx, cy)                              # поликлиника «Ворот»
    L(6).put('DHBombTargetLAVOffice', cx, cy)                           # офис LA-V
    wx0, wy0, wx1, wy1 = WAREHOUSE
    L(0).put('DHBombTargetLAVWarehouse', (wx0 + wx1) // 2, (wy0 + wy1) // 2)
    px0, py0, px1, py1 = MP
    L(0).put('DHBombTargetPoliceLobby', (px0 + px1) // 2, py0 + 4)      # лобби военпола (дежурная часть)
    for cx, cy in [(-35, -50), (30, -55), (-30, 19)]:
        L(-3).put('DHFrontLair', cx, cy)


def task_spots():
    """Места для закладок и бомб заданий военпола (решение автора 2026-11-12): случайные свободные клетки пола на
    уровнях −3..+1, кроме здания военпола и лестничных проёмов."""
    rnd = random.Random(1112)
    mx0, my0, mx1, my1 = MP
    for z, count in ((-3, 10), (-2, 12), (-1, 12), (0, 24), (1, 10)):
        lv = L(z)
        taken = {(e[1], e[2]) for e in lv.ents}
        shafts = set(SHAFT_CELLS.get(z, ())) | set(STAIR_CELLS.get(z, ()))
        cells = sorted(c for c in lv.tiles
                       if c not in lv.walls and c not in taken and c not in shafts
                       and not (mx0 <= c[0] <= mx1 and my0 <= c[1] <= my1)
                       and X0 <= c[0] <= X1 and Y0 <= c[1] <= Y1)
        for c in rnd.sample(cells, min(count, len(cells))):
            lv.put('DHTaskSpot', c[0], c[1])


def main():
    streets()
    square()
    market()
    gates()
    clinic()
    police()
    side_buildings()
    panels()
    warehouse()
    landing_zone()
    beacons()
    metro()
    sewers()
    tunnels()
    catacombs()
    clean_passages()
    rock_walls()
    task_spots()
    task_markers()

    # проёмы лестниц: пол убирается (на земле — нет, там под ним грунт планеты)
    for z, cells in SHAFT_CELLS.items():
        if z == 0:
            continue
        for cell in cells:
            L(z).tiles.pop(cell, None)
            L(z).unwall(*cell)

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
