#!/usr/bin/env python3
# DarkestHour: перенос готовых отсеков техники из RussianCM (RMC-14 + CMU) — план осады п. 5, решение автора 2026-11-19.
#
# Что делает:
#   1. берёт карты отсеков танка, БТР, хамви (RMC-14) и грузовика (CMU) из клона RussianCM;
#   2. переносит прототипы их деталей (стены, борта, колёса, крыши, люки, приборы) в наши прототипы DHInterior*:
#      родители раскрываются, остаются только компоненты, которые есть у нас (Sprite, Physics, Fixtures, Occluder…);
#      люкам, заряжателям и смотровым приборам добавляются наши компоненты;
#   3. кресла RMC меняются на наши кресла-посты (DHInteriorSeat*: сел — управляешь), радиостанция — на тактическую
#      карту, свет — на наш; камеры, наномед, шкафчики, телефоны и автомат БТР убираются;
#   4. у грузовика к кабине CMU пристраивается кузов с двумя лавками на 8 мест (решение автора 2026-11-19);
#   5. пишет гриды отсеков в Resources/Maps/_DarkestHour/Vehicles/Interiors/, прототипы деталей и плитки — в
#      Resources/Prototypes/_DarkestHour/Vehicles/, русские названия — в ss14-ru.
#
# Запуск из корня репозитория: python3 Tools/_DarkestHour/vehicles/port_interiors.py [путь к клону RussianCM]
# Повторный запуск воспроизводит файлы. Спрайты и плитки — cmss13 через RMC-14/RussianCM (CC-BY-SA-3.0), лежат в
# Resources/Textures/_RMC14/... (переносятся отдельно, см. meta.json и attributions.yml).

import base64
import math
import os
import struct
import sys

import yaml

SRC = sys.argv[1] if len(sys.argv) > 1 else '/home/claude/flex5hybrid/russiancm'
OUT_MAPS = 'Resources/Maps/_DarkestHour/Vehicles/Interiors'
OUT_PROTO = 'Resources/Prototypes/_DarkestHour/Vehicles/interior_parts_rmc.yml'
OUT_TILES = 'Resources/Prototypes/_DarkestHour/Vehicles/interior_tiles.yml'
OUT_FTL = 'Resources/Locale/ru-RU/ss14-ru/prototypes/_DarkestHour/Vehicles/interior_parts_rmc.ftl'

MAPS = {
    'tank': 'Resources/Maps/_RMC14/Vehicles/tank_interior.yml',
    'apc': 'Resources/Maps/_RMC14/Vehicles/apc_transport_interior.yml',
    'humvee': 'Resources/Maps/_RMC14/Vehicles/humvee_interior.yml',
    'truck': 'Content.CMU/Resources/Maps/CMU14/Vehicles/Interiors/truck_interior.yml',
}

PROTO_DIRS = ['Resources/Prototypes/_RMC14/Vehicles', 'Content.CMU/Resources/Prototypes/CMU14']
TILE_FILE = 'Resources/Prototypes/_RMC14/Tiles/vehicle_interior.yml'

# Кресла и прочее рабочее — на наши прототипы (interior_parts.yml, написаны руками).
SEATS = {
    'RMCSeatTankDriver': 'DHInteriorSeatDriver',
    'RMCSeatArmoredDriver': 'DHInteriorSeatDriver',
    'RMCSeatHumveeDriver': 'DHInteriorSeatDriver',
    'RMCSeatTankGunner': 'DHInteriorSeatGunner',
    'RMCSeatArmoredWeaponsOperator': 'DHInteriorSeatGunner',
    'RMCSeatHumveeWeaponsOperator': 'DHInteriorSeatGunner',
    'RMCSeatVehiclePassenger': 'DHInteriorSeatPassenger',
    'RMCSeatHumveePassenger': 'DHInteriorSeatPassenger',
    'CMChairComfy': 'DHInteriorSeatPassengerComfy',
    'AU14VehicleRadioSet': 'DHInteriorTacticalMap',
    'RMCLightFixtureSmallBlueAlwaysPoweredOffset': 'DHVehicleInteriorLight',
    'RMCWallInvisibleBulletBlocker': 'DHInteriorInvisibleWall',
    'RMCWallInvisibleBulletLightBlocker': 'DHInteriorInvisibleWall',
    'VehicleInteriorExtinguisher': 'ExtinguisherCabinetFilled',
}
DROP = {'VehicleInteriorCamera', 'VehicleInteriorNanomed', 'VehicleInteriorLocker', 'VehicleTankLocker',
        'VehicleInteriorWallPhone', 'RMCRotaryPhoneWallmount', 'VehicleAPCSupplyVendor'}

# Деталям RMC с рабочей ролью — наши компоненты поверх спрайта.
CLICK = [{'type': 'Clickable'}, {'type': 'InteractionOutline'}]
EXTRA = {
    'VehicleTankAmmoLoader': CLICK + [{'type': 'DHVehicleAmmoRack'}],
    'VehicleAmmoLoader': CLICK + [{'type': 'DHVehicleAmmoRack'}],
    'VehicleHumveeAmmoLoader': CLICK + [{'type': 'DHVehicleAmmoRack'}],
    'VehicleTankExitHatch': CLICK + [{'type': 'DHVehicleHatch'}],
    'VehicleAPCExteriorDoor': CLICK + [{'type': 'DHVehicleHatch'}],
    'VehicleHumveeExitDoor': CLICK + [{'type': 'DHVehicleHatch'}],
    'VehicleHumveeExitDoorDirectional': CLICK + [{'type': 'DHVehicleHatch'}],
    'VehicleVanExteriorDoor': CLICK + [{'type': 'DHVehicleHatch'}],
    'VehicleViewport': CLICK + [{'type': 'DHVehiclePeriscope'}],
    'VehicleViewportUnanchored': CLICK + [{'type': 'DHVehiclePeriscope'}],
    'VehicleHumveeViewportDoor': CLICK + [{'type': 'DHVehiclePeriscope'}],
    'VehicleHumveeViewportFrontNorth': CLICK + [{'type': 'DHVehiclePeriscope'}],
}

# Компоненты RMC, которые переносим как есть (остальные — системы RMC, у нас их нет).
KEEP = {'Transform', 'Sprite', 'Physics', 'Fixtures', 'Occluder', 'Clickable', 'InteractionOutline', 'Tag', 'WallMount',
        'Airtight'}

# Тайлы: пустота RMC — чёрный пол (у хамви весь пол — «пустота» под спрайтом шасси).
VOID = {'RMCVoid': 'DHFloorVehicleInteriorVoid'}

RU_NAMES = {
    'tank interior piece': 'деталь отсека танка',
    'tank interior roof': 'крыша отсека танка',
    'APC interior piece': 'деталь отсека БТР',
    'APC interior roof': 'крыша отсека БТР',
    'van interior piece': 'деталь кабины грузовика',
    'van interior roof': 'крыша кабины грузовика',
    'humvee interior piece': 'деталь отсека хамви',
    'vehicle interior piece': 'деталь отсека',
}


class Loader(yaml.SafeLoader):
    pass


class Tagged(dict):
    """Значение с тегом YAML (!type:PhysShapeAabb) — пишется обратно с тем же тегом."""

    def __init__(self, tag, value):
        super().__init__(value)
        self.tag = tag


def _tagged(loader, suffix, node):
    value = loader.construct_mapping(node, deep=True) if isinstance(node, yaml.MappingNode) else {}
    return Tagged('!' + suffix, value)


Loader.add_multi_constructor('!', _tagged)


class Dumper(yaml.SafeDumper):
    pass


def _represent_tagged(dumper, data):
    return dumper.represent_mapping(data.tag, dict(data))


Dumper.add_representer(Tagged, _represent_tagged)


def _indent_lists(dumper):
    def increase_indent(flow=False, indentless=False):
        return yaml.SafeDumper.increase_indent(dumper, flow, False)
    return increase_indent


def dump(data):
    class D(Dumper):
        def increase_indent(self, flow=False, indentless=False):
            return super().increase_indent(flow, False)
    return yaml.dump(data, Dumper=D, allow_unicode=True, sort_keys=False, default_flow_style=False, width=120)


def load(path):
    with open(path, encoding='utf-8-sig') as f:
        return yaml.load(f, Loader=Loader)


# --- индекс прототипов RMC/CMU ---
INDEX = {}
for base in PROTO_DIRS:
    for root, _, files in os.walk(os.path.join(SRC, base)):
        for name in files:
            if not name.endswith('.yml'):
                continue
            try:
                docs = load(os.path.join(root, name))
            except yaml.YAMLError:
                continue
            for doc in docs or []:
                if isinstance(doc, dict) and doc.get('type') == 'entity' and isinstance(doc.get('id'), str):
                    INDEX[doc['id']] = doc


def flatten(proto_id):
    """Компоненты прототипа с раскрытыми родителями (поля дочернего перекрывают поля родителя)."""
    doc = INDEX[proto_id]
    comps = {}
    meta = {'name': None, 'description': None}
    parents = doc.get('parent') or []
    if isinstance(parents, str):
        parents = [parents]
    for parent in parents:
        if parent not in INDEX:
            continue
        pcomps, pmeta = flatten(parent)
        for comp in pcomps:
            comps[comp['type']] = {**comps.get(comp['type'], {}), **comp}
        for key in meta:
            meta[key] = pmeta[key] or meta[key]
    for comp in doc.get('components') or []:
        if comp['type'] == 'RemoveComponents':
            for removed in comp.get('components') or []:
                comps.pop(removed['type'], None)
            continue
        comps[comp['type']] = {**comps.get(comp['type'], {}), **comp}
    for key in meta:
        meta[key] = doc.get(key) or meta[key]
    return list(comps.values()), meta


def dh_id(rmc_id):
    name = rmc_id
    for prefix in ('Vehicle', 'Interior'):
        if name.startswith(prefix):
            name = name[len(prefix):]
    return 'DHInterior' + name


def port_proto(rmc_id):
    comps, meta = flatten(rmc_id)
    out = []
    for comp in comps:
        if comp['type'] not in KEEP:
            continue
        comp = dict(comp)
        if comp['type'] == 'Tag':
            comp['tags'] = [t for t in comp.get('tags', []) if t == 'HideContextMenu']
            if not comp['tags']:
                continue
        roof = any(c['type'] == 'RMCSpriteFade' for c in comps)
        if roof and comp['type'] in ('Clickable', 'InteractionOutline'):
            continue    # крыша не должна перехватывать клики внутри отсека
        if comp['type'] == 'Sprite' and not os.path.exists(os.path.join('Resources/Textures', comp.get('sprite', ''))):
            continue    # метки RMC (landmarks.rsi) и прочее, чего у нас нет, — без спрайта
        if comp['type'] == 'Sprite' and roof:
            # Крыши RMC гаснут, когда смотришь изнутри (RMCSpriteFade); у нас — всегда полупрозрачные.
            comp['color'] = '#ffffff59'
        out.append(comp)
    present = {c['type'] for c in out}
    for comp in EXTRA.get(rmc_id, []):
        if comp['type'] not in present:
            out.append(dict(comp))
    entity = {
        'type': 'entity',
        'id': dh_id(rmc_id),
        'name': meta['name'] or 'vehicle interior piece',
        'description': meta['description'] or 'A part of the vehicle interior.',
        'categories': ['HideSpawnMenu'],
        'placement': {'mode': 'SnapgridCenter'},
        'components': out,
    }
    return entity


# --- карты ---
def read_tiles(grid):
    tiles = {}
    for comp in grid['components']:
        if comp['type'] != 'MapGrid':
            continue
        for key, chunk in comp['chunks'].items():
            cx, cy = map(int, key.split(','))
            raw = base64.b64decode(chunk['tiles'])
            for i in range(256):
                tid, flags, variant = struct.unpack_from('<iBB', raw, i * 7)
                rot = raw[i * 7 + 6]
                tiles[(cx * 16 + i % 16, cy * 16 + i // 16)] = (tid, flags, variant, rot)
    return tiles


def write_tiles(tiles):
    chunks = {}
    for (x, y), value in tiles.items():
        chunks.setdefault((math.floor(x / 16), math.floor(y / 16)), {})[(x % 16, y % 16)] = value
    out = {}
    for (cx, cy), cells in sorted(chunks.items()):
        raw = bytearray(256 * 7)
        for (lx, ly), (tid, flags, variant, rot) in cells.items():
            struct.pack_into('<iBBB', raw, (ly * 16 + lx) * 7, tid, flags, variant, rot)
        out[f'{cx},{cy}'] = {'ind': f'{cx},{cy}', 'tiles': base64.b64encode(bytes(raw)).decode(), 'version': 7}
    return out


def parse_pos(text):
    x, y = text.split(',')
    return float(x), float(y)


def build(kind, path, used_protos, used_tiles):
    data = load(os.path.join(SRC, path))
    tilemap = data['tilemap']
    groups = data['entities']
    grid_uid = data['grids'][0]

    grid = None
    placed = []          # (proto, x, y, rot, extra components)
    for group in groups:
        for ent in group['entities']:
            if ent['uid'] == grid_uid:
                grid = ent
                continue
            proto = group['proto']
            if not proto:
                continue
            xform = next(c for c in ent['components'] if c['type'] == 'Transform')
            if xform.get('parent') != grid_uid or proto in DROP:
                continue
            x, y = parse_pos(xform.get('pos', '0,0'))
            rot = xform.get('rot')
            types = {c['type'] for c in ent['components']}
            placed.append([proto, x, y, rot, types])

    # тайлы: пустоту RMC и Space — в 0 (пусто), остальное — наши имена
    tiles = {}
    names = {}
    for pos, (tid, flags, variant, rot) in read_tiles(grid).items():
        name = tilemap.get(tid, 'Space')
        if name == 'Space':
            continue
        name = VOID.get(name, name)
        tiles[pos] = name
        names[name] = None

    if kind == 'truck':
        extend_truck(tiles, placed)

    # итоговая таблица тайлов
    tile_ids = {'Space': 0}
    for name in sorted({n for n in tiles.values()}):
        tile_ids[name] = len(tile_ids)
        used_tiles.add(name)
    packed = {pos: (tile_ids[name], 0, 0, 0) for pos, name in tiles.items()}

    # сущности
    by_proto = {}
    for proto, x, y, rot, types in placed:
        target = SEATS.get(proto)
        if kind == 'apc' and proto == 'RMCSeatArmoredPortGunOperator':
            # Нос отсека — на восток: северный борт — левый.
            target = 'DHInteriorSeatPortLeft' if y > 0.5 else 'DHInteriorSeatPortRight'
        if proto == 'CMChairComfy' and 'VehicleDriverSeat' in types:
            target = 'DHInteriorSeatDriverComfy'
        if target is None:
            if proto not in INDEX:
                if not proto.startswith('DH'):
                    print(f'{kind}: нет прототипа {proto} — пропущен')
                    continue
                target = proto
            else:
                used_protos.add(proto)
                target = dh_id(proto)
        extra = []
        # отдельная стена кабины CMU с выходом (VehicleExit на экземпляре) — у нас это проём в кузов
        by_proto.setdefault(target, []).append((x, y, rot, extra))

    uid = 1
    grid_entity = {
        'uid': uid,
        'components': [
            {'type': 'MetaData', 'name': f'{kind} interior'},
            {'type': 'Transform', 'parent': 'invalid'},
            {'type': 'MapGrid', 'chunks': write_tiles(packed)},
            {'type': 'Broadphase'},
            {'type': 'Physics', 'bodyStatus': 'InAir', 'angularDamping': 0.05, 'linearDamping': 0.05,
             'fixedRotation': False, 'bodyType': 'Dynamic'},
            {'type': 'Fixtures', 'fixtures': {}},
            {'type': 'OccluderTree'},
        ],
    }
    out_groups = [{'proto': '', 'entities': [grid_entity]}]
    count = 1
    for proto in sorted(by_proto):
        ents = []
        for x, y, rot, extra in by_proto[proto]:
            uid += 1
            xform = {'type': 'Transform', 'pos': f'{x:g},{y:g}', 'parent': 1}
            if rot is not None:
                xform['rot'] = rot
            ents.append({'uid': uid, 'components': [xform] + extra})
        out_groups.append({'proto': proto, 'entities': ents})
        count += len(ents)

    out = {
        'meta': {'format': 7, 'category': 'Grid', 'engineVersion': '289.0.0', 'forkId': '', 'forkVersion': '',
                 'time': '11/19/2026 12:00:00', 'entityCount': count},
        'maps': [],
        'grids': [1],
        'orphans': [1],
        'nullspace': [],
        'tilemap': {v: k for k, v in tile_ids.items()},
        'entities': out_groups,
    }
    os.makedirs(OUT_MAPS, exist_ok=True)
    with open(os.path.join(OUT_MAPS, f'{kind}.yml'), 'w', encoding='utf-8') as f:
        f.write(dump(out))
    print(f'{kind}: {count} сущностей, {len(tiles)} тайлов')


def extend_truck(tiles, placed):
    """
    Кузов за кабиной CMU (решение автора 2026-11-19): кабина — x 2..4, задняя стенка — x = 2 (два выхода). Задняя
    стенка кабины становится проёмом, за ней — крытый кузов 5 × 4 с лавками вдоль бортов и люком в корме.
    """
    floor = 'CMFloorPlate'
    # проём: убираем задние стенки кабины с выходом
    placed[:] = [p for p in placed if not (p[0] in ('VehicleVanBack1', 'VehicleVanBack2') and abs(p[1] - 2.5) < 0.01)]
    for x in range(-3, 3):
        for y in range(-3, 1):
            tiles[(x, y)] = floor
    for x in range(-4, 3):
        for y in (-4, 1):
            tiles[(x, y)] = floor
    for y in range(-4, 2):
        tiles[(-4, y)] = floor
    # борта и корма — стенки кузова той же кабины
    for x in range(-4, 2):
        placed.append(['VehicleVanBack2', x + 0.5, 1.5, None, set()])
        placed.append(['VehicleVanBack2', x + 0.5, -3.5, None, set()])
    placed.append(['VehicleVanBack2', 2.5, 1.5, None, set()])
    placed.append(['VehicleVanBack2', 2.5, -3.5, None, set()])
    for y in (-3, 0):
        placed.append(['VehicleVanBack2', -3.5, y + 0.5, None, set()])
    placed.append(['VehicleVanExteriorDoor', -3.5, -0.5, None, set()])
    placed.append(['VehicleVanExteriorDoor', -3.5, -1.5, None, set()])
    # лавки: 4 у левого борта (смотрят на юг), 4 у правого (на север)
    for x in range(-2, 2):
        placed.append(['RMCSeatVehiclePassenger', x + 0.5, 0.5, None, set()])
        placed.append(['RMCSeatVehiclePassenger', x + 0.5, -2.5, 3.141592653589793, set()])
    placed.append(['RMCLightFixtureSmallBlueAlwaysPoweredOffset', -0.5, -0.5, None, set()])


def port_tiles(used):
    docs = load(os.path.join(SRC, TILE_FILE))
    out = ['# DarkestHour: плитка отсеков техники из RMC-14 (через RussianCM; CC-BY-SA-3.0, cmss13). Генерирует',
           '# Tools/_DarkestHour/vehicles/port_interiors.py — не править руками.', '',
           dump([{'type': 'tile', 'abstract': True, 'parent': 'CMTileBase', 'id': 'DHFloorVehicleInteriorBase',
                  'name': 'tiles-dh-vehicle-interior-floor', 'deconstructTools': []}]),
           dump([{'type': 'tile', 'parent': 'DHFloorVehicleInteriorBase', 'id': 'DHFloorVehicleInteriorVoid',
                  'sprite': '/Textures/_RMC14/Tiles/vehicle_interior/void.png'}])]
    for doc in docs:
        if doc.get('type') != 'tile' or doc['id'] not in used:
            continue
        sprite = doc.get('sprite', '').replace('/Textures/Tiles/vehicle_interior/',
                                               '/Textures/_RMC14/Tiles/vehicle_interior/')
        out.append(dump([{'type': 'tile', 'parent': 'DHFloorVehicleInteriorBase', 'id': doc['id'],
                          'sprite': sprite}]))
    with open(OUT_TILES, 'w', encoding='utf-8') as f:
        f.write('\n'.join(out))


def main():
    used_protos = set()
    used_tiles = set()
    for kind, path in MAPS.items():
        build(kind, path, used_protos, used_tiles)

    entities = [port_proto(p) for p in sorted(used_protos)]
    header = ('# DarkestHour: детали отсеков техники из RMC-14 / CMU (через RussianCM) — план осады п. 5.\n'
              '# Генерирует Tools/_DarkestHour/vehicles/port_interiors.py — не править руками. Спрайты — cmss13 через\n'
              '# RMC-14 (CC-BY-SA-3.0). Рабочие кресла, свет и прочее — interior_parts.yml.\n\n')
    with open(OUT_PROTO, 'w', encoding='utf-8') as f:
        f.write(header + dump(entities))

    ftl = []
    for ent in entities:
        name = RU_NAMES.get(ent['name'], ent['name'])
        if 'hatch' in ent['name'].lower() or 'door' in ent['id'].lower():
            name = 'люк'
        if 'viewport' in ent['id'].lower():
            name = 'смотровой прибор'
        if 'ammoloader' in ent['id'].lower():
            name = 'механизм заряжания'
        ftl.append(f'ent-{ent["id"]} = {name}\n    .desc = {{ "" }}')
    os.makedirs(os.path.dirname(OUT_FTL), exist_ok=True)
    with open(OUT_FTL, 'w', encoding='utf-8') as f:
        f.write('\n'.join(ftl) + '\n')

    port_tiles(used_tiles)
    print(f'прототипов деталей: {len(entities)}, плиток: {len(used_tiles)}')


if __name__ == '__main__':
    main()
