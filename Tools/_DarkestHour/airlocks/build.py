"""Airlocks in Metallistick Group colours.

The steel-blue body of every station airlock becomes the corporate warm grey; department paint takes the
department colour of the uniforms (Tools/_DarkestHour/recolor): command graphite with brass, security khaki,
medical teal, science violet, engineering ochre, supply tobacco. Glass panes and status lights keep their colour.
External, firelock and blast doors keep their safety red/yellow; CentComm, syndicate, shuttle, exo and service
doors are left as they are.

Run from the repository root:  python3 Tools/_DarkestHour/airlocks/build.py
Writes Resources/Textures/_DarkestHour/Structures/Doors/Airlocks/{Standard,Glass}/<name>.rsi and points the
prototypes at them (safe to run again); preview.png next to this script is not committed."""
import colorsys
import glob
import json
import os
import re
import sys
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, os.path.join(HERE, '..', 'recolor'))
import recolor as rc  # noqa: E402

SRC = 'Structures/Doors/Airlocks'
TEX = os.path.join(ROOT, 'Resources/Textures')
OUT = os.path.join(TEX, '_DarkestHour', SRC)
MARK = '# DarkestHour: шлюз в цветах Metallistick (Tools/_DarkestHour/airlocks)'

WARM_GREY = rc.ramp('121110', '211f1c', '35322d', '4f4b44', '6f6a61', '958f84', 'bdb6a9')
IMPERIAL_BLACK = rc.ramp('060607', '0e0e10', '18191c', '24252a', '33353b', '484b52', '62666e')
REPUBLIC_GREEN = rc.ramp('0b130a', '142113', '1d2e1b', '283d24', '35502f', '46643d', '5a7a4f')
COMMAND = rc.ramp('141518', '23252a', '383632', '5d5038', '8f7442', 'c09d5a', 'e0c27c')

# paint rules: (hue from, hue to, ramp); first match wins, None = any hue
DEPT = {
    'neutral': [(20, 70, rc.OCHRE)],                      # hazard stripes on maintenance doors
    'command': [(None, None, COMMAND)],
    'security': [(None, None, rc.KHAKI)],
    'medical': [(15, 60, rc.OCHRE), (70, 170, rc.KHAKI), (None, None, rc.TEAL)],
    'science': [(None, None, rc.VIOLET)],
    'engineering': [(160, 215, rc.TEAL), (None, None, rc.OCHRE)],
    'supply': [(255, 335, rc.STEEL), (None, None, rc.TAN)],
    # Imperial Marine Corps (former syndicate doors): white paint on a black body, gold stays
    'imperial': [(35, 65, None), (None, None, rc.IVORY)],
    # Republican Army Corps: khaki paint on a green body, republic green trim
    'republic': [(70, 170, rc.ramp('1c2b0c', '2f4714', '43641d', '5e8c2a', '7fae46', 'a9cf72')), (None, None, rc.KHAKI)],
}

# body ramp per department (default: corporate warm grey)
BODY = {'imperial': IMPERIAL_BLACK, 'republic': REPUBLIC_GREEN}

DOORS = {
    'airlock-base': 'neutral', 'airlock-maints': 'neutral', 'airlock-hatch': 'neutral',
    'airlock-hatch-maints': 'neutral', 'airlock-vault': 'neutral',
    'command-base': 'command', 'security-base': 'security',
    'medical-base': 'medical', 'medical-chem': 'medical', 'medical-viro': 'medical',
    'science-base': 'science',
    'engineering-base': 'engineering', 'engineering-atmos': 'engineering',
    'cargo-base': 'supply', 'cargo-mining': 'supply', 'cargo-salvage': 'supply',
    'syndicate-base': 'imperial', 'shuttle-syndicate': 'imperial', 'airlock-hatch-syndicate': 'imperial',
}


def door_px(c, dept):
    r, g, b = c
    h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    y = rc.lum(c)
    if s >= 0.38 and v >= 0.1:
        for lo, hi, rmp in DEPT[dept]:
            if lo is None or rc.hue_in(h, lo, hi):
                return c if rmp is None else rc.from_ramp(rmp, (y - 0.02) / 0.8)
        return c
    # steel-blue body and dark greys -> warm corporate grey (or the faction body colour)
    return rc.from_ramp(BODY.get(dept, WARM_GREY), y / 0.75)


def glass_mask(state_file):
    """Glass panes: bluish pixels of the plain glass airlock in the same state (all glass doors share the layout)."""
    path = os.path.join(TEX, SRC, 'Glass', 'airlock-base.rsi', state_file)
    if not os.path.exists(path):
        return None
    im = Image.open(path).convert('RGBA')
    mask = set()
    px = im.load()
    for yy in range(im.height):
        for xx in range(im.width):
            p = px[xx, yy]
            if p[3] == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(*(k / 255 for k in p[:3]))
            if 185 <= h * 360 <= 225 and s >= 0.3 and v >= 0.3:
                mask.add((xx, yy))
    return mask


def recolor(path_in, path_out, dept, glass):
    im = Image.open(path_in).convert('RGBA')
    px = im.load()
    mask = glass_mask(os.path.basename(path_in)) if glass and dept not in BODY else None
    cache = {}
    for yy in range(im.height):
        for xx in range(im.width):
            p = px[xx, yy]
            if p[3] == 0 or (mask and (xx, yy) in mask):
                continue
            if p[:3] not in cache:
                cache[p[:3]] = door_px(p[:3], dept)
            px[xx, yy] = cache[p[:3]] + (p[3],)
    im.save(path_out)


def build():
    done = []
    for kind in ('Standard', 'Glass'):
        for name, dept in DOORS.items():
            src = os.path.join(TEX, SRC, kind, name + '.rsi')
            if not os.path.isdir(src):
                continue
            dst = os.path.join(OUT, kind, name + '.rsi')
            os.makedirs(dst, exist_ok=True)
            for f in os.listdir(dst):
                os.remove(os.path.join(dst, f))
            for f in os.listdir(src):
                if f.endswith('.png'):
                    recolor(os.path.join(src, f), os.path.join(dst, f), dept, kind == 'Glass')
            with open(os.path.join(src, 'meta.json'), encoding='utf-8-sig') as f:
                meta = json.load(f)
            meta['copyright'] = (meta.get('copyright', '') +
                                 ' | Recoloured for The Darkest Hour (Tools/_DarkestHour/airlocks)').strip(' |')
            with open(os.path.join(dst, 'meta.json'), 'w', encoding='utf-8') as f:
                json.dump(meta, f, indent=4, ensure_ascii=False)
                f.write('\n')
            done.append((kind, name))
    return done


def apply(done):
    changed = 0
    for path in glob.glob(os.path.join(ROOT, 'Resources/Prototypes/**/*.yml'), recursive=True):
        text = open(path, encoding='utf-8').read()
        new = text
        for kind, name in done:
            old = '%s/%s/%s.rsi' % (SRC, kind, name)
            pat = re.compile(r'(?<!_DarkestHour/)' + re.escape(old))
            new = pat.sub('_DarkestHour/' + old, new)
        if new != text:
            # mark the changed lines once
            lines = []
            for ln in new.split('\n'):
                if '_DarkestHour/' + SRC in ln and MARK not in ln and '#' not in ln:
                    ln += ' ' + MARK
                lines.append(ln)
            open(path, 'w', encoding='utf-8').write('\n'.join(lines))
            changed += 1
    return changed


def preview(done):
    S, Z = 32, 3
    cells = []
    for kind, name in done:
        a = Image.open(os.path.join(TEX, SRC, kind, name + '.rsi', 'closed.png')).convert('RGBA').crop((0, 0, S, S))
        b = Image.open(os.path.join(OUT, kind, name + '.rsi', 'closed.png')).convert('RGBA').crop((0, 0, S, S))
        cells.append((kind[0] + ':' + name, a, b))
    cols = 8
    rows = (len(cells) + cols - 1) // cols
    img = Image.new('RGBA', (cols * 2 * S * Z, rows * (S * Z + 12)), (40, 40, 46, 255))
    d = ImageDraw.Draw(img)
    for i, (n, a, b) in enumerate(cells):
        x, y = (i % cols) * 2 * S * Z, (i // cols) * (S * Z + 12)
        img.alpha_composite(a.resize((S * Z, S * Z), Image.NEAREST), (x, y))
        img.alpha_composite(b.resize((S * Z, S * Z), Image.NEAREST), (x + S * Z, y))
        d.text((x + 2, y + S * Z), n, fill=(230, 230, 230))
    img.save(os.path.join(HERE, 'preview.png'))


if __name__ == '__main__':
    done = build()
    print('rsi:', len(done), 'prototype files:', apply(done))
    preview(done)
