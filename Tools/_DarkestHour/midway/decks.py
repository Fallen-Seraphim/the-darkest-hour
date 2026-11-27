#!/usr/bin/env python3
"""
Палубы «Мидуэя» (решение автора 2026-11-27, как в CMU-14): кабина стоит на стойках на уровень выше земли, под ней —
нижний ярус (уровень земли) с рампой, стойками шасси и брюхом корпуса, над ней — ходовая крыша.

Из RuCM (CMU-14 `Content.CMU/Resources/Maps/CMU14/Dropships/Mohawk/midway_lower.yml`, `midway_upper.yml`):
- midway_underside.yml — рампа (в опущенном виде), переборки рампы, пульт рампы снаружи, стойки шасси и их люки,
  брюхо корпуса; тайлы невидимые — только под рампой и под стойками (чтобы на нижнем ярусе не «залипали» прохожие);
- midway_roof.yml — невидимые тайлы по крыше и одна картинка крыши, собранная из 274 тайлов CMU (roof.rsi);
- в кабину (midway.yml) добавляются лестница на крышу и верхний край рампы (переход вниз).

Запуск из корня репозитория (нужен разреженный чекаут RuCM с текстурами Mohawk):
  python3 Tools/_DarkestHour/midway/decks.py <путь к RuCM> <папка с midway_lower.yml и midway_upper.yml из RuCM>
"""
import base64
import json
import math
import os
import shutil
import struct
import sys

import yaml
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
RUCM = sys.argv[1]
SRC = sys.argv[2]
TEX = RUCM + '/Content.CMU/Resources/Textures/CMU14/Dropships/Mohawk'
OUT_TEX = ROOT + '/Resources/Textures/_CMU14/Dropships/Mohawk'
MAPS = ROOT + '/Resources/Maps/_DarkestHour/Shuttles'


class Loader(yaml.SafeLoader):
    pass


Loader.add_multi_constructor('!', lambda loader, suffix, node: None)


def load(path):
    return yaml.load(open(path, encoding='utf-8'), Loader=Loader)


def grid_tiles(doc):
    tm = doc['tilemap']
    grid = [e for p in doc['entities'] for e in p['entities'] if e['uid'] == 1][0]
    tiles = {}
    for c in grid['components']:
        if c['type'] != 'MapGrid':
            continue
        for ch in c['chunks'].values():
            cx, cy = map(int, ch['ind'].split(','))
            b = base64.b64decode(ch['tiles'])
            n = len(b) // 256
            for i in range(256):
                t = struct.unpack_from('<i', b, i * n)[0]
                if t:
                    tiles[(cx * 16 + i % 16, cy * 16 + i // 16)] = tm[t]
    return tiles


def placements(doc):
    out = []
    for p in doc['entities']:
        for e in p['entities']:
            for c in e['components']:
                if c['type'] == 'Transform' and c.get('parent') == 1 and 'pos' in c:
                    x, y = map(float, c['pos'].split(','))
                    rot = float(str(c.get('rot', '0 rad')).split()[0])
                    out.append((p['proto'], x, y, rot))
    return out


def chunks(cells):
    """cells: {(x, y): 1} → [(cx, cy, base64)] с тайлом №1 (DHMidwayTileInvisible)."""
    by = {}
    for (x, y), t in cells.items():
        by.setdefault((x // 16, y // 16), {})[(x % 16, y % 16)] = t
    res = []
    for (cx, cy), c in sorted(by.items()):
        data = bytearray()
        for i in range(256):
            data += struct.pack('<iBBB', c.get((i % 16, i // 16), 0), 0, 0, 0)
        res.append((cx, cy, base64.b64encode(bytes(data)).decode()))
    return res


def write_grid(path, name, cells, ents):
    L = ['meta:', '  format: 7', '  category: Grid', '  engineVersion: 264.0.0', "  forkId: ''", "  forkVersion: ''",
         "  time: '2026-11-27T00:00:00'", '  entityCount: ENTITY_COUNT', 'maps: []', 'grids:', '- 1', 'orphans:', '- 1',
         'nullspace: []', 'tilemap:', '  0: Space', '  1: DHMidwayTileInvisible', 'entities:', "- proto: ''",
         '  entities:', '  - uid: 1', '    components:', '    - type: MetaData', f'      name: {name}',
         '    - type: Transform', '      parent: invalid', '    - type: MapGrid', '      canSplit: false',
         '      chunks:']
    for cx, cy, data in chunks(cells):
        L += [f'        {cx},{cy}:', f'          ind: {cx},{cy}', f'          tiles: {data}', '          version: 7']
    L += ['    - type: Broadphase', '    - type: Physics', '      bodyType: Dynamic', '      fixedRotation: false',
          '    - type: Fixtures', '      fixtures: {}']
    groups = {}
    for proto, x, y, rot in ents:
        groups.setdefault(proto, []).append((x, y, rot))
    uid = 2
    for proto in sorted(groups):
        L += [f'- proto: {proto}', '  entities:']
        for x, y, rot in groups[proto]:
            L += [f'  - uid: {uid}', '    components:', '    - type: Transform']
            if rot:
                L.append(f'      rot: {rot!r} rad')
            L += [f'      pos: {x},{y}', '      parent: 1']
            uid += 1
    L.append('...')
    open(path, 'w', encoding='utf-8', newline='\n').write('\n'.join(L).replace('ENTITY_COUNT', str(uid - 1)) + '\n')
    print(os.path.relpath(path, ROOT), 'тайлов', len(cells), 'сущностей', uid - 1)


def copy_rsi(src, dst, states):
    os.makedirs(dst, exist_ok=True)
    meta = json.load(open(src + '/meta.json'))
    meta['states'] = [s for s in meta['states'] if s['name'] in states]
    meta['copyright'] = meta.get('copyright', '') + ' Via RussianCM (CMU-14).'
    json.dump(meta, open(dst + '/meta.json', 'w'), indent=4, ensure_ascii=False)
    for s in states:
        shutil.copy(f'{src}/{s}.png', f'{dst}/{s}.png')


# ------------------------------------------------------------------ нижний ярус

LOWER_MAP = {
    'CMUMohawkMidwayMidway2ceeae3579': 'DHMidwayLandingGearFront',
    'CMUMohawkMidwayMidway86eff9dfe7': 'DHMidwayLandingGearRear',
    'CMUMohawkMidwayMidway2135bc3fe4': 'DHMidwayGearHatchFront',
    'CMUMohawkMidwayMidway4dc70b978c': 'DHMidwayGearHatchRear',
    'CMUMohawkMidwayMidway621f2a9650': 'DHMidwayBelly',
    'CMUMohawkMidwayRampBulkhead': 'DHMidwayRampBulkhead',
    'CMUMohawkMidwayMidwayAft5b1b2c6551': 'DHMidwayRampControlOutside',
}
for n in range(4, 13):
    LOWER_MAP[f'CMUMohawkMidwayLoweredRamp{n}'] = f'DHMidwayRampLowered{n}'

lower = load(SRC + '/midway_lower.yml')
under_ents, under_cells = [], {}
for proto, x, y, rot in placements(lower):
    if proto not in LOWER_MAP:
        continue
    new = LOWER_MAP[proto]
    # Оригинальные повороты стоек/люков заданы в прототипах CMU — переносим в расстановку.
    if proto in ('CMUMohawkMidwayMidway2ceeae3579', 'CMUMohawkMidwayMidway2135bc3fe4'):
        rot = math.pi
    under_ents.append((new, x, y, rot))
    if new.startswith(('DHMidwayRamp', 'DHMidwayLandingGear')):
        under_cells[(math.floor(x), math.floor(y))] = 1
# Верхний край рампы (переход наверх) — ряд опущенной рампы под переборками.
for x in (-0.5, 0.5, 1.5):
    under_ents.append(('DHMidwayRampEntryUp', x, -3.5, 0.0))
write_grid(MAPS + '/midway_underside.yml', 'Midway underside', under_cells, under_ents)

copy_rsi(TEX + '/obj/structures/machinery/midway/misc_96x96.rsi',
         OUT_TEX + '/obj/structures/machinery/midway/misc_96x96.rsi', ['landing_gear', 'gear_hatch'])
copy_rsi(TEX + '/turf/midway/belly.rsi', OUT_TEX + '/turf/midway/belly.rsi', ['belly'])

# ------------------------------------------------------------------ крыша

upper = load(SRC + '/midway_upper.yml')
roof = grid_tiles(upper)
xs = [x for x, _ in roof]
ys = [y for _, y in roof]
x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
W, H = (x1 - x0 + 1) * 32, (y1 - y0 + 1) * 32
img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
for (x, y), tile in roof.items():
    im = Image.open(f'{TEX}/tiles/midway_roof/{tile}.png').convert('RGBA').crop((0, 0, 32, 32))
    img.alpha_composite(im, ((x - x0) * 32, (y1 - y) * 32))
rsi = OUT_TEX + '/turf/midway/roof.rsi'
os.makedirs(rsi, exist_ok=True)
img.save(rsi + '/roof.png')
json.dump({'version': 1, 'license': 'CC-BY-SA-3.0',
           'copyright': 'CM-SS13 contributors: thwomper, PoltavskaPraca, Steelpoint (cmss13-devs/cmss13#12866). '
                        'Midway roof tiles from CMU-14 via RussianCM, assembled into one image for The Darkest Hour.',
           'size': {'x': W, 'y': H}, 'states': [{'name': 'roof'}]},
          open(rsi + '/meta.json', 'w'), indent=4)
cx, cy = (x0 + x1 + 1) / 2, (y0 + y1 + 1) / 2
roof_ents = [('DHMidwayRoofArt', cx, cy, 0.0), ('DHMidwayRoofLadderTop', -1.5, -8.5, 0.0)]
assert (-2, -9) in roof, 'под лестницей на крыше нет тайла'
write_grid(MAPS + '/midway_roof.yml', 'Midway roof', {k: 1 for k in roof}, roof_ents)
print('крыша', W, 'x', H, 'центр', cx, cy)

# ------------------------------------------------------------------ кабина

cabin_path = MAPS + '/midway.yml'
text = open(cabin_path, encoding='utf-8').read()
if 'DHMidwayRampEntryDown' not in text:
    uids = [int(l.split(':')[1]) for l in text.splitlines() if l.strip().startswith('- uid:')]
    uid = max(uids) + 1
    add = ['- proto: DHMidwayRampEntryDown', '  entities:']
    for x in (-0.5, 0.5, 1.5):
        add += [f'  - uid: {uid}', '    components:', '    - type: Transform', '      parent: 1', f'      pos: {x},-4.5']
        uid += 1
    add += ['- proto: DHMidwayRoofLadderBottom', '  entities', ]
    add[-1] = '  entities:'
    add += [f'  - uid: {uid}', '    components:', '    - type: Transform', '      parent: 1', '      pos: -1.5,-8.5']
    body = text.rstrip('\n')
    assert body.endswith('...')
    body = body[:-3].rstrip('\n') + '\n' + '\n'.join(add) + '\n...\n'
    import re
    count = int(re.search(r'entityCount: (\d+)', body).group(1)) + 4
    body = re.sub(r'entityCount: \d+', f'entityCount: {count}', body, count=1)
    open(cabin_path, 'w', encoding='utf-8', newline='\n').write(body)
    print('кабина: +3 края рампы, +лестница на крышу')
