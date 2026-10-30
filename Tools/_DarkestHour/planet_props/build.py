#!/usr/bin/env python3
"""DarkestHour: спрайты ЛЭП и подстанции для карты планеты. Рисуются процедурно. CC-BY-SA-3.0.

  Resources/Textures/_DarkestHour/Structures/Power/power_line.rsi — опора ЛЭП (32×96, решётчатая башня с траверсой).
  Resources/Textures/_DarkestHour/Structures/Power/power_line_wire.rsi — провода (32×32: пролёт с юга на север, три фазы).
  Resources/Textures/_DarkestHour/Structures/Power/transformer.rsi — силовой трансформатор 3×3 (96×96).
"""
import json
import os
import random

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
TEX = os.path.join(ROOT, 'Resources/Textures/_DarkestHour/Structures/Power')

STEEL_D = (58, 62, 66, 255)
STEEL = (112, 118, 124, 255)
STEEL_L = (160, 166, 170, 255)
WIRE = (28, 28, 30, 235)
INSUL = (200, 205, 190, 255)
INSUL_D = (140, 145, 130, 255)
CONC = (128, 126, 120, 255)
CONC_D = (92, 90, 86, 255)
OIL = (74, 86, 70, 255)
OIL_D = (52, 62, 50, 255)
OIL_L = (104, 118, 98, 255)
WARN = (210, 170, 40, 255)

# три фазы: x-координаты концов траверсы (общие для опоры и проводов)
PHASES = (5, 16, 26)


def meta(states, size, copyright_):
    return {
        'version': 1,
        'license': 'CC-BY-SA-3.0',
        'copyright': copyright_,
        'size': {'x': size[0], 'y': size[1]},
        'states': states,
    }


def pylon():
    w, h = 32, 96
    img = Image.new('RGBA', (w, h))
    d = ImageDraw.Draw(img)
    # тень на земле
    d.ellipse([4, 86, 28, 94], fill=(0, 0, 0, 60))
    # ноги башни (сужаются кверху)
    base_l, base_r, top_l, top_r = 7, 24, 13, 18
    top, bottom = 18, 90
    d.line([base_l, bottom, top_l, top], fill=STEEL_D, width=2)
    d.line([base_r, bottom, top_r, top], fill=STEEL_D, width=2)
    # решётка
    for y in range(bottom, top, -8):
        t0 = (bottom - y) / (bottom - top)
        t1 = (bottom - (y - 8)) / (bottom - top)
        l0 = base_l + (top_l - base_l) * t0
        r0 = base_r + (top_r - base_r) * t0
        l1 = base_l + (top_l - base_l) * t1
        r1 = base_r + (top_r - base_r) * t1
        d.line([l0, y, r1, y - 8], fill=STEEL)
        d.line([r0, y, l1, y - 8], fill=STEEL)
        d.line([l0, y, r0, y], fill=STEEL_D)
    # траверсы
    for y, half in ((22, 12), (34, 9)):
        d.rectangle([16 - half, y, 15 + half, y + 1], fill=STEEL_D)
        d.line([16 - half, y + 2, 13, y + 6], fill=STEEL)
        d.line([15 + half, y + 2, 18, y + 6], fill=STEEL)
    # гирлянды изоляторов на верхней траверсе
    for x in PHASES:
        for k in range(3):
            d.rectangle([x - 1, 24 + k * 2, x, 24 + k * 2], fill=INSUL if k % 2 == 0 else INSUL_D)
    # тросостойка
    d.line([16, 8, 16, 18], fill=STEEL_D, width=2)
    d.point((16, 7), fill=STEEL_L)
    # бетонные фундаменты
    for x in (base_l, base_r):
        d.rectangle([x - 2, 88, x + 2, 91], fill=CONC, outline=CONC_D)
    # знак «опасно»
    d.polygon([(14, 66), (19, 66), (16, 61)], fill=WARN, outline=STEEL_D)
    return img


def wire():
    """Пролёт ЛЭП по клетке с юга на север: три фазы и грозотрос."""
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    for x in PHASES:
        d.line([x, 0, x, 31], fill=WIRE)
    d.line([16, 0, 16, 31], fill=(28, 28, 30, 140))
    return img


def wire_icon():
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    for x in PHASES:
        d.line([x, 2, x, 29], fill=WIRE, width=2)
    return img


def transformer():
    s = 96
    img = Image.new('RGBA', (s, s))
    d = ImageDraw.Draw(img)
    rng = random.Random(7)
    # бетонная плита с маслоприёмником
    d.rectangle([1, 6, 94, 94], fill=CONC, outline=CONC_D)
    for _ in range(140):
        x, y = rng.randrange(2, 94), rng.randrange(7, 94)
        img.putpixel((x, y), CONC_D)
    d.rectangle([4, 88, 91, 91], fill=WARN)
    for x in range(4, 92, 8):
        d.polygon([(x, 91), (x + 4, 88), (x + 8, 88), (x + 4, 91)], fill=STEEL_D)
    # радиаторы по бокам
    for x0 in (6, 74):
        d.rectangle([x0, 26, x0 + 15, 76], fill=OIL_D)
        for x in range(x0 + 1, x0 + 15, 3):
            d.rectangle([x, 27, x + 1, 75], fill=OIL_L)
    # бак
    d.rectangle([22, 18, 73, 82], fill=OIL, outline=OIL_D)
    d.rectangle([24, 20, 71, 23], fill=OIL_L)
    for y in range(30, 80, 10):
        d.line([23, y, 72, y], fill=OIL_D)
    # расширитель (бочка масла сверху)
    d.rectangle([26, 8, 69, 15], fill=OIL_L, outline=OIL_D)
    d.line([30, 15, 30, 19], fill=OIL_D)
    # вводы ВН (высокие изоляторы) и НН
    for x in (32, 47, 62):
        d.rectangle([x - 2, 0, x + 2, 18], fill=INSUL, outline=INSUL_D)
        for y in range(2, 18, 3):
            d.line([x - 3, y, x + 3, y], fill=INSUL_D)
        d.rectangle([x - 1, 0, x + 1, 1], fill=STEEL_L)
    for x in (36, 47, 58):
        d.rectangle([x - 1, 58, x + 1, 66], fill=INSUL, outline=INSUL_D)
    # табличка
    d.rectangle([40, 70, 55, 77], fill=(230, 230, 220, 255), outline=STEEL_D)
    d.polygon([(44, 76), (51, 76), (47, 71)], fill=WARN, outline=STEEL_D)
    return img


def save(path, states, size, images):
    os.makedirs(path, exist_ok=True)
    for name, image in images.items():
        image.save(os.path.join(path, name + '.png'))
    with open(os.path.join(path, 'meta.json'), 'w') as f:
        json.dump(meta(states, size, 'Нарисовано процедурно для The Darkest Hour (Tools/_DarkestHour/planet_props/build.py)'),
                  f, indent=2, ensure_ascii=False)


def main():
    # один rsi — один размер кадра, поэтому опора (32×96) и провода (32×32) в разных rsi
    save(os.path.join(TEX, 'power_line.rsi'), [{'name': 'pylon'}], (32, 96), {'pylon': pylon()})
    save(os.path.join(TEX, 'power_line_wire.rsi'), [{'name': 'wire'}, {'name': 'icon'}], (32, 32),
         {'wire': wire(), 'icon': wire_icon()})
    save(os.path.join(TEX, 'transformer.rsi'), [{'name': 'transformer'}], (96, 96), {'transformer': transformer()})


if __name__ == '__main__':
    main()
