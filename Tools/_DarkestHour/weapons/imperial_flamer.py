"""IFL-17 incinerator of the Imperial Marine Corps: the RMC-14 colony flamethrower (FL-9) in imperial colours
(plan item 24). Light steel turns white polymer, mid steel turns black, the red fuel tank turns signal orange,
brass turns ochre; the pilot flame keeps its colour.

Run from the repository root:  python3 Tools/_DarkestHour/weapons/imperial_flamer.py
Writes Resources/Textures/_DarkestHour/Objects/Weapons/Guns/Flamers/ifl17.rsi."""
import colorsys
import json
import os
from PIL import Image

SRC = 'Resources/Textures/_RMC14/Objects/Weapons/Guns/Flamethrower/colony.rsi'
OUT = 'Resources/Textures/_DarkestHour/Objects/Weapons/Guns/Flamers/ifl17.rsi'
KEEP = {'lit'}


def px(c):
    r, g, b, a = c
    if not a:
        return c
    h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    hd = h * 360
    if s < 0.12:
        if v >= 0.5:                                  # light steel -> white polymer
            h2, s2, v2 = 225 / 360, 0.03, min(1.0, 0.70 + (v - 0.5) * 0.9)
        elif v >= 0.2:                                # mid steel -> black steel
            h2, s2, v2 = 235 / 360, 0.08, 0.06 + (v - 0.2) * 0.62
        else:
            return c
    elif (hd < 15 or hd > 345) and s > 0.5:           # red tank -> signal orange
        h2, s2, v2 = 24 / 360, min(1.0, s), min(1.0, v * 1.25 + 0.05)
    elif 30 <= hd <= 60:                              # brass -> ochre
        h2, s2, v2 = 38 / 360, 0.72, min(1.0, v * 1.1)
    else:
        return c
    nr, ng, nb = colorsys.hsv_to_rgb(h2, s2, v2)
    return (round(nr * 255), round(ng * 255), round(nb * 255), a)


def main():
    meta = json.load(open(os.path.join(SRC, 'meta.json')))
    os.makedirs(OUT, exist_ok=True)
    for st in meta['states']:
        im = Image.open(os.path.join(SRC, st['name'] + '.png')).convert('RGBA')
        if st['name'] not in KEEP:
            im.putdata([px(c) for c in im.getdata()])
        im.save(os.path.join(OUT, st['name'] + '.png'))
    meta['copyright'] = meta.get('copyright', '') + '; recoloured for the IFL-17 by DarkestHour ' \
                                                    '(Tools/_DarkestHour/weapons/imperial_flamer.py)'
    with open(os.path.join(OUT, 'meta.json'), 'w') as f:
        json.dump(meta, f, indent=2)
        f.write('\n')


if __name__ == '__main__':
    main()
