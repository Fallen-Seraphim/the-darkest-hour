"""RMC-14 airlocks in the vanilla airlock layout (Terminus and the bunker, plan item 24).

Takes the cmss13 door sprites from an RMC-14 checkout and lays them out as one rsi with the state names the vanilla
door/airlock visualisers expect (closed/open/opening/closing, *_unlit, bolted_unlit, deny_unlit, emergency_unlit,
panel_*, welded, sparks*). RMC sprites carry their lights in the base frames, so the *_unlit states are empty and the
locked/deny/spark overlays become the bolted/deny/sparks layers. Direction 0 is a door in an east-west wall,
direction 2 (east) a door in a north-south wall: the map generators rotate such doors by 90 degrees.

Run from the repository root:  python3 Tools/_DarkestHour/reskin/rmc_airlocks.py /path/to/rmc14
Writes Resources/Textures/_DarkestHour/Structures/Doors/Airlocks/RMC/<name>.rsi and copies the RMC door sounds."""
import json
import os
import shutil
import sys
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
OUT = os.path.join(ROOT, 'Resources/Textures/_DarkestHour/Structures/Doors/Airlocks/RMC')
DOORS = ['hybrisa_door', 'hybrisa_door_glass', 'engi_door', 'engi_door_glass', 'personal_door_glass',
         'almayer_door_black', 'almayer_door_black_glass', 'com_door', 'sec_door', 'sec_door_glass']
SOUNDS = ['airlock.ogg', 'buzz_two.ogg']
EMERGENCY = (255, 200, 40)
S = 32


def frames(path, st):
    """[direction][frame] -> 32x32 image."""
    im = Image.open(os.path.join(path, st['name'] + '.png')).convert('RGBA')
    dirs = st.get('directions', 1)
    n = len(st['delays'][0]) if st.get('delays') else 1
    cols = im.width // S
    out = []
    for d in range(dirs):
        row = []
        for f in range(n):
            i = d * n + f
            row.append(im.crop(((i % cols) * S, (i // cols) * S, (i % cols) * S + S, (i // cols) * S + S)))
        out.append(row)
    if dirs == 1:
        out = out * 4
    return out, st.get('delays', [[1.0]])[0]


def blank(n=1):
    return [[Image.new('RGBA', (S, S)) for _ in range(n)] for _ in range(4)]


def tint(fr, rgb):
    res = []
    for row in fr:
        r2 = []
        for im in row:
            px = im.load()
            o = Image.new('RGBA', (S, S))
            po = o.load()
            for y in range(S):
                for x in range(S):
                    a = px[x, y][3]
                    if a:
                        po[x, y] = rgb + (a,)
            r2.append(o)
        res.append(r2)
    return res


def write(rsi, states, meta_src):
    os.makedirs(rsi, exist_ok=True)
    meta = {'version': 1, 'license': meta_src.get('license', 'CC-BY-SA-3.0'),
            'copyright': meta_src.get('copyright', '') + '; laid out for SS14 airlocks by DarkestHour '
                         '(Tools/_DarkestHour/reskin/rmc_airlocks.py)',
            'size': {'x': S, 'y': S}, 'states': []}
    for name, (fr, delays) in states.items():
        n = len(fr[0])
        cols = min(4 * n, 8)
        rows = (4 * n + cols - 1) // cols
        sheet = Image.new('RGBA', (cols * S, rows * S))
        for d in range(4):
            for f in range(n):
                i = d * n + f
                sheet.paste(fr[d][f], ((i % cols) * S, (i // cols) * S))
        sheet.save(os.path.join(rsi, name + '.png'))
        st = {'name': name, 'directions': 4}
        if n > 1:
            st['delays'] = [list(delays)] * 4
        meta['states'].append(st)
    with open(os.path.join(rsi, 'meta.json'), 'w') as f:
        json.dump(meta, f, indent=2)
        f.write('\n')


def build(rmc, name):
    src = os.path.join(rmc, 'Resources/Textures/_RMC14/Structures/Doors/Airlocks', name + '.rsi')
    meta = json.load(open(os.path.join(src, 'meta.json')))
    st = {s['name']: s for s in meta['states']}
    get = lambda n: frames(src, st[n])  # noqa: E731
    closed = get('closed')
    states = {
        'assembly': closed, 'closed': closed, 'open': get('open'),
        'opening': get('opening'), 'closing': get('closing'),
        'closed_unlit': (blank(), [1]), 'open_unlit': (blank(), [1]),
        'opening_unlit': (blank(), [1]), 'closing_unlit': (blank(), [1]),
        'bolted_unlit': get('locked'), 'deny_unlit': get('deny'),
        'emergency_unlit': (tint(get('locked')[0], EMERGENCY), [1]),
        'panel_open': get('panel_open'), 'panel_closed': (blank(), [1]),
        'panel_opening': (blank(), [1]), 'panel_closing': (blank(), [1]),
        'welded': get('welded'),
    }
    for s in ('sparks', 'sparks_broken', 'sparks_damaged', 'sparks_open'):
        states[s] = get('spark')
    write(os.path.join(OUT, name + '.rsi'), states, meta)


def main():
    rmc = sys.argv[1]
    for d in DOORS:
        build(rmc, d)
    a_src = os.path.join(rmc, 'Resources/Audio/_RMC14/Machines')
    a_dst = os.path.join(ROOT, 'Resources/Audio/_RMC14/Machines')
    attr = open(os.path.join(a_dst, 'attributions.yml')).read()
    src_attr = open(os.path.join(a_src, 'attributions.yml')).read().split('\n\n')
    for s in SOUNDS:
        shutil.copy(os.path.join(a_src, s), os.path.join(a_dst, s))
        if f'["{s}"]' not in attr:
            block = next(b for b in src_attr if f'["{s}"]' in b)
            attr = attr.rstrip('\n') + '\n\n' + block.strip('\n') + '\n'
    open(os.path.join(a_dst, 'attributions.yml'), 'w').write(attr)
    print('ok', len(DOORS), 'doors')


if __name__ == '__main__':
    main()
