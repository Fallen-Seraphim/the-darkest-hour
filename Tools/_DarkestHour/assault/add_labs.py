"""Chemistry labs of the Assault mode (author's decision 2026-11-11: a chem lab on the carrier and at the base).

The maps are hand-edited after make_maps.py, so this script patches the current files in place instead of
regenerating them (idempotent: a map that already has a DHChemDispenser is left alone).

- Firebase Terminus (terminus.yml): the north-east corner of the medbay (x -167..-163, y 66..68) is walled off as the
  lab, with a glass door from the medbay; the medbay sink and locker stay inside it.
- Stage 6: a ventilator and a life support system by the medbay beds of both maps.
- IC-3 Sovereign (imperial_carrier.yml): a new compartment south of the gun deck wing (x 8..14, y -28..-22), door in
  the wing's south wall. Air comes in from the gun deck when the door opens.

The lab machines need no power (DHChemDispenser, DHChemMaster, DHElectrolysisUnit, DHChemistryHotplate).

Run from the repository root:  python3 Tools/_DarkestHour/assault/add_labs.py"""
import os
import re
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_maps import blocks, join, uid_of, decode_tiles, encode_chunks  # noqa: E402

def p(x, y):
    return '%s,%s' % (x + 0.5, y + 0.5)


def patch(path, adds, drop_at=(), floor=None, grid=1, marker='DHChemDispenser'):
    s = open(path, encoding='utf-8').read()
    if '- proto: %s\n' % marker in s:
        print(path, 'already has', marker)
        return

    if floor:
        dark = int(re.search(r'^  (\d+): FloorDark$', s, re.M).group(1))
        space = int(re.search(r'^  (\d+): Space$', s, re.M).group(1))
        tiles = decode_tiles(s)
        for pos in floor:
            tiles[pos] = struct.pack('<iBBB', dark, 0, 0, 0)
        tiles = {k: (v if len(v) == 7 else v + b'\x00') for k, v in tiles.items()}
        chunks_text = encode_chunks(tiles, struct.pack('<iBBB', space, 0, 0, 0))
        s = re.sub(r'(    - type: MapGrid\n      chunks:\n)(?:        .*\n|          .*\n)+',
                   lambda m: m.group(1) + chunks_text, s, count=1)

    head, protos, tail = blocks(s)
    drop = {p(x, y) for x, y in drop_at}
    removed = 0
    for proto, ents in protos:
        if not proto.startswith('Wall'):
            continue
        keep = []
        for e in ents:
            m = re.search(r'pos: (-?[\d.]+),(-?[\d.]+)', e)
            if m and '%s,%s' % (float(m.group(1)), float(m.group(2))) in drop:
                removed += 1
                continue
            keep.append(e)
        ents[:] = keep

    nxt = max(uid_of(e) for _, ents in protos for e in ents) + 1
    for proto, x, y in adds:
        e = '  - uid: %d\n    components:\n    - type: Transform\n      pos: %s\n      parent: %d\n' % (nxt, p(x, y), grid)
        nxt += 1
        for name, ents in protos:
            if name == proto:
                ents.append(e)
                break
        else:
            protos.append([proto, [e]])

    s = join(head, protos, tail)
    count = len(re.findall(r'^  - uid: \d+$', s, re.M))
    s = re.sub(r'entityCount: \d+', 'entityCount: %d' % count, s, count=1)
    open(path, 'w', encoding='utf-8').write(s)
    print(path, 'added', len(adds), 'removed walls', removed, 'entities', count)


def lab_kit(x0, y0, x1, y1):
    """Lab furniture in the room x0..x1, y0..y1 (inclusive, interior): machines along the north wall, crates along
    the south wall."""
    return [
        ('DHChemDispenser', x0, y1),
        ('DHChemMaster', x0 + 1, y1),
        ('TableReinforced', x1 - 1, y1),
        ('TableReinforced', x1, y1),
        ('DHChemistryHotplate', x1 - 1, y1),
        ('DHElectrolysisUnit', x1, y1),
        ('DHCrateChemistryAcids', x0, y0),
        ('DHCrateChemistryBases', x0 + 1, y0),
        ('DHCrateChemistryOrganics', x0 + 2, y0),
    ]


def terminus():
    walls = [(x, 65) for x in range(-168, -162) if x != -165] + [(-168, y) for y in (66, 67, 68)]
    adds = [('WallSolid', x, y) for x, y in walls] + [('AirlockMedicalGlass', -165, 65)]
    # sink (-166, 67) and medical locker (-164, 67) already stand in the corner
    adds += [
        ('DHChemDispenser', -167, 68),
        ('DHChemMaster', -166, 68),
        ('TableReinforced', -165, 68),
        ('TableReinforced', -164, 68),
        ('TableReinforced', -163, 68),
        ('DHChemistryHotplate', -165, 68),
        ('DHElectrolysisUnit', -164, 68),
        ('BoxBeaker', -163, 68),
        ('LargeBeaker', -163, 68),
        ('DHCrateChemistryAcids', -167, 66),
        ('DHCrateChemistryBases', -163, 66),
        ('DHCrateChemistryOrganics', -163, 67),
    ]
    patch('Resources/Maps/_DarkestHour/Assault/terminus.yml', adds)


def carrier():
    room = [(x, y) for x in range(8, 15) for y in range(-28, -21)]
    walls = [(x, y) for x, y in room if (x in (8, 14) or y == -28) and y != -22]
    adds = [('WallPlastitanium', x, y) for x, y in walls] + [('AirlockShuttleSyndicate', 11, -22)]
    adds += lab_kit(9, -27, 13, -23)
    adds += [
        ('TableReinforced', 13, -25),
        ('BoxBeaker', 13, -25),
        ('LargeBeaker', 13, -25),
        ('LockerMedicineFilled', 13, -27),
        ('LightPostSmall', 9, -25),
    ]
    patch('Resources/Maps/_DarkestHour/Shuttles/imperial_carrier.yml', adds, drop_at=[(11, -22)], floor=room)


def medbay_machines():
    """Stage 6 (author's decision 2026-11-11): a ventilator and a life support system next to the medbay beds."""
    # Terminus medbay: beds at (-170, 60/62/66) — machines against the west wall between them
    patch('Resources/Maps/_DarkestHour/Assault/terminus.yml',
          [('DHVentilator', -171, 61), ('DHLifeSupport', -171, 63)], marker='DHLifeSupport')
    # carrier medbay: beds at (2, -11) and (2, -14)
    patch('Resources/Maps/_DarkestHour/Shuttles/imperial_carrier.yml',
          [('DHVentilator', 1, -11), ('DHLifeSupport', 1, -13)], marker='DHLifeSupport')


if __name__ == '__main__':
    terminus()
    carrier()
    medbay_machines()
