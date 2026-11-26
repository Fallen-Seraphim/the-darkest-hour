"""Шаттл ЧВК LA-V для события «Осады» (план п. 11, решение автора 2026-11-26: взять готовый из RMC/CMU).

Берётся PMC-шаттл RMC-14/CMU (`rmc_ert_pmc_shuttle.yml`, корпус We-Yu-LWI «Runner» из cmss13, CC-BY-SA-3.0) из
чекаута RussianCM: нужные спрайты корпуса, двигателей, люков и плитки копируются к нам, детали корпуса и люки
становятся нашими прототипами `DHLAV*`, предметы — нашими аналогами, метки появления — метками отряда LA-V,
пульт навигации — пультом эвакуации LA-V.

Запуск из корня:  python3 Tools/_DarkestHour/lav/make_shuttle.py /path/to/russiancm
Пишет: Resources/Maps/_DarkestHour/Shuttles/lav_shuttle.yml, Resources/Prototypes/_DarkestHour/ThirdParty/
lav_shuttle_parts.yml, Resources/Textures/_DarkestHour/Shuttles/LAV/*."""
import json
import os
import re
import subprocess
import sys

import yaml

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
MAP_SRC = 'Content.CMU/Resources/Maps/CMU14/Dropships/rmc_ert_pmc_shuttle.yml'
MAP_OUT = 'Resources/Maps/_DarkestHour/Shuttles/lav_shuttle.yml'
PROTO_OUT = 'Resources/Prototypes/_DarkestHour/ThirdParty/lav_shuttle_parts.yml'
TEX = 'Resources/Textures/_DarkestHour/Shuttles/LAV'
RMC_TEX = 'Resources/Textures/_RMC14'

# Плитка RMC → наша (копия текстуры).
TILES = {
    'CMShuttleTileRasputin2': 'shuttle/rasputin2.png', 'CMShuttleTileRasputin3': 'shuttle/rasputin3.png',
    'CMShuttleTileRasputin5': 'shuttle/rasputin5.png', 'CMShuttleTileRasputin11': 'shuttle/rasputin11.png',
    'CMShuttleTileRasputin14': 'shuttle/rasputin14.png', 'CMShuttleTileRasputin15': 'shuttle/rasputin15.png',
    'CMFloorCorsatBasicOfficePlate': 'corsat/spiralplate.png',
}
TILE_SWAP = {'CMShuttleTileInvisible': 'DHMidwayTileInvisible', 'CMFloorPlating': 'Plating'}

# Предметы и мебель → наши аналоги; None — убрать.
SWAP = {
    'CMComputerDropshipNavigationERTCLFThirdParty': 'DHLAVShuttleConsole',
    'CMSeatPilot': 'DHLAVSeatPilot', 'RMCSeatDark': 'DHLAVSeat',
    'CMRack': 'CMRack', 'RMCLightFixtureBlueOffset': 'RMCLightFixtureAlwaysPowered',
    'CMFirstAidKitFilled': 'MedkitFilled', 'CMHealthAnalyzer': 'HandheldHealthAnalyzer',
    'RMCToolboxSyndicateFilled': 'ToolboxSyndicateFilled',
    'RMCPMCCrewHatch': 'DHLAVHatch', 'RMCPMCDoorAft': 'DHLAVCargoDoor',
    'RMCERTShuttleEngineLeft01': 'DHLAVEngineLeft', 'RMCERTShuttleEngineRight01': 'DHLAVEngineRight',
    'thirdpartyleaderspawnmarker': 'DHLAVSpawnLeader', 'thirdpartymemberspawnmarker': 'DHLAVSpawnMember',
    'thirdpartyentityspawnmarker': None,
    'CMBeltMedicBagFilled': None, 'RMCMREWeYaPMC': None, 'RMCAlmayerStairs': None,
    'RMCPlatformHybrisaThree': None, 'RMCPlatformHybrisaThreeCornerSmall': None,
    'RMCPlatformHybrisaThreeStair': None, 'RMCPlatformHybrisaThreeStairAlt': None,
}
GRID_KEEP = {'MetaData', 'Transform', 'MapGrid', 'Broadphase', 'Physics', 'Fixtures', 'OccluderTree', 'SpreaderGrid',
             'Shuttle', 'GridPathfinding', 'Gravity', 'DecalGrid', 'GridAtmosphere', 'GasTileOverlay',
             'RadiationGridResistance'}


def git(rucm, path, binary=False):
    out = subprocess.run(['git', '-C', rucm, 'show', 'HEAD:' + path], capture_output=True, check=True).stdout
    return out if binary else out.decode('utf-8-sig')


def copy_rsi(rucm, src, dst, states=None):
    meta = json.loads(git(rucm, src + '/meta.json'))
    if states is not None:
        meta['states'] = [s for s in meta['states'] if s['name'] in states]
        assert len(meta['states']) == len(states), (src, states)
    os.makedirs(dst, exist_ok=True)
    for st in meta['states']:
        open(os.path.join(dst, st['name'] + '.png'), 'wb').write(git(rucm, f"{src}/{st['name']}.png", True))
    meta['copyright'] += '; used for the LA-V shuttle of The Darkest Hour (Tools/_DarkestHour/lav/make_shuttle.py)'
    with open(os.path.join(dst, 'meta.json'), 'w') as f:
        json.dump(meta, f, indent=2)
        f.write('\n')


class L(yaml.SafeLoader):
    pass


L.add_multi_constructor('!', lambda l, s, n: None)


def main():
    rucm = sys.argv[1]
    os.chdir(ROOT)
    src = git(rucm, MAP_SRC)
    data = yaml.load(src, Loader=L)
    weya = {e['id']: e for e in yaml.load(git(rucm, 'Resources/Prototypes/_RMC14/Entities/Structures/Dropships/weya_walls.yml'),
                                          Loader=L)}

    # Детали корпуса: какие состояния weya.rsi нужны.
    hull = {}
    for block in data['entities']:
        proto = block['proto']
        if proto in weya:
            e = weya[proto]
            comps = {c['type']: c for c in e.get('components', [])}
            state = str(comps['Sprite']['state'])
            hull[proto] = (state, e.get('name'), 'Occluder' in comps)
    copy_rsi(rucm, RMC_TEX.replace('Resources/Textures', 'Resources/Textures') + '/Structures/Dropships/weya.rsi',
             TEX + '/hull.rsi', sorted({s for s, _, _ in hull.values()}))
    copy_rsi(rucm, RMC_TEX + '/Structures/Dropships/ert_shuttle.rsi', TEX + '/engines.rsi',
             ['left_engine_01', 'right_engine_01'])
    copy_rsi(rucm, RMC_TEX + '/Structures/Dropships/Airlocks/pmc_airlock.rsi', TEX + '/hatch.rsi')
    copy_rsi(rucm, RMC_TEX + '/Structures/Dropships/Airlocks/pmc_cargo.rsi', TEX + '/cargo_door.rsi')
    os.makedirs(TEX + '/tiles', exist_ok=True)
    for tile, png in TILES.items():
        open(f'{TEX}/tiles/{os.path.basename(png)}', 'wb').write(git(rucm, f'{RMC_TEX}/Tiles/{png}', True))

    # Прототипы деталей корпуса и плитки.
    lines = ['# Сгенерировано Tools/_DarkestHour/lav/make_shuttle.py — детали корпуса и плитка шаттла LA-V (We-Yu-LWI',
             '# «Runner» из cmss13 через RMC-14, CC-BY-SA-3.0). Не править руками.', '']
    for proto, (state, name, transparent) in sorted(hull.items()):
        new = 'DHLAVHull' + re.sub(r'\W', '', proto.replace('RMCWeya', ''))
        SWAP[proto] = new
        lines += ['- type: entity', '  parent: DHLAVHullBase', f'  id: {new}']
        if name:
            lines.append(f'  name: hull {name}')
        lines += ['  components:', '  - type: Sprite', f'    state: "{state}"', '  - type: Icon', f'    state: "{state}"']
        if transparent:
            lines += ['  - type: Occluder', '    enabled: false']
        lines.append('')
    for tile, png in TILES.items():
        new = 'DHLAVTile' + os.path.splitext(os.path.basename(png))[0].capitalize()
        TILE_SWAP[tile] = new
        lines += ['- type: tile', '  parent: DHMidwayTileBase', f'  id: {new}', '  name: tiles-dh-lav-floor',
                  f'  sprite: /Textures/_DarkestHour/Shuttles/LAV/tiles/{os.path.basename(png)}', '']
    with open(PROTO_OUT, 'w') as f:
        f.write('\n'.join(lines).rstrip('\n') + '\n')

    # Карта.
    head, body = src.split('\nentities:\n', 1)
    for old, new in TILE_SWAP.items():
        head = re.sub(rf'(?m)^(  \d+: ){old}$', rf'\g<1>{new}', head)
    missing = set(re.findall(r'(?m)^  \d+: (\w+)$', head)) - set(TILE_SWAP.values()) - {'Space'}
    assert not missing, missing
    blocks = re.split(r'(?m)^(?=- proto: )', body)
    out = []
    count = 0
    for b in blocks:
        if not b.strip():
            continue
        proto = re.match(r'- proto: (.*)\n', b).group(1).strip().strip('"\'')
        if b.rstrip().endswith('...'):
            b = b.rstrip()[:-3].rstrip() + '\n'
        if proto == '':
            res, skip = [], False
            for ln in b.split('\n'):
                m = re.match(r'    - type: (\w+)', ln)
                if m:
                    skip = m.group(1) not in GRID_KEEP
                if not skip:
                    res.append(ln)
            b = '\n'.join(res)
            b = re.sub(r'name: .*', 'name: LA-V «Vanguard Runner»', b, count=1)
            out.append(b)
            count += 1
            continue
        assert proto in SWAP, proto
        new = SWAP[proto]
        if new is None:
            continue
        b = b.replace(f'- proto: {proto}\n', f'- proto: {new}\n', 1)
        count += len(re.findall(r'(?m)^  - uid: ', b))
        out.append(b)
    res = head + '\nentities:\n' + ''.join(x if x.endswith('\n') else x + '\n' for x in out) + '...\n'
    res = re.sub(r'entityCount: \d+', f'entityCount: {count}', res)
    with open(MAP_OUT, 'w') as f:
        f.write(res)
    print('lav shuttle:', count, 'entities,', len(hull), 'hull parts')


if __name__ == '__main__':
    main()
