"""Remaps the nuclear operative base and shuttle for the Imperial Raid: renames them, turns the blood-red decals
imperial white, adds an armour maintenance rack (heavy IMB-1 modules are swapped only next to it) and, on the standalone IL-7
Gladius, spawn points for marines made by an admin.

Run from the repository root:  python3 Tools/_DarkestHour/imperial_raid/remap.py   (safe to run again)"""
import colorsys
import re

FILES = {
    'Resources/Maps/Shuttles/infiltrator.yml': {
        'names': [('GX-13 Infiltrator', 'IL-7 Gladius')],
        'racks': [(1, '-4.5,-16.5')],
        # точки появления морпехов, назначенных админом (правило DHImperialRaidSpawn грузит только этот шаттл)
        'raid_shuttle': True,
        'spawns': [(1, '-2.5,-16.5'), (1, '-0.5,-16.5'), (1, '1.5,-16.5'), (1, '-1.5,-17.5'), (1, '0.5,-17.5')],
    },
    'Resources/Maps/Nonstations/nukieplanet.yml': {
        'names': [('Syndicate Outpost', 'Imperial Forward Base'), ('GX-13 Infiltrator', 'IL-7 Gladius')],
        'racks': [(104, '6.5,-4.5')],
    },
}


def recolor(m):
    hexa = m.group(2)
    rgb = tuple(int(hexa[i:i + 2], 16) for i in (0, 2, 4))
    alpha = hexa[6:8] if len(hexa) == 8 else 'FF'
    h, s, v = colorsys.rgb_to_hsv(*(c / 255 for c in rgb))
    if s >= 0.35 and (h * 360 >= 330 or h * 360 < 20):
        return m.group(1) + "'#D8D4CC%s'" % alpha  # blood red -> imperial white
    return m.group(0)


def main():
    for path, spec in FILES.items():
        s = open(path, encoding='utf-8').read()
        for old, new in spec['names']:
            s = s.replace(old, new)
        # only decal colours (pipes, lights and the like keep theirs)
        s = re.sub(r"(- node:\n\s+color: )'#([0-9A-Fa-f]{6,8})'", recolor, s)
        for proto, key in (('DHArmorRack', 'racks'), ('SpawnPointNukies', 'spawns')):
            if '- proto: %s\n' % proto in s or not spec.get(key):
                continue
            uids = [int(x) for x in re.findall(r'^  - uid: (\d+)$', s, re.M)]
            nxt = max(uids) + 1
            block = '- proto: %s\n  entities:\n' % proto
            for parent, pos in spec[key]:
                block += ('  - uid: %d\n    components:\n    - type: Transform\n      pos: %s\n      parent: %d\n'
                          % (nxt, pos, parent))
                nxt += 1
            body = s.rstrip('\n')
            if body.endswith('...'):
                body = body[:-3].rstrip('\n') + '\n' + block + '...'
            else:
                body = body + '\n' + block.rstrip('\n')
            s = body + '\n'
            count = len(re.findall(r'^  - uid: \d+$', s, re.M))
            s = re.sub(r'entityCount: \d+', 'entityCount: %d' % count, s, count=1)
        # отдельный Gladius (правило DHImperialRaidSpawn) должен считаться шаттлом Рейда — ради ультиматума;
        # на базе Рейда этот же файл грузится с addComponents NukeOpsShuttle, повторно компонент не добавится
        if spec.get('raid_shuttle') and 'type: NukeOpsShuttle' not in s:
            s = s.replace('    - type: MetaData\n      name: IL-7 Gladius\n',
                          '    - type: MetaData\n      name: IL-7 Gladius\n    - type: NukeOpsShuttle\n', 1)
        open(path, 'w', encoding='utf-8').write(s)
        print(path, 'ok')


if __name__ == '__main__':
    main()
