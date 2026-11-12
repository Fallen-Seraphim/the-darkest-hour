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

Запуск из корня репозитория:  python3 Tools/_DarkestHour/carrier/build_carrier.py
Карта целиком перезаписывается (Resources/Maps/_DarkestHour/Shuttles/imperial_carrier.yml)."""
import base64
import json
import math
import struct
from collections import deque

OUT = 'Resources/Maps/_DarkestHour/Shuttles/imperial_carrier.yml'
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

T = {}
W = {}
E = []
CABLE = {'CableHV': set(), 'CableMV': set(), 'CableApcExtension': set()}
HULL, WALL, WHITE, WINDOW = 'DHCarrierHull', 'DHCarrierWall', 'DHCarrierWallWhite', 'DHCarrierWindow'
WHITE_ROOMS = {'labs', 'or1', 'or2', 'chem', 'morgue'}
FLOOR = {
    'rift': 'CMFloorBlack', 'art': 'CMFloorPlate', 'zone': 'CMFloorPlate', 'runway': 'CMFloorPlate',
    'cargo': 'CMFloorCargo', 'barracks': 'CMFloorSteel', 'brief': 'CMFloorOffice', 'pvo': 'CMFloorBlack',
    'hq': 'CMFloorBlack', 'hqhall': 'CMFloorSteel', 'cmd1': 'CMFloorOffice', 'cmd2': 'CMFloorOffice',
    'cmd3': 'CMFloorOffice', 'cmd4': 'CMFloorOffice', 'engL': 'CMFloorBlack', 'eng': 'CMFloorBlack',
    'engR': 'CMFloorBlack', 'labs': 'CMFloorWhite', 'or1': 'CMFloorSterile', 'or2': 'CMFloorSterile',
    'chem': 'RMCFloorResearchContainment', 'morgue': 'CMFloorSterile',
}
for p, r in REGION.items():
    T[p] = FLOOR[r]

N8 = [(dx, dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy]
N4 = [(0, 1), (0, -1), (1, 0), (-1, 0)]
RANK = {n: i for i, n in enumerate(ORDER)}
for (x, y), r in REGION.items():
    for dx, dy in N8:
        o = REGION.get((x + dx, y + dy))
        if o is None:
            W[(x, y)] = HULL
            break
        if o != r and RANK[r] < RANK[o]:
            W[(x, y)] = WHITE if r in WHITE_ROOMS or o in WHITE_ROOMS else WALL
INNER = {}  # помещение -> клетки пола без стен
for p, r in REGION.items():
    if p not in W:
        INNER.setdefault(r, set()).add(p)


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


APCS = []
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
    put(breech, gx, gy, 0.0)
    put(barrel, gx, gy + 1, 0.0)
    put('DHComputerNavalArtillery', gx + 1, gy - 2, FACE['S'])
    put('ChairOfficeDark', gx + 1, gy - 3, FACE['N'])
for i, wh in enumerate(('HighExplosive', 'ArmorPiercing', 'Incendiary', 'Cluster')):
    put('DHHeavyShell300' + wh, x0 + i, y0, n=3)
    put('DHHeavyShell570' + wh, x0 + 5 + i, y0, n=2)
    put('DHHeavyShell300' + wh, x0 + 10 + i, y0, n=3)
put('SyndieFlag', x0, y1)
put('SyndieFlag', x0 + 6, y1)
door('art', 'zone', 'AirlockSecurityGlassLocked', width=3)

# --- «Зона вылета 1» и ВПП
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
put('WeldingFuelTankFull', lcx + 3, ry0 + 3)
put('Rack', lcx - 3, ry0 + 1)
put('DHAircraftFlareCassette', lcx - 3, ry0 + 1, n=6, force=True)
put('Welder', lcx - 3, ry0 + 1, force=True)
put('Rack', lcx - 4, ry0 + 1)
put('DHAircraftCannonAmmo', lcx - 4, ry0 + 1, n=2, force=True)
for i, munition in enumerate(('DHAircraftBombHE', 'DHAircraftBombHE', 'DHAircraftBombCluster', 'DHAircraftBombNapalm',
                               'DHAircraftBombMagnesium', 'DHAircraftGuidedBomb', 'DHAircraftMissileAT', 'DHAircraftMissileAT',
                               'DHAircraftMissileHE', 'DHAircraftMissileHE')):
    put(munition, lcx + 4 + i // 2, ry0 + 1 + i % 2)
# техника и десантное снаряжение — у бортов ВПП
put('DHVehicleDropPalletTankEmpire', rx1 - 1, ry1 - 2)
put('DHVehicleDropPalletIM30', rx1 - 1, ry1 - 5)
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

# --- ПВО (треугольник)
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

# --- карго
x0, y0, x1, y1 = ibox('cargo')
ex, ey = x0 + 5, (y0 + y1) // 2
put('DHSupplyElevator', ex, ey)
put('DHComputerSupply', ex + 5, ey + 3, FACE['S'])
put('ChairOfficeDark', ex + 5, ey + 2, FACE['N'])
for i in range(4):
    put('CrateFilledSpawner', x1 - 1 - i, y1)
put('Rack', x1, y0)
put('Rack', x1 - 1, y0)
put('LockerEvacRepairFilled', x1, y0 + 3)
door('cargo', 'zone', 'AirlockCargoGlassLocked', width=3)
door('cargo', 'runway', 'AirlockCargoGlassLocked', width=2)

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
            put('SpawnPointAnyJob', x, row_y)
            put('SpawnPointAnyJob', x, row_y - 1)
            spawns += 2
    for x in range(x0 + 2, x1, 4):
        put('LockerSyndicatePersonal', x, row_y + 2)
put('TableReinforced', x1 - 1, y0)
put('TableReinforced', x1, y0)
door('barracks', 'cargo', width=3)
door('barracks', 'brief', width=2)
door('barracks', 'eng', 'AirlockEngineeringLocked', width=2)
door('barracks', 'runway', width=2)

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
door('brief', 'runway', width=3)
door('brief', 'hq')
door('brief', 'eng', 'AirlockEngineeringLocked', width=2)

# --- штаб и комнаты командования
x0, y0, x1, y1 = ibox('hq')
bridge = ['DHComputerRiftNav', 'DHComputerLanding', 'DHComputerOverwatchEmpire', 'DHComputerSquadsEmpire',
          'DHComputerOrbitalScan', 'ComputerRadar']
for i, proto in enumerate(bridge):
    x = x0 + 1 + i * 2
    put(proto, x, y1, FACE['S'])
    put('ChairOfficeDark', x, y1 - 1, FACE['N'])
put('ComputerComms', x0, y0 + 4, FACE['E'])
put('ChairOfficeDark', x0 + 1, y0 + 4, FACE['W'])
put('ComputerCrewMonitoring', x1, y0 + 4, FACE['W'])
put('ChairOfficeDark', x1 - 1, y0 + 4, FACE['E'])
cx = (x0 + x1) // 2
put('TableReinforced', cx - 1, y0 + 2)
put('TableReinforced', cx, y0 + 2)
put('TableReinforced', cx + 1, y0 + 2)
put('DHFieldPhoneEmpireBridge', cx, y0 + 3)
put('SyndieFlag', x0, y0)
put('SyndieFlag', x1, y0)
door('hq', 'pvo', 'AirlockCommandGlassLocked')
door('hq', 'hqhall', 'AirlockCommandGlassLocked')
for room in ('cmd1', 'cmd2', 'cmd3', 'cmd4'):
    door(room, 'hqhall', 'AirlockCommandGlassLocked')
    a0, b0, a1, b1 = ibox(room)
    put('Bed', a0, b1)
    put('BedsheetSyndie', a0, b1, force=True)
    put('LockerSyndicatePersonal', a1, b1)
    put('TableReinforced', a0, b0)
    put('TableReinforced', a0 + 1, b0)
    put('ChairOfficeDark', a0, b0 + 1, FACE['S'])
    put('PottedPlantRandom', a1, b0)
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
for room, side in (('rift', 'W'), ('art', 'W'), ('zone', 'N'), ('runway', 'E'), ('pvo', 'S'), ('cargo', 'W'),
                   ('barracks', 'W'), ('brief', 'S'), ('hq', 'E'), ('hqhall', 'S'), ('cmd1', 'E'), ('cmd2', 'E'),
                   ('cmd3', 'E'), ('cmd4', 'E'), ('engL', 'W'), ('eng', 'S'), ('engR', 'E'), ('labs', 'W'),
                   ('or1', 'W'), ('or2', 'E'), ('chem', 'S'), ('morgue', 'W')):
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
apc_zone = next(a for a in APCS if a[0] == 'zone')
CABLE['CableApcExtension'].update(ring)
CABLE['CableApcExtension'] = {p for p in CABLE['CableApcExtension'] if p not in HOLE}

# --- СН: от подстанций до каждого ЛКП по клеткам корабля
ship = set(T)
for room, wall, inner in APCS:
    CABLE['CableMV'].update(bfs(SUBS[0], wall, ship))
CABLE['CableMV'].update(bfs(SUBS[0], SUBS[1], ship))

print('помещений', len(INNER), 'ЛКП', len(APCS), 'точек появления', spawns)

# =============================================================== запись

TILE_IDS = ['Space'] + sorted(set(T.values()))
TID = {n: i for i, n in enumerate(TILE_IDS)}


def chunks():
    by = {}
    for (x, y), name in T.items():
        by.setdefault((x // 16, y // 16), {})[(x % 16, y % 16)] = TID[name]
    out = []
    for (cx, cy), cells in sorted(by.items()):
        data = bytearray()
        for i in range(256):
            data += struct.pack('<iBBB', cells.get((i % 16, i // 16), 0), 0, 0, 0)
        out.append((cx, cy, base64.b64encode(bytes(data)).decode()))
    return out


def atmos():
    by = {}
    for (x, y) in T:
        by.setdefault((x // 4, y // 4), 0)
        by[(x // 4, y // 4)] |= 1 << ((x % 4) + (y % 4) * 4)
    return by


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
for i, n in enumerate(TILE_IDS):
    w('  %d: %s' % (i, n))
w('entities:')
w('- proto: ""')
w('  entities:')
w('  - uid: 1')
w('    components:')
w('    - type: MetaData')
w('      name: IC-3 Sovereign')
w('    - type: Transform')
w('      parent: invalid')
w('    - type: MapGrid')
w('      chunks:')
for cx, cy, data in chunks():
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
for (cx, cy), mask in sorted(atmos().items()):
    w('          %d,%d:' % (cx, cy))
    w('            0: %d' % mask)
w('        chunkSize: 4')
w('    - type: GasTileOverlay')
w('    - type: RadiationGridResistance')
w('    - type: NavMap')
w('    - type: ExplosionAirtightGrid')
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
open(OUT, 'w', encoding='utf-8').write(text)
print(OUT, 'entities', uid - 1, 'tiles', len(T), 'hole', len(HOLE))
