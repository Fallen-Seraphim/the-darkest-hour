#!/usr/bin/env python3
"""DarkestHour: флаги сторон (решение автора 2026-11-13), по образцу флагов стран CMU (cmss13 plantable_flag).

- Соединённая Республика: белое полотнище, тонкие полосы сверху и снизу, по центру чёрные буквы «UR».
- ОФСК: красное полотнище, золотая звезда в верхнем левом углу.
- Первая Империя: чёрное полотнище, золотой двуглавый орёл с мечом и скипетром, под ним имперский крест —
  круг с косым крестом внутри (орёл и крест золотые).

Пишет Resources/Textures/_DarkestHour/Objects/Flags/{republic,empire,front}_flag.rsi (64×64, как в CMU): icon (флаг
на плече), deploy (воткнут, развевается — 4 кадра), inhand-left/right (рука с флагом CMU, полотнище заменено нашим).
Тот же рисунок полотнища берёт генератор терминала зоны захвата (control_points/build.py).
Запуск из корня репозитория: python3 Tools/_DarkestHour/flags/build_flags.py
"""
import json
import math
import os

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
OUT = os.path.join(ROOT, 'Resources/Textures/_DarkestHour/Objects/Flags')
CMU = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'cmu')

CLOTH = (22, 16)
WHITE = (236, 236, 230, 255)
BLACK = (22, 22, 24, 255)
RED = (172, 34, 34, 255)
GOLD = (222, 176, 62, 255)
SILVER = (186, 188, 198, 255)
POLE_DARK = (58, 58, 58, 255)
POLE = (106, 106, 106, 255)
POLE_LIGHT = (158, 158, 158, 255)
POLE_PALETTE = {(58, 58, 58), (106, 106, 106), (20, 20, 20), (158, 158, 158), (143, 143, 143)}

LETTERS = {
    'U': ['##..##', '##..##', '##..##', '##..##', '##..##', '##..##', '##..##', '######', '.####.'],
    'R': ['#####.', '##..##', '##..##', '##..##', '#####.', '####..', '##.##.', '##..##', '##..##'],
}
STAR = ['..#..', '#####', '.###.', '.#.#.', '#...#']
# двуглавый орёл (левая половина, правая — зеркально), 17×10
EAGLE_LEFT = ['#.....#..', '##...##..', '###.###..', '####..#..', '.####..##', '..####.##', '#..######',
              '##...####', '..#...###', '...#.#.##']
IMPERIAL_CROSS = ['.###.', '##.##', '#.#.#', '##.##', '.###.']


def stamp(img, pattern, x0, y0, color, mark='#'):
    for y, row in enumerate(pattern):
        for x, c in enumerate(row):
            if c == mark and 0 <= x0 + x < img.width and 0 <= y0 + y < img.height:
                img.putpixel((x0 + x, y0 + y), color)


def emblem_empire():
    """Орёл 17×15: корона над головами, клювы наружу, меч (серебро) и скипетр, внизу крест в круге."""
    w, h = 17, 15
    grid = [['.'] * w for _ in range(h)]
    for y, left in enumerate(EAGLE_LEFT):
        for x, c in enumerate(left + left[:-1][::-1]):
            grid[y][x] = c
    for y, row in enumerate(IMPERIAL_CROSS):
        for x, c in enumerate(row):
            if c == '#':
                grid[10 + y][6 + x] = '#'
    for y in range(8, 15):
        grid[y][3] = '+'            # клинок
    for x in (2, 3, 4):
        grid[10][x] = '+'           # гарда
    for y in range(7, 15):
        grid[y][13] = '#'           # скипетр
    for x in (12, 13, 14):
        grid[8][x] = '#'
    return [''.join(r) for r in grid]


def cloth(kind, w=CLOTH[0], h=CLOTH[1]):
    """Ровное полотнище w×h."""
    if kind == 'republic':
        img = Image.new('RGBA', (w, h), WHITE)
        for x in range(w):
            img.putpixel((x, 1), BLACK)
            img.putpixel((x, h - 2), BLACK)
        lw = 6 + 2 + 6
        x0, y0 = (w - lw) // 2, (h - 9) // 2
        stamp(img, LETTERS['U'], x0, y0, BLACK)
        stamp(img, LETTERS['R'], x0 + 8, y0, BLACK)
    elif kind == 'front':
        img = Image.new('RGBA', (w, h), RED)
        stamp(img, STAR, 2, 2, GOLD)
    elif kind == 'empire':
        img = Image.new('RGBA', (w, h), BLACK)
        e = emblem_empire()
        x0, y0 = (w - 17) // 2, max(0, (h - 15) // 2)
        stamp(img, e, x0, y0, GOLD)
        stamp(img, e, x0, y0, SILVER, mark='+')
    elif kind == 'neutral':
        img = Image.new('RGBA', (w, h), WHITE)
    else:
        raise ValueError(kind)
    return img


def waved(flat, frame, frames=4):
    """Полотнище на ветру: столбцы смещаются по синусу (у древка — меньше), складки темнее и светлее."""
    w, h = flat.size
    out = Image.new('RGBA', (w, h + 2))
    for x in range(w):
        phase = x * 0.55 - frame * 2 * math.pi / frames
        amp = 1.2 * min(1.0, x / 6)
        dy = round(math.sin(phase) * amp)
        shade = 1.0 + 0.13 * math.cos(phase) * min(1.0, x / 4)
        for y in range(h):
            r, g, b, a = flat.getpixel((x, y))
            out.putpixel((x, y + 1 + dy), (min(255, int(r * shade)), min(255, int(g * shade)),
                                           min(255, int(b * shade)), a))
    return out


def deploy_frame(kind, frame):
    """Воткнутый флаг 64×64: древко на клетке (низ — край клетки), полотнище вправо."""
    img = Image.new('RGBA', (64, 64))
    px, top, bottom = 22, 6, 47
    for y in range(top, bottom + 1):
        img.putpixel((px, y), POLE)
        img.putpixel((px + 1, y), POLE_DARK)
    for y in range(top - 3, top):
        img.putpixel((px, y), POLE_LIGHT)
        img.putpixel((px + 1, y), POLE_LIGHT)
    img.putpixel((px - 1, bottom), POLE_DARK)
    img.putpixel((px + 2, bottom), POLE_DARK)
    img.alpha_composite(waved(cloth(kind), frame), (px + 2, top - 1))
    return img


def icon(kind):
    """Флаг на плече: тот же флаг, наклонённый по диагонали, как в CMU."""
    flat = deploy_frame(kind, 0)
    return flat.rotate(-35, resample=Image.NEAREST, center=(32, 40), translate=(4, -6))


def inhand(kind, side):
    """Рука с флагом из CMU (флаг UPP): полотнище каждого кадра заменено нашим рисунком, натянутым на его габарит
    (у древка — край у древка), складки — по яркости исходного полотнища."""
    src = Image.open(os.path.join(CMU, f'upp_inhand-{side}.png')).convert('RGBA')
    flat = cloth(kind)
    out = src.copy()
    for cy in range(0, src.height, 64):
        for cx in range(0, src.width, 64):
            cells, poles = [], []
            for y in range(cy, cy + 64):
                for x in range(cx, cx + 64):
                    r, g, b, a = src.getpixel((x, y))
                    if a == 0:
                        continue
                    (poles if (r, g, b) in POLE_PALETTE else cells).append((x, y))
            if not cells:
                continue
            x0 = min(p[0] for p in cells)
            x1 = max(p[0] for p in cells)
            y0 = min(p[1] for p in cells)
            y1 = max(p[1] for p in cells)
            pole_x = sum(p[0] for p in poles) / len(poles) if poles else x0
            # у древка — край у древка; у Республики буквы не зеркалим, чтобы «UR» читалось с обеих сторон
            flip = pole_x > (x0 + x1) / 2 and kind != 'republic'
            for x, y in cells:
                u = (x - x0) / max(1, x1 - x0)
                v = (y - y0) / max(1, y1 - y0)
                if flip:
                    u = 1 - u
                fx = min(flat.width - 1, int(u * flat.width))
                fy = min(flat.height - 1, int(v * flat.height))
                r, g, b, a = src.getpixel((x, y))
                k = max(0.65, min(1.2, (r + g + b) / (136 + 38 + 38)))
                cr, cg, cb, _ = flat.getpixel((fx, fy))
                out.putpixel((x, y), (min(255, int(cr * k)), min(255, int(cg * k)), min(255, int(cb * k)), a))
    return out


def sheet(frames, cols):
    w, h = frames[0].size
    rows = (len(frames) + cols - 1) // cols
    out = Image.new('RGBA', (w * cols, h * rows))
    for i, f in enumerate(frames):
        out.paste(f, ((i % cols) * w, (i // cols) * h))
    return out


def main():
    for kind in ('republic', 'empire', 'front'):
        d = os.path.join(OUT, f'{kind}_flag.rsi')
        os.makedirs(d, exist_ok=True)
        icon(kind).save(os.path.join(d, 'icon.png'))
        sheet([deploy_frame(kind, f) for f in range(4)], 4).save(os.path.join(d, 'deploy.png'))
        for side in ('left', 'right'):
            inhand(kind, side).save(os.path.join(d, f'inhand-{side}.png'))
        delays = [0.5] * 4
        meta = {
            'version': 1, 'license': 'CC-BY-SA-3.0',
            'copyright': 'Drawn for The Darkest Hour (Tools/_DarkestHour/flags/build_flags.py) after the plantable '
                         'flags of CMU/cmss13; in-hand sprites recoloured from CMU upp_flag.rsi, taken from cmss13 '
                         '(items_lefthand_64.dmi, items_righthand_64.dmi)',
            'size': {'x': 64, 'y': 64},
            'states': [{'name': 'icon'}, {'name': 'deploy', 'delays': [delays]},
                       {'name': 'inhand-left', 'directions': 4, 'delays': [delays] * 4},
                       {'name': 'inhand-right', 'directions': 4, 'delays': [delays] * 4}],
        }
        with open(os.path.join(d, 'meta.json'), 'w') as f:
            json.dump(meta, f, indent=4)
    print('флаги:', OUT)


if __name__ == '__main__':
    main()
