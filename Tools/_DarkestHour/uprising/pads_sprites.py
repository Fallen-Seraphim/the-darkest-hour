#!/usr/bin/env python3
"""Иконки наплечников и наколенников (решение автора 2026-11-06): военпол — серо-синие, ОФСК — оливково-бурые.
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
    json.dump({'version': 1, 'license': 'CC-BY-SA-3.0',
               'copyright': 'Drawn for The Darkest Hour (Tools/_DarkestHour/uprising/pads_sprites.py)',
               'size': {'x': 32, 'y': 32}, 'states': states}, open(OUT + '/meta.json', 'w'), indent=2)


if __name__ == '__main__':
    main()
