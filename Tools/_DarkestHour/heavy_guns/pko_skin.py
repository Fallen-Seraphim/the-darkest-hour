"""Republican anti-orbital guns (RAO-400/700) in the style of the Imperial naval guns (plan item 24).

The naval guns of the carrier use the cmss13 orbital cannon from RMC-14 (_RMC14/Structures/orbital_cannon.rsi). The
anti-orbital guns get the same gun in Republican colours: the gunmetal body turns olive drab, the orange cables turn
khaki; black outlines, the hazard yellow, the shells and the muzzle flash keep their colours. Cables are found by
their position in the unloaded frame, so the flash of the firing animation is not touched.

Run from the repository root:  python3 Tools/_DarkestHour/heavy_guns/pko_skin.py
Writes Resources/Textures/_DarkestHour/Structures/HeavyGuns/pko.rsi."""
import colorsys
import json
import os
from PIL import Image

SRC = 'Resources/Textures/_RMC14/Structures/orbital_cannon.rsi'
OUT = 'Resources/Textures/_DarkestHour/Structures/HeavyGuns/pko.rsi'
F = 128
OLIVE_H, OLIVE_S = 80 / 360, 0.40     # body: olive drab
KHAKI_H, KHAKI_S = 44 / 360, 0.50     # cables: khaki


def is_cable(c):
    h, s, v = colorsys.rgb_to_hsv(*(x / 255 for x in c[:3]))
    return c[3] and s > 0.45 and (h < 25 / 360 or h > 350 / 360) and v > 0.15


def body(c):
    r, g, b, a = c
    h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    if not a or s > 0.2 or v < 0.07:
        return c
    t = min(1.0, (v - 0.07) / 0.3)          # the darkest steel stays nearly black
    nr, ng, nb = colorsys.hsv_to_rgb(OLIVE_H, OLIVE_S * t, min(1.0, v * (1 + 0.15 * t)))
    return (round(nr * 255), round(ng * 255), round(nb * 255), a)


def cable(c):
    r, g, b, a = c
    v = max(r, g, b) / 255
    nr, ng, nb = colorsys.hsv_to_rgb(KHAKI_H, KHAKI_S, min(1.0, 0.18 + v * 0.55))
    return (round(nr * 255), round(ng * 255), round(nb * 255), a)


def main():
    meta = json.load(open(os.path.join(SRC, 'meta.json')))
    ref = Image.open(os.path.join(SRC, 'obc_unloaded.png')).convert('RGBA').load()
    mask = {(x, y) for x in range(F) for y in range(F) if is_cable(ref[x, y])}
    os.makedirs(OUT, exist_ok=True)
    for st in meta['states']:
        im = Image.open(os.path.join(SRC, st['name'] + '.png')).convert('RGBA')
        px = im.load()
        for y in range(im.height):
            for x in range(im.width):
                c = px[x, y]
                if (x % F, y % F) in mask and is_cable(c):
                    px[x, y] = cable(c)
                else:
                    px[x, y] = body(c)
        im.save(os.path.join(OUT, st['name'] + '.png'))
    meta['copyright'] += '; recoloured for the Republican anti-orbital guns by DarkestHour ' \
                         '(Tools/_DarkestHour/heavy_guns/pko_skin.py)'
    with open(os.path.join(OUT, 'meta.json'), 'w') as f:
        json.dump(meta, f, indent=2)
        f.write('\n')


if __name__ == '__main__':
    main()
