#!/usr/bin/env python3
"""DarkestHour: плиты крыш, которые видит пилот сверху (решение автора 2026-11-13): металл, шифер, бетон. 32×32,
бесшовные. Запуск из корня репозитория: python3 Tools/_DarkestHour/roofs/roof_sprites.py"""
import json
import os
import random
from PIL import Image

OUT = 'Resources/Textures/_DarkestHour/Effects/roofs.rsi'


def clamp(v):
    return max(0, min(255, int(v)))


def metal(rng):
    im = Image.new('RGBA', (32, 32))
    px = im.load()
    for x in range(32):
        phase = x % 4                       # профнастил: гребень, скат, впадина, скат
        base = {0: 150, 1: 128, 2: 104, 3: 122}[phase]
        for y in range(32):
            n = rng.randint(-5, 5)
            seam = -18 if y in (0, 16) else 0  # стык листов
            v = base + n + seam
            px[x, y] = (clamp(v * 0.93), clamp(v * 0.97), clamp(v * 1.02), 255)
    return im


def slate(rng):
    im = Image.new('RGBA', (32, 32))
    px = im.load()
    for y in range(32):
        row = y // 4
        offset = 4 if row % 2 else 0
        for x in range(32):
            tile = ((x + offset) // 8, row)
            r = random.Random(tile[0] * 31 + tile[1] * 7 + 3)
            v = 70 + r.randint(-8, 8) + rng.randint(-3, 3)
            if y % 4 == 3:                  # тень под рядом плиток
                v -= 22
            if (x + offset) % 8 == 0:       # шов между плитками
                v -= 14
            px[x, y] = (clamp(v * 0.95), clamp(v * 0.95), clamp(v * 1.08), 255)
    return im


def concrete(rng):
    im = Image.new('RGBA', (32, 32))
    px = im.load()
    for y in range(32):
        for x in range(32):
            v = 158 + rng.randint(-9, 9)
            if x in (0, 31) or y in (0, 31):   # швы между плитами
                v -= 20
            if rng.random() < 0.02:            # пятна и выбоины
                v -= 30
            px[x, y] = (clamp(v), clamp(v * 0.98), clamp(v * 0.94), 255)
    return im


def main():
    os.makedirs(OUT, exist_ok=True)
    rng = random.Random(20261113)
    states = {'metal': metal(rng), 'slate': slate(rng), 'concrete': concrete(rng)}
    for name, im in states.items():
        im.save(f'{OUT}/{name}.png')
    json.dump({'version': 1, 'license': 'CC-BY-SA-3.0', 'copyright': 'Drawn by Claude (Anthropic) for The Darkest Hour',
               'size': {'x': 32, 'y': 32}, 'states': [{'name': n} for n in states]},
              open(f'{OUT}/meta.json', 'w'), indent=2)


if __name__ == '__main__':
    main()
