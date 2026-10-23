"""Shuttle of the Republic sweep team, made from the vanilla death squad shuttle (ERT-Small-Deathsquad): renamed,
decals recoloured green and khaki, CentComm doors replaced with Republican ones, the death squad loot (Hristov,
anti-materiel ammo, telescopic shields, the AI-law crate, the nuke plushie) removed; an armour maintenance rack,
spare ammunition and the eight ghost-role spawners added.

Run from the repository root:  python3 Tools/_DarkestHour/republic/make_shuttle.py"""
import colorsys
import re

SRC = 'Resources/Maps/Shuttles/AdminSpawn/ERT-Small-Deathsquad.yml'
DST = 'Resources/Maps/_DarkestHour/Shuttles/republic_sweep.yml'

s = open(SRC, encoding='utf-8').read()
s = s.replace('NT-Ares', 'RAC Cordon')


def remap(m):
    hexa = m.group(2)
    rgb = tuple(int(hexa[i:i + 2], 16) for i in (0, 2, 4))
    a = hexa[6:8] if len(hexa) == 8 else 'FF'
    _, sat, v = colorsys.rgb_to_hsv(*(c / 255 for c in rgb))
    if sat < 0.15:
        new = 'D2C79A' if v > 0.6 else '4A5A40'   # white trim -> khaki
    else:
        new = '4E6B2E'                            # blue -> republic green
    return m.group(1) + "'#%s%s'" % (new, a)


# only decal colours (pipes, lights and the like keep theirs)
s = re.sub(r"(- node:\n\s+color: )'#([0-9A-Fa-f]{6,8})'", remap, s)


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


def uid(e):
    return int(re.match(r'  - uid: (\d+)', e).group(1))


head, protos, tail = blocks(s)

# the death squad loot and the AI-law crate with everything in it
DROP = {'WeaponSniperHristov', 'MagazineBoxAntiMateriel', 'TelescopicShield', 'PlushieNuke', 'CrateCommandSecure'}
dropped = set()
for proto, ents in protos:
    if proto in DROP:
        dropped |= {uid(e) for e in ents}
for proto, ents in protos:
    ents[:] = [e for e in ents if uid(e) not in dropped and not any(
        re.search(r'\n      parent: %d\n' % d, e) for d in dropped)]

RENAME = {
    'AirlockCentralCommandGlass': 'DHAirlockRepublicGlassLocked',
    'AirlockGlassShuttle': 'DHAirlockGlassShuttleRepublic',
    'WindoorSecureCentralCommandLocked': 'DHWindoorRepublicLocked',
}
for p in protos:
    p[0] = RENAME.get(p[0], p[0])

nxt = max(uid(e) for _, ents in protos for e in ents) + 1


def add(proto, positions):
    global nxt
    ents = []
    for pos in positions:
        ents.append('  - uid: %d\n    components:\n    - type: Transform\n      pos: %s\n      parent: 1\n' % (nxt, pos))
        nxt += 1
    protos.append([proto, ents])


add('DHArmorRack', ['-0.5,2.5'])
# spare ammunition on the armoury table where the Hristov lay
add('DHMagazine127x105SVP', ['-3.9,5.5', '-3.7,5.5'])
add('DHMagazineBox556x45FMG', ['-4.5,5.5'])
add('DHFlamerTankNapalm', ['-4.2,5.7'])
add('DHSpawnerRepublicCommander', ['-4.5,4.5'])
add('DHSpawnerRepublicSniper', ['-3.5,4.5'])
add('DHSpawnerRepublicMachinegunner', ['-1.5,4.5'])
add('DHSpawnerRepublicFlamer', ['-0.5,4.5'])
add('DHSpawnerRepublicRifleman', ['-2.5,1.5', '-1.5,1.5'])
add('DHSpawnerRepublicMedic', ['-2.5,0.5', '-1.5,0.5'])

s = join(head, protos, tail)
count = len(re.findall(r'^  - uid: \d+$', s, re.M))
s = re.sub(r'entityCount: \d+', 'entityCount: %d' % count, s, count=1)
open(DST, 'w', encoding='utf-8').write(s)
print('entities', count, 'dropped', len(dropped))
