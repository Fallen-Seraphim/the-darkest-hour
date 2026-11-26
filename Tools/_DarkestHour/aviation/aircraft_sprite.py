"""Спрайт имперского штурмовика «Кречет» (вид сверху, нос вверх).

По замечаниям автора (2026-11-12): без пушки в носу и без мотогондол A-10; двигатели — гондолы на концах крыла,
у корня крыла по бокам кабины — воздухозаборники как у Су-25; прямое крыло от F9F-5; цвета — воронёная сталь
с белыми и чёрными полосами и золотой окантовкой (без красного). Фонарь — отдельные состояния:
«canopy_open» (откинут, самолёт пуст) и «canopy_closed» (матовый, пилот внутри); в корпусе — кабина изнутри (кресло,
приборная доска, пульты). Подвесы — «hp{узел}_{тип}» на каждый из 4 узлов из спрайтов боеприпасов RMC-14, видны из-под крыла (план осады п. 16, 2026-11-20).
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
    # катапультное кресло (пилот садится внутрь, отдельной сущности-кресла не видно — план осады п. 16)
    d.rectangle([77, 44, 83, 57], fill=EDGE)
    d.rectangle([78, 45, 82, 56], fill=(60, 52, 40, 255))
    d.rectangle([78, 41, 82, 44], fill=EDGE)
    d.rectangle([79, 42, 81, 43], fill=(200, 160, 40, 255))
    d.line([(78, 51), (82, 51)], fill=(30, 30, 30, 255))
    return outline(im)


def canopy():
    """Фонарь: рама и полупрозрачное стекло с бликами — поверх пилота."""
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([72, 30, 88, 62], outline=EDGE)
    d.ellipse([73, 31, 87, 61], fill=(52, 78, 104, 215), outline=(70, 75, 82, 255))  # тонированное стекло: пилота почти не видно
    d.line([(73, 48), (87, 48)], fill=(70, 75, 82, 255))                                  # переплёт
    d.line([(76, 36), (77, 44)], fill=(230, 245, 255, 150))                               # блики
    d.line([(78, 34), (78, 38)], fill=(230, 245, 255, 120))
    d.line([(76, 51), (77, 56)], fill=(230, 245, 255, 90))
    return im


def canopy_open():
    """Фонарь откинут назад (самолёт пуст, план осады п. 16): рама со стеклом сдвинута на гаргрот, кабина открыта."""
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    dy = 28
    d.ellipse([72, 30 + dy, 88, 62 + dy], outline=EDGE)
    d.ellipse([73, 31 + dy, 87, 61 + dy], fill=(52, 78, 104, 170), outline=(70, 75, 82, 255))
    d.line([(73, 48 + dy), (87, 48 + dy)], fill=(70, 75, 82, 255))
    d.line([(76, 36 + dy), (77, 44 + dy)], fill=(230, 245, 255, 150))
    d.line([(78, 34 + dy), (78, 38 + dy)], fill=(230, 245, 255, 120))
    # кромка кабины без стекла
    d.ellipse([72, 30, 88, 62], outline=EDGE)
    return im


def canopy_closed():
    """Фонарь закрыт, стекло матовое (пилот внутри, снаружи не виден)."""
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([72, 30, 88, 62], fill=EDGE)
    d.ellipse([73, 31, 87, 61], fill=(44, 52, 62, 255), outline=(70, 75, 82, 255))
    d.ellipse([75, 33, 85, 59], fill=(52, 62, 74, 255))
    d.line([(73, 48), (87, 48)], fill=(70, 75, 82, 255))
    d.line([(76, 37), (76, 43)], fill=(120, 132, 146, 255))                               # матовый блик
    d.point((78, 35), fill=(110, 122, 136, 255))
    return im


# --- подвесы под крылом (план осады п. 16, уточнение автора 2026-11-20): те же спрайты боеприпасов, что у предметов
# (RMC-14, CC-BY-SA-3.0), уменьшенные вдвое и повёрнутые носом вперёд. Слой рисуется ПОД корпусом — из-под крыла видно
# только хвостовую часть подвеса.
PYLONS = (32, 56, 104, 128)
RMC = 'Resources/Textures/_RMC14/Objects/'
# тип → (rsi, состояние, последняя строка самого боеприпаса — ниже тележка, повернуть носом вверх, низ подвеса по y)
STORE_SOURCES = {
    'bomb_he': ('dropship_ammo_64.rsi', 'jdam', 52, True, 106),
    'bomb_cluster': ('dropship_ammo_64.rsi', 'cluster', 51, True, 106),
    'bomb_napalm': ('dropship_ammo_64.rsi', 'napalm', 50, True, 106),
    'bomb_magnesium': ('dropship_ammo_64.rsi', 'jdami', 52, True, 106),
    'bomb_guided': ('dropship_ammo_64.rsi', 'paveway', 50, True, 104),
    'missile_at': ('dropship_ammo_64.rsi', 'single', 50, True, 104),
    'missile_he': ('dropship_ammo_64.rsi', 'banshee', 49, True, 104),
    'cannon_pod': ('dropship_attachments.rsi', '30mm_cannon', 36, False, 102),
    # ПТБ (решение автора 2026-11-26) — свой рисунок (Tools/_DarkestHour/aviation/drop_tank.py)
    'drop_tank': ('Resources/Textures/_DarkestHour/Objects/Aviation/drop_tank.rsi', 'icon', 54, True, 106),
}


def store(kind, x):
    rsi, state, last_row, rotate, bottom = STORE_SOURCES[kind]
    src = Image.open((rsi if rsi.startswith('Resources/') else RMC + rsi) + '/' + state + '.png').convert('RGBA')
    src = src.crop((0, 0, src.size[0], last_row + 1))
    src = src.crop(src.getbbox())
    if rotate:
        src = src.rotate(90, expand=True)                                                 # нос (справа) — вперёд, вверх
    src = src.resize((max(1, src.size[0] // 2), max(1, src.size[1] // 2)), Image.NEAREST)
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    im.alpha_composite(src, (x - src.size[0] // 2, bottom - src.size[1]))
    return im


STORES = tuple(STORE_SOURCES)


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
canopy_open().save(OUT + '/canopy_open.png')
canopy_closed().save(OUT + '/canopy_closed.png')
states = [{'name': 'base'}, {'name': 'shadow'}, {'name': 'canopy'}, {'name': 'canopy_open'}, {'name': 'canopy_closed'}]
for i, x in enumerate(PYLONS):
    for kind in STORES:
        store(kind, x).save(OUT + f'/hp{i}_{kind}.png')
        states.append({'name': f'hp{i}_{kind}'})
json.dump({
    'version': 1,
    'license': 'CC-BY-SA-3.0',
    'copyright': 'Drawn for The Darkest Hour (2026-11-12, canopy states 2026-11-20): Imperial attack aircraft after F9F-5 Panther and Su-25. Hardpoint stores (hp*) are downscaled dropship_ammo_64 and dropship_attachments sprites taken from cmss13 via RMC-14 (CC-BY-SA-3.0); the drop tank is drawn for The Darkest Hour.',
    'size': {'x': S, 'y': S},
    'states': states,
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

# Боеприпасы штурмовика — спрайты RMC-14 (dropship_ammo.rsi, dropship_ammo_64.rsi), не рисуются здесь.
