"""Значки приказов ком-модуля (план осады п. 26): держать позицию, в атаку, двигаться.
Запуск из корня: python3 Tools/_DarkestHour/ui/order_icons.py"""
import json, os
from PIL import Image, ImageDraw

OUT = 'Resources/Textures/_DarkestHour/Interface/Actions/orders.rsi'
S = 32
BG = (34, 30, 22, 255)
RIM = (201, 162, 39, 255)
FG = (240, 214, 140, 255)
DARK = (20, 18, 14, 255)


def base():
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([1, 1, 30, 30], radius=4, fill=BG, outline=RIM)
    return im, d


def hold():
    im, d = base()
    d.polygon([(16, 6), (25, 9), (24, 18), (16, 26), (8, 18), (7, 9)], fill=FG, outline=DARK)   # щит
    d.line([(16, 9), (16, 23)], fill=DARK)
    d.line([(10, 13), (22, 13)], fill=DARK)
    return im


def attack():
    im, d = base()
    for y in (8, 15):                                                                          # два шеврона вперёд
        d.polygon([(8, y + 8), (16, y), (24, y + 8), (24, y + 11), (16, y + 3), (8, y + 11)], fill=FG, outline=DARK)
    return im


def move():
    im, d = base()
    for x in (6, 15):                                                                          # стрелки вправо
        d.polygon([(x, 9), (x + 8, 16), (x, 23), (x, 19), (x + 4, 16), (x, 13)], fill=FG, outline=DARK)
    return im


os.makedirs(OUT, exist_ok=True)
for name, fn in (('hold', hold), ('attack', attack), ('move', move)):
    fn().save(f'{OUT}/{name}.png')
json.dump({'version': 1, 'license': 'CC-BY-SA-3.0',
           'copyright': 'Drawn for The Darkest Hour (2026-11-20): squad order icons.',
           'size': {'x': S, 'y': S}, 'states': [{'name': n} for n in ('hold', 'attack', 'move')]},
          open(OUT + '/meta.json', 'w'), indent=4)
print('ok')
