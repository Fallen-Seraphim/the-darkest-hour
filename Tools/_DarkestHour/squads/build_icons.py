#!/usr/bin/env python3
"""DarkestHour: значки отрядов в худе (8x8) и нашивка отряда на рукаве (решение автора 2026-11-19).

Значок — плашка цвета отряда с буквой отряда; у командира отряда — латунная рамка. Нашивка — белый лоскут на левом
плече, клиент красит его в цвет отряда (DHSquadVisualsSystem).
Запуск из корня репозитория: python3 Tools/_DarkestHour/squads/build_icons.py
"""
import json
import os
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
ICONS = os.path.join(ROOT, 'Resources/Textures/_DarkestHour/Interface/Misc/squad_icons.rsi')
PATCH = os.path.join(ROOT, 'Resources/Textures/_DarkestHour/Mobs/squad_patch.rsi')

# отряд: (цвет, буква 5x5)
SQUADS = {
    'Alpha': ('5fb0ff', ['.###.', '#...#', '#####', '#...#', '#...#']),
    'Bravo': ('ffb347', ['####.', '#...#', '####.', '#...#', '####.']),
    'Charlie': ('c77dff', ['.####', '#....', '#....', '#....', '.####']),
    'Delta': ('5fe07a', ['####.', '#...#', '#...#', '#...#', '####.']),
    'Tau': ('e8604a', ['#####', '..#..', '..#..', '..#..', '..#..']),
    'Epsilon': ('4ad0e8', ['#####', '#....', '####.', '#....', '#####']),
    'Omega': ('e8c84a', ['.###.', '#...#', '#...#', '.#.#.', '##.##']),
}
INK = (20, 20, 22)
EDGE = (60, 62, 66)
BRASS = (214, 176, 84)


def hexc(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def icon(color, glyph, leader):
    fill = hexc(color)
    frame = BRASS if leader else EDGE
    img = Image.new('RGBA', (8, 8), (0, 0, 0, 0))
    px = img.load()
    for y in range(8):
        for x in range(8):
            if x in (0, 7) and y in (0, 7):
                continue
            px[x, y] = (frame if x in (0, 7) or y in (0, 7) else fill) + (255,)
    for j, row in enumerate(glyph):
        for i, ch in enumerate(row):
            if ch == '#':
                px[2 + i, 2 + j] = INK + (255,)
    return img


def patch():
    """Лоскут 2x2 на левом плече: юг, север, восток, запад (порядок направлений RSI)."""
    img = Image.new('RGBA', (32 * 4, 32), (0, 0, 0, 0))
    px = img.load()
    spots = [(21, 16), (8, 16), (15, 16), (15, 16)]  # S, N, E, W — левое плечо персонажа
    for d, (x, y) in enumerate(spots):
        if d == 3:
            continue  # на западе левое плечо скрыто телом
        for dx in range(2):
            for dy in range(2):
                px[32 * d + x + dx, y + dy] = (255, 255, 255, 255)
        # тёмный край снизу, чтобы лоскут читался на светлой форме
        for dx in range(2):
            px[32 * d + x + dx, y + 2] = (40, 40, 40, 160)
    return img


def main():
    os.makedirs(ICONS, exist_ok=True)
    states = []
    for name, (color, glyph) in SQUADS.items():
        for leader in (False, True):
            state = name + ('Leader' if leader else '')
            icon(color, glyph, leader).save(os.path.join(ICONS, state + '.png'))
            states.append({'name': state})
    with open(os.path.join(ICONS, 'meta.json'), 'w', encoding='utf-8') as f:
        json.dump({'version': 1, 'license': 'CC-BY-SA-3.0',
                   'copyright': 'Made for The Darkest Hour (Tools/_DarkestHour/squads/build_icons.py)',
                   'size': {'x': 8, 'y': 8}, 'states': states}, f, indent=4, ensure_ascii=False)
        f.write('\n')

    os.makedirs(PATCH, exist_ok=True)
    sheet = patch()
    # RSI с 4 направлениями: кадры в одну строку
    sheet.save(os.path.join(PATCH, 'patch.png'))
    with open(os.path.join(PATCH, 'meta.json'), 'w', encoding='utf-8') as f:
        json.dump({'version': 1, 'license': 'CC-BY-SA-3.0',
                   'copyright': 'Made for The Darkest Hour (Tools/_DarkestHour/squads/build_icons.py)',
                   'size': {'x': 32, 'y': 32}, 'states': [{'name': 'patch', 'directions': 4}]}, f, indent=4,
                  ensure_ascii=False)
        f.write('\n')


if __name__ == '__main__':
    main()
