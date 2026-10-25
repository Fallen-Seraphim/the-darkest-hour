"""Draft maps of the Assault mode (to test the mode until the author draws the real ones):

- Firebase Terminus (Resources/Maps/_DarkestHour/Assault/terminus.yml) — the Republican garrison base on the surface
  of REP-27-7: a 60x44 compound behind a reinforced wall with three gates; HQ with the command room, the comms room and
  the vault holding the experimental sample (Republic command access), barracks with 25 bunks, armoury, medbay,
  workshop, three reinforced pads for the future anti-air batteries and light posts (the planet has nights).
  The map is the planet surface: the base grid becomes station "Terminus" (gameMap DHTerminus); biome, air and light
  are added at round start (DHPlanetBaseSystem).
- Terminus Bunker (Resources/Maps/_DarkestHour/Assault/terminus_bunker.yml) — the garrison bunker under the HQ, a level
  of its own (Grid category, loaded by DHPlanetBaseSystem): command post with overwatch, the vault with the sample,
  shelter, the store with the cargo lift; hatches in the HQ and the barracks lead down.
- Imperial dropship (Resources/Maps/_DarkestHour/Shuttles/imperial_dropship.yml) — the suborbital dropship, two of
  them dock to the carrier's port airlocks (DHDropshipSystem flies them between the carrier and the landing zones).
- IC-3 Sovereign (Resources/Maps/_DarkestHour/Shuttles/imperial_carrier.yml) — the Imperial landing carrier, made from
  the Syndicate evacuation shuttle: rift navigation and landing consoles, a self-powered rift generator and
  supercapacitor, an armour rack, 20 job and 4 late-join spawn points; cash and the CentComm holopad removed.

Run from the repository root:  python3 Tools/_DarkestHour/assault/make_maps.py"""
import base64
import re
import struct

# ---------------------------------------------------------------- base

TILES = {
    'Space': 0,
    'FloorConcrete': 1,
    'FloorSteel': 2,
    'FloorDark': 3,
    'FloorReinforced': 4,
    'FloorWhite': 5,
    'FloorTechMaint': 6,
    'FloorDesert': 7,
}

X0, X1 = -30, 29   # inclusive
Y0, Y1 = -22, 21   # walled compound
M1 = 44            # top of the mountain massif north of the compound


class Base:
    def __init__(self):
        self.tiles = {}
        self.ents = {}  # proto -> [(x, y)]
        self.blocked = set()

    def tile(self, x, y, name):
        self.tiles[(x, y)] = name

    def fill(self, x0, y0, x1, y1, name):
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                self.tile(x, y, name)

    def put(self, proto, x, y, solid=True, rot=None):
        self.ents.setdefault(proto, []).append((x, y, rot))
        if solid:
            self.blocked.add((x, y))

    def clear(self, x, y):
        """Removes walls/windows at a tile (doors and gates)."""
        for proto in ('WallReinforced', 'WallSolid', 'ReinforcedWindow', 'WallRockSand', 'WallPlastitanium'):
            self.ents[proto] = [e for e in self.ents.get(proto, []) if e[:2] != (x, y)]
        self.blocked.discard((x, y))

    def walls(self, x0, y0, x1, y1, proto):
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                if x in (x0, x1) or y in (y0, y1):
                    self.put(proto, x, y)

    def door(self, proto, x, y):
        self.clear(x, y)
        self.put(proto, x, y)

    def window(self, x, y):
        self.clear(x, y)
        self.put('ReinforcedWindow', x, y)


def build_base():
    b = Base()
    b.fill(X0, Y0, X1, Y1, 'FloorConcrete')

    # perimeter with gates: south (main), north, east
    b.walls(X0, Y0, X1, Y1, 'WallReinforced')
    for x in range(-3, 3):
        b.clear(x, Y0)
    for x in range(-2, 2):
        b.clear(x, Y1)
    for y in range(-2, 2):
        b.clear(X1, y)

    # HQ: command room (west), comms (south-east), vault (north-east)
    b.fill(-9, 8, 10, 19, 'FloorDark')
    b.walls(-9, 8, 10, 19, 'WallSolid')
    for y in range(8, 20):
        b.put('WallSolid', 3, y)
    for x in range(3, 11):
        b.put('WallSolid', x, 14)
    b.door('DHAirlockRepublicLocked', 0, 8)
    b.door('DHAirlockRepublicGlass', 3, 11)
    b.door('DHAirlockRepublicCommandLocked', 6, 14)
    for x in (-7, -6, -3, -2, 6, 7):
        b.window(x, 8)
    b.fill(4, 15, 9, 18, 'FloorReinforced')
    # command table, chairs
    for x in range(-6, 0):
        b.put('TableReinforced', x, 14)
    for x in (-6, -4, -2):
        b.put('Chair', x, 15)
        b.put('Chair', x, 13)
    b.put('Binoculars', -5, 14, solid=False)
    # overwatch: two consoles at the north wall of the command room, chairs in front of them
    for x in (-3, -1):
        b.put('DHComputerOverwatchRepublic', x, 18)
        b.put('ChairOfficeDark', x, 17, solid=False, rot='3.141592653589793')
    # vault: the sample was moved down into the bunker vault (artillery could destroy it here)
    b.put('TableReinforced', 6, 17)
    # comms room: tables
    for x in (5, 6, 7):
        b.put('Table', x, 12)

    # barracks (west-north)
    b.fill(-28, 4, -14, 19, 'FloorSteel')
    b.walls(-28, 4, -14, 19, 'WallSolid')
    b.door('DHAirlockRepublicLocked', -14, 11)
    b.door('DHAirlockRepublicLocked', -21, 4)
    for y in (12, 13):
        b.window(-28, y)
    bunks = []
    for row in (18, 16, 7, 5):
        for x in range(-27, -15, 2):
            bunks.append((x, row))
    for x, y in bunks[:20]:
        b.put('Bed', x, y, solid=False)
        b.put('SpawnPointAnyJob', x, y, solid=False)
    for x in (-26, -24, -18, -16, -22):
        b.put('SpawnPointAnyJob', x, 11, solid=False)
    for x in (-21, -20, -19):
        b.put('SpawnPointLatejoin', x, 12, solid=False)

    # armoury (west-south)
    b.fill(-28, -12, -14, 1, 'FloorSteel')
    b.walls(-28, -12, -14, 1, 'WallSolid')
    b.door('DHAirlockRepublicLocked', -21, 1)
    b.door('DHAirlockRepublicLocked', -14, -5)
    b.put('DHArmorRack', -26, -1)
    b.put('DHArmorRack', -24, -1)
    for x in range(-26, -16, 2):
        b.put('TableReinforced', x, -6)
        b.put('TableReinforced', x, -9)
    ammo = ['DHMagazine556x45FMG', 'DHMagazine556x45FMG', 'DHMagazine762x51FMG', 'DHMagazine10x24SVP',
            'DHMagazineBox556x45FMG']
    for i, x in enumerate(range(-26, -16, 2)):
        b.put(ammo[i % len(ammo)], x, -6, solid=False)
        b.put(ammo[(i + 2) % len(ammo)], x, -9, solid=False)

    # medbay (east-north)
    b.fill(14, 6, 28, 19, 'FloorWhite')
    b.walls(14, 6, 28, 19, 'WallSolid')
    b.door('DHAirlockRepublicLocked', 14, 12)
    for y in (15, 16):
        b.window(28, y)
    for x in (17, 20, 23, 26):
        b.put('MedicalBed', x, 17)
    for x in (17, 19, 21):
        b.put('Table', x, 8)
    b.put('MedkitCombatFilled', 17, 8, solid=False)
    b.put('MedkitFilled', 19, 8, solid=False)
    b.put('MedkitFilled', 21, 8, solid=False)

    # workshop (east-south)
    b.fill(14, -12, 28, 2, 'FloorTechMaint')
    b.walls(14, -12, 28, 2, 'WallSolid')
    b.door('DHAirlockRepublicLocked', 14, -5)
    for x in (17, 19, 21):
        b.put('Table', x, -10)
    b.put('SheetSteel', 17, -10, solid=False)
    b.put('ClothingBeltUtilityFilled', 19, -10, solid=False)
    b.put('Shovel', 21, -10, solid=False)

    # fortifications: tents and sandbags for the garrison
    for x in (23, 24, 25):
        b.put('Table', x, -10)
    b.put('DHTentRepublicFolded', 23, -10, solid=False)
    b.put('DHTentRepublicMedicalFolded', 24, -10, solid=False)
    b.put('DHTentRepublicCommandFolded', 25, -10, solid=False)
    for x in (17, 19, 21, 23):
        b.put('DHSandbagsFull25', x, -7, solid=False)
        b.put('DHSandbagsEmpty25', x, -5, solid=False)
    b.put('Shovel', 25, -7, solid=False)
    b.put('Shovel', 25, -5, solid=False)
    # comms: the garrison comms tower in the yard east of the main gate, field telephones at the HQ, the main gate
    # and the battery
    b.put('DHCommsRelayRepublic', 16, -16)
    b.put('DHFieldPhoneRepublicHQ', -8, 10)
    b.put('DHFieldPhoneRepublicGate', -1, -19)
    b.put('DHFieldPhoneRepublicBattery', 3, 31)
    # 90 mm infantry gun covering the main gate, with a crate of shells
    b.put('DHFieldGun90', 3, -18, rot='0')
    b.put('DHCrateFieldGun90', 5, -18)
    # mortar crates of the garrison (mortar, laser designator, 18 shells each)
    b.put('DHCrateMortar', 27, -10)
    b.put('DHCrateMortar', 27, -8)

    # air defence of the base: two interceptor missile silos and two twin 30 mm autocannons on reinforced pads
    for cx, cy, proto in ((-24, -18, 'DHMissileSilo'), (23, -18, 'DHMissileSilo'),
                          (-6, 3, 'DHAutocannonTwin30'), (6, 3, 'DHAutocannonTwin30')):
        b.fill(cx - 1, cy - 1, cx + 1, cy + 1, 'FloorReinforced')
        b.put(proto, cx, cy)
        ammo = 'DHInterceptorMissile' if proto == 'DHMissileSilo' else 'DHAutocannonAmmoBox30'
        for dx in (-1, 1):
            b.put(ammo, cx + dx, cy - 1, solid=False)

    # passages to the bunker below: hatches in the command room and the barracks, the cargo lift in the yard
    b.put('DHHatchTerminusHQ', 1, 10)
    b.put('DHHatchTerminusBarracks', -15, 14)
    b.fill(7, -4, 9, -2, 'FloorReinforced')
    b.put('DHLiftPlatformTerminus', 8, -3, solid=False)
    b.put('DHLiftPanelTerminus', 10, -3)

    build_massif(b)

    # light posts: yard and rooms (the planet has nights)
    for x, y in ((-10, -16), (10, -16), (-10, 0), (10, 0), (0, -8), (-6, 21 - 2), (12, 21 - 2), (-20, 2),
                 (-25, 14), (-17, 9), (-6, 17), (0, 11), (8, 10), (25, 10), (25, -2), (-25, -4), (-3, -20),
                 (3, -20), (27, -16), (-28 + 1, -20)):
        if (x, y) not in b.blocked:
            b.put('LightPostSmall', x, y)

    return b


def build_massif(b):
    """Mountain massif north of the compound with the anti-orbital battery dug into it: a tunnel from the north gate,
    a gun gallery with the 700 mm and two 400 mm guns facing north (barrels in open pits), the shell store (west) and
    the battery command post (east); every gun has its own fire control console."""
    b.fill(X0, Y1 + 1, X1, M1, 'FloorDesert')
    for x in range(X0, X1 + 1):
        for y in range(Y1 + 1, M1 + 1):
            # ragged outline of the massif
            edge = min(x - X0, X1 - x, M1 - y)
            if edge < 2 and (x * 7 + y * 3) % 5 < 2:
                continue
            b.put('WallRockSand', x, y)

    def carve(x0, y0, x1, y1, tile):
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                b.clear(x, y)
                b.tile(x, y, tile)

    carve(-2, Y1 + 1, 1, 29, 'FloorConcrete')         # tunnel from the north gate
    carve(-15, 30, 15, 33, 'FloorReinforced')         # gun gallery
    carve(-24, 28, -17, 34, 'FloorSteel')             # shell store
    carve(-16, 31, -16, 32, 'FloorReinforced')
    carve(17, 28, 24, 34, 'FloorDark')                # fire control bunker
    carve(16, 31, 16, 32, 'FloorReinforced')

    # guns: breech on the gallery's north row, barrel in a pit in front of it
    for x, cal in ((0, '700'), (-11, '400'), (11, '400')):
        carve(x, 34, x, 34, 'FloorReinforced')
        b.put('DHHeavyGunBreech' + cal, x, 33)
        b.put('DHHeavyGunBarrel' + cal, x, 34)

    # the shell store: per calibre and warhead
    warheads = ('HighExplosive', 'ArmorPiercing', 'Incendiary', 'Cluster')
    for i, wh in enumerate(warheads):
        for n in range(2):
            b.put('DHHeavyShell700' + wh, -23 + i, 33 - n, solid=False)
        for n in range(3):
            b.put('DHHeavyShell400' + wh, -23 + i, 30 - n, solid=False)
    b.put('LightPostSmall', -18, 33)

    # every gun has its own fire control console with the gunner's chair in front of it (the console links to
    # the nearest breech); the bunker is the battery command post
    for x in (-13, -2, 9):
        b.put('DHComputerAntiOrbital', x, 32)
        b.put('ChairOfficeDark', x, 31, solid=False, rot='3.141592653589793')
    b.put('TableReinforced', 21, 33)
    b.put('TableReinforced', 23, 33)
    b.put('Binoculars', 23, 33, solid=False)
    b.put('LightPostSmall', 18, 29)

    for x in (-12, -4, 4, 12):
        b.put('LightPostSmall', x, 30)
    b.put('LightPostSmall', -1, 25)


def chunks(tiles):
    out = {}
    for (x, y), name in tiles.items():
        cx, cy = x // 16, y // 16
        out.setdefault((cx, cy), {})[(x - cx * 16, y - cy * 16)] = TILES[name]
    result = []
    for (cx, cy), cells in sorted(out.items()):
        data = bytearray()
        for i in range(256):
            lx, ly = i % 16, i // 16
            data += struct.pack('<iBB', cells.get((lx, ly), 0), 0, 0)
        result.append((cx, cy, base64.b64encode(bytes(data)).decode()))
    return result


def write_base(b, path):
    lines = []
    w = lines.append
    w('meta:')
    w('  format: 7')
    w('  category: Map')
    w('  engineVersion: 266.0.0')
    w('  forkId: ""')
    w('  forkVersion: ""')
    w('  time: 10/24/2026 12:00:00')
    w('  entityCount: ENTITY_COUNT')
    w('maps:')
    w('- 1')
    w('grids:')
    w('- 2')
    w('orphans: []')
    w('nullspace: []')
    w('tilemap:')
    for name, idx in sorted(TILES.items(), key=lambda kv: kv[1]):
        w('  %d: %s' % (idx, name))
    w('entities:')
    w('- proto: ""')
    w('  entities:')
    w('  - uid: 1')
    w('    components:')
    w('    - type: MetaData')
    w('      name: REP-27-7')
    w('    - type: Transform')
    w('    - type: Map')
    w('      mapPaused: True')
    w('    - type: GridTree')
    w('    - type: Broadphase')
    w('    - type: OccluderTree')
    w('  - uid: 2')
    w('    components:')
    w('    - type: MetaData')
    w('      name: Firebase Terminus')
    w('    - type: Transform')
    w('      parent: 1')
    w('    - type: MapGrid')
    w('      chunks:')
    for cx, cy, data in chunks(b.tiles):
        w('        %d,%d:' % (cx, cy))
        w('          ind: %d,%d' % (cx, cy))
        w('          tiles: %s' % data)
        w('          version: 6')
    w('    - type: Broadphase')
    w('    - type: Physics')
    w('      bodyStatus: InAir')
    w('      angularDamping: 0.05')
    w('      linearDamping: 0.05')
    w('      fixedRotation: False')
    w('      bodyType: Dynamic')
    w('    - type: Fixtures')
    w('      fixtures: {}')
    w('    - type: BecomesStation')
    w('      id: Terminus')
    w('    - type: OccluderTree')
    w('    - type: GridPathfinding')
    w('    - type: DecalGrid')
    w('      chunkCollection:')
    w('        version: 2')
    w('        nodes: []')
    w('    - type: SpreaderGrid')
    uid = 3
    for proto in sorted(b.ents):
        positions = b.ents[proto]
        if not positions:
            continue
        w('- proto: %s' % proto)
        w('  entities:')
        for x, y, rot in positions:
            w('  - uid: %d' % uid)
            w('    components:')
            w('    - type: Transform')
            if rot is not None:
                w('      rot: %s rad' % rot)
            w('      pos: %s,%s' % (x + 0.5, y + 0.5))
            w('      parent: 2')
            uid += 1
    w('...')
    text = '\n'.join(lines) + '\n'
    text = text.replace('ENTITY_COUNT', str(uid - 1))
    with open(path, 'w', encoding='utf-8') as f:
        f.write(text)
    print(path, 'entities', uid - 1, 'tiles', len(b.tiles))


# ---------------------------------------------------------------- bunker

def build_bunker():
    """The garrison bunker under the HQ (a level of its own, same coordinates as the surface): corridor with the
    ladders, command post with overwatch, the vault with the experimental sample, shelter and the store with the
    cargo lift."""
    b = Base()
    bx0, bx1, by0, by1 = -16, 16, -2, 22
    b.fill(bx0, by0, bx1, by1, 'FloorDark')
    b.walls(bx0, by0, bx1, by1, 'WallReinforced')
    for x in range(bx0, bx1 + 1):
        b.put('WallReinforced', x, 12)
        b.put('WallReinforced', x, 8)
    for y in list(range(by0, 9)) + list(range(12, by1 + 1)):
        b.put('WallReinforced', 0, y)
    b.fill(-15, 9, 15, 11, 'FloorSteel')

    b.door('DHAirlockRepublicLocked', -8, 12)          # command post
    b.door('DHAirlockRepublicCommandLocked', 8, 12)    # vault
    b.door('DHAirlockRepublicLocked', -8, 8)           # shelter
    for x in (11, 12, 13):                             # wide opening into the store for the lift cargo
        b.clear(x, 8)

    # ladders up (same links as the hatches above)
    b.put('DHLadderTerminusHQ', 1, 10)
    b.put('DHLadderTerminusBarracks', -15, 10)

    # command post: overwatch and the map table
    b.fill(-15, 13, -1, 21, 'FloorReinforced')
    for x in (-12, -10):
        b.put('DHComputerOverwatchRepublic', x, 21)
        b.put('ChairOfficeDark', x, 20, solid=False, rot='3.141592653589793')
    for x in range(-12, -5):
        b.put('TableReinforced', x, 16)
    for x in (-11, -9, -7):
        b.put('Chair', x, 15, solid=False)
        b.put('Chair', x, 17, solid=False, rot='3.141592653589793')
    b.put('Binoculars', -9, 16, solid=False)

    b.put('DHFieldPhoneRepublicBunker', -14, 20)

    # the vault: the experimental sample
    b.fill(1, 13, 15, 21, 'FloorReinforced')
    b.put('TableReinforced', 8, 18)
    b.put('DHExperimentalSample', 8, 18, solid=False)

    # shelter: bunks and two medical beds
    for x in range(-14, -1, 2):
        b.put('Bed', x, 6, solid=False)
    b.put('MedicalBed', -14, 0)
    b.put('MedicalBed', -12, 0)
    b.put('Table', -4, 0)
    b.put('MedkitCombatFilled', -4, 0, solid=False)
    b.put('Table', -3, 0)
    b.put('DHIFAKFilled', -3, 0, solid=False)
    b.put('DHIFAKFilled', -3, 0, solid=False)

    # store: the cargo lift, ammunition and field fortifications
    b.fill(7, 3, 9, 5, 'FloorReinforced')
    b.put('DHLiftPlatformTerminus', 8, 4, solid=False)
    b.put('DHLiftPanelTerminus', 10, 4)
    for x in (2, 3, 4, 5):
        b.put('TableReinforced', x, 6)
    for x, proto in ((2, 'DHMagazine556x45FMG'), (3, 'DHMagazine556x45FMG'), (4, 'DHMagazine762x51FMG'),
                     (5, 'DHMagazineBox556x45FMG')):
        b.put(proto, x, 6, solid=False)
        b.put(proto, x, 6, solid=False)
    for x in (13, 14):
        b.put('DHSandbagsFull25', x, 1, solid=False)
        b.put('DHSandbagsEmpty25', x, 0, solid=False)
    b.put('Shovel', 15, 0, solid=False)
    b.put('Shovel', 15, 1, solid=False)

    # lights (always powered: the bunker has no wiring), on the tiles under the north walls
    for x, y in ((-12, 21), (-4, 21), (4, 21), (12, 21), (-12, 11), (-4, 11), (4, 11), (12, 11),
                 (-12, 7), (-4, 7), (4, 7), (13, 7)):
        if (x, y) not in b.blocked:
            b.put('AlwaysPoweredWallLight', x, y, solid=False)
    return b


def write_grid(b, path, name, extra=()):
    """A Grid-category file (loaded onto its own map by DHPlanetBaseSystem)."""
    lines = []
    w = lines.append
    w('meta:')
    w('  format: 7')
    w('  category: Grid')
    w('  engineVersion: 266.0.0')
    w('  forkId: ""')
    w('  forkVersion: ""')
    w('  time: 10/24/2026 12:00:00')
    w('  entityCount: ENTITY_COUNT')
    w('maps: []')
    w('grids:')
    w('- 1')
    w('orphans:')
    w('- 1')
    w('nullspace: []')
    w('tilemap:')
    for tname, idx in sorted(TILES.items(), key=lambda kv: kv[1]):
        w('  %d: %s' % (idx, tname))
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
    for cx, cy, data in chunks(b.tiles):
        w('        %d,%d:' % (cx, cy))
        w('          ind: %d,%d' % (cx, cy))
        w('          tiles: %s' % data)
        w('          version: 6')
    w('    - type: Broadphase')
    w('    - type: Physics')
    w('      bodyStatus: InAir')
    w('      angularDamping: 0.05')
    w('      linearDamping: 0.05')
    w('      fixedRotation: False')
    w('      bodyType: Dynamic')
    w('    - type: Fixtures')
    w('      fixtures: {}')
    w('    - type: OccluderTree')
    w('    - type: GridPathfinding')
    w('    - type: DecalGrid')
    w('      chunkCollection:')
    w('        version: 2')
    w('        nodes: []')
    w('    - type: SpreaderGrid')
    for line in extra:
        w(line)
    uid = 2
    for proto in sorted(b.ents):
        positions = b.ents[proto]
        if not positions:
            continue
        w('- proto: %s' % proto)
        w('  entities:')
        for x, y, rot in positions:
            w('  - uid: %d' % uid)
            w('    components:')
            w('    - type: Transform')
            if rot is not None:
                w('      rot: %s rad' % rot)
            w('      pos: %s,%s' % (x + 0.5, y + 0.5))
            w('      parent: 1')
            uid += 1
    w('...')
    text = '\n'.join(lines) + '\n'
    text = text.replace('ENTITY_COUNT', str(uid - 1))
    with open(path, 'w', encoding='utf-8') as f:
        f.write(text)
    print(path, 'entities', uid - 1, 'tiles', len(b.tiles))


def build_dropship():
    """A suborbital dropship of the landing force (9x11): cockpit with the flight console, ten seats, two docking
    airlocks on the starboard side (they dock to the carrier's port airlocks) and a rear ramp of three doors."""
    b = Base()
    b.fill(0, 0, 8, 10, 'FloorDark')
    b.fill(1, 8, 7, 9, 'FloorReinforced')
    b.walls(0, 0, 8, 10, 'WallPlastitanium')
    east = '1.5707963267948966'
    for y in (5, 7):
        b.clear(8, y)
        b.put('AirlockGlassShuttleSyndicate', 8, y, rot=east)
    for x in (3, 4, 5):
        b.door('AirlockSyndicateGlass', x, 0)
    b.put('DHComputerDropship', 4, 9)
    b.put('ChairOfficeDark', 4, 8, solid=False, rot='3.141592653589793')
    b.put('DHFieldPhoneEmpireDropship', 2, 8)
    for y in range(2, 7):
        b.put('Chair', 1, y, solid=False, rot='-1.5707963267948966')
        b.put('Chair', 6, y, solid=False, rot=east)
    for x, y in ((2, 9), (6, 9)):
        b.put('AlwaysPoweredWallLight', x, y, solid=False)
    for x in (2, 6):
        b.put('AlwaysPoweredWallLight', x, 1, solid=False, rot='3.141592653589793')
    return b


# ---------------------------------------------------------------- carrier

def blocks(text):
    """Split the entity list into (proto, [entity text]) and the head/tail around it."""
    head, rest = text.split('entities:\n- proto: ""', 1)
    rest = '- proto: ""' + rest
    tail = ''
    if rest.rstrip('\n').endswith('...'):
        rest, tail = rest.rstrip('\n')[:-3], '...\n'
    protos = []
    for chunk in re.split(r'(?m)^(?=- proto: )', rest):
        if not chunk.strip():
            continue
        first, body = chunk.split('\n', 1)
        body = body.split('  entities:\n', 1)[1] if '  entities:\n' in body else body
        ents = [e for e in re.split(r'(?m)^(?=  - uid: )', body) if e.strip()]
        protos.append([first[len('- proto: '):], ents])
    return head + 'entities:\n', protos, tail


def join(head, protos, tail):
    out = head
    for proto, ents in protos:
        if not ents:
            continue
        out += '- proto: %s\n  entities:\n' % proto + ''.join(e if e.endswith('\n') else e + '\n' for e in ents)
    return out + tail


def uid_of(e):
    return int(re.match(r'  - uid: (\d+)', e).group(1))


def decode_tiles(s):
    """Tiles of a grid file (format 7 chunks): {(x, y): tile id} and the byte size per tile."""
    tiles = {}
    for ind, data, ver in re.findall(r'ind: (-?\d+,-?\d+)\n\s+tiles: (\S+)\n\s+version: (\d+)', s):
        cx, cy = map(int, ind.split(','))
        raw = base64.b64decode(data)
        size = 7 if ver == '7' else 6
        for i in range(256):
            tiles[(cx * 16 + i % 16, cy * 16 + i // 16)] = raw[i * size:(i + 1) * size]
    return tiles


def encode_chunks(tiles, space):
    """Back to format 7 chunks: tiles {(x, y): 7 bytes}; missing tiles are `space`."""
    by_chunk = {}
    for (x, y), raw in tiles.items():
        by_chunk.setdefault((x // 16, y // 16), {})[(x % 16, y % 16)] = raw
    out = []
    for (cx, cy), cells in sorted(by_chunk.items()):
        data = bytearray()
        for i in range(256):
            data += cells.get((i % 16, i // 16), space)
        out.append('        %d,%d:\n          ind: %d,%d\n          tiles: %s\n          version: 7\n'
                   % (cx, cy, cx, cy, base64.b64encode(bytes(data)).decode()))
    return ''.join(out)


def build_carrier(src, dst, name, drop, adds, floor=None):
    s = open(src, encoding='utf-8').read()
    s = s.replace('GL Evac Serpent', name)

    if floor:
        # extra floor tiles (the gun deck wing): FloorDark of the file's own tile map
        dark = int(re.search(r'^  (\d+): FloorDark$', s, re.M).group(1))
        space = int(re.search(r'^  (\d+): Space$', s, re.M).group(1))
        tiles = decode_tiles(s)
        for pos in floor:
            tiles[pos] = struct.pack('<iBBB', dark, 0, 0, 0)
        tiles = {k: (v if len(v) == 7 else v + b'\x00') for k, v in tiles.items()}
        chunks_text = encode_chunks(tiles, struct.pack('<iBBB', space, 0, 0, 0))
        s = re.sub(r'(    - type: MapGrid\n      chunks:\n)(?:        .*\n|          .*\n)+', lambda m: m.group(1) + chunks_text, s, count=1)

    head, protos, tail = blocks(s)
    dropped = set()
    for proto, ents in protos:
        if proto in drop:
            dropped |= {uid_of(e) for e in ents}
    for proto, ents in protos:
        ents[:] = [e for e in ents if uid_of(e) not in dropped]

    nxt = max(uid_of(e) for _, ents in protos for e in ents) + 1
    for proto, positions in adds:
        ents = []
        for pos in positions:
            rot = None
            if isinstance(pos, tuple):
                pos, rot = pos
            ents.append('  - uid: %d\n    components:\n    - type: Transform\n%s      pos: %s\n      parent: 1\n'
                        % (nxt, '      rot: %s rad\n' % rot if rot else '', pos))
            nxt += 1
        protos.append([proto, ents])

    s = join(head, protos, tail)
    count = len(re.findall(r'^  - uid: \d+$', s, re.M))
    s = re.sub(r'entityCount: \d+', 'entityCount: %d' % count, s, count=1)
    open(dst, 'w', encoding='utf-8').write(s)
    print(dst, 'entities', count, 'dropped', len(dropped))


def main():
    write_base(build_base(), 'Resources/Maps/_DarkestHour/Assault/terminus.yml')
    write_grid(build_bunker(), 'Resources/Maps/_DarkestHour/Assault/terminus_bunker.yml', 'Terminus Bunker')
    write_grid(build_dropship(), 'Resources/Maps/_DarkestHour/Shuttles/imperial_dropship.yml', 'Dropship',
               extra=['    - type: Shuttle', '    - type: Gravity', '      enabled: True', '      inherent: True'])

    seats = ['%s,%s' % (x + 0.5, y + 0.5) for y in (-9, -16) for x in (-3, -2, -1, 1, 2, 3)]
    seats += ['%s,%s' % (x + 0.5, -17.5) for x in (-5, -4, -2, -1, 1, 2, 4, 5)]

    # gun deck wing on the starboard side (east of the hull, x 8..14): the docking airlocks of the hull lead into it
    wing = [(x, y) for x in range(8, 15) for y in range(-22, -1)]
    walls = ['%s,%s' % (x + 0.5, y + 0.5) for x, y in wing if x == 14 or y in (-22, -2)]
    east = '-1.5707963267948966'
    p = lambda x, y: '%s,%s' % (x + 0.5, y + 0.5)
    shells = []
    for i, wh in enumerate(('HighExplosive', 'ArmorPiercing', 'Incendiary', 'Cluster')):
        shells.append(('DHHeavyShell570' + wh, [p(8 + i, -11)] * 2))
        shells.append(('DHHeavyShell300' + wh, [p(8 + i, -7)] * 3))
    build_carrier(
        'Resources/Maps/Shuttles/emergency_syndicate.yml',
        'Resources/Maps/_DarkestHour/Shuttles/imperial_carrier.yml',
        'IC-3 Sovereign',
        {'SpaceCash100', 'HolopadCentCommEvacShuttle'},
        [
            ('DHComputerRiftNav', ['3.5,-1.5']),
            ('DHComputerLanding', ['6.5,-2.5']),
            ('DHRiftGeneratorShip', ['-4.5,-20.5']),
            ('DHRiftSupercapacitorShip', ['-2.5,-20.5']),
            ('DHArmorRack', ['-5.5,-21.5']),
            ('SpawnPointAnyJob', seats),
            ('SpawnPointLatejoin', ['-1.5,-7.5', '-0.5,-7.5', '0.5,-7.5', '1.5,-7.5']),
            # gun deck: 570 mm main gun and two 300 mm guns facing starboard, barrels through the hull
            ('WallPlastitanium', walls),
            ('DHHeavyGunBreech570', [(p(12, -12), east)]),
            ('DHHeavyGunBarrel570', [(p(13, -12), east)]),
            ('DHHeavyGunBreech300', [(p(12, -5), east), (p(12, -19), east)]),
            ('DHHeavyGunBarrel300', [(p(13, -5), east), (p(13, -19), east)]),
            # every gun has its own fire control console with the gunner's chair in front of it
            ('DHComputerNavalArtillery', [p(9, -5), p(10, -13), p(10, -19)]),
            ('ChairOfficeDark', [(p(9, -6), '3.141592653589793'), (p(10, -14), '3.141592653589793'),
                                 (p(10, -20), '3.141592653589793')]),
            # point defence: four interceptor missile launchers and spare missiles
            ('DHMissileLauncher', [p(10, -3), p(10, -21), p(13, -9), p(13, -16)]),
            ('DHInterceptorMissile', [p(8, -21), p(8, -21), p(8, -3), p(8, -3)]),
            ('LightPostSmall', [p(11, -8), p(11, -16)]),
            # overwatch console on the bridge: the landing force is watched from orbit and from the air
            ('DHComputerOverwatchEmpire', [p(5, -4)]),
            # comms: the carrier antenna on the gun deck and the bridge telephone
            ('DHCommsRelayEmpire', [p(12, -3)]),
            ('DHFieldPhoneEmpireBridge', [p(4, -5)]),
            ('ChairOfficeDark', [(p(5, -5), '3.141592653589793')]),
            # paradrop beacons for the landing force
            ('DHDropBeacon', [p(11, -21), p(11, -21), p(12, -21), p(12, -21)]),
            # field tents and sandbags for a forward position
            ('DHTentImperialFolded', [p(-6, -20)]),
            ('DHTentImperialMedicalFolded', [p(-6, -20)]),
            ('DHSandbagsEmpty25', [p(-5, -22), p(-5, -22)]),
            ('DHCrateMortar', [p(-3, -21), p(-2, -21)]),
            # 75 mm infantry gun for the landing (rolled off the landed carrier or dropped by parachute)
            ('DHFieldGun75', [p(-3, -20)]),
            ('DHCrateFieldGun75', [p(-2, -22)]),
            ('Shovel', [p(-4, -22), p(-4, -22)]),
        ] + shells,
        floor=wing)


if __name__ == '__main__':
    main()
