"""Подвесной топливный бак (ПТБ) штурмовика (решение автора 2026-11-26): сигара в имперской гамме — белый корпус,
чёрные носовой обтекатель и стабилизаторы, охряной поясок, оранжевая заливная горловина; на тележке, как боеприпасы
RMC в dropship_ammo_64 (последняя строка самого бака — 52, ниже тележка: так его берёт aircraft_sprite.py).

Запуск из корня: python3 Tools/_DarkestHour/aviation/drop_tank.py"""
import json
import os
from PIL import Image

OUT = 'Resources/Textures/_DarkestHour/Objects/Aviation/drop_tank.rsi'
K = (14, 14, 17, 255)
STEEL = [(36, 36, 42, 255), (58, 58, 66, 255), (86, 86, 96, 255)]
WHITE = [(150, 151, 157, 255), (192, 193, 199, 255), (222, 223, 227, 255), (245, 245, 247, 255)]
OCHRE = [(118, 80, 24, 255), (166, 118, 38, 255), (206, 160, 58, 255)]
ORANGE = (236, 120, 30, 255)


def shade(ramp, y, top, bottom):
    t = (y - top) / max(1, bottom - top)
    if t < 0.2:
        return ramp[-1]
    if t < 0.55:
        return ramp[-2]
    if t < 0.85:
        return ramp[1] if len(ramp) > 2 else ramp[0]
    return ramp[0]


def tank():
    im = Image.new('RGBA', (64, 64))
    px = im.load()
    top, bottom = 40, 52          # корпус: y 40…52, x 8…58
    mid = (top + bottom) / 2
    half = (bottom - top) / 2
    for x in range(6, 60):
        # профиль: острый нос справа, хвост сужается слева
        if x > 50:
            r = half * max(0.0, (60 - x) / 10) ** 0.7
        elif x < 14:
            r = half * (0.55 + 0.45 * (x - 6) / 8)
        else:
            r = half
        for y in range(top, bottom + 1):
            if abs(y + 0.5 - mid) <= r:
                ramp = STEEL if x > 52 else (OCHRE if 30 <= x <= 32 else WHITE)
                px[x, y] = shade(ramp, y, mid - r, mid + r)
    # стабилизаторы (хвост слева)
    for i in range(5):
        px[6 + i, top - 3 + i] = STEEL[1]
        px[6 + i, bottom + 3 - i] = STEEL[0]
    for x in range(5, 9):
        px[x, top - 3] = STEEL[2]
        px[x, bottom + 3] = STEEL[0]
    # заливная горловина и швы
    px[22, top] = ORANGE
    px[23, top] = ORANGE
    for x in (18, 42):
        for y in range(top + 2, bottom - 1):
            if px[x, y][3]:
                px[x, y] = WHITE[0]
    # контур
    src = im.copy().load()
    for x in range(64):
        for y in range(64):
            if src[x, y][3]:
                continue
            if any(0 <= x + dx < 64 and 0 <= y + dy < 64 and src[x + dx, y + dy][3] and src[x + dx, y + dy] != K
                   for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                px[x, y] = K
    # тележка (как у боеприпасов RMC): балка и два колеса
    for x in range(14, 50):
        px[x, 55] = STEEL[1]
        px[x, 56] = STEEL[0]
    for cx in (18, 45):
        for x in range(cx - 2, cx + 3):
            for y in range(57, 61):
                px[x, y] = K if (x in (cx - 2, cx + 2) or y in (57, 60)) else STEEL[2]
    for x in (20, 44):
        for y in range(53, 55):
            px[x, y] = STEEL[1]
    return im


def main():
    os.makedirs(OUT, exist_ok=True)
    tank().save(os.path.join(OUT, 'icon.png'))
    json.dump({'version': 1, 'license': 'CC-BY-SA-3.0',
               'copyright': 'Drawn for The Darkest Hour (Tools/_DarkestHour/aviation/drop_tank.py)',
               'size': {'x': 64, 'y': 64}, 'states': [{'name': 'icon'}]},
              open(os.path.join(OUT, 'meta.json'), 'w'), indent=4)


if __name__ == '__main__':
    main()
