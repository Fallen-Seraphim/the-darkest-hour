#!/usr/bin/env python3
"""DarkestHour: терминал зоны захвата (Resources/Textures/_DarkestHour/Structures/control_point.rsi).

Мачта с флагом над полевым терминалом, 32×64: состояния base (терминал и мачта), flag_neutral, flag_republic,
flag_empire (флаг развевается — 3 кадра), screen (огонёк экрана). Рисуется процедурно. CC-BY-SA-3.0.
"""
import json
import os

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
OUT = os.path.join(ROOT, 'Resources/Textures/_DarkestHour/Structures/control_point.rsi')
W, H = 32, 64

DARK = (46, 50, 54, 255)
METAL = (104, 110, 116, 255)
LIGHT = (150, 156, 162, 255)
POLE = (180, 184, 188, 255)


def base():
    img = Image.new('RGBA', (W, H))
    d = ImageDraw.Draw(img)
    # мачта
    d.rectangle([15, 6, 16, 50], fill=POLE)
    d.point((15, 5), fill=LIGHT)
    # терминал
    d.rectangle([8, 44, 23, 58], fill=METAL, outline=DARK)
    d.rectangle([10, 46, 21, 52], fill=DARK)
    d.rectangle([9, 58, 22, 61], fill=DARK)
    d.line([10, 55, 21, 55], fill=LIGHT)
    return img


def flag(colors, emblem, wave):
    img = Image.new('RGBA', (W, H))
    d = ImageDraw.Draw(img)
    top = 7
    for x in range(17, 30):
        dy = [0, 0, 1, 1, 0, 0, -1, -1][(x + wave * 2) % 8] if x > 19 else 0
        for y in range(top, top + 11):
            band = (y - top) * len(colors) // 11
            d.point((x, y + dy), fill=colors[band])
    if emblem:
        dy = [0, 0, 1, 1, 0, 0, -1, -1][(23 + wave * 2) % 8]
        d.rectangle([22, top + 4 + dy, 24, top + 6 + dy], fill=emblem)
    return img


def screen(color):
    img = Image.new('RGBA', (W, H))
    d = ImageDraw.Draw(img)
    d.rectangle([11, 47, 20, 51], fill=color)
    return img


def sheet(frames):
    out = Image.new('RGBA', (W * len(frames), H))
    for i, f in enumerate(frames):
        out.paste(f, (i * W, 0))
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    states = []
    base().save(os.path.join(OUT, 'base.png'))
    states.append({'name': 'base'})

    flags = {
        'flag_neutral': ([(220, 220, 220, 255)], None),
        'flag_republic': ([(78, 107, 46, 255), (126, 160, 72, 255), (78, 107, 46, 255)], (232, 214, 120, 255)),
        'flag_empire': ([(28, 28, 30, 255), (236, 236, 236, 255), (28, 28, 30, 255)], (200, 40, 40, 255)),
    }
    for name, (colors, emblem) in flags.items():
        sheet([flag(colors, emblem, w) for w in range(3)]).save(os.path.join(OUT, name + '.png'))
        states.append({'name': name, 'delays': [[0.4, 0.4, 0.4]]})

    screen((120, 220, 140, 255)).save(os.path.join(OUT, 'screen.png'))
    states.append({'name': 'screen'})

    icon = base()
    icon.alpha_composite(flag(flags['flag_neutral'][0], None, 0))
    icon.save(os.path.join(OUT, 'icon.png'))
    states.append({'name': 'icon'})

    meta = {'version': 1, 'license': 'CC-BY-SA-3.0',
            'copyright': 'Procedurally drawn for The Darkest Hour (Tools/_DarkestHour/control_points/build.py)',
            'size': {'x': W, 'y': H}, 'states': states}
    with open(os.path.join(OUT, 'meta.json'), 'w') as f:
        json.dump(meta, f, indent=4)


if __name__ == '__main__':
    main()
