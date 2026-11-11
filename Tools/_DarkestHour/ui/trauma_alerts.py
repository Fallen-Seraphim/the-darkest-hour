#!/usr/bin/env python3
"""Иконки алертов травм TDH (решение автора 2026-11-11): боль (4 степени) и перелом (наложена шина / нет).
Рисуются попиксельно 32×32 с тёмной обводкой, как ванильные алерты. Лицензия — CC-BY-SA-3.0 (свои рисунки).
Запуск из корня репозитория: python3 Tools/_DarkestHour/ui/trauma_alerts.py"""
import json
import math
import os

from PIL import Image, ImageDraw

OUT = 'Resources/Textures/_DarkestHour/Interface/Alerts'
S = 32
OUTLINE = (20, 12, 8, 255)


def outline(img):
    """Обводка в 1 пиксель вокруг непрозрачного."""
    src = img.copy()
    px, out = src.load(), img.load()
    for y in range(S):
        for x in range(S):
            if px[x, y][3] > 0:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < S and 0 <= ny < S and px[nx, ny][3] > 0:
                    out[x, y] = OUTLINE
                    break
    return img


def burst(points, r_out, r_in, rot=0.0):
    c = (S - 1) / 2
    pts = []
    for i in range(points * 2):
        r = r_out if i % 2 == 0 else r_in
        a = rot + math.pi * i / points
        pts.append((c + r * math.cos(a), c + r * math.sin(a)))
    return pts


def pain(level):
    """Боль: колючая вспышка; чем сильнее — тем больше лучей, крупнее и краснее."""
    colors = [((240, 200, 70), (255, 240, 170)), ((240, 140, 40), (255, 210, 120)),
              ((220, 50, 30), (255, 150, 90)), ((150, 10, 20), (230, 60, 50))]
    outer, inner = colors[level]
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rays = [6, 7, 8, 10][level]
    r = [10, 11.5, 13, 14][level]
    d.polygon(burst(rays, r, r * 0.45, 0.2), fill=outer + (255,))
    d.polygon(burst(rays, r * 0.55, r * 0.25, 0.2 + math.pi / rays), fill=inner + (255,))
    if level == 3:  # шок — молния поперёк
        d.line([(10, 8), (16, 15), (13, 17), (21, 25)], fill=(255, 250, 230, 255), width=2)
    return outline(img)


def bone(img, color):
    d = ImageDraw.Draw(img)
    # диагональная кость: стержень и две пары «шишек» на концах
    d.line([(9, 23), (23, 9)], fill=color, width=5)
    for cx, cy in ((7, 22), (10, 25), (22, 7), (25, 10)):
        d.ellipse([cx - 3, cy - 3, cx + 3, cy + 3], fill=color)


def fracture(splinted):
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    bone(img, (236, 228, 205, 255))
    d = ImageDraw.Draw(img)
    # трещина поперёк середины
    crack = [(12, 13), (15, 15), (14, 17), (17, 19), (19, 19)]
    d.line(crack, fill=(200, 30, 25, 255), width=2)
    if splinted:  # шина: две коричневые повязки
        for t in (0.32, 0.68):
            x, y = 9 + 14 * t, 23 - 14 * t
            d.line([(x - 5, y - 5), (x + 5, y + 5)], fill=(150, 105, 55, 255), width=3)
    return outline(img)


def main():
    for name, states in (('pain', [('pain%d' % i, pain(i)) for i in range(4)]),
                         ('fracture', [('fracture', fracture(False)), ('splinted', fracture(True))])):
        rsi = os.path.join(OUT, name + '.rsi')
        os.makedirs(rsi, exist_ok=True)
        for state, img in states:
            img.save(os.path.join(rsi, state + '.png'))
        meta = {'version': 1, 'license': 'CC-BY-SA-3.0',
                'copyright': 'Made for The Darkest Hour (Tools/_DarkestHour/ui/trauma_alerts.py)',
                'size': {'x': S, 'y': S}, 'states': [{'name': s} for s, _ in states]}
        json.dump(meta, open(os.path.join(rsi, 'meta.json'), 'w'), indent=2)


if __name__ == '__main__':
    main()
