"""Recolour vanilla clothing into the Metallistick Group palette.

Each department scheme maps the saturated colours of a hue range onto a new colour ramp by brightness, so
shading and outlines stay intact. Unsaturated pixels (white, grey, black) are warmed slightly towards the
grey-beige corporate palette. The result goes to Resources/Textures/_DarkestHour/<same path>.rsi and the
prototypes are pointed at it by apply.py.

Run from the repository root:  python3 Tools/_DarkestHour/recolor/recolor.py && python3 Tools/_DarkestHour/recolor/apply.py"""
import colorsys
import json
import os
import shutil
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
SRC = os.path.join(ROOT, 'Resources/Textures')
DST = os.path.join(ROOT, 'Resources/Textures/_DarkestHour')


def hexc(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def ramp(*hexes):
    return [hexc(h) for h in hexes]


# Ramps go from dark to light; a pixel picks its colour by brightness.
KHAKI = ramp('2e2c1f', '4f4a33', '746c4a', '978e66', 'b9ae86', 'ddd2b2')
BEIGE = ramp('3a3328', '5e5442', '857861', 'aa9d83', 'cbbfa5', 'e8dcbf')
STEEL = ramp('1f2124', '33363a', '4a4e53', '646970', '83888f', 'a8adb3')
GRAPHITE = ramp('16171a', '24262a', '33363b', '45484e', '5c6067', '7a7f87')
BRASS = ramp('4a3812', '6e5420', '977535', 'b99651', 'd8b96f', 'f0d898')
TEAL = ramp('14313a', '1f4a56', '2d6676', '3f8496', '5ea3b3', '8cc4cf')
VIOLET = ramp('261b33', '3a2a4c', '503c66', '685283', '846fa0', 'a896bf')
OCHRE = ramp('3a250e', '5c3a14', '82531c', 'a86d27', 'c98a3a', 'e3ad5c')
TAN = ramp('33261a', '503c28', '6e553a', '8d6f4e', 'ad8d68', 'cdb08a')
# Imperial Marine Corps (IMB-1): white plates over black
IVORY = ramp('4a4741', '6f6b64', '938f88', 'b7b3ab', 'd8d4cc', 'f3f1ea')

WARM_WHITE = (238, 232, 218)


def hue_in(h, lo, hi):
    h *= 360
    return lo <= h <= hi if lo <= hi else (h >= lo or h <= hi)


# scheme: list of (hue from, hue to, minimum saturation, ramp); first match wins
SCHEMES = {
    # corporate security: red -> khaki, blue -> steel grey, gold stays
    'security': [(330, 20, 0.25, KHAKI, 0.28), (190, 260, 0.2, STEEL, 0.1)],
    # command: navy -> graphite, gold -> brass, green/red trims -> brass
    'command': [(190, 265, 0.18, GRAPHITE), (35, 60, 0.3, BRASS), (330, 20, 0.35, BRASS)],
    # medical: blues/cyans -> muted teal
    'medical': [(170, 250, 0.15, TEAL)],
    # science: purples -> muted violet
    'science': [(255, 335, 0.15, VIOLET)],
    # engineering: yellow/orange -> ochre, red -> ochre
    'engineering': [(20, 70, 0.3, OCHRE), (345, 20, 0.4, OCHRE)],
    # supply: browns/oranges -> tan, gold stays brass, salvage purple -> steel
    'supply': [(15, 45, 0.2, TAN, 0.1), (45, 62, 0.35, BRASS), (255, 335, 0.2, STEEL, 0.15)],
    # Imperial Marine Corps (former syndicate gear): blood red -> imperial white, black stays, gold stays
    'imperial': [(330, 25, 0.3, IVORY, 0.2), (260, 330, 0.3, IVORY, 0.2)],
}


def lum(c):
    r, g, b = c
    return (0.299 * r + 0.587 * g + 0.114 * b) / 255


def from_ramp(rmp, y):
    y = max(0.0, min(1.0, y))
    pos = y * (len(rmp) - 1)
    i = min(int(pos), len(rmp) - 2)
    t = pos - i
    a, b = rmp[i], rmp[i + 1]
    return tuple(int(round(a[k] + (b[k] - a[k]) * t)) for k in range(3))


def recolor_px(c, scheme):
    r, g, b = c
    h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    for rule in SCHEMES[scheme]:
        lo, hi, smin, rmp = rule[:4]
        lift = rule[4] if len(rule) > 4 else 0.0
        if s >= smin and v > 0.08 and hue_in(h, lo, hi):
            # stretch brightness a little so the darkest parts stay dark and highlights stay light;
            # `lift` brightens dark source colours (vanilla security red is darker than khaki should be)
            y = lum(c)
            return from_ramp(rmp, (y - 0.05) / 0.85 * (1 - lift) + lift)
    if s < 0.16 and v > 0.55:
        # whites and light greys: warm them up
        k = (v - 0.55) / 0.45 * 0.6
        return tuple(int(round(c[i] + (WARM_WHITE[i] - c[i]) * k * (c[i] / 255))) for i in range(3))
    if s < 0.16:
        # dark greys: a faint warm tint
        return (min(255, int(r * 1.03)), min(255, int(g * 1.01)), int(b * 0.95))
    return c


def recolor_image(path_in, path_out, scheme):
    im = Image.open(path_in).convert('RGBA')
    px = im.load()
    cache = {}
    for y in range(im.height):
        for x in range(im.width):
            p = px[x, y]
            if p[3] == 0:
                continue
            key = p[:3]
            if key not in cache:
                cache[key] = recolor_px(key, scheme)
            px[x, y] = cache[key] + (p[3],)
    im.save(path_out)


def load_targets():
    with open(os.path.join(os.path.dirname(__file__), 'targets.json'), encoding='utf-8') as f:
        return json.load(f)


def build():
    targets = load_targets()
    for rsi, scheme in targets.items():
        src = os.path.join(SRC, rsi)
        dst = os.path.join(DST, rsi)
        if os.path.exists(dst):
            shutil.rmtree(dst)
        os.makedirs(dst)
        for fn in os.listdir(src):
            if fn.endswith('.png'):
                recolor_image(os.path.join(src, fn), os.path.join(dst, fn), scheme)
        with open(os.path.join(src, 'meta.json'), encoding='utf-8-sig') as f:
            meta = json.load(f)
        meta['copyright'] = (meta.get('copyright', '') +
                             ' | Recoloured for The Darkest Hour (Tools/_DarkestHour/recolor)').strip(' |')
        with open(os.path.join(dst, 'meta.json'), 'w', encoding='utf-8') as f:
            json.dump(meta, f, indent=4, ensure_ascii=False)
            f.write('\n')
    return targets


if __name__ == '__main__':
    build()
