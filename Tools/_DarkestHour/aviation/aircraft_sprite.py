"""Спрайт имперского штурмовика (вид сверху, нос вверх): прямое крыло и законцовочные баки как у F9F-5 «Пантера»,
двухмоторная гондольная схема с двухкилевым оперением и 30-мм пушкой в носу как у A-10C. Имперские цвета:
воронёная сталь, багровые полосы, золотая окантовка. Запуск из корня: python3 Tools/_DarkestHour/aviation/aircraft_sprite.py"""
import json, os
from PIL import Image, ImageDraw

OUT = 'Resources/Textures/_DarkestHour/Vehicles/Aircraft/attack_aircraft.rsi'
S = 160
BODY = (74, 78, 85, 255)
BODY_L = (104, 109, 118, 255)
BODY_D = (52, 55, 60, 255)
EDGE = (28, 30, 34, 255)
RED = (142, 31, 31, 255)
RED_D = (104, 20, 22, 255)
GOLD = (201, 162, 39, 255)
GLASS = (43, 74, 94, 255)
GLASS_L = (120, 170, 196, 255)
DARK = (24, 25, 28, 255)


def hull():
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # крыло (прямое, лёгкое сужение) с законцовочными баками
    d.polygon([(12, 66), (72, 60), (88, 60), (148, 66), (148, 82), (88, 90), (72, 90), (12, 82)], fill=BODY)
    d.polygon([(12, 66), (72, 60), (72, 70), (12, 72)], fill=BODY_L)          # передняя кромка — светлее
    d.line([(12, 82), (72, 90)], fill=BODY_D, width=2)
    d.line([(88, 90), (148, 82)], fill=BODY_D, width=2)
    for x0 in (2, 146):
        d.ellipse([x0, 54, x0 + 12, 94], fill=BODY_D)
        d.ellipse([x0 + 2, 56, x0 + 8, 76], fill=BODY_L)
        d.rectangle([x0 + 3, 86, x0 + 9, 90], fill=RED)
    # багровые полосы и золотые имперские ромбы на крыльях
    for x0, x1 in ((22, 30), (130, 138)):
        d.polygon([(x0, 65), (x1, 64), (x1, 83), (x0, 82)], fill=RED)
        d.line([(x0, 65), (x0, 82)], fill=GOLD)
        d.line([(x1, 64), (x1, 83)], fill=GOLD)
    for cx in (46, 114):
        d.polygon([(cx, 68), (cx + 6, 74), (cx, 80), (cx - 6, 74)], fill=GOLD)
        d.polygon([(cx, 71), (cx + 3, 74), (cx, 77), (cx - 3, 74)], fill=RED)
    # узлы подвески (4) — тёмные пилоны под крылом
    for x in (34, 60, 100, 126):
        d.rectangle([x - 2, 76, x + 2, 92], fill=DARK)
        d.rectangle([x - 1, 77, x + 1, 91], fill=BODY_D)
    # горизонтальное оперение и два киля
    d.polygon([(40, 132), (120, 132), (120, 142), (40, 142)], fill=BODY)
    d.line([(40, 132), (120, 132)], fill=BODY_L)
    for x in (38, 116):
        d.rectangle([x, 122, x + 6, 150], fill=BODY_D)
        d.rectangle([x + 1, 123, x + 2, 149], fill=BODY_L)
        d.rectangle([x, 144, x + 6, 150], fill=RED)
    # фюзеляж с заострённым носом
    d.polygon([(80, 6), (86, 18), (89, 40), (89, 140), (84, 152), (76, 152), (71, 140), (71, 40), (74, 18)], fill=BODY)
    d.polygon([(80, 6), (74, 18), (71, 40), (71, 140), (78, 140), (78, 18)], fill=BODY_L)
    d.line([(80, 60), (80, 140)], fill=BODY_D)
    # пушка 30 мм в носу и красная полоса на носу
    d.rectangle([79, 1, 81, 9], fill=DARK)
    d.polygon([(74, 22), (86, 22), (87, 27), (73, 27)], fill=RED)
    d.line([(73, 28), (87, 28)], fill=GOLD)
    # воздухозаборники у корня крыла (как у «Пантеры»)
    d.polygon([(66, 60), (71, 58), (71, 64), (64, 64)], fill=DARK)
    d.polygon([(94, 60), (89, 58), (89, 64), (96, 64)], fill=DARK)
    # фонарь кабины
    d.ellipse([73, 32, 87, 60], fill=EDGE)
    d.ellipse([74, 33, 86, 59], fill=GLASS)
    d.ellipse([76, 36, 80, 46], fill=GLASS_L)
    d.line([(74, 50), (86, 50)], fill=EDGE)
    # гондолы двигателей (как у A-10) на хвостовой части
    for x0 in (56, 90):
        d.ellipse([x0, 96, x0 + 14, 132], fill=BODY_D)
        d.ellipse([x0 + 2, 98, x0 + 7, 128], fill=BODY_L)
        d.ellipse([x0 + 3, 96, x0 + 11, 102], fill=DARK)        # воздухозаборник
        d.rectangle([x0 + 4, 128, x0 + 10, 133], fill=DARK)       # сопло
        d.line([(x0 + 1, 112), (x0 + 13, 112)], fill=RED)
    # сопло фюзеляжа
    d.rectangle([77, 150, 83, 154], fill=DARK)
    # обводка
    a = im.split()[3]
    out = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    px = im.load()
    for y in range(S):
        for x in range(S):
            if a.getpixel((x, y)) == 0 and any(
                    0 <= x + dx < S and 0 <= y + dy < S and a.getpixel((x + dx, y + dy)) > 0
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                out.putpixel((x, y), EDGE)
    out.alpha_composite(im)
    return out


def shadow(h):
    """Тень под самолётом в полёте/на палубе — отдельный слой, рисуется смещённой."""
    a = h.split()[3]
    sh = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    sh.putalpha(a.point(lambda v: 90 if v else 0))
    return sh


def seat():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([12, 10, 20, 24], fill=EDGE)
    d.rectangle([13, 11, 19, 23], fill=(60, 52, 40, 255))
    d.rectangle([13, 8, 19, 11], fill=EDGE)
    d.rectangle([14, 9, 18, 10], fill=(200, 160, 40, 255))
    d.line([(13, 17), (19, 17)], fill=(30, 30, 30, 255))
    return im


os.makedirs(OUT, exist_ok=True)
h = hull()
h.save(OUT + '/base.png')
shadow(h).save(OUT + '/shadow.png')
json.dump({
    'version': 1,
    'license': 'CC-BY-SA-3.0',
    'copyright': 'Drawn for The Darkest Hour (2026-11-12): Imperial attack aircraft after F9F-5 Panther and A-10C.',
    'size': {'x': S, 'y': S},
    'states': [{'name': 'base'}, {'name': 'shadow'}],
}, open(OUT + '/meta.json', 'w'), indent=4)

SEAT = 'Resources/Textures/_DarkestHour/Vehicles/Aircraft/aircraft_seat.rsi'
os.makedirs(SEAT, exist_ok=True)
seat().save(SEAT + '/seat.png')
json.dump({
    'version': 1,
    'license': 'CC-BY-SA-3.0',
    'copyright': 'Drawn for The Darkest Hour (2026-11-12): attack aircraft ejection seat.',
    'size': {'x': 32, 'y': 32},
    'states': [{'name': 'seat'}],
}, open(SEAT + '/meta.json', 'w'), indent=4)
print('ok')
