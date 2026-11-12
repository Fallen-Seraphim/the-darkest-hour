"""Имперский десантный авианосец IC-3 «Соверен» по схеме автора (2026-11-11), нос — вверху (+y).

Сверху вниз:
- разрывной генератор и суперконденсатор (нос);
- корабельная артиллерия: 570 мм в центре и два 300 мм, у каждого свой пульт;
- «Зона вылета 1» со стоянкой «Мидуэя» (вырез в палубе по клеткам челнока, по краю — воздушные завесы),
  рядом ВПП (открытая палуба), по правому борту — ПВО (ракетные установки и РЛС) и склад техники;
- карго: лифт снабжения с пультом; напротив — подготовка (вендоматы снаряжения, стойка брони, станок);
- казармы в духе «Алмаера» (двухъярусные койки, шкафчики, точки появления) и брифинговая;
- штаб (мостик) и комнаты командования;
- инженерный отсек между двумя двигателями: два термоядерных реактора (ВН) → СМЭС → подстанции (СН) →
  ЛКП в каждом помещении (НН) → свет и техника;
- корма: медотсек, две операционные, химическая лаборатория с вирусологией, морг.

Стены, окна и плитка — «Алмаер» из RMC-14 (Prototypes/_DarkestHour/Carrier). Запуск из корня репозитория:
    python3 Tools/_DarkestHour/carrier/build_carrier.py
Карта целиком перезаписывается (Resources/Maps/_DarkestHour/Shuttles/imperial_carrier.yml)."""
import base64
import json
import math
import struct

OUT = 'Resources/Maps/_DarkestHour/Shuttles/imperial_carrier.yml'
MIDWAY = json.load(open('Tools/_DarkestHour/midway/footprint.json'))

PI = math.pi
# Поворот настенного (светильник, ЛКП): к какой стене прикреплён — N: 0, S: π, W: π/2, E: -π/2.
WALLROT = {'N': 0.0, 'S': PI, 'W': PI / 2, 'E': -PI / 2}
# Поворот «лицом» в сторону: машина/стул смотрит на юг при 0.
FACE = {'S': 0.0, 'N': PI, 'E': PI / 2, 'W': -PI / 2}

T = {}      # (x, y) -> плитка
W = {}      # (x, y) -> стена / окно / дверь
E = []      # (proto, x, y, rot)
CABLE = {'CableHV': set(), 'CableMV': set(), 'CableApcExtension': set()}
HULL = 'DHCarrierHull'
WALL = 'DHCarrierWall'
WHITE = 'DHCarrierWallWhite'
WINDOW = 'DHCarrierWindow'


def floor(x0, y0, x1, y1, tile):
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            T[(x, y)] = tile


def room(x0, y0, x1, y1, tile, wall=WALL):
    """Комната: пол внутри, стены по кольцу вокруг (если там ещё не дверь)."""
    floor(x0, y0, x1, y1, tile)
    for x in range(x0 - 1, x1 + 2):
        for y in range(y0 - 1, y1 + 2):
            if x0 <= x <= x1 and y0 <= y <= y1:
                if W.get((x, y)) in (WALL, WHITE):
                    del W[(x, y)]
                continue
            T.setdefault((x, y), tile)
            if (x, y) not in W or W[(x, y)] in (WALL, WHITE):
                W[(x, y)] = wall


def opening(x0, y0, x1, y1):
    """Проём без двери (между смежными помещениями)."""
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            W.pop((x, y), None)


def door(x, y, proto='AirlockSyndicateGlass'):
    W[(x, y)] = proto


def put(proto, x, y, rot=None, n=1):
    for _ in range(n):
        E.append((proto, x, y, rot))


def line(kind, a, b):
    """Кабель по L-пути: сначала по x, потом по y."""
    (x0, y0), (x1, y1) = a, b
    step = 1 if x1 >= x0 else -1
    for x in range(x0, x1 + step, step):
        CABLE[kind].add((x, y0))
    step = 1 if y1 >= y0 else -1
    for y in range(y0, y1 + step, step):
        CABLE[kind].add((x1, y))


ROOMS = []  # (имя, прямоугольник, стена с ЛКП, позиция ЛКП)


def powered(name, x0, y0, x1, y1, apc, side, lights=True, step=5):
    """ЛКП на стене + «гребёнка» НН через комнату (радиус приёма 3 клетки) + светильники по северной и южной стенам."""
    ax, ay = apc
    put('APCBasic', ax, ay, {'N': 0.0, 'S': PI, 'W': PI / 2, 'E': -PI / 2}[side])
    # НН: от ЛКП внутрь комнаты, затем горизонтальные линии каждые 6 рядов, соединённые вертикалью.
    inside = {'N': (ax, ay - 1), 'S': (ax, ay + 1), 'W': (ax + 1, ay), 'E': (ax - 1, ay)}[side]
    CABLE['CableApcExtension'].add((ax, ay))
    cx = min(max(inside[0], x0), x1)
    rows = list(range(y0 + 2, y1 + 1, 6)) or [y0]
    if y1 - rows[-1] > 3:
        rows.append(max(y0, y1 - 2))
    line('CableApcExtension', inside, (cx, rows[0]))
    line('CableApcExtension', (cx, rows[0]), (cx, rows[-1]))
    for r in rows:
        line('CableApcExtension', (x0, r), (x1, r))
    # СН до магистрали x = 0.
    line('CableMV', (ax, ay), (0, ay))
    ROOMS.append(name)
    if lights:
        for x in range(x0 + 1, x1, step):
            if (x, y1 + 1) in W:
                put('Poweredlight', x, y1, WALLROT['N'])
            if (x, y0 - 1) in W and y1 - y0 >= 4:
                put('Poweredlight', x, y0, WALLROT['S'])


# =============================================================== помещения

# --- нос: разрывной генератор и суперконденсатор
room(-6, 66, 6, 72, 'CMFloorBlack')
put('DHRiftGeneratorShip', -3, 70)
put('DHRiftSupercapacitorShip', 3, 70)
put('Rack', -5, 66)
put('ClosetEmergencyFilledRandom', 5, 66)
door(0, 65, 'AirlockEngineeringLocked')
powered('rift', -6, 66, 6, 72, (-7, 69), 'W')

# --- корабельная артиллерия
room(-14, 54, 14, 64, 'CMFloorPlate')
NORTH = 0.0  # ствол на клетку выше казённика
for gx, breech, barrel in ((-8, 'DHHeavyGunBreech300', 'DHHeavyGunBarrel300'),
                           (0, 'DHHeavyGunBreech570', 'DHHeavyGunBarrel570'),
                           (8, 'DHHeavyGunBreech300', 'DHHeavyGunBarrel300')):
    put(breech, gx, 60, NORTH)
    put(barrel, gx, 61, NORTH)
    put('DHComputerNavalArtillery', gx + 2, 58, FACE['S'])
    put('ChairOfficeDark', gx + 2, 57, FACE['N'])
for i, wh in enumerate(('HighExplosive', 'ArmorPiercing', 'Incendiary', 'Cluster')):
    put('DHHeavyShell300' + wh, -13 + i, 55, n=3)
    put('DHHeavyShell300' + wh, 10 + i, 55, n=3)
    put('DHHeavyShell570' + wh, -2 + i, 55, n=2)
put('SyndieFlag', -14, 64)
put('SyndieFlag', 14, 64)
door(0, 53, 'AirlockSecurityGlassLocked')
door(-1, 53, 'AirlockSecurityGlassLocked')
door(1, 53, 'AirlockSecurityGlassLocked')
powered('artillery', -14, 54, 14, 64, (-15, 59), 'W')

# --- ангарная палуба: «Зона вылета 1» (запад) и ВПП (центр), ПВО и склад техники (восток)
room(-23, 20, 13, 51, 'CMFloorPlate')
for y in range(20, 52):          # разметка ВПП
    T[(-2, y)] = 'RMCFloorPlatingStripedWest'
    T[(12, y)] = 'RMCFloorPlatingStripedEast'
for x in range(-1, 12):
    T[(x, 51)] = 'RMCFloorPlatingStripedNorth'
    T[(x, 20)] = 'RMCFloorPlatingStriped'
powered('hangar', -23, 20, 13, 51, (14, 47), 'E', step=6)
line('CableApcExtension', (-23, 21), (13, 21))
line('CableApcExtension', (13, 21), (13, 47))
# стоянка «Мидуэя»: центр челнока на клетке (-14, 34), вырез — по клеткам грида челнока
MID = (-14, 34)
put('DHDropshipHangarMidway', *MID)
HOLE = {(MID[0] + x, MID[1] + y) for x, y in MIDWAY}
# ПВО
room(15, 34, 23, 51, 'CMFloorBlack')
put('DHRadarEmpire', 19, 43)
for p in ((17, 49), (21, 49), (17, 37), (21, 37)):
    put('DHMissileLauncher', *p)
put('DHInterceptorMissile', 15, 51, n=4)
put('DHInterceptorMissile', 23, 34, n=4)
put('DHCommsRelayEmpire', 22, 43)
door(14, 43, 'AirlockSecurityGlassLocked')
door(14, 42, 'AirlockSecurityGlassLocked')
powered('aa', 15, 34, 23, 51, (24, 42), 'E')
# склад техники и десантного снаряжения
room(15, 20, 23, 32, 'CMFloorCargo')
put('DHVehicleDropPalletTankEmpire', 17, 30)
put('DHVehicleDropPalletIM30', 20, 30)
put('DHFieldGun75', 17, 26)
put('DHCrateFieldGun75', 18, 26)
put('DHCrateMortar', 20, 26)
put('DHCrateMortar', 21, 26)
put('DHCrateParachutes', 22, 26)
put('DHCrateParachutes', 22, 25)
put('DHDropBeacon', 16, 22, n=4)
put('DHTentImperialFolded', 18, 22)
put('DHTentImperialMedicalFolded', 19, 22)
put('DHCamoNetFolded', 20, 22, n=2)
put('DHClothingOuterGhillie', 21, 22, n=2)
put('DHSandbagsEmpty25', 22, 22, n=2)
put('DHBarbedWire10', 23, 22, n=2)
put('Shovel', 23, 23, n=2)
for x in range(16, 23):
    if x % 2 == 0:
        put('Rack', x, 20)
door(14, 26, 'AirlockCargoGlassLocked')
door(14, 25, 'AirlockCargoGlassLocked')
powered('vehicles', 15, 20, 23, 32, (24, 27), 'E')
# проход с ангарной палубы к носу
door(-1, 52, 'AirlockSyndicateGlass')
door(0, 52, 'AirlockSyndicateGlass')
door(1, 52, 'AirlockSyndicateGlass')

# --- главный коридор (x -1..1) от карго до кормы
room(-1, -13, 1, 18, 'CMFloorSteel')
opening(-1, 19, 1, 19)
powered('corridor', -1, -13, 1, 18, (2, 4), 'E', step=6)

# --- карго: лифт снабжения
room(-23, 7, -3, 18, 'CMFloorCargo')
put('DHSupplyElevator', -13, 12)
put('DHComputerSupply', -6, 15, FACE['S'])
put('ChairOfficeDark', -6, 14, FACE['N'])
for x in (-22, -20, -18):
    put('CrateFilledSpawner', x, 17)
put('Rack', -22, 7)
put('Rack', -21, 7)
put('LockerEvacRepairFilled', -4, 7)
door(-2, 12, 'AirlockCargoGlassLocked')
door(-2, 11, 'AirlockCargoGlassLocked')
opening(-23, 19, -15, 19)   # грузы выкатываются прямо на ангарную палубу
powered('cargo', -23, 7, -3, 18, (-24, 12), 'W')

# --- подготовка: вендоматы снаряжения
room(3, 7, 23, 18, 'CMFloorSteel')
put('DHGearVendorEmpireWeapons', 5, 18)
put('DHGearVendorEmpireAttachments', 7, 18)
put('DHGearVendorEmpireEquipment', 9, 18)
put('DHGearVendorEmpireMedic', 11, 18)
put('DHArmorRack', 14, 18)
put('DHArmorRack', 15, 18)
put('DHFieldLathe', 18, 18)
put('VendingMachineTankDispenserEVA', 21, 18)
put('VendingMachineSec', 22, 18)
put('WeaponCapacitorRecharger', 13, 10)
put('PowerCellRecharger', 14, 10)
put('TableReinforced', 13, 10)
put('TableReinforced', 14, 10)
for x in range(5, 12, 2):
    put('TableReinforced', x, 12)
door(2, 12)
door(2, 11)
powered('prep', 3, 7, 23, 18, (24, 12), 'E')

# --- казармы (как на «Алмаере»: ряды двухъярусных коек со шкафчиками)
room(-23, -12, -3, 5, 'CMFloorSteel')
spawns = []
for row_y in (3, -2, -7):
    for x in range(-22, -4, 2):
        put('RMCBedBunk', x, row_y)
        spawns.append((x, row_y))
        spawns.append((x, row_y - 1))
for x in range(-22, -4, 3):
    put('LockerSyndicatePersonal', x, 5)
put('TableReinforced', -6, -11)
put('TableReinforced', -5, -11)
put('ChairFolding', -6, -10, FACE['S'])
put('ChairFolding', -5, -10, FACE['S'])
for x, y in spawns:
    put('SpawnPointAnyJob', x, y)
door(-2, 0)
door(-2, -1)
door(-2, -9)
powered('barracks', -23, -12, -3, 5, (-24, -2), 'W')

# --- брифинговая
room(3, -12, 23, 5, 'CMFloorOffice')
put('Screen', 13, 6, WALLROT['N'])
put('BannerSyndicate', 9, 5)
put('BannerSyndicate', 17, 5)
put('TableReinforced', 12, 3)
put('TableReinforced', 13, 3)
put('TableReinforced', 14, 3)
for y in (0, -2, -4, -6, -8):
    for x in list(range(6, 12)) + list(range(15, 21)):
        put('ChairFolding', x, y, FACE['N'])
for p in ((5, -11), (6, -11), (7, -11), (8, -11), (19, -11), (20, -11)):
    put('SpawnPointLatejoin', *p)
door(2, 0)
door(2, -1)
powered('briefing', 3, -12, 23, 5, (24, -3), 'E')

# --- поперечный коридор (штабной ярус)
room(-23, -16, 23, -14, 'CMFloorSteel')
opening(-1, -13, 1, -13)
powered('cross_n', -23, -16, 23, -14, (-12, -13), 'N', step=6)

# --- штаб (мостик)
room(-9, -30, 9, -18, 'CMFloorBlack')
for x in range(-8, 9):
    W[(x, -17)] = WINDOW if abs(x) > 2 else W.get((x, -17), WALL)
door(0, -17, 'AirlockCommandGlassLocked')
door(-1, -17, 'AirlockCommandGlassLocked')
door(1, -17, 'AirlockCommandGlassLocked')
bridge = [
    ('DHComputerRiftNav', -1, -19), ('DHComputerLanding', 1, -19),
    ('DHComputerOverwatchEmpire', -6, -19), ('DHComputerSquadsEmpire', -4, -19),
    ('DHComputerOrbitalScan', 4, -19), ('ComputerRadar', 6, -19),
    ('ComputerComms', -8, -24), ('ComputerCrewMonitoring', 8, -24),
]
for proto, x, y in bridge:
    rot = FACE['S'] if y == -19 else (FACE['E'] if x < 0 else FACE['W'])
    put(proto, x, y, rot)
    cx, cy = (x, y - 1) if y == -19 else ((x + 1, y) if x < 0 else (x - 1, y))
    put('ChairOfficeDark', cx, cy, FACE['N'] if y == -19 else (FACE['W'] if x < 0 else FACE['E']))
put('DHFieldPhoneEmpireBridge', 0, -27)
put('TableReinforced', -1, -26)
put('TableReinforced', 0, -26)
put('TableReinforced', 1, -26)
put('SyndieFlag', -8, -30)
put('SyndieFlag', 8, -30)
put('DrinkChampagneBottleFull', 0, -26)
powered('bridge', -9, -30, 9, -18, (10, -24), 'E')
# комнаты командования
room(-23, -23, -11, -18, 'CMFloorOffice')   # капитан
room(-23, -30, -11, -25, 'CMFloorOffice')   # командир десанта
room(11, -23, 23, -18, 'CMFloorOffice')     # командир операции
room(11, -30, 23, -25, 'CMFloorOffice')     # офицер поддержки
for x0, y0, x1, y1, dx in ((-23, -23, -11, -18, -17), (-23, -30, -11, -25, -17),
                           (11, -23, 23, -18, 17), (11, -30, 23, -25, 17)):
    put('Bed', x0 + 1 if x0 < 0 else x1 - 1, y1)
    put('BedsheetSyndie', x0 + 1 if x0 < 0 else x1 - 1, y1)
    put('TableReinforced', dx, y0 + 2)
    put('TableReinforced', dx + 1, y0 + 2)
    put('ChairOfficeDark', dx, y0 + 1, FACE['N'])
    put('LockerSyndicatePersonal', x1 - 1 if x0 < 0 else x0 + 1, y1)
    put('PottedPlantRandom', x1 if x0 < 0 else x0, y0)
door(-17, -17, 'AirlockCommandGlassLocked')
door(17, -17, 'AirlockCommandGlassLocked')
door(-10, -27, 'AirlockCommandGlassLocked')
door(10, -27, 'AirlockCommandGlassLocked')
door(-17, -24, 'AirlockCommandGlassLocked')
door(17, -24, 'AirlockCommandGlassLocked')
powered('cmd_w', -23, -30, -11, -18, (-24, -24), 'W', step=6)
powered('cmd_e', 11, -30, 23, -18, (24, -24), 'E', step=6)

# --- поперечный коридор (инженерный ярус)
room(-23, -34, 23, -32, 'CMFloorSteel')
door(0, -31, 'AirlockCommandGlassLocked')
powered('cross_s', -23, -34, 23, -32, (-12, -31), 'N', step=6)

# --- инженерный отсек
room(-10, -50, 10, -36, 'CMFloorBlack')
REACTORS = ((-5, -40), (5, -40))
for rx, ry in REACTORS:
    put('DHFusionReactorShip', rx, ry)
put('CableTerminal', 0, -42, 0.0)
put('SMESBasic', 0, -43)
put('SubstationBasic', -2, -45)
put('SubstationBasic', 2, -45)
put('DHFusionFuelCassette', -8, -36, n=4)
put('DHFusionFuelCassette', 8, -36, n=4)
put('LockerEngineerFilled', -8, -50)
put('LockerEngineerFilled', -7, -50)
put('VendingMachineEngivend', 8, -50)
put('VendingMachineYouTool', 7, -50)
put('CrateEngineeringCableBulk', -4, -50)
put('SheetSteel', -2, -50)
put('SheetPlasteel', -1, -50)
put('SheetGlass', 1, -50)
put('WeldingFuelTankFull', 4, -50)
put('WaterTankFull', 5, -50)
door(0, -35, 'AirlockEngineeringLocked')
door(-1, -35, 'AirlockEngineeringLocked')
door(1, -35, 'AirlockEngineeringLocked')
powered('engineering', -10, -50, 10, -36, (-11, -46), 'W')
# ВН: реакторы → терминал → СМЭС → подстанции
line('CableHV', REACTORS[0], REACTORS[1])
line('CableHV', (0, -40), (0, -45))
line('CableHV', (-2, -45), (2, -45))
for sx in (-2, 2):
    line('CableMV', (sx, -45), (0, -45))
line('CableMV', (0, -45), (0, 70))

# --- двигатели
for sign in (-1, 1):
    x0, x1 = (-23, -12) if sign < 0 else (12, 23)
    room(x0, -62, x1, -36, 'CMFloorBlack')
    for x in range(x0, x1 + 1):
        T[(x, -64)] = 'CMFloorBlack'
        put('Thruster', x, -64)
    put('ClosetEmergencyFilledRandom', x0 if sign < 0 else x1, -36)
    put('ClosetEmergencyN2FilledRandom', x0 + 1 if sign < 0 else x1 - 1, -36)
    door(-11 if sign < 0 else 11, -43, 'AirlockEngineeringLocked')
    door(-11 if sign < 0 else 11, -42, 'AirlockEngineeringLocked')
    powered('engine%d' % sign, x0, -62, x1, -36, ((x0 - 1) if sign < 0 else (x1 + 1), -48),
            'W' if sign < 0 else 'E')
    # тяговые за корпусом: НН до них
    line('CableApcExtension', (x0, -62), (x1, -62))
    for x in range(x0, x1 + 1):
        CABLE['CableApcExtension'].add((x, -63))

# --- корма: медотсек, операционные, химлаборатория
room(-10, -58, 10, -52, 'CMFloorWhite', WHITE)
for x in range(-9, 10, 3):
    put('MedicalBed', x, -52)
put('StasisBed', -9, -58)
put('StasisBed', -8, -58)
put('DHLifeSupport', -5, -58)
put('DHVentilator', -4, -58)
put('DHIVDripFilled', -6, -53)
put('DHIVDripFilled', 0, -53)
put('DHIVDripFilled', 6, -53)
put('VendingMachineMedical', 9, -58)
put('LockerMedicineFilled', 8, -58)
put('LockerMedicineFilled', 7, -58)
put('Defibrillator', 2, -58)
put('TableReinforcedGlass', 2, -58)
put('TableReinforcedGlass', 3, -58)
put('HandheldHealthAnalyzer', 3, -58)
put('MedkitCombatFilled', 3, -58)
put('DHBloodPackFull', 2, -58, n=4)
put('VendingMachineWallMedical', -10, -55, WALLROT['W'])
door(0, -51, 'AirlockMedicalGlassLocked')
door(-1, -51, 'AirlockMedicalGlassLocked')
door(1, -51, 'AirlockMedicalGlassLocked')
powered('medbay', -10, -58, 10, -52, (11, -55), 'E')
# морг (в машинном проходе медотсека)
for x in (-9, -8, -7):
    put('Morgue', x, -56, FACE['N'])
# операционные
for sign in (-1, 1):
    x0, x1 = (-10, -5) if sign < 0 else (5, 10)
    room(x0, -67, x1, -60, 'CMFloorSterile', WHITE)
    cx = (x0 + x1) // 2
    put('OperatingTable', cx, -64)
    put('DHVentilator', cx - 1, -64)
    put('DHLifeSupport', cx + 1, -64)
    put('TableReinforcedGlass', x0, -67)
    put('TableReinforcedGlass', x0 + 1, -67)
    put('DHFieldSurgeryKitFilled', x0, -67)
    put('SawAdvanced', x0 + 1, -67)
    put('DHAutoclave', x1, -67)
    put('DHIVDripFilled', cx, -62)
    put('BoxLatexGloves', x0 + 1, -67)
    put('BoxSterileMask', x0 + 1, -67)
    put('SinkWide', x1, -60, FACE['S'])
    door(cx, -59, 'AirlockMedicalGlassLocked')
    powered('or%d' % sign, x0, -67, x1, -60, ((x0 - 1) if sign < 0 else (x1 + 1), -63), 'W' if sign < 0 else 'E')
# химическая лаборатория и вирусология
room(-3, -67, 3, -60, 'RMCFloorResearchContainment', WHITE)
put('DHChemDispenser', -3, -60)
put('DHChemDispenser', -2, -60)
put('DHChemMaster', 2, -60)
put('DHChemMaster', 3, -60)
put('DHChemistryHotplate', -3, -63)
put('DHChemistryHotplate', -3, -64)
put('DHElectrolysisUnit', 3, -63)
put('DHFumeHood', -3, -67)
put('DHFumeHood', -2, -67)
put('DHPathogenAnalyzer', 2, -67)
put('DHVirologyComputer', 3, -67)
put('DHCrateChemistryAcids', -1, -67)
put('DHCrateChemistryBases', 0, -67)
put('DHCrateChemistryOrganics', 1, -67)
put('TableReinforcedGlass', 0, -63)
put('TableReinforcedGlass', 1, -63)
put('BoxBeaker', 0, -63)
put('LargeBeaker', 1, -63, n=2)
put('BoxSyringe', 1, -63)
put('BoxPillCanister', 0, -63)
put('SinkWide', 0, -60, FACE['S'])
door(0, -59, 'AirlockMedicalGlassLocked')
put('APCBasic', 0, -68, PI)
CABLE['CableApcExtension'].update({(0, -68), (0, -67)})
line('CableApcExtension', (-3, -66), (3, -66))
line('CableApcExtension', (0, -67), (0, -61))
line('CableApcExtension', (-3, -62), (3, -62))
line('CableMV', (0, -68), (0, -45))
put('Poweredlight', -1, -60, WALLROT['N'])
put('Poweredlight', 1, -67, WALLROT['S'])

# =============================================================== корпус и вырез под челнок

# внешняя обшивка: стены, граничащие с космосом, — корпус
tiles = set(T)
for (x, y), proto in list(W.items()):
    if proto in (WALL, WHITE):
        if any((x + dx, y + dy) not in tiles for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
            W[(x, y)] = HULL

# вырез: без плитки, стен и сущностей
for p in HOLE:
    T.pop(p, None)
    W.pop(p, None)
    for k in CABLE:
        CABLE[k].discard(p)
E[:] = [e for e in E if (e[1], e[2]) not in HOLE or e[0] == 'DHDropshipHangarMidway']
# воздушные завесы по краю выреза (проходимы, газ не пускают): клетка палубы, сторона к вырезу
SEAL_ROT = {(0, -1): 0.0, (0, 1): PI, (1, 0): PI / 2, (-1, 0): -PI / 2}
for (x, y) in list(T):
    for (dx, dy), rot in SEAL_ROT.items():
        if (x + dx, y + dy) in HOLE and (x, y) not in W:
            put('DHHangarAirSeal', x, y, rot)

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
w('  time: 11/11/2026 22:30:00')
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
print(OUT, 'entities', uid - 1, 'tiles', len(T), 'hole', len(HOLE), 'rooms', len(ROOMS))
