#!/usr/bin/env python3
"""HUD-тема TDH «Терминал» (выбор автора 2026-11-11): слоты с рублеными углами в янтарной гамме.

Берёт текстуры темы Minimalist (рубленые углы) и недостающие — из Default, перекрашивает по яркости в градиент
тёмная умбра → янтарь (альфа сохраняется). Иконки контрабанды и стрелки-SVG копируются как есть.

Запуск из корня репозитория:  python3 Tools/_DarkestHour/ui/hud_theme.py
Выход: Resources/Textures/_DarkestHour/Interface/Terminal/
"""
import glob
import os
import shutil

from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..')
SRC = ROOT + '/Resources/Textures/Interface'
OUT = ROOT + '/Resources/Textures/_DarkestHour/Interface/Terminal'

DARK = (16, 12, 8)
MID = (92, 64, 28)
LIGHT = (232, 170, 70)
KEEP = ('ContrabandOverlay',)


def ramp(v):
    """Яркость 0..1 → цвет: тёмная умбра → коричневый → янтарь."""
    if v < 0.5:
        t = v / 0.5
        a, b = DARK, MID
    else:
        t = (v - 0.5) / 0.5
        a, b = MID, LIGHT
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def recolor(src, dst):
    im = Image.open(src).convert('RGBA')
    px = im.load()
    lo, hi = 1.0, 0.0
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a:
                v = (0.3 * r + 0.59 * g + 0.11 * b) / 255
                lo, hi = min(lo, v), max(hi, v)
    span = max(hi - lo, 1e-3)
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if not a:
                continue
            # абсолютная яркость (без растяжки контраста — иначе бледные куски хранилища становятся полосатыми)
            v = min(1.0, (0.3 * r + 0.59 * g + 0.11 * b) / 255 * 1.5)
            px[x, y] = ramp(v) + (a,)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    im.save(dst)


def main():
    if os.path.isdir(OUT):
        shutil.rmtree(OUT)
    files = {}
    for base in ('Default', 'Minimalist'):          # Minimalist перекрывает Default
        for f in glob.glob('%s/%s/**/*' % (SRC, base), recursive=True):
            if os.path.isfile(f):
                files[os.path.relpath(f, '%s/%s' % (SRC, base))] = f
    for rel, f in sorted(files.items()):
        dst = os.path.join(OUT, rel)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        if rel.endswith('.png') and not any(k in rel for k in KEEP) and '.svg.' not in rel:
            recolor(f, dst)
        else:
            shutil.copy(f, dst)
    print('files', len(files))


if __name__ == '__main__':
    main()
