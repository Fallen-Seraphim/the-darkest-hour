"""Shuttles of the Republic sweep teams, made from vanilla ERT shuttles:

- RAC Cordon (sweep team, Indomitus) — from the death squad shuttle (ERT-Small-Deathsquad): death squad loot (Hristov,
  anti-materiel ammo, telescopic shields, the AI-law crate, the nuke plushie) removed; armour rack, spare ammunition and
  eight ghost-role spawners added.
- RAC Verdict (reinforced sweep team, Deadend) — from ERT-Medium-Base: Nanotrasen poster removed; the tactical nuclear
  warhead, armour rack, spare ammunition and ten ghost-role spawners added in the bay.

Both: renamed, decals recoloured green and khaki, CentComm and security doors replaced with Republican ones.

Run from the repository root:  python3 Tools/_DarkestHour/republic/make_shuttle.py"""
import colorsys
import re

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



RENAME = {
    'AirlockCentralCommandGlass': 'DHAirlockRepublicGlassLocked',
    'AirlockCentralCommandGlassLocked': 'DHAirlockRepublicGlassLocked',
    'AirlockSecurityGlassLocked': 'DHAirlockRepublicGlassLocked',
    'AirlockGlassShuttle': 'DHAirlockGlassShuttleRepublic',
    'WindoorSecureCentralCommandLocked': 'DHWindoorRepublicLocked',
}


def build(src, dst, names, drop, adds):
    s = open(src, encoding='utf-8').read()
    for old, new in names:
        s = s.replace(old, new)
    # only decal colours (pipes, lights and the like keep theirs)
    s = re.sub(r"(- node:\n\s+color: )'#([0-9A-Fa-f]{6,8})'", remap, s)

    head, protos, tail = blocks(s)
    dropped = set()
    for proto, ents in protos:
        if proto in drop:
            dropped |= {uid(e) for e in ents}
    for proto, ents in protos:
        ents[:] = [e for e in ents if uid(e) not in dropped and not any(
            re.search(r'\n      parent: %d\n' % d, e) for d in dropped)]
    merged = {}
    for proto, ents in protos:
        merged.setdefault(RENAME.get(proto, proto), []).extend(ents)
    protos = [[proto, ents] for proto, ents in merged.items()]

    nxt = max(uid(e) for _, ents in protos for e in ents) + 1
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


build('Resources/Maps/Shuttles/AdminSpawn/ERT-Small-Deathsquad.yml',
      'Resources/Maps/_DarkestHour/Shuttles/republic_sweep.yml',
      [('NT-Ares', 'RAC Cordon')],
      {'WeaponSniperHristov', 'MagazineBoxAntiMateriel', 'TelescopicShield', 'PlushieNuke', 'CrateCommandSecure'},
      [
          ('DHArmorRack', ['-0.5,2.5']),
          # spare ammunition on the armoury table where the Hristov lay
          ('DHMagazine127x105SVP', ['-3.9,5.5', '-3.7,5.5']),
          ('DHMagazineBox556x45FMG', ['-4.5,5.5']),
          ('DHFlamerTankNapalm', ['-4.2,5.7']),
          ('DHSpawnerRepublicCommander', ['-4.5,4.5']),
          ('DHSpawnerRepublicSniper', ['-3.5,4.5']),
          ('DHSpawnerRepublicMachinegunner', ['-1.5,4.5']),
          ('DHSpawnerRepublicFlamer', ['-0.5,4.5']),
          ('DHSpawnerRepublicRifleman', ['-2.5,1.5', '-1.5,1.5']),
          ('DHSpawnerRepublicMedic', ['-2.5,0.5', '-1.5,0.5']),
      ])

build('Resources/Maps/Shuttles/AdminSpawn/ERT-Medium-Base.yml',
      'Resources/Maps/_DarkestHour/Shuttles/republic_deadend.yml',
      [('NT-Phoenix', 'RAC Verdict')],
      {'PosterLegitNanotrasenLogo'},
      [
          ('DHTacticalWarhead', ['-2.5,6.5']),
          ('DHArmorRack', ['-6.5,8.5']),
          ('DHMagazine127x105SVP', ['-6.6,3.6', '-6.4,3.6']),
          ('DHMagazineBox556x45FMG', ['-5.6,3.5', '-5.4,3.5']),
          ('DHMagazine556x45FMG', ['-4.6,3.5', '-4.4,3.5', '-4.5,3.7']),
          ('DHFlamerTankNapalm', ['0.5,3.5']),
          ('DHSpawnerRepublicDeadendCommander', ['-2.5,8.5']),
          ('DHSpawnerRepublicDeadendSniper', ['-4.5,8.5']),
          ('DHSpawnerRepublicDeadendMachinegunner', ['-0.5,8.5', '-6.5,6.5']),
          ('DHSpawnerRepublicDeadendRifleman', ['-5.5,4.5', '-3.5,4.5', '-1.5,4.5']),
          ('DHSpawnerRepublicDeadendMedic', ['0.5,4.5', '-6.5,4.5']),
          ('DHSpawnerRepublicDeadendFlamer', ['1.5,6.5']),
      ])
