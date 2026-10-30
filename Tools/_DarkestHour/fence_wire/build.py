"""Колючая проволока поверх сетки-рабицы: накладка по форме каждого кадра забора (fence.rsi, 4 направления).

Широкий кадр (панель сетки) — спираль колючки вдоль верхней кромки; узкий (столб/торец) — спираль вокруг столба.
Запуск: python3 Tools/_DarkestHour/fence_wire/build.py (из корня репозитория).
"""
import json
import math
import os

from PIL import Image

SRC = 'Resources/Textures/Structures/Walls/fence.rsi'
OUT = 'Resources/Textures/_DarkestHour/Structures/fence_wire.rsi'
STATES = ['straight', 'corner', 'end']
DARK = (70, 72, 74, 255)
MID = (125, 128, 130, 255)
LIGHT = (185, 188, 190, 255)


def put(img, x, y, c):
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), c)


def frame_overlay(frame):
    w, h = frame.size
    alpha = frame.split()[3]
    mask = [[alpha.getpixel((x, y)) > 0 for x in range(w)] for y in range(h)]
    cols = [x for x in range(w) if any(mask[y][x] for y in range(h))]
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    if not cols:
        return out

    # Узкие участки — столбы: колючка обвивает их по всей высоте.
    runs = []
    start = prev = None
    for x in cols:
        if start is None:
            start = prev = x
        elif x == prev + 1:
            prev = x
        else:
            runs.append((start, prev))
            start = prev = x
    runs.append((start, prev))

    for a, b in runs:
        if b - a + 1 <= 5:
            cx = (a + b) // 2
            rows = [y for y in range(h) if any(mask[y][x] for x in range(a, b + 1))]
            for y in range(min(rows), max(rows) + 1):
                dx = round(2.2 * math.sin(y * 0.85))
                put(out, cx + dx, y, MID if dx >= 0 else DARK)
                if y % 4 == 0:
                    put(out, cx - 3, y, LIGHT)
                    put(out, cx + 3, y, LIGHT)
            continue

        # Панель: спираль над верхней кромкой каждой колонки.
        for x in range(a, b + 1):
            top = min(y for y in range(h) if mask[y][x])
            # Спираль лежит на верхней жерди (над ней в кадре места нет — панель упирается в край тайла).
            base = max(top, 2) + 1
            dy = round(1.6 * math.sin(x * 0.9))
            put(out, x, base + dy, DARK)
            put(out, x, base - dy, MID)
            if x % 3 == 0:
                put(out, x, base - 2, DARK)
                put(out, x, base + 3, DARK)
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    meta = {
        'version': 1,
        'license': 'CC-BY-SA-3.0',
        'copyright': 'Barbed wire overlay drawn procedurally for The Darkest Hour (Tools/_DarkestHour/fence_wire) '
                     'from the shape of the chain link fence sprites (Structures/Walls/fence.rsi).',
        'size': {'x': 32, 'y': 32},
        'states': [],
    }
    for state in STATES:
        sheet = Image.open(os.path.join(SRC, state + '.png')).convert('RGBA')
        out = Image.new('RGBA', sheet.size, (0, 0, 0, 0))
        for fy in range(0, sheet.height, 32):
            for fx in range(0, sheet.width, 32):
                out.paste(frame_overlay(sheet.crop((fx, fy, fx + 32, fy + 32))), (fx, fy))
        out.save(os.path.join(OUT, state + '.png'))
        meta['states'].append({'name': state, 'directions': 4})
    with open(os.path.join(OUT, 'meta.json'), 'w') as f:
        json.dump(meta, f, indent=2)


if __name__ == '__main__':
    main()
