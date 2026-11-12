"""Спрайт имперского штурмовика «Кречет» (вид сверху, нос вверх).

По замечаниям автора (2026-11-12): без пушки в носу и без мотогондол A-10; двигатели — гондолы на концах крыла,
у корня крыла по бокам кабины — воздухозаборники как у Су-25; прямое крыло от F9F-5; цвета — воронёная сталь
с белыми и чёрными полосами и золотой окантовкой (без красного). Фонарь — отдельное состояние «canopy»:
полупрозрачное стекло с рамой рисуется поверх пилота, а в корпусе — кабина изнутри (кресло, приборная доска, пульты).
Запуск из корня: python3 Tools/_DarkestHour/aviation/aircraft_sprite.py"""
import json, os
from PIL import Image, ImageDraw

OUT = 'Resources/Textures/_DarkestHour/Vehicles/Aircraft/attack_aircraft.rsi'
S = 160
BODY = (78, 83, 90, 255)
BODY_L = (108, 114, 123, 255)
BODY_LL = (132, 138, 147, 255)
BODY_D = (55, 58, 64, 255)
PANEL = (64, 68, 75, 255)
EDGE = (24, 26, 30, 255)
WHITE = (226, 228, 230, 255)
BLACK = (20, 21, 24, 255)
GOLD = (201, 162, 39, 255)
DARK = (30, 31, 35, 255)
TUB = (38, 40, 44, 255)
SCREEN = (90, 170, 120, 255)
AMBER = (214, 160, 52, 255)
NAV_G = (90, 220, 120, 255)
NAV_W = (240, 240, 240, 255)


def outline(im):
    a = im.split()[3]
    out = Image.new('RGBA', im.size, (0, 0, 0, 0))
    w, h = im.size
    for y in range(h):
        for x in range(w):
            if a.getpixel((x, y)) == 0 and any(
                    0 <= x + dx < w and 0 <= y + dy < h and a.getpixel((x + dx, y + dy)) > 0
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                out.putpixel((x, y), EDGE)
    out.alpha_composite(im)
    return out


def hull():
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    # --- крыло: прямое, лёгкое сужение, элероны и закрылки
    d.polygon([(16, 68), (66, 62), (94, 62), (144, 68), (144, 84), (94, 92), (66, 92), (16, 84)], fill=BODY)
    d.polygon([(16, 68), (66, 62), (66, 70), (16, 73)], fill=BODY_L)                     # передняя кромка
    d.polygon([(144, 68), (94, 62), (94, 70), (144, 73)], fill=BODY_L)
    for x0, x1, y0, y1 in ((20, 44, 82, 79), (116, 140, 79, 82)):                         # элероны
        d.line([(x0, y0), (x1, y1)], fill=BODY_D)
    for x0, x1, y0, y1 in ((46, 66, 86, 88), (94, 114, 88, 86)):                          # закрылки
        d.line([(x0, y0), (x1, y1)], fill=BODY_D)
    for x in (30, 50, 110, 130):                                                          # нервюры (панели)
        d.line([(x, 69), (x, 86)], fill=PANEL)
    # белые и чёрные полосы у законцовок, золотые ромбы
    for x0 in (22, 130):
        d.rectangle([x0, 67, x0 + 3, 84], fill=WHITE)
        d.rectangle([x0 + 4, 67, x0 + 7, 84], fill=BLACK)
        d.rectangle([x0 + 8, 67, x0 + 8, 84], fill=GOLD)
    for cx in (42, 118):
        d.polygon([(cx, 69), (cx + 6, 75), (cx, 81), (cx - 6, 75)], fill=GOLD)
        d.polygon([(cx, 71), (cx + 4, 75), (cx, 79), (cx - 4, 75)], fill=BLACK)
        d.point((cx, 75), fill=WHITE)

    # --- гондолы двигателей на концах крыла
    for x0 in (2, 144):
        d.ellipse([x0, 50, x0 + 14, 104], fill=BODY_D)
        d.ellipse([x0 + 2, 52, x0 + 8, 96], fill=BODY_L)
        d.ellipse([x0 + 2, 50, x0 + 12, 58], fill=DARK)                                  # заборник
        d.ellipse([x0 + 4, 51, x0 + 10, 56], fill=BLACK)
        d.rectangle([x0 + 3, 100, x0 + 11, 106], fill=DARK)                               # сопло
        d.line([(x0 + 4, 104), (x0 + 10, 104)], fill=(70, 60, 50, 255))
        d.rectangle([x0 + 2, 72, x0 + 12, 74], fill=WHITE)                                # полоса
        d.rectangle([x0 + 2, 75, x0 + 12, 76], fill=BLACK)
        for y in (62, 84, 92):
            d.line([(x0 + 2, y), (x0 + 12, y)], fill=PANEL)
    d.point((9, 49), fill=NAV_W)
    d.point((151, 49), fill=NAV_G)

    # --- пилоны и балочные держатели (4)
    for x in (32, 56, 104, 128):
        d.rectangle([x - 2, 78, x + 2, 96], fill=DARK)
        d.rectangle([x - 1, 79, x + 1, 95], fill=BODY_D)
        d.line([(x - 3, 90), (x + 3, 90)], fill=BLACK)

    # --- горизонтальное оперение и киль
    d.polygon([(46, 132), (76, 128), (84, 128), (114, 132), (114, 141), (84, 140), (76, 140), (46, 141)], fill=BODY)
    d.line([(46, 132), (76, 128)], fill=BODY_L)
    d.line([(84, 128), (114, 132)], fill=BODY_L)
    d.line([(48, 138), (74, 137)], fill=BODY_D)
    d.line([(86, 137), (112, 138)], fill=BODY_D)
    for x0 in (46, 108):
        d.rectangle([x0, 132, x0 + 5, 141], fill=WHITE)
    d.rectangle([78, 116, 82, 152], fill=BODY_D)                                          # киль сверху
    d.line([(79, 117), (79, 151)], fill=BODY_LL)
    d.rectangle([78, 146, 82, 152], fill=BLACK)

    # --- фюзеляж
    d.polygon([(80, 8), (85, 18), (89, 38), (90, 70), (89, 120), (85, 150), (75, 150), (71, 120), (70, 70), (71, 38), (75, 18)], fill=BODY)
    d.polygon([(80, 8), (75, 18), (71, 38), (70, 70), (71, 120), (75, 150), (78, 150), (78, 18)], fill=BODY_L)
    d.line([(80, 64), (80, 126)], fill=BODY_D)                                            # гаргрот
    for y in (30, 64, 96, 120):                                                           # стыки панелей
        d.line([(71, y), (89, y)], fill=PANEL)
    for y in range(100, 118, 4):                                                          # заклёпки
        d.point((73, y), fill=BODY_D)
        d.point((87, y), fill=BODY_D)
    d.polygon([(78, 9), (82, 9), (84, 17), (76, 17)], fill=BLACK)                         # радиопрозрачный нос
    d.polygon([(74, 20), (86, 20), (87, 24), (73, 24)], fill=WHITE)                       # носовая полоса
    d.line([(73, 25), (87, 25)], fill=GOLD)
    d.rectangle([77, 144, 83, 152], fill=DARK)                                            # сопло ВСУ

    # --- воздухозаборники у корня крыла, по бокам кабины (как у Су-25)
    for side in (-1, 1):
        x_in, x_out = (70, 58) if side < 0 else (90, 102)
        d.polygon([(x_in, 46), (x_out, 50), (x_out, 72), (x_in, 72)], fill=BODY_D)
        d.polygon([(x_in, 46), (x_out, 50), (x_out, 55), (x_in, 52)], fill=BLACK)         # зев
        d.line([(x_out, 56), (x_out, 72)], fill=BODY_LL if side < 0 else PANEL)
        d.line([(x_in, 60), (x_out, 60)], fill=PANEL)

    # --- кабина изнутри (фонарь — отдельным слоем поверх пилота)
    d.ellipse([72, 30, 88, 62], fill=EDGE)
    d.ellipse([73, 31, 87, 61], fill=TUB)
    d.rectangle([75, 34, 85, 37], fill=DARK)                                              # приборная доска
    d.point((77, 35), fill=SCREEN)
    d.point((80, 35), fill=AMBER)
    d.point((83, 35), fill=SCREEN)
    d.line([(74, 42), (74, 54)], fill=(60, 62, 66, 255))                                  # боковые пульты
    d.line([(86, 42), (86, 54)], fill=(60, 62, 66, 255))
    d.point((74, 46), fill=AMBER)
    d.point((86, 48), fill=SCREEN)
    return outline(im)


def canopy():
    """Фонарь: рама и полупрозрачное стекло с бликами — поверх пилота."""
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([72, 30, 88, 62], outline=EDGE)
    d.ellipse([73, 31, 87, 61], fill=(120, 170, 200, 70), outline=(70, 75, 82, 255))
    d.line([(73, 48), (87, 48)], fill=(70, 75, 82, 255))                                  # переплёт
    d.line([(76, 36), (77, 44)], fill=(230, 245, 255, 150))                               # блики
    d.line([(78, 34), (78, 38)], fill=(230, 245, 255, 120))
    d.line([(76, 51), (77, 56)], fill=(230, 245, 255, 90))
    return im


def shadow(h):
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
canopy().save(OUT + '/canopy.png')
json.dump({
    'version': 1,
    'license': 'CC-BY-SA-3.0',
    'copyright': 'Drawn for The Darkest Hour (2026-11-12): Imperial attack aircraft after F9F-5 Panther and Su-25.',
    'size': {'x': S, 'y': S},
    'states': [{'name': 'base'}, {'name': 'shadow'}, {'name': 'canopy'}],
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


# --- боеприпасы штурмовика: фугасная бомба и короб снарядов 30 мм (иконки 32×32)
def bomb_he():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([10, 4, 22, 26], fill=(70, 78, 64, 255))
    d.rectangle([10, 10, 22, 22], fill=(70, 78, 64, 255))
    d.ellipse([12, 5, 16, 16], fill=(96, 106, 88, 255))
    d.rectangle([10, 13, 22, 14], fill=GOLD)
    d.polygon([(11, 24), (16, 30), (21, 24)], fill=(50, 54, 48, 255))
    d.line([(16, 24), (16, 30)], fill=EDGE)
    d.rectangle([14, 2, 18, 4], fill=EDGE)
    return outline(im)


def ammo_box():
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([5, 10, 27, 26], fill=(74, 82, 60, 255))
    d.rectangle([5, 10, 27, 13], fill=(96, 104, 80, 255))
    d.rectangle([13, 16, 19, 22], fill=GOLD)
    d.text((14, 16), '', fill=BLACK)
    for x in (8, 11, 21, 24):
        d.rectangle([x, 6, x + 1, 10], fill=(200, 160, 60, 255))
        d.point((x, 6), fill=(150, 150, 150, 255))
    return outline(im)


MUN = 'Resources/Textures/_DarkestHour/Vehicles/Aircraft/munitions.rsi'
os.makedirs(MUN, exist_ok=True)
bomb_he().save(MUN + '/bomb_he.png')
ammo_box().save(MUN + '/ammo_30mm.png')
json.dump({
    'version': 1,
    'license': 'CC-BY-SA-3.0',
    'copyright': 'Drawn for The Darkest Hour (2026-11-12): attack aircraft munitions.',
    'size': {'x': 32, 'y': 32},
    'states': [{'name': 'bomb_he'}, {'name': 'ammo_30mm'}],
}, open(MUN + '/meta.json', 'w'), indent=4)
print('munitions ok')
