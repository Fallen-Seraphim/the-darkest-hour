"""Sprites of the implants (Siege plan item 29): a sealed bio-capsule with a coloured band and an indicator per kind.

Run from the repository root:  python3 Tools/_DarkestHour/implants/build.py"""
import json
import os
from PIL import Image, ImageDraw

OUT = 'Resources/Textures/_DarkestHour/Objects/Specific/Medical/implants.rsi'

OUTLINE = (24, 26, 30, 255)
SHELL = (150, 156, 164, 255)
SHELL_L = (196, 202, 210, 255)
SHELL_D = (104, 110, 118, 255)

KINDS = {
    'base': (120, 124, 130, 255),
    'pulsar': (210, 50, 56, 255),
    'echo': (70, 150, 220, 255),
    'scale': (150, 120, 70, 255),
    'silence': (130, 90, 190, 255),
    'filter': (90, 180, 120, 255),
    'hemostat': (170, 30, 40, 255),
    'tactic': (230, 170, 40, 255),
}


def capsule(band):
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((7, 12, 25, 20), radius=4, fill=SHELL, outline=OUTLINE)
    d.line((10, 13, 22, 13), SHELL_L)
    d.line((10, 19, 22, 19), SHELL_D)
    d.rectangle((14, 12, 18, 20), fill=band, outline=OUTLINE)
    d.point((16, 15), (255, 255, 255, 255))
    for x in (9, 23):                     # contacts
        d.point((x, 16), (220, 190, 90, 255))
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    meta = {'version': 1, 'license': 'CC-BY-SA-3.0',
            'copyright': 'Made for The Darkest Hour (Tools/_DarkestHour/implants/build.py)',
            'size': {'x': 32, 'y': 32}, 'states': []}
    for name, band in KINDS.items():
        capsule(band).save(os.path.join(OUT, name + '.png'))
        meta['states'].append({'name': name})
    with open(os.path.join(OUT, 'meta.json'), 'w', encoding='utf-8') as f:
        json.dump(meta, f, indent=4)
        f.write('\n')


if __name__ == '__main__':
    main()
