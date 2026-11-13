#!/usr/bin/env python3
"""DarkestHour: терминал зоны захвата (Resources/Textures/_DarkestHour/Structures/control_point.rsi).

Мачта с флагом над полевым терминалом, 32×64: состояния base (терминал и мачта), flag_neutral, flag_republic,
flag_empire, flag_front (флаг развевается — 4 кадра), screen (огонёк экрана). Рисуется процедурно. CC-BY-SA-3.0.
Флаги сторон — те же, что у переносных флагов (Tools/_DarkestHour/flags/build_flags.py, решение автора 2026-11-13).
"""
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'flags'))
import build_flags  # noqa: E402

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
OUT = os.path.join(ROOT, 'Resources/Textures/_DarkestHour/Structures/control_point.rsi')
W, H = 32, 64

DARK = (46, 50, 54, 255)
METAL = (104, 110, 116, 255)
LIGHT = (150, 156, 162, 255)
POLE = (180, 184, 188, 255)


MAST_X = 8


def base():
    img = Image.new('RGBA', (W, H))
    d = ImageDraw.Draw(img)
    # мачта — у левого края терминала, полотнище вправо
    d.rectangle([MAST_X, 4, MAST_X + 1, 45], fill=POLE)
    d.point((MAST_X, 3), fill=LIGHT)
    d.point((MAST_X + 1, 3), fill=LIGHT)
    # терминал
    d.rectangle([8, 44, 23, 58], fill=METAL, outline=DARK)
    d.rectangle([10, 46, 21, 52], fill=DARK)
    d.rectangle([9, 58, 22, 61], fill=DARK)
    d.line([10, 55, 21, 55], fill=LIGHT)
    return img


def flag(kind, frame):
    img = Image.new('RGBA', (W, H))
    img.alpha_composite(build_flags.waved(build_flags.cloth(kind), frame), (MAST_X + 2, 3))
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

    for kind in ('neutral', 'republic', 'empire', 'front'):
        sheet([flag(kind, f) for f in range(4)]).save(os.path.join(OUT, f'flag_{kind}.png'))
        states.append({'name': f'flag_{kind}', 'delays': [[0.5, 0.5, 0.5, 0.5]]})

    screen((120, 220, 140, 255)).save(os.path.join(OUT, 'screen.png'))
    states.append({'name': 'screen'})

    icon = base()
    icon.alpha_composite(flag('neutral', 0))
    icon.save(os.path.join(OUT, 'icon.png'))
    states.append({'name': 'icon'})

    meta = {'version': 1, 'license': 'CC-BY-SA-3.0',
            'copyright': 'Procedurally drawn for The Darkest Hour (Tools/_DarkestHour/control_points/build.py)',
            'size': {'x': W, 'y': H}, 'states': states}
    with open(os.path.join(OUT, 'meta.json'), 'w') as f:
        json.dump(meta, f, indent=4)


if __name__ == '__main__':
    main()
