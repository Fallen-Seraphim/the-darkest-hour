"""Hardsuits for The Darkest Hour, taken from Monolith (CC-BY-SA-3.0 / CC0 sprites) and recoloured into the
Metallistick Group palette.

Monolith hardsuit helmets use the old sprite states (off-/on-equipped-HELMET, icon-flash); this tool converts
them to the states our helmets expect: equipped-head (helmet with the lamp off), equipped-head-light (only the
pixels the lamp lights up), icon, icon-unshaded (empty), light-overlay (lit pixels of the icon).

Run from the repository root with the path to a Monolith checkout:
    python3 Tools/_DarkestHour/hardsuits/build.py /path/to/monolith
Writes Resources/Textures/_DarkestHour/Clothing/{OuterClothing,Head}/Hardsuits/<name>.rsi and preview.png next to
this script (the preview is not committed)."""
import colorsys
import json
import os
import sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, os.path.join(HERE, '..', 'recolor'))
import recolor as rc  # noqa: E402

OUT = os.path.join(ROOT, 'Resources/Textures/_DarkestHour/Clothing')

# Metall Armed: beige plates, warm graphite body, role accent
MA_PLATES = rc.ramp('3d362b', '5f5545', '857962', 'a79b82', 'c6baa0', 'e2d7bd')
MA_BODY = rc.ramp('121214', '1c1d20', '27292d', '34363b', '45484e', '5a5e65')
ACCENTS = {
    'ma_command': rc.BRASS,
    'ma_weapons': rc.OCHRE,
    'ma_medic': rc.TEAL,
    'ma_rifle': rc.KHAKI,
}


def ma_px(c, accent):
    r, g, b = c
    h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    y = rc.lum(c)
    # bright saturated trims take the role accent; dark tinted parts are the suit body
    if s >= 0.35 and v >= 0.45:
        return rc.from_ramp(accent, (y - 0.05) / 0.85)
    if v >= 0.48:
        return rc.from_ramp(MA_PLATES, (y - 0.4) / 0.6)
    return rc.from_ramp(MA_BODY, y / 0.5)


def make_px(scheme):
    if scheme in ACCENTS:
        accent = ACCENTS[scheme]
        return lambda c: ma_px(c, accent)
    return lambda c: rc.recolor_px(c, scheme)


def recolor(im, fn):
    im = im.convert('RGBA')
    px = im.load()
    cache = {}
    for y in range(im.height):
        for x in range(im.width):
            p = px[x, y]
            if p[3] == 0:
                continue
            if p[:3] not in cache:
                cache[p[:3]] = fn(p[:3])
            px[x, y] = cache[p[:3]] + (p[3],)
    return im


def lit_pixels(on, off):
    """Pixels that change when the lamp turns on: the light layer drawn over the unlit sprite."""
    on, off = on.convert('RGBA'), off.convert('RGBA')
    out = Image.new('RGBA', on.size, (0, 0, 0, 0))
    po, pf, pt = on.load(), off.load(), out.load()
    for y in range(on.height):
        for x in range(on.width):
            a, b = po[x, y], pf[x, y]
            if a[3] == 0:
                continue
            brighter = rc.lum(a[:3]) - (rc.lum(b[:3]) if b[3] else 0)
            if brighter > 0.12:
                pt[x, y] = a
    return out


# Each target: (kind, source rsi under Resources/Textures, destination name, scheme)
TARGETS = [
    # --- Metall Armed response team (references: Monolith ERT hardsuits, TSF hardsuits)
    ('outer', 'Clothing/OuterClothing/Hardsuits/ERTSuits/ertleader.rsi', 'metall_armed_commander', 'ma_command'),
    ('outer', 'Clothing/OuterClothing/Hardsuits/ERTSuits/ertengineer.rsi', 'metall_armed_weapons', 'ma_weapons'),
    ('outer', 'Clothing/OuterClothing/Hardsuits/ERTSuits/ertmedical.rsi', 'metall_armed_medic', 'ma_medic'),
    ('outer', 'Clothing/OuterClothing/Hardsuits/ERTSuits/ertsecurity.rsi', 'metall_armed_rifleman', 'ma_rifle'),
    ('helmet', 'Clothing/Head/Hardsuits/ERThelmets/ertleader.rsi', 'metall_armed_commander', 'ma_command'),
    ('helmet', 'Clothing/Head/Hardsuits/ERThelmets/ertengineer.rsi', 'metall_armed_weapons', 'ma_weapons'),
    ('helmet', 'Clothing/Head/Hardsuits/ERThelmets/ertmedical.rsi', 'metall_armed_medic', 'ma_medic'),
    ('helmet', 'Clothing/Head/Hardsuits/ERThelmets/ertsecurity.rsi', 'metall_armed_rifleman', 'ma_rifle'),
]

# species suffixes our game has; others in the source are dropped
SPECIES = ['', '-vox', '-reptilian']


def build_outer(src, dst, fn, meta):
    states = []
    for st in meta['states']:
        name = st['name']
        if name.startswith('equipped-OUTERCLOTHING') and name[len('equipped-OUTERCLOTHING'):] not in SPECIES:
            continue
        recolor(Image.open(os.path.join(src, name + '.png')), fn).save(os.path.join(dst, name + '.png'))
        states.append(st)
    return states


def build_helmet(src, dst, fn, meta):
    have = {s['name']: s for s in meta['states']}
    states = []

    def save(name, im, like):
        im.save(os.path.join(dst, name + '.png'))
        st = dict(have[like])
        st['name'] = name
        states.append(st)

    icon = Image.open(os.path.join(src, 'icon.png'))
    save('icon', recolor(icon, fn), 'icon')
    save('icon-unshaded', Image.new('RGBA', icon.size, (0, 0, 0, 0)), 'icon')
    flash = 'icon-flash' if 'icon-flash' in have else 'icon'
    save('light-overlay', recolor(lit_pixels(Image.open(os.path.join(src, flash + '.png')), icon), fn), 'icon')

    for sp in SPECIES:
        off, on = 'off-equipped-HELMET' + sp, 'on-equipped-HELMET' + sp
        if off not in have:
            continue
        off_im = Image.open(os.path.join(src, off + '.png'))
        on_im = Image.open(os.path.join(src, on + '.png')) if on in have else off_im
        save('equipped-head' + sp, recolor(off_im, fn), off)
        save('equipped-head-unshaded' + sp, Image.new('RGBA', off_im.size, (0, 0, 0, 0)), off)
        save('equipped-head-light' + sp, recolor(lit_pixels(on_im, off_im), fn), off)
    return states


def main(mono):
    previews = []
    for kind, rsi, name, scheme in TARGETS:
        src = os.path.join(mono, 'Resources/Textures', rsi)
        folder = 'OuterClothing/Hardsuits' if kind == 'outer' else 'Head/Hardsuits'
        dst = os.path.join(OUT, folder, name + '.rsi')
        os.makedirs(dst, exist_ok=True)
        for f in os.listdir(dst):
            os.remove(os.path.join(dst, f))
        with open(os.path.join(src, 'meta.json'), encoding='utf-8-sig') as f:
            meta = json.load(f)
        fn = make_px(scheme)
        states = (build_outer if kind == 'outer' else build_helmet)(src, dst, fn, meta)
        out = {
            'version': 1,
            'license': meta.get('license', 'CC-BY-SA-3.0'),
            'copyright': (meta.get('copyright', '') + ' | Taken from Monolith (' + rsi + '), recoloured and converted '
                          'for The Darkest Hour (Tools/_DarkestHour/hardsuits)').strip(' |'),
            'size': meta['size'],
            'states': states,
        }
        with open(os.path.join(dst, 'meta.json'), 'w', encoding='utf-8') as f:
            json.dump(out, f, indent=4, ensure_ascii=False)
            f.write('\n')
        previews.append((kind, name, dst))
    preview(previews)


def first_frame(path, size):
    im = Image.open(path).convert('RGBA')
    return im.crop((0, 0, size, size))


def preview(items):
    Z, S = 4, 32
    cells = []
    for kind, name, dst in items:
        if kind == 'outer':
            names = ['icon', 'equipped-OUTERCLOTHING']
        else:
            names = ['icon', 'equipped-head', 'equipped-head-light']
        cells.append((kind + ' ' + name, [first_frame(os.path.join(dst, n + '.png'), S) for n in names]))
    W = 3 * S * Z + 8
    sheet = Image.new('RGBA', (W * 2, (len(cells) + 1) // 2 * (S * Z + 4)), (46, 47, 52, 255))
    for i, (_, ims) in enumerate(cells):
        ox, oy = (i % 2) * W, (i // 2) * (S * Z + 4)
        for j, im in enumerate(ims):
            sheet.alpha_composite(im.resize((S * Z, S * Z), Image.NEAREST), (ox + j * S * Z, oy))
    sheet.save(os.path.join(HERE, 'preview.png'))


if __name__ == '__main__':
    main(sys.argv[1])
