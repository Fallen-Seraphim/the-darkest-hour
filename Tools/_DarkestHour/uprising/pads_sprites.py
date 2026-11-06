#!/usr/bin/env python3
"""Наплечники и наколенники (решение автора 2026-11-06): военпол — серо-синие, ОФСК — оливково-бурые.
Иконки и вид на персонаже рисуются здесь: на теле — по маске частей тела человека (наплечник — верх руки,
наколенник — колено). Носятся в слотах рук и ног (armLeft/… ).
Выход: Resources/Textures/_DarkestHour/Objects/Clothing/limb_pads.rsi"""
import json
import os

from PIL import Image, ImageDraw

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..')
OUT = ROOT + '/Resources/Textures/_DarkestHour/Objects/Clothing/limb_pads.rsi'

PALETTES = {
    'mp': ((52, 62, 82), (88, 104, 132), (130, 148, 176), (30, 34, 42)),
    'front': ((70, 64, 40), (108, 98, 62), (148, 138, 92), (40, 34, 26)),
}


def shade(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c) + (255,)


def knee(dr, x, y, pal):
    dark, mid, light, strap = pal
    # ремни
    dr.rectangle([x - 1, y + 3, x + 9, y + 4], fill=shade(strap, 1.0))
    dr.rectangle([x - 1, y + 9, x + 9, y + 10], fill=shade(strap, 1.0))
    # чашка
    dr.rounded_rectangle([x, y, x + 8, y + 13], radius=4, fill=shade(mid, 1.0), outline=shade(dark, 0.8))
    dr.rounded_rectangle([x + 2, y + 2, x + 6, y + 8], radius=2, fill=shade(light, 1.0))
    dr.line([x + 2, y + 11, x + 6, y + 11], fill=shade(dark, 1.0))


def shoulder(dr, x, y, pal, flip):
    dark, mid, light, strap = pal
    pts = [(x, y + 9), (x + 2, y + 2), (x + 7, y), (x + 12, y + 2), (x + 13, y + 9), (x + 10, y + 12), (x + 3, y + 12)]
    if flip:
        pts = [(2 * x + 13 - px, py) for px, py in pts]
    dr.polygon(pts, fill=shade(mid, 1.0), outline=shade(dark, 0.8))
    dr.line([(x + 3, y + 5), (x + 10, y + 5)], fill=shade(light, 1.0))
    dr.line([(x + 3, y + 8), (x + 10, y + 8)], fill=shade(dark, 1.0))
    dr.rectangle([x + 5, y + 12, x + 8, y + 14], fill=shade(strap, 1.0))


PARTS = ROOT + '/Resources/Textures/Mobs/Species/Human/parts.rsi'
FRAMES = ((0, 0), (32, 0), (0, 32), (32, 32))


def worn(part, rows, pal):
    """
    Наплечник/наколенник на теле (4 направления) по маске части тела человека: строки rows (доли высоты части,
    от, до), расширенные на пиксель наружу; контур тёмный, середина — основной цвет, верх — блик.
    """
    src = Image.open('%s/%s.png' % (PARTS, part)).convert('RGBA')
    out = Image.new('RGBA', src.size, (0, 0, 0, 0))
    dark, mid, light, strap = pal
    for fx, fy in FRAMES:
        frame = src.crop((fx, fy, fx + 32, fy + 32))
        box = frame.getbbox()
        if not box:
            continue
        h = box[3] - box[1]
        top = box[1] + int(h * rows[0])
        bottom = max(top + 2, box[1] + int(h * rows[1] + 0.5))
        px = frame.load()
        cells = {(x, y) for y in range(top, bottom) for x in range(32) if px[x, y][3]}
        grown = {(x + dx, y) for (x, y) in cells for dx in (-1, 0, 1)} | {(x, top - 1) for (x, _) in cells}
        for (x, y) in grown:
            if not (0 <= x < 32 and 0 <= y < 32):
                continue
            edge = any((x + dx, y + dy) not in grown for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            color = shade(dark, 0.8) if edge else (shade(light, 1.0) if y <= top else shade(mid, 1.0))
            out.putpixel((fx + x, fy + y), color)
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    states = []
    for name, pal in PALETTES.items():
        im = Image.new('RGBA', (32, 32))
        dr = ImageDraw.Draw(im)
        knee(dr, 6, 10, pal)
        knee(dr, 18, 10, pal)
        im.save(OUT + '/knee_%s.png' % name)
        im = Image.new('RGBA', (32, 32))
        dr = ImageDraw.Draw(im)
        shoulder(dr, 2, 9, pal, False)
        shoulder(dr, 17, 9, pal, True)
        im.save(OUT + '/shoulder_%s.png' % name)
        states += [{'name': 'knee_%s' % name}, {'name': 'shoulder_%s' % name}]
        for side in ('left', 'right'):
            worn(side[0] + '_arm', (0.0, 0.35), pal).save(OUT + '/shoulder_%s-%s.png' % (name, side))
            worn(side[0] + '_leg', (0.40, 0.62), pal).save(OUT + '/knee_%s-%s.png' % (name, side))
            states += [{'name': 'shoulder_%s-%s' % (name, side), 'directions': 4},
                       {'name': 'knee_%s-%s' % (name, side), 'directions': 4}]
    json.dump({'version': 1, 'license': 'CC-BY-SA-3.0',
               'copyright': 'Drawn for The Darkest Hour by Tools/_DarkestHour/uprising/pads_sprites.py; worn sprites '
                            'follow the shape of the space-station-14 human body parts (CC-BY-SA-3.0)',
               'size': {'x': 32, 'y': 32}, 'states': states}, open(OUT + '/meta.json', 'w'), indent=2)


if __name__ == '__main__':
    main()
