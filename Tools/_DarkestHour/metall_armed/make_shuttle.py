"""Shuttle of the Metall Armed response team, made from the vanilla ERT-Medium-BasePlus shuttle: renamed, decals
recoloured into the Metallistick palette, Nanotrasen decor and the cut shotgun cabinet removed, the radar console
replaced with a gunnery console, two 30mm autocannons and the six ghost-role spawners added.

Run from the repository root:  python3 Tools/_DarkestHour/metall_armed/make_shuttle.py"""
import re, colorsys
src = 'Resources/Maps/Shuttles/AdminSpawn/ERT-Medium-BasePlus.yml'
dst = 'Resources/Maps/_DarkestHour/Shuttles/metall_armed_response.yml'
s = open(src).read()

s = s.replace('NT-Griffin', 'MA-Bastion')


def remap(m):
    hexa = m.group(1)
    rgb = tuple(int(hexa[i:i + 2], 16) for i in (0, 2, 4))
    a = hexa[6:8] if len(hexa) == 8 else 'FF'
    h, sat, v = colorsys.rgb_to_hsv(*(c / 255 for c in rgb))
    if sat < 0.15:
        new = 'D9CDB2' if v > 0.6 else '7D766A'
    else:
        hd = h * 360
        if hd < 20 or hd >= 330:
            new = '8D6F4E'
        elif hd < 70:
            new = 'B99651'
        elif hd < 170:
            new = '978E66'
        elif hd < 260:
            new = '646970'
        else:
            new = '6E6656'
    return f"color: '#{new}{a}'"


s = re.sub(r"color: '#([0-9A-Fa-f]{6,8})'", remap, s)


def drop_proto(text, proto):
    out, skip, n = [], False, 0
    for ln in text.split('\n'):
        if ln.startswith('- proto: '):
            skip = ln == '- proto: ' + proto
            n += skip
        if not skip:
            out.append(ln)
    assert n == 1, proto
    return '\n'.join(out)


for p in ['AltarNanotrasen', 'PosterLegitNanotrasenLogo', 'ShotGunCabinetFilled']:
    s = drop_proto(s, p)
s = s.replace('- proto: ComputerRadar\n', '- proto: DHComputerGunnery\n')
s = s.replace('- proto: WindoorSecureCentralCommandLocked\n', '- proto: WindoorCentralCommandLocked\n')

uids = [int(x) for x in re.findall(r'^  - uid: (\d+)$', s, re.M)]
nxt = max(uids) + 1


def ent(proto, items):
    global nxt
    out = f'- proto: {proto}\n  entities:\n'
    for pos in items:
        out += f'  - uid: {nxt}\n    components:\n    - type: Transform\n      pos: {pos}\n      parent: 1\n'
        nxt += 1
    return out


add = [
    ent('DHShipGun30mm', ['-5.5,9.5', '0.5,9.5']),
    ent('DHSpawnerMetallArmedCommander', ['-3.5,7.5']),
    ent('DHSpawnerMetallArmedWeapons', ['-1.5,7.5']),
    ent('DHSpawnerMetallArmedMedic', ['-1.5,6.5']),
    ent('DHSpawnerMetallArmedRifleman', ['-6.5,7.5', '-6.5,6.5', '1.5,6.5']),
]
s = s.rstrip('\n')
assert s.endswith('...')
s = s[:-3].rstrip('\n') + '\n' + ''.join(add) + '...\n'
count = len(re.findall(r'^  - uid: \d+$', s, re.M))
s = re.sub(r'entityCount: \d+', f'entityCount: {count}', s, count=1)
open(dst, 'w').write(s)
print('entities', count)
