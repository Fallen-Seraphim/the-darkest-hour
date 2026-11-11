#!/usr/bin/env python3
"""Иконки алертов TDH (решение автора 2026-11-11): травмы, лекарства, стационар, облучение, связь.
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


def new():
    return Image.new('RGBA', (S, S), (0, 0, 0, 0))


def drop(d, cx, cy, r, color):
    """Капля: круг снизу и треугольник сверху."""
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=color)
    d.polygon([(cx - r + 1, cy - 1), (cx + r - 1, cy - 1), (cx, cy - r * 2.2)], fill=color)


def body(d, color):
    """Силуэт торса с головой."""
    d.ellipse([12, 3, 20, 11], fill=color)
    d.rounded_rectangle([8, 12, 24, 29], radius=4, fill=color)


def internal_bleeding(level):
    img = new()
    d = ImageDraw.Draw(img)
    body(d, (205, 190, 170, 255))
    for i in range(level + 1):
        drop(d, [16, 12, 20][i], [22, 20, 20][i], 3, (190, 20, 25, 255))
    return outline(img)


def shrapnel(level):
    img = new()
    d = ImageDraw.Draw(img)
    shards = [[(8, 22), (14, 12), (16, 24)], [(17, 8), (25, 12), (19, 17)], [(18, 22), (27, 20), (22, 28)],
              [(5, 9), (11, 6), (9, 14)]]
    for k in range(level + 2):
        d.polygon(shards[k], fill=(170, 175, 182, 255))
        a, b, _ = shards[k]
        d.line([a, b], fill=(235, 238, 242, 255))
    d.ellipse([13, 14, 18, 19], fill=(180, 25, 25, 255))
    return outline(img)


def tourniquet():
    img = new()
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([12, 2, 20, 30], radius=3, fill=(220, 190, 160, 255))  # конечность
    d.rectangle([9, 13, 23, 18], fill=(190, 30, 30, 255))  # жгут
    d.rectangle([21, 11, 26, 20], fill=(60, 60, 60, 255))  # закрутка
    d.line([(9, 15), (23, 15)], fill=(240, 90, 80, 255))
    return outline(img)


def organ_failure():
    img = new()
    d = ImageDraw.Draw(img)
    # почка/орган: бобовидная форма
    d.ellipse([7, 6, 25, 27], fill=(150, 60, 70, 255))
    d.ellipse([18, 13, 27, 20], fill=(0, 0, 0, 0))
    d.line([(11, 9), (16, 15), (12, 19), (17, 25)], fill=(30, 10, 10, 255), width=2)
    return outline(img)


def pill(d, color, x0=6, y0=12):
    d.rounded_rectangle([x0, y0, x0 + 20, y0 + 9], radius=4, fill=color)
    d.rectangle([x0 + 10, y0, x0 + 20, y0 + 9], fill=(240, 240, 240, 255))
    d.rounded_rectangle([x0 + 10, y0, x0 + 20, y0 + 9], radius=4, fill=(240, 240, 240, 255))
    d.rectangle([x0 + 10, y0, x0 + 13, y0 + 9], fill=(240, 240, 240, 255))


def painkiller():
    img = new()
    d = ImageDraw.Draw(img)
    d.polygon(burst(6, 9, 4, 0.2), fill=(230, 160, 60, 110))
    pill(d, (70, 140, 230, 255), 6, 18)
    return outline(img)


def cross(d, color, cx=16, cy=16, a=11, w=4):
    d.rectangle([cx - w, cy - a, cx + w, cy + a], fill=color)
    d.rectangle([cx - a, cy - w, cx + a, cy + w], fill=color)


def omnireazin(rebound):
    img = new()
    d = ImageDraw.Draw(img)
    cross(d, (120, 210, 160, 255))
    cross(d, (190, 245, 210, 255), a=8, w=2)
    if rebound:
        d.polygon([(x + 6, y + 6) for x, y in burst(6, 7, 3, 0.3)], fill=(230, 60, 40, 255))
    return outline(img)


def phantom():
    img = new()
    d = ImageDraw.Draw(img)
    d.polygon(burst(7, 12, 5, 0.1), fill=(160, 120, 210, 150))
    d.polygon(burst(7, 6, 3, 0.55), fill=(220, 200, 255, 200))
    return outline(img)


def withdrawal(kind):
    color = {'opioid': (210, 205, 225, 255), 'sedative': (120, 160, 235, 255), 'stimulant': (240, 200, 60, 255)}[kind]
    img = new()
    d = ImageDraw.Draw(img)
    pill(d, color, 6, 11)
    # «дрожь» и запрет
    for y in (6, 26):
        d.line([(6, y), (10, y - 2), (14, y), (18, y - 2), (22, y), (26, y - 2)], fill=(230, 80, 60, 255), width=2)
    return outline(img)


def lungs(d, color):
    d.ellipse([5, 8, 15, 28], fill=color)
    d.ellipse([17, 8, 27, 28], fill=color)
    d.rectangle([15, 3, 17, 14], fill=color)


def respiratory():
    img = new()
    d = ImageDraw.Draw(img)
    lungs(d, (120, 150, 210, 255))
    d.polygon([(11, 15), (21, 15), (16, 24)], fill=(220, 40, 40, 255))  # стрелка вниз
    return outline(img)


def overdose():
    img = new()
    d = ImageDraw.Draw(img)
    pill(d, (210, 60, 60, 255), 6, 17)
    d.ellipse([11, 2, 21, 12], fill=(235, 230, 220, 255))  # череп
    d.rectangle([13, 11, 19, 14], fill=(235, 230, 220, 255))
    d.point([(14, 7), (18, 7)], fill=(30, 20, 20, 255))
    d.rectangle([13, 6, 14, 8], fill=(30, 20, 20, 255))
    d.rectangle([18, 6, 19, 8], fill=(30, 20, 20, 255))
    return outline(img)


def heart(d, color, cx=16, cy=15, r=6):
    d.ellipse([cx - r * 2 + 1, cy - r, cx + 1, cy + r - 1], fill=color)
    d.ellipse([cx - 1, cy - r, cx + r * 2 - 1, cy + r - 1], fill=color)
    d.polygon([(cx - r * 2 + 1, cy + 1), (cx + r * 2 - 1, cy + 1), (cx, cy + r * 2 + 1)], fill=color)


def machine(kind):
    img = new()
    d = ImageDraw.Draw(img)
    if kind == 'ventilator':
        lungs(d, (120, 170, 220, 255))
        d.line([(16, 3), (16, 0)], fill=(200, 200, 200, 255), width=2)
    else:
        heart(d, (200, 50, 60, 255))
    d.line([(2, 26), (9, 26), (11, 21), (14, 30), (17, 24), (19, 26), (30, 26)], fill=(90, 230, 120, 255), width=2)
    return outline(img)


def clinical_death(perfused):
    img = new()
    d = ImageDraw.Draw(img)
    heart(d, (200, 50, 60, 255) if perfused else (120, 110, 115, 255))
    line = [(2, 26), (10, 26), (12, 21), (15, 30), (18, 24), (20, 26), (30, 26)] if perfused else [(2, 26), (30, 26)]
    d.line(line, fill=(90, 230, 120, 255) if perfused else (230, 60, 50, 255), width=2)
    return outline(img)


def trefoil(level, color):
    img = new()
    d = ImageDraw.Draw(img)
    c = (S - 1) / 2
    r = [8, 10, 12, 14][level]
    d.ellipse([c - r - 1, c - r - 1, c + r + 1, c + r + 1], fill=(30, 25, 10, 255))
    for k in range(3):
        a0 = -90 + k * 120 - 30
        d.pieslice([c - r, c - r, c + r, c + r], a0, a0 + 60, fill=color)
    d.ellipse([c - 2, c - 2, c + 2, c + 2], fill=color)
    return outline(img)


def crystal(level):
    img = new()
    d = ImageDraw.Draw(img)
    shades = [(170, 120, 230, 255), (160, 90, 230, 255), (145, 60, 220, 255), (130, 30, 210, 255)][level]
    n = level + 1
    for k in range(n):
        x = 16 + (k - (n - 1) / 2) * 6
        h = 18 + (2 - abs(k - (n - 1) / 2)) * 2
        d.polygon([(x - 3, 28), (x + 3, 28), (x + 3, 28 - h + 4), (x, 28 - h), (x - 3, 28 - h + 4)], fill=shades)
        d.line([(x, 28 - h + 1), (x, 26)], fill=(225, 200, 255, 255))
    return outline(img)


def no_signal():
    img = new()
    d = ImageDraw.Draw(img)
    d.line([(16, 30), (16, 10)], fill=(190, 190, 190, 255), width=2)
    for r in (5, 9, 13):
        d.arc([16 - r, 10 - r, 16 + r, 10 + r], 220, 320, fill=(190, 190, 190, 255), width=2)
    d.line([(6, 6), (26, 26)], fill=(220, 40, 40, 255), width=3)
    return outline(img)


def load(level):
    """Перегруз: гиря; жёлтая — тяжело, оранжевая — рывка нет, красная — полный перегруз."""
    color = [(230, 200, 60, 255), (235, 130, 40, 255), (210, 40, 30, 255)][level]
    img = new()
    d = ImageDraw.Draw(img)
    d.ellipse([10, 5, 22, 15], outline=color, width=3)  # дужка
    d.polygon([(8, 14), (24, 14), (27, 27), (5, 27)], fill=color)
    d.rectangle([12, 18, 20, 21], fill=(40, 30, 20, 255))  # «кг»
    for i in range(level):  # стрелки вниз — степень
        x = 9 + i * 12
        d.polygon([(x, 29), (x + 4, 29), (x + 2, 31)], fill=(255, 255, 255, 255))
    return outline(img)


ICONS = {
    'internal_bleeding': [('ib%d' % i, lambda i=i: internal_bleeding(i)) for i in range(3)],
    'shrapnel': [('shrapnel%d' % i, lambda i=i: shrapnel(i)) for i in range(3)],
    'tourniquet': [('icon', tourniquet)],
    'organ': [('failure', organ_failure)],
    'painkiller': [('icon', painkiller)],
    'omnireazin': [('active', lambda: omnireazin(False)), ('rebound', lambda: omnireazin(True))],
    'phantom': [('icon', phantom)],
    'withdrawal': [(k, lambda k=k: withdrawal(k)) for k in ('opioid', 'sedative', 'stimulant')],
    'respiratory': [('depressed', respiratory)],
    'overdose': [('icon', overdose)],
    'machines': [('ventilator', lambda: machine('ventilator')), ('lifesupport', lambda: machine('lifesupport'))],
    'clinical_death': [('countdown', lambda: clinical_death(False)), ('perfused', lambda: clinical_death(True))],
    'dose': [('rad%d' % i, lambda i=i: trefoil(i, (240, 210, 40, 255))) for i in range(4)]
            + [('chronite%d' % i, lambda i=i: crystal(i)) for i in range(4)],
    'comms': [('nosignal', no_signal)],
    'load': [('load%d' % i, lambda i=i: load(i)) for i in range(3)],
}


def save(name, states):
    rsi = os.path.join(OUT, name + '.rsi')
    os.makedirs(rsi, exist_ok=True)
    for state, img in states:
        img.save(os.path.join(rsi, state + '.png'))
    meta = {'version': 1, 'license': 'CC-BY-SA-3.0',
            'copyright': 'Made for The Darkest Hour (Tools/_DarkestHour/ui/trauma_alerts.py)',
            'size': {'x': S, 'y': S}, 'states': [{'name': s} for s, _ in states]}
    json.dump(meta, open(os.path.join(rsi, 'meta.json'), 'w'), indent=2)


def main():
    save('pain', [('pain%d' % i, pain(i)) for i in range(4)])
    save('fracture', [('fracture', fracture(False)), ('splinted', fracture(True))])
    for name, states in ICONS.items():
        save(name, [(state, make()) for state, make in states])


if __name__ == '__main__':
    main()
