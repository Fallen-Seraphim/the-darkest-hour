"""Draft maps of the Assault mode (to test the mode until the author draws the real ones):

- Firebase Terminus (Resources/Maps/_DarkestHour/Assault/terminus.yml) — the Republican garrison base on the surface
  of REP-27-7: a 60x44 compound behind a reinforced wall with three gates; HQ with the command room, the comms room and
  the vault holding the experimental sample (Republic command access), barracks with 25 bunks, armoury, medbay,
  workshop, three reinforced pads for the future anti-air batteries and light posts (the planet has nights).
  The map is the planet surface: the base grid becomes station "Terminus" (gameMap DHTerminus); biome, air and light
  are added at round start (DHPlanetBaseSystem).
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
}

X0, X1 = -30, 29   # inclusive
Y0, Y1 = -22, 21


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

    def put(self, proto, x, y, solid=True):
        self.ents.setdefault(proto, []).append((x, y))
        if solid:
            self.blocked.add((x, y))

    def clear(self, x, y):
        """Removes walls/windows at a tile (doors and gates)."""
        for proto in ('WallReinforced', 'WallSolid', 'ReinforcedWindow'):
            if (x, y) in self.ents.get(proto, []):
                self.ents[proto].remove((x, y))
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
    # vault: the sample on a reinforced table
    b.put('TableReinforced', 6, 17)
    b.put('DHExperimentalSample', 6, 17, solid=False)
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

    # pads for the future anti-air batteries
    for cx, cy in ((-24, -18), (23, -18), (-6, 3)):
        b.fill(cx - 1, cy - 1, cx + 1, cy + 1, 'FloorReinforced')

    # light posts: yard and rooms (the planet has nights)
    for x, y in ((-10, -16), (10, -16), (-10, 0), (10, 0), (0, -8), (-6, 21 - 2), (12, 21 - 2), (-20, 2),
                 (-25, 14), (-17, 9), (-6, 17), (0, 11), (8, 10), (25, 10), (25, -2), (-25, -4), (-3, -20),
                 (3, -20), (27, -16), (-28 + 1, -20)):
        if (x, y) not in b.blocked:
            b.put('LightPostSmall', x, y)

    return b


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
        for x, y in positions:
            w('  - uid: %d' % uid)
            w('    components:')
            w('    - type: Transform')
            w('      pos: %s,%s' % (x + 0.5, y + 0.5))
            w('      parent: 2')
            uid += 1
    w('...')
    text = '\n'.join(lines) + '\n'
    text = text.replace('ENTITY_COUNT', str(uid - 1))
    with open(path, 'w', encoding='utf-8') as f:
        f.write(text)
    print(path, 'entities', uid - 1, 'tiles', len(b.tiles))


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


def build_carrier(src, dst, name, drop, adds):
    s = open(src, encoding='utf-8').read()
    s = s.replace('GL Evac Serpent', name)
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
            ents.append('  - uid: %d\n    components:\n    - type: Transform\n      pos: %s\n      parent: 1\n'
                        % (nxt, pos))
            nxt += 1
        protos.append([proto, ents])

    s = join(head, protos, tail)
    count = len(re.findall(r'^  - uid: \d+$', s, re.M))
    s = re.sub(r'entityCount: \d+', 'entityCount: %d' % count, s, count=1)
    open(dst, 'w', encoding='utf-8').write(s)
    print(dst, 'entities', count, 'dropped', len(dropped))


def main():
    write_base(build_base(), 'Resources/Maps/_DarkestHour/Assault/terminus.yml')

    seats = ['%s,%s' % (x + 0.5, y + 0.5) for y in (-9, -16) for x in (-3, -2, -1, 1, 2, 3)]
    seats += ['%s,%s' % (x + 0.5, -17.5) for x in (-5, -4, -2, -1, 1, 2, 4, 5)]
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
        ])


if __name__ == '__main__':
    main()
