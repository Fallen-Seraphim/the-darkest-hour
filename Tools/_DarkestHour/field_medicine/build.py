"""Sprites of the field medicine of the Assault mode: a combat tourniquet, a rubber tourniquet, a hemostatic dressing,
an occlusive chest seal, the soldier's first aid kit (IFAK) and the field surgical kit.

Run from the repository root:  python3 Tools/_DarkestHour/field_medicine/build.py"""
import json
import os
from PIL import Image, ImageDraw

OUT = 'Resources/Textures/_DarkestHour/Objects/Specific/Medical/field_medicine.rsi'

OUTLINE = (24, 26, 28, 255)
BLACK = (44, 46, 50, 255)
BLACK_L = (78, 82, 88, 255)
RED = (176, 44, 40, 255)
RED_L = (214, 82, 70, 255)
RUBBER = (150, 74, 44, 255)
RUBBER_L = (190, 108, 70, 255)
OLIVE = (86, 96, 58, 255)
OLIVE_L = (120, 132, 82, 255)
OLIVE_D = (60, 68, 40, 255)
WHITE = (226, 228, 222, 255)
SILVER = (168, 172, 176, 255)
BLUE = (60, 110, 170, 255)


def r(d, box, fill, outline=OUTLINE):
    d.rectangle(box, fill=fill, outline=outline)


def tourniquet_cat():
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    # coiled black strap
    d.ellipse((6, 9, 25, 26), outline=OUTLINE, width=5)
    d.ellipse((7, 10, 24, 25), outline=BLACK, width=3)
    d.arc((8, 11, 23, 24), 200, 320, BLACK_L, 1)
    # windlass rod with red tip and the clip
    d.line((9, 7, 23, 13), OUTLINE, 4)
    d.line((10, 7, 22, 12), BLACK_L, 2)
    r(d, (20, 10, 24, 14), RED)
    r(d, (13, 20, 18, 24), RED_L)
    return img


def tourniquet_rubber():
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    # rubber tube coiled in loops
    for i, y in enumerate((10, 15, 20)):
        d.arc((5, y - 4, 27, y + 6), 180, 360, OUTLINE, 4)
        d.arc((6, y - 3, 26, y + 5), 180, 360, RUBBER if i % 2 else RUBBER_L, 2)
    d.line((5, 15, 5, 26), OUTLINE, 3)
    d.line((5, 16, 5, 25), RUBBER, 1)
    r(d, (3, 25, 8, 28), SILVER)  # hook
    return img


def packet(color, mark):
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    r(d, (6, 7, 25, 25), color)
    d.line((7, 8, 24, 8), tuple(min(255, c + 40) for c in color[:3]) + (255,))
    # tear notch and label
    d.line((6, 11, 8, 11), OUTLINE)
    r(d, (10, 12, 21, 21), WHITE)
    mark(d)
    return img


def hemostatic():
    def mark(d):
        r(d, (14, 13, 17, 20), RED, None)
        r(d, (12, 15, 19, 18), RED, None)
    return packet(OLIVE, mark)


def chest_seal():
    def mark(d):
        d.ellipse((12, 13, 19, 20), fill=BLUE, outline=OUTLINE)
        d.point((15, 16), WHITE)
    return packet((70, 74, 80, 255), mark)


def ifak():
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    r(d, (5, 9, 26, 27), OLIVE)
    r(d, (5, 9, 26, 14), OLIVE_D)            # flap
    d.line((6, 15, 25, 15), OLIVE_L)
    r(d, (14, 12, 17, 16), BLACK)            # buckle
    for x in (8, 22):                        # molle straps
        d.line((x, 17, x, 26), OLIVE_D)
    r(d, (13, 19, 18, 24), RED, None)        # red cross
    r(d, (11, 21, 20, 22), RED, None)
    r(d, (15, 19, 16, 24), RED, None)
    return img


def surgery_kit():
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    r(d, (3, 10, 28, 25), OLIVE)
    d.line((4, 11, 27, 11), OLIVE_L)
    r(d, (12, 7, 19, 10), BLACK)             # handle
    r(d, (8, 15, 23, 21), WHITE)
    d.line((10, 18, 20, 18), SILVER)         # scalpel
    d.line((20, 17, 22, 18), SILVER)
    r(d, (14, 15, 17, 21), RED, None)
    return img


def held():
    sheet = Image.new('RGBA', (128, 32))
    spots = [(16, 20), (16, 20), (13, 20), (19, 20)]
    for i, (x, y) in enumerate(spots):
        frame = Image.new('RGBA', (32, 32))
        d = ImageDraw.Draw(frame)
        r(d, (x - 2, y - 2, x + 1, y + 1), OLIVE)
        sheet.alpha_composite(frame, (32 * i, 0))
    return sheet


def main():
    os.makedirs(OUT, exist_ok=True)
    states = [
        ('tourniquet_cat', tourniquet_cat()),
        ('tourniquet_rubber', tourniquet_rubber()),
        ('hemostatic', hemostatic()),
        ('chest_seal', chest_seal()),
        ('ifak', ifak()),
        ('surgery_kit', surgery_kit()),
    ]
    meta = {'version': 1, 'license': 'CC-BY-SA-3.0',
            'copyright': 'Made for The Darkest Hour (Tools/_DarkestHour/field_medicine/build.py)',
            'size': {'x': 32, 'y': 32}, 'states': []}
    for name, img in states:
        img.save(os.path.join(OUT, name + '.png'))
        meta['states'].append({'name': name})
    for name in ('inhand-left', 'inhand-right'):
        held().save(os.path.join(OUT, name + '.png'))
        meta['states'].append({'name': name, 'directions': 4})
    with open(os.path.join(OUT, 'meta.json'), 'w', encoding='utf-8') as f:
        json.dump(meta, f, indent=4)
        f.write('\n')


if __name__ == '__main__':
    main()
