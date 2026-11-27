"""Имперский десантный авианосец IC-3 «Соверен» точно по схеме автора (2026-11-11), нос — вверху.

Схема задана прямоугольниками и треугольниками в пикселях рисунка автора (PX ниже); 1 клетка = 4 px.
Помещения примыкают друг к другу без коридоров, как на схеме:
- разрывной генератор и суперконденсатор — узкий шпиль на носу;
- корабельная артиллерия со скосом по правому борту;
- «Зона вылета 1» (стоянка «Мидуэя»: вырез в палубе по клеткам челнока, по краю — воздушные завесы) и ВПП справа;
- карго под «Зоной вылета 1», казармы под карго, брифинговая под ВПП;
- ПВО — треугольник справа от ВПП, под ним штаб и комнаты командования;
- инженерный отсек между двумя двигателями;
- блок на корме — медицинская и химическая лаборатории с операционными (просьба автора 2026-11-11).
Питание: реакторы (ВН) → терминал → СМЭС → подстанции (СН) → ЛКП в каждом помещении (НН) → свет и техника.

Три уровня (решения автора 2026-11-27):
- 0 — нижняя палуба (этот грид — сам авианосец): шпиль, артиллерия, ангар челнока (нижний ярус «Мидуэя» стоит на полу),
  карго, казармы, брифинговая, инженерный, двигатели, лаборатории; под ВПП — ангарная палуба (орудия, снаряжение,
  экипажи), под ПВО — погреб, под надстройкой — каюты командования и кают-компания;
- 1 — полётная палуба (imperial_carrier_deck.yml): ВПП со штурмовиком и лифтами техники, вырез под кабину «Мидуэя»,
  ПВО, основание надстройки;
- 2 — надстройка (imperial_carrier_island.yml): мостик и штаб, над ней — крыша.
Между уровнями — универсальные лестницы и грузовой Z-лифт (ВПП ↔ ангарная палуба). Уровни 1 и 2 питаются от фидера
нижней палубы. Гриды верхних уровней ходят вслед за авианосцем (DHCarrierDecksSystem).

Запуск из корня репозитория:  python3 Tools/_DarkestHour/carrier/build_carrier.py
Карты целиком перезаписываются (Resources/Maps/_DarkestHour/Shuttles/imperial_carrier*.yml)."""
import base64
import json
import os
import math
import re
import struct
from collections import deque

OUT = 'Resources/Maps/_DarkestHour/Shuttles/imperial_carrier.yml'
OUT_DECK = 'Resources/Maps/_DarkestHour/Shuttles/imperial_carrier_deck.yml'
OUT_ISLAND = 'Resources/Maps/_DarkestHour/Shuttles/imperial_carrier_island.yml'
MIDWAY = json.load(open('Tools/_DarkestHour/midway/footprint.json'))
PI = math.pi

# ---------------------------------------------------------------- схема автора (пиксели рисунка)
PX0, PY0, SCALE = 715, 422, 4
PX = {  # имя: многоугольник (x вправо, y вниз)
    'rift':     [(715, 185), (735, 185), (735, 297), (715, 297)],
    'art':      [(715, 297), (779, 297), (807, 422), (715, 422)],
    'zone':     [(715, 422), (807, 422), (807, 526), (715, 526)],
    'runway':   [(807, 422), (875, 422), (875, 678), (807, 678)],
    'cargo':    [(715, 526), (807, 526), (807, 630), (715, 630)],
    'barracks': [(715, 630), (807, 630), (807, 822), (715, 822)],
    'brief':    [(807, 678), (875, 678), (875, 822), (807, 822)],
    'pvo':      [(875, 598), (935, 674), (875, 674)],
    'hq':       [(875, 674), (935, 674), (935, 822), (875, 822)],
    'engL':     [(739, 822), (767, 822), (767, 942), (739, 942)],
    'eng':      [(767, 822), (875, 822), (875, 962), (767, 962)],
    'engR':     [(875, 822), (903, 822), (903, 942), (875, 942)],
    'labs':     [(783, 962), (859, 962), (859, 1094), (783, 1094)],
}
ORDER = list(PX)


def to_tiles(poly):
    return [(round((x - PX0) / SCALE), -round((y - PY0) / SCALE)) for x, y in poly]


POLY = {k: to_tiles(v) for k, v in PX.items()}


def inside(poly, px, py):
    c = False
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        if (y1 > py) != (y2 > py):
            xi = x1 + (py - y1) * (x2 - x1) / (y2 - y1)
            if px < xi:
                c = not c
    return c


REGION = {}   # (x, y) -> имя помещения
for name in ORDER:
    poly = POLY[name]
    xs = [p[0] for p in poly]
    ys = [p[1] for p in poly]
    for x in range(min(xs), max(xs) + 1):
        for y in range(min(ys), max(ys) + 1):
            if (x, y) not in REGION and inside(poly, x + 0.5, y + 0.5):
                REGION[(x, y)] = name

# ---------------------------------------------------------------- подразделы внутри помещений (свои стены)
SUB = {}  # (x, y) -> имя подраздела; стены между подразделами строятся так же, как между помещениями


def subdivide(parent, name, x0, y0, x1, y1):
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            if REGION.get((x, y)) == parent:
                REGION[(x, y)] = name
    ORDER.append(name)


def bbox(name):
    cells = [p for p, r in REGION.items() if r == name]
    return (min(p[0] for p in cells), min(p[1] for p in cells), max(p[0] for p in cells), max(p[1] for p in cells))


# штаб: мостик сверху, ниже — коридор и четыре комнаты командования
hx0, hy0, hx1, hy1 = bbox('hq')
subdivide('hq', 'cmd1', hx0, hy0 + 10, (hx0 + hx1) // 2 - 1, hy0 + 18)
subdivide('hq', 'cmd2', (hx0 + hx1) // 2 + 1, hy0 + 10, hx1, hy0 + 18)
subdivide('hq', 'cmd3', hx0, hy0, (hx0 + hx1) // 2 - 1, hy0 + 9)
subdivide('hq', 'cmd4', (hx0 + hx1) // 2 + 1, hy0, hx1, hy0 + 9)
subdivide('hq', 'hqhall', (hx0 + hx1) // 2, hy0, (hx0 + hx1) // 2, hy0 + 18)
# лаборатории: медотсек сверху, снизу операционные и химия с вирусологией, морг
lx0, ly0, lx1, ly1 = bbox('labs')
subdivide('labs', 'or1', lx0, ly0, lx0 + 5, ly0 + 9)
subdivide('labs', 'chem', lx0 + 6, ly0, lx1 - 6, ly0 + 9)
subdivide('labs', 'or2', lx1 - 5, ly0, lx1, ly0 + 9)
subdivide('labs', 'morgue', lx1 - 5, ly1 - 6, lx1, ly1)

HULL, WALL, WHITE, WINDOW = 'DHCarrierHull', 'DHCarrierWall', 'DHCarrierWallWhite', 'DHCarrierWindow'
WHITE_ROOMS = {'labs', 'or1', 'or2', 'chem', 'morgue'}
FLOOR = {
    'rift': 'CMFloorBlack', 'art': 'CMFloorPlate', 'zone': 'CMFloorPlate', 'runway': 'CMFloorPlate',
    'cargo': 'CMFloorCargo', 'barracks': 'CMFloorSteel', 'brief': 'CMFloorOffice', 'pvo': 'CMFloorBlack',
    'hq': 'CMFloorBlack', 'hqhall': 'CMFloorSteel', 'cmd1': 'CMFloorOffice', 'cmd2': 'CMFloorOffice',
    'cmd3': 'CMFloorOffice', 'cmd4': 'CMFloorOffice', 'engL': 'CMFloorBlack', 'eng': 'CMFloorBlack',
    'engR': 'CMFloorBlack', 'labs': 'CMFloorWhite', 'or1': 'CMFloorSterile', 'or2': 'CMFloorSterile',
    'chem': 'RMCFloorResearchContainmentFloor2North', 'morgue': 'CMFloorSterile',
    # уровни (2026-11-27)
    'hangar': 'CMFloorPlate', 'magazine': 'CMFloorBlack', 'wardroom': 'CMFloorOffice', 'island': 'CMFloorSteel',
    'bridge': 'CMFloorBlack',
}
N8 = [(dx, dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy]
N4 = [(0, 1), (0, -1), (1, 0), (-1, 0)]

# ---------------------------------------------------------------- уровни
BASE_REGION = dict(REGION)
BASE_ORDER = list(ORDER)
HQ_ROOMS = {'hq', 'hqhall', 'cmd1', 'cmd2', 'cmd3', 'cmd4'}
RENAME0 = {'runway': 'hangar', 'pvo': 'magazine', 'hq': 'wardroom'}
DECK_REGIONS = {
    0: ({p: RENAME0.get(r, r) for p, r in BASE_REGION.items()}, [RENAME0.get(n, n) for n in BASE_ORDER]),
    1: ({p: ('island' if r in HQ_ROOMS else r) for p, r in BASE_REGION.items()
         if r in ('zone', 'runway', 'pvo') or r in HQ_ROOMS}, ['zone', 'runway', 'pvo', 'island']),
    2: ({p: 'bridge' for p, r in BASE_REGION.items() if r in HQ_ROOMS}, ['bridge']),
}
DECKS = {}


def use_deck(level):
    """Сделать уровень текущим: все помощники ниже работают с его клетками, стенами и сущностями."""
    global REGION, ORDER, RANK, T, W, E, CABLE, INNER, APCS
    d = DECKS[level]
    REGION, ORDER, RANK, T, W, E, CABLE, INNER, APCS = (d['REGION'], d['ORDER'], d['RANK'], d['T'], d['W'], d['E'],
                                                        d['CABLE'], d['INNER'], d['APCS'])


def new_deck(level):
    region, order = DECK_REGIONS[level]
    d = {'REGION': dict(region), 'ORDER': list(order), 'RANK': {n: i for i, n in enumerate(order)}, 'T': {}, 'W': {},
         'E': [], 'CABLE': {'CableHV': set(), 'CableMV': set(), 'CableApcExtension': set()}, 'INNER': {}, 'APCS': []}
    DECKS[level] = d
    use_deck(level)
    for p, r in REGION.items():
        T[p] = FLOOR[r]
    for (x, y), r in REGION.items():
        for dx, dy in N8:
            o = REGION.get((x + dx, y + dy))
            if o is None:
                W[(x, y)] = HULL
                break
            if o != r and RANK[r] < RANK[o]:
                W[(x, y)] = WHITE if r in WHITE_ROOMS or o in WHITE_ROOMS else WALL
    for p, r in REGION.items():
        if p not in W:
            INNER.setdefault(r, set()).add(p)


new_deck(0)


def ibox(name):
    c = INNER[name]
    return (min(p[0] for p in c), min(p[1] for p in c), max(p[0] for p in c), max(p[1] for p in c))


def free(p):
    return p in T and p not in W


# ---------------------------------------------------------------- двери и проёмы
def between(a, b):
    """Стены между помещениями a и b, через которые можно пройти (с одной стороны пол a, с другой — пол b)."""
    out = []
    for p, w in W.items():
        if w == HULL:
            continue
        x, y = p
        for (dx, dy) in ((1, 0), (0, 1)):
            s1, s2 = (x - dx, y - dy), (x + dx, y + dy)
            if s1 in INNER.get(a, ()) and s2 in INNER.get(b, ()) or s1 in INNER.get(b, ()) and s2 in INNER.get(a, ()):
                out.append(p)
    return sorted(set(out))


def door(a, b, proto='AirlockSyndicateGlass', width=1, at=None):
    c = between(a, b)
    if not c:
        raise SystemExit('нет общей стены: %s / %s' % (a, b))
    if at is None:
        mid = c[len(c) // 2]
    else:
        mid = min(c, key=lambda p: abs(p[0] - at[0]) + abs(p[1] - at[1]))
    i = c.index(mid)
    chosen = c[max(0, i - width // 2): i - width // 2 + width]
    for p in chosen:
        W[p] = proto
    return chosen


def opening(a, b, margin=1):
    c = between(a, b)
    for p in c[margin:len(c) - margin]:
        del W[p]
        INNER.setdefault(REGION[p], set()).add(p)


def put(proto, x, y, rot=None, n=1, force=False):
    if not force and not free((x, y)):
        return False
    for _ in range(n):
        E.append((proto, x, y, rot))
    return True


FACE = {'S': 0.0, 'N': PI, 'E': PI / 2, 'W': -PI / 2}


def spawn(proto, cells, n):
    """Точки появления должности (решение автора 2026-11-13): до n штук на свободных клетках пола (не стена)."""
    placed = 0
    for p in cells:
        if placed >= n:
            break
        if p in T and p not in W and not any(e[0].startswith('DHSpawnPoint') and (e[1], e[2]) == p for e in E):
            E.append((proto, p[0], p[1], None))
            placed += 1
    assert placed, 'нет места для ' + proto
    return placed


# ---------------------------------------------------------------- питание
def bfs(start, goal, allowed):
    q = deque([start])
    prev = {start: None}
    while q:
        c = q.popleft()
        if c == goal:
            break
        for dx, dy in N4:
            n = (c[0] + dx, c[1] + dy)
            if n not in prev and n in allowed:
                prev[n] = c
                q.append(n)
    if goal not in prev:
        raise SystemExit('нет пути %s -> %s' % (start, goal))
    path = []
    c = goal
    while c is not None:
        path.append(c)
        c = prev[c]
    return path


SUBSTATION = None


def apc(room, prefer='W'):
    """ЛКП на стене помещения + НН «гребёнкой» (ряды через 6 клеток) + свет по северным и южным стенам."""
    cells = INNER[room]
    x0, y0, x1, y1 = ibox(room)
    # стена для ЛКП: соседняя с полом этого помещения, не дверь; ближе к выбранной стороне
    cand = []
    for (x, y) in cells:
        for side, (dx, dy) in (('W', (-1, 0)), ('E', (1, 0)), ('N', (0, 1)), ('S', (0, -1))):
            w = (x + dx, y + dy)
            if W.get(w) in (WALL, WHITE, HULL) and REGION.get(w) in (room, None) or W.get(w) in (WALL, WHITE, HULL):
                cand.append((side, w, (x, y)))
    key = {'W': lambda c: (c[0] != 'W', c[2][0], abs(c[2][1] - (y0 + y1) / 2)),
           'E': lambda c: (c[0] != 'E', -c[2][0], abs(c[2][1] - (y0 + y1) / 2)),
           'N': lambda c: (c[0] != 'N', -c[2][1], abs(c[2][0] - (x0 + x1) / 2)),
           'S': lambda c: (c[0] != 'S', c[2][1], abs(c[2][0] - (x0 + x1) / 2))}[prefer]
    taken = {(a[1], a[2]) for a in APCS}
    side, wall, inner = min((c for c in cand if (c[1], c[2]) not in taken and c[1] not in {a[1] for a in APCS}), key=key)
    E.append(('APCBasic', wall[0], wall[1], {'N': 0.0, 'S': PI, 'W': PI / 2, 'E': -PI / 2}[side]))
    APCS.append((room, wall, inner))
    CABLE['CableApcExtension'].add(wall)
    rows = list(range(y0 + 2, y1 + 1, 6)) or [y0]
    if y1 - rows[-1] > 3:
        rows.append(max(y0, y1 - 2))
    for r in rows:
        run = sorted(x for (x, y) in cells if y == r)
        if not run:
            continue
        for x in run:
            CABLE['CableApcExtension'].add((x, r))
        target = min(((x, r) for x in run), key=lambda p: abs(p[0] - inner[0]))
        for p in bfs(inner, target, cells):
            CABLE['CableApcExtension'].add(p)
    # свет
    for (x, y) in sorted(cells):
        for side, (dx, dy), rot in (('N', (0, 1), 0.0), ('S', (0, -1), PI)):
            if (x + dx, y + dy) in W and W[(x + dx, y + dy)] in (WALL, WHITE, HULL, WINDOW):
                if (x - x0) % 5 == 2 and (side == 'N' or y1 - y0 >= 4):
                    E.append(('Poweredlight', x, y, rot))
    if x1 - x0 <= 4:   # узкие помещения — свет по боковой стене
        for (x, y) in sorted(cells):
            if (x - 1, y) in W and (y - y0) % 5 == 2:
                E.append(('Poweredlight', x, y, PI / 2))


# ================================================================ наполнение
def C(name):
    x0, y0, x1, y1 = ibox(name)
    return (x0 + x1) // 2, (y0 + y1) // 2


# --- разрывной генератор и суперконденсатор (шпиль)
x0, y0, x1, y1 = ibox('rift')
cx = (x0 + x1) // 2
put('DHRiftGeneratorShip', cx, y1 - 2)
put('DHRiftSupercapacitorShip', cx, y1 - 6)
put('ClosetEmergencyFilledRandom', x0, y0 + 1)
door('rift', 'art', 'AirlockEngineeringLocked')

# --- артиллерия
x0, y0, x1, y1 = ibox('art')
guns = (('DHHeavyGunBreech300', 'DHHeavyGunBarrel300', x0 + 3),
        ('DHHeavyGunBreech570', 'DHHeavyGunBarrel570', (x0 + x1) // 2 - 1),
        ('DHHeavyGunBreech300', 'DHHeavyGunBarrel300', x1 - 6))
gy = y1 - 8
for breech, barrel, gx in guns:
    # орудия орбитального удара (спрайт RMC) — стволом на запад, рисунок на 4 клетки: gx - 3 … gx + 1
    put(breech, gx, gy, PI / 2)
    put(barrel, gx - 1, gy, PI / 2)
    put('DHComputerNavalArtillery', gx + 1, gy - 2, FACE['S'])
    put('ChairOfficeDark', gx + 1, gy - 3, FACE['N'])
    spawn('DHSpawnPointImperialArtillerist', [(gx + 1, gy - 4), (gx + 2, gy - 4)], 1)
for i, wh in enumerate(('HighExplosive', 'ArmorPiercing', 'Incendiary', 'Cluster')):
    put('DHHeavyShell300' + wh, x0 + i, y0, n=3)
    put('DHHeavyShell570' + wh, x0 + 5 + i, y0, n=2)
    put('DHHeavyShell300' + wh, x0 + 10 + i, y0, n=3)
put('SyndieFlag', x0, y1)
put('SyndieFlag', x0 + 6, y1)
door('art', 'zone', 'AirlockSecurityGlassLocked', width=3)

# --- ангар челнока (уровень 0): пол целый, на нём стоит нижний ярус «Мидуэя»; кабина — уровнем выше, в вырезе
# полётной палубы. Клетка (0, 0) «Мидуэя»: вверх 10 клеток, вниз 12; сверху и снизу — проход.
x0, y0, x1, y1 = ibox('zone')
MID = ((x0 + x1) // 2, y1 - 11)
HOLE = {(MID[0] + x, MID[1] + y) for x, y in MIDWAY}
bad = [p for p in HOLE if REGION.get(p) != 'zone' or p in W]
if bad:
    raise SystemExit('«Мидуэй» не помещается в «Зону вылета 1»: %s' % sorted(bad)[:5])
opening('zone', 'hangar', margin=2)

# --- ангарная палуба (уровень 0, под ВПП): орудия, снаряжение десанта, экипажи и расчёты
rx0, ry0, rx1, ry1 = ibox('hangar')
put('DHFieldGun75', rx1 - 1, ry1 - 8)
put('DHCrateFieldGun75', rx1 - 1, ry1 - 9)
put('DHCrateMortar', rx1 - 1, ry1 - 11)
put('DHCrateMortar', rx1 - 1, ry1 - 12)
put('DHCrateParachutes', rx1 - 1, ry1 - 14)
put('DHCrateParachutes', rx1 - 1, ry1 - 15)
put('DHDropBeacon', rx1 - 1, ry1 - 17, n=4)
put('DHTentImperialFolded', rx1 - 1, ry1 - 19)
put('DHTentImperialMedicalFolded', rx1 - 1, ry1 - 20)
put('DHCamoNetFolded', rx1 - 1, ry1 - 22, n=2)
put('DHClothingOuterGhillie', rx1 - 1, ry1 - 23, n=2)
put('DHFlagEmpire', rx1 - 1, ry1 - 24, n=2)   # водрузить на захваченной позиции
spawn('DHSpawnPointImperialArmor', [(rx1 - 3, ry1 - 8 - i) for i in range(5)], 4)
spawn('DHSpawnPointImperialArtillery', [(rx1 - 3, ry1 - 14 - i) for i in range(6)], 4)
assert put('DHPowerLoader', rx0 + 2, ry1 - 2), 'погрузчик на ангарной палубе'

# --- погреб (уровень 0, под ПВО): запас ракет-перехватчиков
door('hangar', 'magazine', 'AirlockSecurityGlassLocked')
mx0, my0, mx1, my1 = ibox('magazine')
put('DHInterceptorMissile', mx0, my0, n=6)
put('DHInterceptorMissile', mx0 + 1, my0, n=6)
put('Rack', mx0 + 2, my0)

# --- карго
x0, y0, x1, y1 = ibox('cargo')
ex, ey = x0 + 5, (y0 + y1) // 2
put('DHSupplyElevator', ex, ey)
for dx, dy in ((4, -3), (4, 3), (-3, -3), (-3, 3), (5, 0)):   # погрузчик у грузового лифта
    if put('DHPowerLoader', ex + dx, ey + dy):
        break
else:
    raise SystemExit('нет места для погрузчика у грузового лифта')
put('DHComputerSupply', ex + 5, ey + 3, FACE['S'])
# площадка сброса на маяк 3×3 и её консоль (план осады п. 4)
put('DHSupplyDropPad', x0 + 3, y0 + 3)
assert put('DHComputerSupplyDrop', x0 + 6, y0 + 3, FACE['W']), 'консоль сброса грузов'
put('ChairOfficeDark', ex + 5, ey + 2, FACE['N'])
for i in range(4):
    put('CrateFilledSpawner', x1 - 1 - i, y1)
put('Rack', x1, y0)
put('Rack', x1 - 1, y0)
put('LockerEvacRepairFilled', x1, y0 + 3)
# каргоавтоматы (план осады п. 27): боеприпасы и мелочи — у правой стены карго
for proto in ('DHCargoVendorEmpireAmmo', 'DHCargoVendorEmpireSupplies'):
    assert any(put(proto, x1, y) for y in range(y0 + 4, y1)), proto
door('cargo', 'zone', 'AirlockCargoGlassLocked', width=3)
door('cargo', 'hangar', 'AirlockCargoGlassLocked', width=2)

# --- казармы (как на «Алмаере») с уголком подготовки у входа
x0, y0, x1, y1 = ibox('barracks')
vend = ['DHGearVendorEmpireWeapons', 'DHGearVendorEmpireAttachments', 'DHGearVendorEmpireEquipment',
        'DHGearVendorEmpireMedic', 'DHArmorRack', 'DHArmorRack', 'DHFieldLathe', 'VendingMachineTankDispenserEVA']
for i, v in enumerate(vend):
    put(v, x0 + 1 + i * 2, y1)
put('WeaponCapacitorRecharger', x1, y1 - 2)
put('PowerCellRecharger', x1, y1 - 3)
spawns = 0
for row_y in range(y1 - 5, y0 + 2, -5):
    for x in range(x0 + 1, x1, 2):
        if put('RMCBedBunk', x, row_y):
            put('DHSpawnPointImperialInfantry', x, row_y)
            put('DHSpawnPointImperialInfantry', x, row_y - 1)
            spawns += 2
    for x in range(x0 + 2, x1, 4):
        put('LockerSyndicatePersonal', x, row_y + 2)
put('TableReinforced', x1 - 1, y0)
put('TableReinforced', x1, y0)
door('barracks', 'cargo', width=3)
door('barracks', 'brief', width=2)
door('barracks', 'eng', 'AirlockEngineeringLocked', width=2)
door('barracks', 'hangar', width=2)

# --- брифинговая
x0, y0, x1, y1 = ibox('brief')
put('Screen', (x0 + x1) // 2, y1 + 1, 0.0, force=True)
put('BannerSyndicate', x0, y1)
put('BannerSyndicate', x1, y1)
for x in range((x0 + x1) // 2 - 1, (x0 + x1) // 2 + 2):
    put('TableReinforced', x, y1 - 1)
for y in range(y1 - 4, y0 + 3, -2):
    for x in range(x0 + 1, x1):
        if x != (x0 + x1) // 2:
            put('ChairFolding', x, y, FACE['N'])
for x in range(x0, x0 + 6):
    put('SpawnPointLatejoin', x, y0)
door('brief', 'hangar', width=3)
door('brief', 'wardroom')
door('brief', 'eng', 'AirlockEngineeringLocked', width=2)

# --- кают-компания и каюты командования (уровень 0, под надстройкой); мостик и штаб — на уровне 2
x0, y0, x1, y1 = ibox('wardroom')
cx = (x0 + x1) // 2
for dx in (-1, 0, 1):
    put('TableReinforced', cx + dx, y0 + 2)
    put('ChairOfficeDark', cx + dx, y0 + 1, FACE['N'])
put('DHFlagEmpirePlanted', x0, y0)
put('DHFlagEmpirePlanted', x1, y0)
# каюты командования: только командный доступ Империи (решение автора 2026-11-13); в каждой — своя должность и
# стойки снаряжения (раздатчики снаряжения и модулей, стойка обслуживания брони)
CMD = 'DHAirlockImperialCommandGlassLocked'
door('wardroom', 'magazine', CMD)
door('wardroom', 'hqhall', CMD)
CABINS = (('cmd1', 'DHSpawnPointImperialOperationCommander'), ('cmd2', 'DHSpawnPointImperialCaptain'),
          ('cmd3', 'DHSpawnPointImperialMarineCommander'), ('cmd4', 'DHSpawnPointImperialSupportOfficer'))
for room, point in CABINS:
    door(room, 'hqhall', CMD)
    a0, b0, a1, b1 = ibox(room)
    put('Bed', a0, b1)
    put('BedsheetSyndie', a0, b1, force=True)
    put('LockerSyndicatePersonal', a1, b1)
    put('DHGearVendorEmpireEquipment', a0 + 2, b1)
    put('DHGearVendorEmpireAttachments', a0 + 3, b1)
    put('DHArmorRack', a1, b1 - 2)
    put('TableReinforced', a0, b0)
    put('TableReinforced', a0 + 1, b0)
    put('ChairOfficeDark', a0, b0 + 1, FACE['S'])
    put('PottedPlantRandom', a1, b0)
    spawn(point, [(a0 + 2, (b0 + b1) // 2), (a0 + 3, (b0 + b1) // 2)], 1)
door('cmd3', 'engR', 'AirlockEngineeringLocked')

# --- инженерный отсек
x0, y0, x1, y1 = ibox('eng')
cx = (x0 + x1) // 2
REACTORS = ((cx - 6, y1 - 4), (cx + 6, y1 - 4))
for rx, ry in REACTORS:
    put('DHFusionReactorShip', rx, ry)
put('CableTerminal', cx, y1 - 6, 0.0)
put('SMESBasic', cx, y1 - 7)
SUBS = ((cx - 2, y1 - 9), (cx + 2, y1 - 9))
for sx, sy in SUBS:
    put('SubstationBasic', sx, sy)
put('DHFusionFuelCassette', x0, y1, n=4)
put('DHFusionFuelCassette', x1, y1, n=4)
for i, proto in enumerate(('LockerEngineerFilled', 'LockerEngineerFilled', 'VendingMachineEngivend', 'VendingMachineYouTool',
                           'CrateEngineeringCableBulk', 'SheetSteel', 'SheetPlasteel', 'SheetGlass',
                           'WeldingFuelTankFull', 'WaterTankFull')):
    put(proto, x0 + 1 + i * 2, y0)
spawn('DHSpawnPointImperialTechnician', [(cx + d, y0 + 3) for d in (-3, -2, 2, 3)], 3)
door('eng', 'engL', 'AirlockEngineeringLocked', width=2)
door('eng', 'engR', 'AirlockEngineeringLocked', width=2)
door('eng', 'labs', 'AirlockMedicalGlassLocked', width=3)
line_hv = bfs(REACTORS[0], REACTORS[1], INNER['eng'] | set(REACTORS))
CABLE['CableHV'].update(line_hv)
CABLE['CableHV'].update(bfs((cx, y1 - 4), SUBS[0], INNER['eng']))
CABLE['CableHV'].update(bfs((cx, y1 - 4), SUBS[1], INNER['eng']))
CABLE['CableHV'].update({(cx, y1 - 6), (cx, y1 - 7)})
for s in SUBS:
    CABLE['CableMV'].add(s)

# --- двигатели
for room in ('engL', 'engR'):
    x0, y0, x1, y1 = ibox(room)
    put('ClosetEmergencyFilledRandom', x0, y1)
    put('ClosetEmergencyN2FilledRandom', x1, y1)
    for x in range(x0 - 1, x1 + 2):
        T[(x, y0 - 2)] = 'CMFloorBlack'
        E.append(('Thruster', x, y0 - 2, 0.0))
        CABLE['CableApcExtension'].update({(x, y0 - 1), (x, y0), (x, y0 + 1)})

# --- лаборатории
x0, y0, x1, y1 = ibox('labs')
for x in range(x0, x1 + 1, 3):
    put('MedicalBed', x, y1)
put('StasisBed', x0, y0)
put('StasisBed', x0 + 1, y0)
put('DHLifeSupport', x0 + 3, y0)
put('DHVentilator', x0 + 4, y0)
for x in range(x0 + 1, x1, 6):
    put('DHIVDripFilled', x, y1 - 1)
put('VendingMachineMedical', x1, y0)
put('LockerMedicineFilled', x1 - 1, y0)
put('LockerMedicineFilled', x1 - 2, y0)
put('TableReinforcedGlass', x1 - 4, y0)
put('Defibrillator', x1 - 4, y0, force=True)
put('TableReinforcedGlass', x1 - 5, y0)
put('HandheldHealthAnalyzer', x1 - 5, y0, force=True)
put('MedkitCombatFilled', x1 - 5, y0, force=True)
put('DHBloodPackFull', x1 - 4, y0, n=4, force=True)
put('VendingMachineWallMedical', x0 - 1, (y0 + y1) // 2, PI / 2, force=True)
spawn('DHSpawnPointImperialMedic', [(x, (y0 + y1) // 2) for x in range(x0 + 2, x1 - 1, 3)], 4)
for room in ('or1', 'or2'):
    a0, b0, a1, b1 = ibox(room)
    cx = (a0 + a1) // 2
    put('OperatingTable', cx, (b0 + b1) // 2)
    put('DHVentilator', cx - 1, (b0 + b1) // 2)
    put('DHLifeSupport', cx + 1, (b0 + b1) // 2)
    put('DHIVDripFilled', cx, (b0 + b1) // 2 + 2)
    put('TableReinforcedGlass', a0, b0)
    put('TableReinforcedGlass', a0 + 1, b0)
    put('DHFieldSurgeryKitFilled', a0, b0, force=True)
    put('SawAdvanced', a0 + 1, b0, force=True)
    put('BoxLatexGloves', a0 + 1, b0, force=True)
    put('BoxSterileMask', a0 + 1, b0, force=True)
    put('DHAutoclave', a1, b0)
    put('SinkWide', a1, b1, FACE['S'])
    door(room, 'labs', 'AirlockMedicalGlassLocked')
a0, b0, a1, b1 = ibox('chem')
for i, proto in enumerate(('DHChemDispenser', 'DHChemDispenser', 'DHChemMaster', 'DHChemMaster')):
    put(proto, a0 + i, b1)
put('SinkWide', a1, b1, FACE['S'])
put('DHChemistryHotplate', a0, b1 - 3)
put('DHChemistryHotplate', a0, b1 - 4)
put('DHElectrolysisUnit', a1, b1 - 3)
for i, proto in enumerate(('DHFumeHood', 'DHFumeHood', 'DHCrateChemistryAcids', 'DHCrateChemistryBases', 'DHCrateChemistryOrganics')):
    put(proto, a0 + i, b0)
put('DHPathogenAnalyzer', a1 - 1, b0)
put('DHVirologyComputer', a1, b0)
put('TableReinforcedGlass', (a0 + a1) // 2, (b0 + b1) // 2)
put('TableReinforcedGlass', (a0 + a1) // 2 + 1, (b0 + b1) // 2)
for proto in ('BoxBeaker', 'LargeBeaker', 'LargeBeaker', 'BoxSyringe', 'BoxPillCanister'):
    put(proto, (a0 + a1) // 2, (b0 + b1) // 2, force=True)
door('chem', 'labs', 'AirlockMedicalGlassLocked', width=2)
a0, b0, a1, b1 = ibox('morgue')
for y in range(b0, b1 + 1, 2):
    put('Morgue', a0, y, FACE['E'])
put('BodyBagFolded', a1, b0, n=4)
door('morgue', 'labs', 'AirlockMedicalGlassLocked')

# --- ЛКП в каждом помещении (до выреза: кабели через вырез потом удаляются, поэтому у «Зоны вылета 1» ЛКП справа)
for room, side in (('rift', 'W'), ('art', 'W'), ('zone', 'N'), ('hangar', 'E'), ('magazine', 'S'), ('cargo', 'W'),
                   ('barracks', 'W'), ('brief', 'S'), ('wardroom', 'E'), ('hqhall', 'S'), ('cmd1', 'E'), ('cmd2', 'E'),
                   ('cmd3', 'E'), ('cmd4', 'E'), ('engL', 'W'), ('eng', 'S'), ('engR', 'E'), ('labs', 'W'),
                   ('or1', 'W'), ('or2', 'E'), ('chem', 'S'), ('morgue', 'W')):
    apc(room, side)

# --- пилоты челнока — у рампы в ангаре
spawn('DHSpawnPointImperialPilot', sorted((p for p in INNER['zone'] if p not in HOLE), key=lambda p: (-p[0], -p[1])), 3)

# --- СН: от подстанций до каждого ЛКП по клеткам корабля
ship = set(T)
for room, wall, inner in APCS:
    CABLE['CableMV'].update(bfs(SUBS[0], wall, ship))
CABLE['CableMV'].update(bfs(SUBS[0], SUBS[1], ship))


# --- отсеки для живучести авианосца (план осады п. 19, решения автора 2026-11-19): прямоугольники клеток каждого
# отсека — в прототип dhCarrierLayout (свой на каждом уровне); в каждом отсеке — аварийный пост; схема повреждений —
# на мостике и в инженерном.
COMPARTMENTS = {  # уровень: [(id, ключ названия, вид, помещения)]
    0: [
        ('hangar', 'dh-carrier-compartment-hangar', 'Hangar', ['zone', 'hangar', 'cargo']),
        ('guns', 'dh-carrier-compartment-guns', 'Guns', ['art']),
        ('engineering', 'dh-carrier-compartment-engineering', 'Engineering', ['eng', 'engL', 'engR']),
        ('rift', 'dh-carrier-compartment-rift', 'None', ['rift']),
        ('magazine', 'dh-carrier-compartment-magazine', 'None', ['magazine']),
        ('cabins', 'dh-carrier-compartment-cabins', 'None', ['wardroom', 'hqhall', 'cmd1', 'cmd2', 'cmd3', 'cmd4']),
        ('barracks', 'dh-carrier-compartment-barracks', 'None', ['barracks']),
        ('brief', 'dh-carrier-compartment-brief', 'None', ['brief']),
        ('labs', 'dh-carrier-compartment-labs', 'None', ['labs', 'or1', 'or2', 'chem', 'morgue']),
    ],
    1: [
        ('deck', 'dh-carrier-compartment-deck', 'Hangar', ['zone', 'runway']),
        ('pvo', 'dh-carrier-compartment-pvo', 'None', ['pvo']),
        ('island', 'dh-carrier-compartment-island', 'None', ['island']),
    ],
    2: [
        ('bridge', 'dh-carrier-compartment-bridge', 'Bridge', ['bridge']),
    ],
}
LAYOUTS = {0: 'DHCarrierLayoutSovereign', 1: 'DHCarrierLayoutSovereignDeck', 2: 'DHCarrierLayoutSovereignIsland'}


def rects(cells):
    """Клетки → прямоугольники: отрезки по строкам, одинаковые отрезки соседних строк сливаются."""
    rows = {}
    for x, y in cells:
        rows.setdefault(y, []).append(x)
    runs = {}
    for y, xs in rows.items():
        xs.sort()
        start = prev = xs[0]
        for x in xs[1:] + [None]:
            if x is not None and x == prev + 1:
                prev = x
                continue
            runs.setdefault((start, prev), []).append(y)
            if x is not None:
                start = prev = x
    out = []
    for (x0, x1), ys in runs.items():
        ys.sort()
        y0 = prev = ys[0]
        for y in ys[1:] + [None]:
            if y is not None and y == prev + 1:
                prev = y
                continue
            out.append((x0, y0, x1 + 1, prev + 1))
            if y is not None:
                y0 = prev = y
    return sorted(out)


def occupied_cells():
    return {(e[1], e[2]) for e in E} | set(W)


def wall_spot(rooms, occupied, doors, skip=frozenset()):
    """Свободная клетка у стены, не у двери (для аварийного поста или консоли)."""
    cells = sorted(p for r in rooms for p in INNER.get(r, ()) if p not in skip)
    for x, y in cells:
        if (x, y) in occupied:
            continue
        near = [(x + dx, y + dy) for dx, dy in N4]
        if any(n in doors for n in near) or not any(n in W for n in near):
            continue
        occupied.add((x, y))
        return x, y
    return None


LAYOUT_TEXT = ['# Сгенерировано Tools/_DarkestHour/carrier/build_carrier.py — не править руками.',
               '# Отсеки авианосца для живучести (план осады п. 19): клетки грида прямоугольниками left,bottom,right,top.',
               '# Свой набор на каждом уровне (2026-11-27): 0 — нижняя палуба, 1 — полётная, 2 — надстройка.']


def compartments(level, skip=frozenset()):
    """Отсеки уровня в прототип и аварийные посты в них."""
    occupied = occupied_cells()
    doors = {(e[1], e[2]) for e in E if 'Airlock' in e[0] or 'Door' in e[0]}
    LAYOUT_TEXT.extend(['', '- type: dhCarrierLayout', '  id: %s' % LAYOUTS[level], '  compartments:'])
    for cid, name, kind, rooms in COMPARTMENTS[level]:
        cells = {p for p, r in REGION.items() if r in rooms and p not in skip}
        LAYOUT_TEXT.extend(['  - id: %s' % cid, '    name: %s' % name, '    kind: %s' % kind, '    boxes:'])
        LAYOUT_TEXT.extend(['    - %d,%d,%d,%d' % b for b in rects(cells)])
        spot = wall_spot(rooms, occupied, doors, skip)
        if spot:
            put('DHCarrierEmergencyPost', spot[0], spot[1], force=True)
    return occupied, doors


def deck_power(room):
    """Питание верхнего уровня: фидер от реакторов нижней палубы (ВН) → подстанция → СН до каждого ЛКП."""
    cells = sorted(INNER[room], key=lambda p: (p[1], p[0]))
    occupied = occupied_cells()
    for x, y in cells:
        if (x, y) in occupied or (x + 1, y) in occupied or (x + 1, y) not in INNER[room]:
            continue
        put('DHCarrierDeckPowerFeed', x, y, force=True)
        put('SubstationBasic', x + 1, y, force=True)
        CABLE['CableHV'].update({(x, y), (x + 1, y)})
        sub = (x + 1, y)
        break
    else:
        raise SystemExit('нет места для фидера на уровне: ' + room)
    ship = set(T)
    CABLE['CableMV'].add(sub)
    for _room, wall, _inner in APCS:
        CABLE['CableMV'].update(bfs(sub, wall, ship))


# ---------------------------------------------------------------- уровень 0: отсеки
compartments(0)
x0, y0, x1, y1 = ibox('eng')
spot = wall_spot(['eng'], occupied_cells(), {(e[1], e[2]) for e in E if 'Airlock' in e[0]})
if spot:
    put('DHComputerCarrierDamage', spot[0], spot[1], force=True)
print('уровень 0: помещений', len(INNER), 'ЛКП', len(APCS), 'точек появления', spawns)

# ================================================================ уровень 1 — полётная палуба
new_deck(1)
# --- «Зона вылета 1» (вырез под кабину «Мидуэя») и ВПП — уровень 1
x0, y0, x1, y1 = ibox('zone')
MID = ((x0 + x1) // 2, y1 - 11)     # клетка (0, 0) «Мидуэя»: вверх 10 клеток, вниз 12; сверху и снизу — проход
put('DHDropshipHangarMidway', *MID, force=True)
HOLE = {(MID[0] + x, MID[1] + y) for x, y in MIDWAY}
bad = [p for p in HOLE if REGION.get(p) != 'zone' or p in W]
if bad:
    raise SystemExit('«Мидуэй» не помещается в «Зону вылета 1»: %s' % sorted(bad)[:5])
opening('zone', 'runway', margin=2)
rx0, ry0, rx1, ry1 = ibox('runway')
for y in range(ry0, ry1 + 1):
    if free((rx0, y)):
        T[(rx0, y)] = 'RMCFloorPlatingStripedWest'
    if free((rx1, y)):
        T[(rx1, y)] = 'RMCFloorPlatingStripedEast'
for y in range(ry0 + 2, ry1 - 1, 4):    # осевая разметка
    T[((rx0 + rx1) // 2, y)] = 'RMCFloorPlatingStriped'
# штурмовик: лифт в начале ВПП (у брифинговой), катапульта — вдоль ВПП к носу; рядом бак горючего и ЛТЦ
lcx = (rx0 + rx1) // 2
put('DHAircraftLift', lcx, ry0 + 3)
assert put('DHComputerAircraftLift', lcx + 3, ry0 + 7, FACE['W']), 'компьютер лифта штурмовика'   # одна кнопка «Поднять»
spawn('DHSpawnPointImperialAttackPilot', [(lcx + d, ry0 + 6) for d in (0, -1, 1)], 2)
put('WeldingFuelTankFull', lcx + 3, ry0 + 3)
put('Rack', lcx - 3, ry0 + 1)
put('DHAircraftFlareCassette', lcx - 3, ry0 + 1, n=6, force=True)
put('Welder', lcx - 3, ry0 + 1, force=True)
put('Rack', lcx - 4, ry0 + 1)
put('DHAircraftCannonAmmo', lcx - 4, ry0 + 1, n=2, force=True)
put('DHAircraftCannonPod', lcx - 5, ry0 + 1)    # пушечные контейнеры 30 мм — вешаются на узлы подвески
put('DHAircraftCannonPod', lcx - 5, ry0 + 2)
put('DHAircraftDropTank', lcx - 6, ry0 + 1)    # ПТБ (решение автора 2026-11-26)
put('DHAircraftDropTank', lcx - 6, ry0 + 2)
assert put('DHPowerLoader', lcx - 6, ry0 + 3), 'погрузчик у лифта штурмовика'
for i, munition in enumerate(('DHAircraftBombHE', 'DHAircraftBombHE', 'DHAircraftBombCluster', 'DHAircraftBombNapalm',
                               'DHAircraftBombMagnesium', 'DHAircraftGuidedBomb', 'DHAircraftMissileAT', 'DHAircraftMissileAT',
                               'DHAircraftMissileHE', 'DHAircraftMissileHE')):
    put(munition, lcx + 4 + i // 2, ry0 + 1 + i % 2)
# лифты техники — у бортов ВПП. Поддоны танка и БТР — в трюмах тяжёлого и лёгкого лифтов 5×5 (п. 4).
assert rx1 - 4 > lcx + 2, 'лифт техники налезает на ось ВПП'
put('DHVehicleLift', rx1 - 2, ry1 - 4)
assert put('DHComputerVehicleLift', rx1, ry1 - 7, FACE['W']), 'компьютер лифта техники'
# лёгкий лифт (БТР, заказанные хамви) — у другого борта ВПП (решение автора 2026-11-26)
assert rx0 + 4 < lcx - 2, 'лёгкий лифт техники налезает на ось ВПП'
put('DHVehicleLiftLight', rx0 + 2, ry1 - 4)
assert put('DHComputerVehicleLift', rx0, ry1 - 7, FACE['E']), 'компьютер лёгкого лифта техники'

# --- ПВО (треугольник) — уровень 1
door('runway', 'pvo', 'AirlockSecurityGlassLocked')
pcells = sorted(INNER['pvo'], key=lambda p: (-p[1], p[0]))
px0, py0, px1, py1 = ibox('pvo')
put('DHRadarEmpire', px0 + 2, py0 + 2)
put('DHCommsRelayEmpire', px0 + 4, py0 + 2)
launchers = []
for p in sorted(INNER['pvo'], key=lambda p: (p[1], -p[0])):
    if len(launchers) >= 4:
        break
    if p[0] >= px0 + 6 and all(abs(p[0] - q[0]) + abs(p[1] - q[1]) >= 3 for q in launchers) and put('DHMissileLauncher', *p):
        launchers.append(p)
put('DHInterceptorMissile', px0, py0, n=6)
put('DHInterceptorMissile', px0 + 1, py0, n=6)

# --- основание надстройки: вход на мостик
CMD = 'DHAirlockImperialCommandGlassLocked'
door('pvo', 'island', CMD)
ix0, iy0, ix1, iy1 = ibox('island')
put('DHFlagEmpirePlanted', ix0, iy1)
put('DHFlagEmpirePlanted', ix1, iy1)

for room, side in (('zone', 'N'), ('runway', 'E'), ('pvo', 'S'), ('island', 'E')):
    apc(room, side)

# --- вырез под «Мидуэй» и воздушные завесы
for p in HOLE:
    T.pop(p, None)
    W.pop(p, None)
    INNER['zone'].discard(p)
    for k in CABLE:
        CABLE[k].discard(p)
E[:] = [e for e in E if (e[1], e[2]) not in HOLE or e[0] == 'DHDropshipHangarMidway']
SEAL_ROT = {(0, -1): 0.0, (0, 1): PI, (1, 0): PI / 2, (-1, 0): -PI / 2}
for (x, y) in list(T):
    for (dx, dy), rot in SEAL_ROT.items():
        if (x + dx, y + dy) in HOLE and (x, y) not in W:
            E.append(('DHHangarAirSeal', x, y, rot))
# свет «Зоны вылета 1» — НН вдоль стен вокруг выреза
zone = INNER['zone']
ring = {p for p in zone if any((p[0] + dx, p[1] + dy) not in zone for dx, dy in N8)}
CABLE['CableApcExtension'].update(ring)
CABLE['CableApcExtension'] = {p for p in CABLE['CableApcExtension'] if p not in HOLE}


deck_power('island')
compartments(1, HOLE)
print('уровень 1: помещений', len(INNER), 'ЛКП', len(APCS))

# ================================================================ уровень 2 — надстройка (мостик и штаб)
new_deck(2)
x0, y0, x1, y1 = ibox('bridge')
bridge = ['DHComputerRiftNav', 'DHComputerLanding', 'DHComputerOverwatchEmpire', 'DHComputerSquadsEmpire',
          'DHComputerOrbitalScan', 'ComputerRadar', 'DHComputerCommandTreeEmpire']  # штаб: древо командования
for i, proto in enumerate(bridge):
    x = x0 + 1 + i * 2
    put(proto, x, y1, FACE['S'])
    put('ChairOfficeDark', x, y1 - 1, FACE['N'])
put('DHComputerAnnounceEmpire', x0, y0 + 4, FACE['E'])  # объявления только своей стороне
put('ChairOfficeDark', x0 + 1, y0 + 4, FACE['W'])
put('ComputerCrewMonitoring', x1, y0 + 4, FACE['W'])
put('ChairOfficeDark', x1 - 1, y0 + 4, FACE['E'])
cx = (x0 + x1) // 2
put('DHWarpPointCarrier', 47, -72, force=True)  # метка телепорта призраков посреди мостика
put('TableReinforced', cx - 1, y0 + 2)
put('TableReinforced', cx, y0 + 2)
put('TableReinforced', cx + 1, y0 + 2)
put('DHFieldPhoneEmpireBridge', cx, y0 + 3)
# флаги Первой Империи по углам мостика (решение автора 2026-11-13)
put('DHFlagEmpirePlanted', x0, y0)
put('DHFlagEmpirePlanted', x1, y0)
# схема повреждений — у правой стены мостика
x0, y0, x1, y1 = ibox('bridge')
put('DHComputerCarrierDamage', x1, y0 + 7, FACE['W'], force=True)
put('ChairOfficeDark', x1 - 1, y0 + 7, FACE['E'], force=True)
apc('bridge', 'E')
deck_power('bridge')
compartments(2)
print('уровень 2: помещений', len(INNER), 'ЛКП', len(APCS))

# ================================================================ переходы между уровнями


def free_on(level, p):
    d = DECKS[level]
    return p in d['T'] and p not in d['W'] and all((e[1], e[2]) != p for e in d['E'])


def near_door(level, p):
    d = DECKS[level]
    return any(d['W'].get((p[0] + dx, p[1] + dy), '').startswith(('Airlock', 'DHAirlock')) for dx, dy in N8)


def ladder(lower, room_lower, upper, room_upper, near, skip=frozenset()):
    """Универсальная лестница (DHZLadder) в одной клетке на двух соседних уровнях."""
    cells = DECKS[lower]['INNER'].get(room_lower, set()) & DECKS[upper]['INNER'].get(room_upper, set())
    cells = [p for p in cells if p not in skip and free_on(lower, p) and free_on(upper, p)
             and not near_door(lower, p) and not near_door(upper, p)]
    if not cells:
        raise SystemExit('нет места для лестницы %s/%s' % (room_lower, room_upper))
    p = min(cells, key=lambda c: (abs(c[0] - near[0]) + abs(c[1] - near[1]), c))
    DECKS[lower]['E'].append(('DHZLadder', p[0], p[1], None))
    DECKS[upper]['E'].append(('DHZLadder', p[0], p[1], None))
    return p


def zlift(lower, room_lower, upper, room_upper, near):
    """Грузовой Z-лифт: платформа 3×3 и пульт рядом на обоих уровнях."""
    both = DECKS[lower]['INNER'].get(room_lower, set()) & DECKS[upper]['INNER'].get(room_upper, set())
    best = None
    for (cx, cy) in sorted(both, key=lambda c: (abs(c[0] - near[0]) + abs(c[1] - near[1]), c)):
        block = [(cx + dx, cy + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1)] + [(cx + 2, cy + 1)]
        if all(c in both and free_on(lower, c) and free_on(upper, c) for c in block):
            best = (cx, cy)
            break
    if best is None:
        raise SystemExit('нет места для Z-лифта %s/%s' % (room_lower, room_upper))
    for level in (lower, upper):
        DECKS[level]['E'].append(('DHZLiftPlatform', best[0], best[1], None))
        DECKS[level]['E'].append(('DHZLiftPanel', best[0] + 2, best[1] + 1, None))
    return best


zx0, zy0, zx1, zy1 = ibox_of = (min(p[0] for p in DECKS[0]['INNER']['zone']), min(p[1] for p in DECKS[0]['INNER']['zone']),
                                max(p[0] for p in DECKS[0]['INNER']['zone']), max(p[1] for p in DECKS[0]['INNER']['zone']))
ladder(0, 'zone', 1, 'zone', (zx0, zy1), HOLE)          # ангар ↔ кольцо вокруг кабины
ladder(0, 'zone', 1, 'zone', (zx0, zy0), HOLE)
hx0, hy0, hx1, hy1 = (min(p[0] for p in DECKS[0]['INNER']['hangar']), min(p[1] for p in DECKS[0]['INNER']['hangar']),
                      max(p[0] for p in DECKS[0]['INNER']['hangar']), max(p[1] for p in DECKS[0]['INNER']['hangar']))
ladder(0, 'hangar', 1, 'runway', (hx0, (hy0 + hy1) // 2))   # ангарная палуба ↔ ВПП
ladder(0, 'hangar', 1, 'runway', (hx0, hy0 + 2))
ladder(0, 'magazine', 1, 'pvo', (hx1, hy0))                 # погреб ↔ ПВО
ladder(0, 'wardroom', 1, 'island', (hx1 + 6, hy0 - 10))     # кают-компания ↔ основание надстройки
ladder(1, 'island', 2, 'bridge', (hx1 + 6, hy0 - 4))        # основание ↔ мостик
zlift(0, 'hangar', 1, 'runway', (hx0 + 3, hy0 + 30))       # груз и поддоны: ВПП ↔ ангарная палуба

os.makedirs('Resources/Prototypes/_DarkestHour/Carrier', exist_ok=True)
open('Resources/Prototypes/_DarkestHour/Carrier/compartments.yml', 'w', encoding='utf-8').write('\n'.join(LAYOUT_TEXT) + '\n')

# =============================================================== запись


def write(level, path, name, carrier):
    use_deck(level)
    tile_ids = ['Space'] + sorted(set(T.values()))
    tid = {n: i for i, n in enumerate(tile_ids)}

    by = {}
    for (x, y), tname in T.items():
        by.setdefault((x // 16, y // 16), {})[(x % 16, y % 16)] = tid[tname]
    chunk_list = []
    for (cx, cy), cells in sorted(by.items()):
        data = bytearray()
        for i in range(256):
            data += struct.pack('<iBBB', cells.get((i % 16, i // 16), 0), 0, 0, 0)
        chunk_list.append((cx, cy, base64.b64encode(bytes(data)).decode()))

    air = {}
    for (x, y) in T:
        air.setdefault((x // 4, y // 4), 0)
        air[(x // 4, y // 4)] |= 1 << ((x % 4) + (y % 4) * 4)

    ents = []
    for (x, y), proto in W.items():
        ents.append((proto, x, y, None))
    ents += E
    for kind, cells in CABLE.items():
        for (x, y) in cells:
            if (x, y) in T:
                ents.append((kind, x, y, None))

    lines = []
    w = lines.append
    w('meta:')
    w('  format: 7')
    w('  category: Grid')
    w('  engineVersion: 289.0.0')
    w('  forkId: ""')
    w('  forkVersion: ""')
    w('  time: 11/12/2026 08:00:00')
    w('  entityCount: ENTITY_COUNT')
    w('maps: []')
    w('grids:')
    w('- 1')
    w('orphans:')
    w('- 1')
    w('nullspace: []')
    w('tilemap:')
    for i, n in enumerate(tile_ids):
        w('  %d: %s' % (i, n))
    w('entities:')
    w('- proto: ""')
    w('  entities:')
    w('  - uid: 1')
    w('    components:')
    w('    - type: MetaData')
    w('      name: %s' % name)
    w('    - type: Transform')
    w('      parent: invalid')
    w('    - type: MapGrid')
    w('      chunks:')
    for cx, cy, data in chunk_list:
        w('        %d,%d:' % (cx, cy))
        w('          ind: %d,%d' % (cx, cy))
        w('          tiles: %s' % data)
        w('          version: 7')
    w('    - type: Broadphase')
    w('    - type: Physics')
    w('      fixedRotation: False')
    w('      bodyType: Dynamic')
    w('      bodyStatus: InAir')
    w('    - type: Fixtures')
    w('      fixtures: {}')
    w('    - type: OccluderTree')
    w('    - type: SpreaderGrid')
    if carrier:
        w('    - type: Shuttle')
        w('      dampingModifier: 0.25')
    w('    - type: ImplicitRoof')
    w('    - type: GridPathfinding')
    w('    - type: Gravity')
    w('      enabled: True')
    w('      inherent: True')
    w('    - type: GridAtmosphere')
    w('      version: 2')
    w('      data:')
    w('        uniqueMixes:')
    w('        - temperature: 293.15')
    w('          volume: 2500')
    w('          moles:')
    w('            Oxygen: 21.824879')
    w('            Nitrogen: 82.10312')
    w('        tiles:')
    for (cx, cy), mask in sorted(air.items()):
        w('          %d,%d:' % (cx, cy))
        w('            0: %d' % mask)
    w('        chunkSize: 4')
    w('    - type: GasTileOverlay')
    w('    - type: RadiationGridResistance')
    w('    - type: NavMap')
    w('    - type: ExplosionAirtightGrid')
    w('    - type: DHCarrierCompartments')
    w('      layout: %s' % LAYOUTS[level])
    by_proto = {}
    for proto, x, y, rot in ents:
        by_proto.setdefault(proto, []).append((x, y, rot))
    uid = 2
    for proto in sorted(by_proto):
        w('- proto: %s' % proto)
        w('  entities:')
        for x, y, rot in by_proto[proto]:
            w('  - uid: %d' % uid)
            w('    components:')
            w('    - type: Transform')
            if rot:
                w('      rot: %r rad' % rot)
            w('      pos: %s,%s' % (x + 0.5, y + 0.5))
            w('      parent: 1')
            uid += 1
    w('...')
    text = '\n'.join(lines) + '\n'
    text = text.replace('ENTITY_COUNT', str(uid - 1))
    open(path, 'w', encoding='utf-8').write(text)
    print(path, 'entities', uid - 1, 'tiles', len(T))


write(0, OUT, 'IC-3 Sovereign', True)
write(1, OUT_DECK, 'IC-3 Sovereign — flight deck', False)
write(2, OUT_ISLAND, 'IC-3 Sovereign — island', False)

# ================================================================ крыша над всем кораблём (решение автора 2026-11-27)
# Крыша — отдельные гриды без своего воздуха (в космосе — вакуум, нужен скафандр): над нижней палубой там, где нет
# полётной (уровень 1), над полётной палубой — кроме ВПП (проём для штурмовика), выреза под «Мидуэй» (там его крыша)
# и надстройки (уровень 2), над надстройкой — уровень 3. Люк с мостика на крышу надстройки, люк из казарм на крышу
# нижней палубы; с крыши надстройки на крышу палубы — спрыгнуть.
ROOF_TILE, ROOF_EDGE = 'RMCFloorPlatingNoBuild', 'RMCFloorPlatingAlt'
foot0 = set(DECKS[0]['T'])
foot1 = set(DECKS[1]['T']) | HOLE
foot2 = set(DECKS[2]['T'])
runway = {p for p, r in DECKS[1]['REGION'].items() if r == 'runway'}


def midway_roof():
    """Клетки ходовой крыши «Мидуэя» (над вырезом стоит его крыша — своя крыша авианосца там не нужна)."""
    out = set()
    text = open('Resources/Maps/_DarkestHour/Shuttles/midway_roof.yml', encoding='utf-8').read()
    for ind, data in re.findall(r'ind: (-?\d+,-?\d+)\n\s+tiles: (\S+)', text):
        cx, cy = map(int, ind.split(','))
        raw = base64.b64decode(data)
        step = len(raw) // 256
        for i in range(256):
            if struct.unpack_from('<i', raw, i * step)[0]:
                out.add((MID[0] + cx * 16 + i % 16, MID[1] + cy * 16 + i // 16))
    return out


ROOFS = {
    1: foot0 - foot1,
    2: foot1 - runway - HOLE - midway_roof() - foot2,
    3: foot2,
}
ROOF_ENTS = {1: [], 2: [], 3: []}


def roof_hatch(level, deck_level, room, near):
    """Люк на крышу: лестница в помещении уровня ниже и над ней — на крыше."""
    d = DECKS[deck_level]
    cells = [p for p in d['INNER'].get(room, ()) if p in ROOFS[level] and free_on(deck_level, p)
             and not near_door(deck_level, p)]
    if not cells:
        raise SystemExit('нет места для люка на крышу: ' + room)
    p = min(cells, key=lambda c: (abs(c[0] - near[0]) + abs(c[1] - near[1]), c))
    d['E'].append(('DHZLadder', p[0], p[1], None))
    ROOF_ENTS[level].append(('DHZLadder', p[0], p[1], None))


b = DECKS[2]['INNER']['bridge']
roof_hatch(3, 2, 'bridge', (min(x for x, _ in b), min(y for _, y in b)))
k = DECKS[0]['INNER']['barracks']
roof_hatch(1, 0, 'barracks', (min(x for x, _ in k), max(y for _, y in k)))
# люки поставлены после записи уровней 0 и 2 — перезаписать их
write(0, OUT, 'IC-3 Sovereign', True)
write(2, OUT_ISLAND, 'IC-3 Sovereign — island', False)


def write_roof(level, path):
    cells = ROOFS[level]
    tiles = {}
    for (x, y) in cells:
        edge = any((x + dx, y + dy) not in cells for dx, dy in N8)
        tiles[(x, y)] = ROOF_EDGE if edge else ROOF_TILE
    tile_ids = ['Space'] + sorted(set(tiles.values()))
    tid = {n: i for i, n in enumerate(tile_ids)}
    by = {}
    for (x, y), tname in tiles.items():
        by.setdefault((x // 16, y // 16), {})[(x % 16, y % 16)] = tid[tname]
    L = ['meta:', '  format: 7', '  category: Grid', '  engineVersion: 289.0.0', '  forkId: ""', '  forkVersion: ""',
         '  time: 11/27/2026 08:00:00', '  entityCount: ENTITY_COUNT', 'maps: []', 'grids:', '- 1', 'orphans:', '- 1',
         'nullspace: []', 'tilemap:']
    L += ['  %d: %s' % (i, n) for i, n in enumerate(tile_ids)]
    L += ['entities:', '- proto: ""', '  entities:', '  - uid: 1', '    components:', '    - type: MetaData',
          '      name: IC-3 Sovereign — roof %d' % level, '    - type: Transform', '      parent: invalid',
          '    - type: MapGrid', '      chunks:']
    for (cx, cy), c in sorted(by.items()):
        data = bytearray()
        for i in range(256):
            data += struct.pack('<iBBB', c.get((i % 16, i // 16), 0), 0, 0, 0)
        L += ['        %d,%d:' % (cx, cy), '          ind: %d,%d' % (cx, cy),
              '          tiles: %s' % base64.b64encode(bytes(data)).decode(), '          version: 7']
    L += ['    - type: Broadphase', '    - type: Physics', '      fixedRotation: False', '      bodyType: Dynamic',
          '      bodyStatus: InAir', '    - type: Fixtures', '      fixtures: {}', '    - type: OccluderTree',
          '    - type: GridPathfinding', '    - type: Gravity', '      enabled: True', '      inherent: True']
    by_proto = {}
    for proto, x, y, rot in ROOF_ENTS[level]:
        by_proto.setdefault(proto, []).append((x, y))
    uid = 2
    for proto in sorted(by_proto):
        L += ['- proto: %s' % proto, '  entities:']
        for x, y in by_proto[proto]:
            L += ['  - uid: %d' % uid, '    components:', '    - type: Transform',
                  '      pos: %s,%s' % (x + 0.5, y + 0.5), '      parent: 1']
            uid += 1
    L.append('...')
    open(path, 'w', encoding='utf-8').write('\n'.join(L).replace('ENTITY_COUNT', str(uid - 1)) + '\n')
    print(path, 'tiles', len(tiles))


for level in (1, 2, 3):
    write_roof(level, 'Resources/Maps/_DarkestHour/Shuttles/imperial_carrier_roof%d.yml' % level)
