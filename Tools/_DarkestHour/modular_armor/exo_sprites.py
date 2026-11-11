#!/usr/bin/env python3
"""Спрайты экзоскелетов и пластин модульной брони (решение автора 2026-11-11).

Экзоскелет — силовая рама в духе «Грани будущего»: стойки вдоль рук и ног, приводы в суставах, хребет и силовой
блок на спине; у среднего и тяжёлого под рамой герметичный комбинезон, лёгкий надевается поверх формы. Пластины —
ламеллярные, немного «самурайские»: до (торс) из поперечных полос со шнуровкой, содэ (наплечники) и котэ (наручи),
хайдатэ (набедренники), наколенник и сунэатэ (поножи). Тяжёлые — массивные (наплечники шире, юбка-кусадзури),
композитные и лёгкие — стройные. Империя — белые пластины с оранжевым акцентом, Республика — зелёные с латунным.
Шлемы — с встроенным противогазом: тяжёлый — с двумя круглыми линзами и рылом фильтра, средний и лёгкий — с узким
визором.

Всё рисуется попиксельно (32x32, четыре направления: юг, север, восток, запад). Лицензия — CC-BY-SA-3.0, свои
рисунки. Запуск из корня репозитория:
    python3 Tools/_DarkestHour/modular_armor/exo_sprites.py
Пишет Resources/Textures/_DarkestHour/Clothing/{OuterClothing,Head}/ModularArmor/*.rsi и превью рядом со скриптом.
"""
import json
import os

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
TEX = os.path.join(ROOT, 'Resources/Textures')
OUT_OUTER = os.path.join(TEX, '_DarkestHour/Clothing/OuterClothing/ModularArmor')
OUT_HEAD = os.path.join(TEX, '_DarkestHour/Clothing/Head/ModularArmor')
COPYRIGHT = 'Drawn for The Darkest Hour (Tools/_DarkestHour/modular_armor/exo_sprites.py)'


def hexc(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


# Палитры: тень контура, тень, основной, свет, блик.
FACTIONS = {
    'imb1': dict(
        name='IMB-1',
        plate=[hexc(c) for c in ('#55585f', '#9a9ea6', '#c9ccd2', '#e6e8eb', '#fbfbfc')],
        lacing=hexc('#2b2d3a'),
        accent=hexc('#e2762a'),
        suit=[hexc(c) for c in ('#141519', '#22242a', '#30333a', '#3d4048')],
        lens_heavy=hexc('#f2c230'),
        lens_light=hexc('#e0302a'),
    ),
    'rmb1': dict(
        name='RMB-1',
        plate=[hexc(c) for c in ('#1f2617', '#3b4729', '#556638', '#6e8248', '#8b9f5d')],
        lacing=hexc('#2a2116'),
        accent=hexc('#c9a445'),
        suit=[hexc(c) for c in ('#15180f', '#22281a', '#2f3624', '#3c442e')],
        lens_heavy=hexc('#f0a028'),
        lens_light=hexc('#e8b030'),
    ),
}
METAL = [hexc(c) for c in ('#141518', '#2f3136', '#4e5159', '#787c86', '#a3a7b0')]
HAZARD = hexc('#e0b028')
GLOW = hexc('#ff9a3a')

DIRS = 4
PARTS = ['torso', 'arm_left', 'arm_right', 'leg_left', 'leg_right']
SWAP = {'arm_left': 'arm_right', 'arm_right': 'arm_left', 'leg_left': 'leg_right', 'leg_right': 'leg_left',
        'torso': 'torso'}


class Canvas:
    def __init__(self):
        self.px = {}

    def put(self, x, y, c):
        if 0 <= x < 32 and 0 <= y < 32 and c is not None:
            self.px[(x, y)] = c

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.put(x, y, c)

    def image(self):
        im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
        for (x, y), c in self.px.items():
            im.putpixel((x, y), c)
        return im


def mirror(im):
    return im.transpose(Image.FLIP_LEFT_RIGHT)


def spans_rect(x0, y0, x1, y1):
    return [(y, x0, x1) for y in range(y0, y1 + 1)]


def plate(cv, spans, pal, band=3, lacing=None, accent=None, light='left', laced=True, top_rim=True):
    """
    Пластина из поперечных полос (ламели): верхняя строка полосы светлая, нижняя — шов; по шву — шнуровка
    вертикальными «шнурами» (у краёв и по центру широкой пластины); края — тёмный контур. spans — [(y, x0, x1)].
    """
    if not spans:
        return
    ys = sorted({y for y, _, _ in spans})
    y_top = ys[0]
    filled = {(x, y) for y, x0, x1 in spans for x in range(x0, x1 + 1)}
    for y, x0, x1 in spans:
        k = (y - y_top) % band
        seam = band > 1 and k == band - 1 and y != ys[-1]
        cords = {x0 + 1, x1 - 1} if x1 - x0 >= 3 else set()
        if x1 - x0 >= 7:
            cords.add((x0 + x1) // 2)
        lit = x0 + 1 if light == 'left' else x1 - 1
        for x in range(x0, x1 + 1):
            edge = (x - 1, y) not in filled or (x + 1, y) not in filled or (x, y + 1) not in filled
            if edge:
                c = pal[0]
            elif seam:
                c = lacing if laced and lacing is not None and x in cords else pal[1]
            elif k == 0 and top_rim:
                c = pal[4] if x == lit else pal[3]
            else:
                c = pal[3] if x == lit else pal[2]
            cv.put(x, y, c)
    if accent is not None:
        # узел шнуровки-акцент по центру первого шва
        y, x0, x1 = spans[min(len(spans) - 1, band - 1)]
        cv.put((x0 + x1) // 2, y, accent)


def bar(cv, x0, y0, x1, y1, pal=METAL):
    """Металлическая стойка: светлая грань слева/сверху, тёмная справа/снизу."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if x0 == x1 and y0 != y1:
                c = pal[3] if (y - y0) % 4 != 3 else pal[2]
            elif y0 == y1:
                c = pal[3]
            else:
                c = pal[3] if x == x0 or y == y0 else (pal[1] if x == x1 or y == y1 else pal[2])
            cv.put(x, y, c)


def joint(cv, x, y, big=False, light=None):
    """Привод сустава: тёмный корпус, светлый центр (у тяжёлого — 2x2)."""
    if big:
        cv.rect(x - 1, y - 1, x + 1, y + 1, METAL[1])
        cv.put(x - 1, y - 1, METAL[0])
        cv.put(x + 1, y + 1, METAL[0])
        cv.put(x, y, light or METAL[4])
    else:
        cv.put(x, y, light or METAL[4])


# ----------------------------------------------------------------------------------------------------------------
# Экзоскелет (слой верхней одежды): комбинезон и рама.

BODY = {
    # (y, x0, x1) — силуэт тела без головы для юга; север такой же, восток свой.
    'south': ([(10, 12, 18), (11, 10, 20), (12, 9, 21), (13, 8, 22), (14, 8, 22)] +
              [(y, 7, 23) for y in range(15, 21)] + [(21, 8, 22), (22, 11, 19)] +
              [(y, 11, 19) for y in range(23, 29)] + [(29, 10, 20), (30, 9, 21)]),
    'east': ([(10, 13, 18), (11, 12, 19)] + [(y, 11, 20) for y in range(12, 21)] + [(21, 11, 19), (22, 12, 19),
             (23, 13, 18)] + [(y, 13, 18) for y in range(24, 29)] + [(29, 13, 19), (30, 13, 20)]),
}
# щели между ногами и между рукой и торсом на юге/севере
GAPS_SOUTH = [(15, y) for y in range(23, 31)] + [(10, y) for y in (16, 17, 21)] + [(20, y) for y in (16, 17, 21)]


def undersuit(cv, suit, view, bulky):
    spans = BODY['east' if view == 'east' else 'south']
    gaps = set(GAPS_SOUTH) if view != 'east' else set()
    if bulky:
        spans = [(y, x0 - (1 if 12 <= y <= 20 else 0), x1 + (1 if 12 <= y <= 20 else 0)) for y, x0, x1 in spans]
    filled = {(x, y) for y, x0, x1 in spans for x in range(x0, x1 + 1) if (x, y) not in gaps}
    for (x, y) in filled:
        edge = (x - 1, y) not in filled or (x + 1, y) not in filled
        c = suit[0] if edge else (suit[2] if (x + y) % 7 == 0 else suit[1])
        cv.put(x, y, c)
    # рёбра комбинезона: швы на груди и бёдрах
    if view != 'east':
        for x in (13, 17):
            for y in range(12, 22):
                if (x, y) in filled:
                    cv.put(x, y, suit[2])
    # ботинки комбинезона
    for y in (30, 31):
        for x0, x1 in (((9, 14), (16, 21)) if view != 'east' else ((13, 20),)):
            for x in range(x0, x1 + 1):
                if y == 31 or (x, y) in filled:
                    cv.put(x, y, suit[0] if x in (x0, x1) else suit[1])


def exo_frame(cls, faction, view):
    """Рама экзоскелета на одном направлении (south / north / east). Запад — зеркало востока."""
    cv = Canvas()
    heavy = cls == 'heavy'
    light = cls == 'light'
    if not light:
        undersuit(cv, faction['suit'], view, heavy)
    w = 2 if heavy else 1  # толщина стоек

    if view in ('south', 'north'):
        back = view == 'north'
        # пояс с пряжкой
        bar(cv, 11, 22, 19, 22)
        cv.put(15, 22, HAZARD if not back else METAL[2])
        # стойки рук по внешней стороне, приводы плеча, локтя и запястья
        for side in (-1, 1):
            ox = 15 + side * 8  # внешний край руки: 7 / 23
            sx = ox if side < 0 else ox - w + 1
            bar(cv, min(sx, sx + w - 1), 12, max(sx, sx + w - 1), 20)
            joint(cv, 15 + side * 6, 11, big=heavy)                  # плечо
            joint(cv, ox + (0 if side < 0 else 0), 16, big=heavy, light=HAZARD if heavy else None)  # локоть
            # стойки ног по внешней стороне: бедро — колено — голень
            lx = 15 + side * 4
            bar(cv, lx, 23, lx, 29) if not heavy else bar(cv, min(lx, lx - side), 23, max(lx, lx - side), 29)
            joint(cv, lx, 23, big=heavy)                                 # тазобедренный привод
            joint(cv, lx, 27, big=heavy, light=HAZARD)                   # колено
            # плечевое коромысло
            bar(cv, min(15 + side * 3, 15 + side * 5), 10, max(15 + side * 3, 15 + side * 5), 10)
        if back:
            # хребет и силовой блок
            bar(cv, 15, 10, 15, 22)
            if not light:
                top, bottom = (11, 17) if heavy else (12, 16)
                x0, x1 = (11, 19) if heavy else (12, 18)
                cv.rect(x0, top, x1, bottom, METAL[1])
                bar(cv, x0, top, x1, top)
                cv.rect(x0, bottom, x1, bottom, METAL[0])
                for x in range(x0 + 1, x1, 2):
                    cv.put(x, top + 2, METAL[0])
                cv.put(x0 + 1, bottom - 1, GLOW)
                cv.put(x1 - 1, bottom - 1, GLOW)
        else:
            # нагрудная сбруя: две полосы от плеч к поясу
            for x, y in ((12, 12), (13, 14), (13, 16), (13, 18), (13, 20)):
                cv.put(x, y, METAL[2])
                cv.put(30 - x, y, METAL[2])
            if heavy:
                cv.rect(14, 13, 16, 14, METAL[1])
                cv.put(15, 13, GLOW)
    else:  # east
        # силовой блок за спиной (спина — слева)
        if light:
            bar(cv, 11, 11, 11, 21)
        else:
            x0 = 8 if heavy else 9
            cv.rect(x0, 11, 11, 18 if heavy else 17, METAL[1])
            bar(cv, x0, 11, 11, 11)
            cv.rect(x0, 18 if heavy else 17, 11, 18 if heavy else 17, METAL[0])
            cv.put(x0, 14, GLOW)
        bar(cv, 12, 10, 12, 22)  # хребет вдоль спины
        bar(cv, 12, 22, 19, 22)  # пояс
        # рука: стойка вдоль плеча и предплечья
        joint(cv, 15, 11, big=heavy)
        bar(cv, 15, 12, 15 + w - 1, 15)
        joint(cv, 16, 16, big=heavy, light=HAZARD if heavy else None)
        bar(cv, 17, 17, 18, 17)
        # нога: стойка по бедру и голени
        joint(cv, 15, 23, big=heavy)
        bar(cv, 15, 24, 15 + w - 1, 26)
        joint(cv, 17, 27, big=heavy, light=HAZARD)
        bar(cv, 16, 28, 16 + w - 1, 29)
    return cv.image()


# ----------------------------------------------------------------------------------------------------------------
# Пластины (по слотам модулей), класс пластин: composite / light / heavy.

def torso_plate(cls, f, view):
    cv = Canvas()
    pal, lac, acc = f['plate'], f['lacing'], f['accent']
    if view in ('south', 'north'):
        back = view == 'north'
        if cls == 'composite':
            # нагрудник: две широкие полосы
            spans = [(12, 12, 18), (13, 12, 18), (14, 12, 18), (15, 13, 17), (16, 13, 17), (17, 13, 17)]
            if back:
                spans = [(y, 12, 18) for y in range(17, 21)]
            plate(cv, spans, pal, band=3, lacing=lac)
        elif cls == 'light':
            # до: ламели через 2 пикселя со шнуровкой
            spans = [(11, 12, 18)] + [(y, 11, 19) for y in range(12, 21)] + [(21, 12, 18)]
            if back:
                spans = [(y, 11, 19) for y in range(17, 22)]
            plate(cv, spans, pal, band=3, lacing=lac, accent=acc if not back else None)
        else:
            # массивный до с воротом и юбкой-кусадзури
            spans = [(10, 12, 18)] + [(y, 10, 20) for y in range(11, 22)]
            if back:
                spans = [(y, 10, 20) for y in range(18, 22)]
            plate(cv, spans, pal, band=3, lacing=lac, accent=acc if not back else None)
            for x0, x1 in ((10, 14), (16, 20)):
                plate(cv, [(y, x0, x1) for y in range(22, 26)], pal, band=3, lacing=lac)
            if not back:
                cv.rect(14, 10, 16, 10, pal[1])  # ворот-нодова
    else:  # east: грудь справа
        if cls == 'composite':
            plate(cv, [(y, 16, 20) for y in range(12, 18)], pal, band=3, lacing=lac, light='right')
        elif cls == 'light':
            plate(cv, [(11, 14, 19)] + [(y, 13, 20) for y in range(12, 22)], pal, band=3, lacing=lac, light='right')
        else:
            plate(cv, [(10, 14, 19)] + [(y, 12, 21) for y in range(11, 22)], pal, band=3, lacing=lac, light='right')
            plate(cv, [(y, 12, 19) for y in range(22, 26)], pal, band=3, lacing=lac, light='right')
    return cv.image()


def arm_plate(cls, f, view):
    """Правая рука персонажа (на юге — слева от зрителя). Остальные получаются зеркалом."""
    cv = Canvas()
    pal, lac, acc = f['plate'], f['lacing'], f['accent']
    if view == 'south':
        if cls == 'composite':
            plate(cv, [(11, 9, 10), (12, 8, 10), (13, 8, 10)], pal, band=3, lacing=lac)        # содэ
            plate(cv, [(y, 7, 9) for y in range(17, 20)], pal, band=3, lacing=lac)              # котэ
        elif cls == 'light':
            plate(cv, [(11, 8, 10), (12, 7, 10), (13, 7, 10), (14, 7, 10), (15, 7, 10)], pal, band=3, lacing=lac)
            plate(cv, [(y, 7, 10) for y in range(17, 21)], pal, band=3, lacing=lac)
        else:
            plate(cv, [(10, 8, 10), (11, 6, 10)] + [(y, 5, 10) for y in range(12, 17)], pal, band=3, lacing=lac,
                  accent=acc)
            plate(cv, [(y, 6, 10) for y in range(17, 21)], pal, band=3, lacing=lac)
    else:  # east — ближняя рука (левая); правую сзади не видно
        if cls == 'composite':
            plate(cv, [(11, 14, 16), (12, 14, 17), (13, 14, 17)], pal, band=3, lacing=lac, light='right')
            plate(cv, [(y, 17, 19) for y in range(17, 20)], pal, band=3, lacing=lac, light='right')
        elif cls == 'light':
            plate(cv, [(11, 14, 17)] + [(y, 13, 18) for y in range(12, 16)], pal, band=3, lacing=lac, light='right')
            plate(cv, [(y, 16, 19) for y in range(16, 20)], pal, band=3, lacing=lac, light='right')
        else:
            plate(cv, [(10, 13, 17)] + [(y, 12, 19) for y in range(11, 17)], pal, band=3, lacing=lac, light='right',
                  accent=acc)
            plate(cv, [(y, 16, 20) for y in range(17, 21)], pal, band=3, lacing=lac, light='right')
    return cv.image()


def leg_plate(cls, f, view):
    """Правая нога персонажа (на юге — слева от зрителя)."""
    cv = Canvas()
    pal, lac = f['plate'], f['lacing']
    if view in ('south', 'north'):
        back = view == 'north'
        if cls == 'composite':
            plate(cv, [(y, 11, 14) for y in range(23, 26)], pal, band=3, lacing=lac)       # хайдатэ
            if not back:
                plate(cv, [(y, 12, 14) for y in range(27, 30)], pal, band=3, lacing=lac)   # сунэатэ
        elif cls == 'light':
            plate(cv, [(y, 11, 14) for y in range(23, 27)], pal, band=3, lacing=lac)
            plate(cv, [(y, 11, 14) for y in range(27, 30)], pal, band=3, lacing=lac, laced=back)
            if not back:
                cv.rect(12, 27, 13, 27, pal[4])  # наколенник
        else:
            plate(cv, [(y, 10, 14) for y in range(23, 27)], pal, band=3, lacing=lac)
            plate(cv, [(y, 10, 14) for y in range(27, 30)], pal, band=3, lacing=lac, laced=False)
            if not back:
                cv.rect(11, 27, 13, 28, pal[4])
                cv.put(12, 28, pal[2])
    else:  # east — ближняя нога (левая)
        if cls == 'composite':
            plate(cv, [(y, 14, 18) for y in range(23, 26)], pal, band=3, lacing=lac, light='right')
            plate(cv, [(y, 16, 18) for y in range(27, 30)], pal, band=3, lacing=lac, light='right')
        elif cls == 'light':
            plate(cv, [(y, 14, 18) for y in range(23, 27)], pal, band=3, lacing=lac, light='right')
            plate(cv, [(y, 15, 18) for y in range(27, 30)], pal, band=3, lacing=lac, light='right', laced=False)
            cv.put(18, 27, pal[4])
        else:
            plate(cv, [(y, 13, 19) for y in range(23, 27)], pal, band=3, lacing=lac, light='right')
            plate(cv, [(y, 14, 19) for y in range(27, 30)], pal, band=3, lacing=lac, light='right', laced=False)
            cv.rect(18, 27, 19, 28, pal[4])
    return cv.image()


def four(south, north, east, west):
    out = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
    for (x, y), im in zip(((0, 0), (32, 0), (0, 32), (32, 32)), (south, north, east, west)):
        out.alpha_composite(im, (x, y))
    return out


EMPTY = Image.new('RGBA', (32, 32), (0, 0, 0, 0))


def part_layers(cls, f):
    """Пять слоёв пластин по четырём направлениям."""
    t = torso_plate(cls, f, 'south'), torso_plate(cls, f, 'north'), torso_plate(cls, f, 'east')
    torso = four(t[0], t[1], t[2], mirror(t[2]))

    a_s, a_e = arm_plate(cls, f, 'south'), arm_plate(cls, f, 'east')
    # правая рука: юг — слева, север — справа (зеркало), восток — дальняя (не видна), запад — ближняя
    arm_right = four(a_s, mirror(a_s), EMPTY, mirror(a_e))
    arm_left = four(mirror(a_s), a_s, a_e, EMPTY)

    l_s, l_n, l_e = leg_plate(cls, f, 'south'), leg_plate(cls, f, 'north'), leg_plate(cls, f, 'east')
    leg_right = four(l_s, mirror(l_n), EMPTY, mirror(l_e))
    leg_left = four(mirror(l_s), l_n, l_e, EMPTY)
    return {'torso': torso, 'arm_left': arm_left, 'arm_right': arm_right, 'leg_left': leg_left,
            'leg_right': leg_right}


# ----------------------------------------------------------------------------------------------------------------
# Шлемы с противогазом.

def helmet(cls, f, view):
    cv = Canvas()
    pal = f['plate']
    heavy = cls == 'heavy'
    lens = f['lens_heavy'] if heavy else f['lens_light']
    mask = METAL[1]
    if view in ('south', 'north'):
        back = view == 'north'
        # купол
        if heavy:
            shell = [(1, 13, 17), (2, 11, 19), (3, 10, 20), (4, 10, 20), (5, 10, 20)]
        else:
            shell = [(2, 13, 17), (3, 12, 18), (4, 11, 19), (5, 11, 19)]
        # лицо — маска, по бокам — сиқоро (ламели назатыльника)
        face = [(y, 12, 18) for y in range(6, 10)] + [(10, 13, 17)]
        for y, x0, x1 in face:
            for x in range(x0, x1 + 1):
                cv.put(x, y, METAL[0] if x in (x0, x1) else mask)
        if back:
            plate(cv, shell + [(y, 10 if heavy else 11, 20 if heavy else 19) for y in range(6, 10)], pal, band=2,
                  lacing=f['lacing'])
        else:
            plate(cv, shell, pal, band=6 if not heavy else 3, lacing=f['lacing'], laced=False)
            # нащёчники
            for x in ((10, 11, 19, 20) if heavy else (11, 19)):
                for y in range(6, 10 if heavy else 9):
                    cv.put(x, y, pal[1] if x in (10, 20) else pal[2])
            if heavy:
                # две круглые линзы и рыло фильтра
                for lx in (13, 17):
                    cv.put(lx, 6, lens)
                    cv.put(lx, 7, lens)
                    cv.put(lx - 1 if lx < 15 else lx + 1, 6, METAL[0])
                cv.rect(14, 8, 16, 10, METAL[2])
                cv.put(15, 9, METAL[0])
                cv.put(14, 10, METAL[0])
                cv.put(16, 10, METAL[0])
                cv.rect(12, 1, 18, 1, pal[4])
                cv.put(15, 2, f['accent'])  # гребень
            else:
                # узкий визор и два щёчных фильтра
                cv.rect(12, 6, 18, 6, lens)
                cv.put(12, 6, METAL[0])
                cv.put(18, 6, METAL[0])
                cv.put(13, 9, METAL[2])
                cv.put(17, 9, METAL[2])
                cv.put(15, 8, METAL[0])
    else:  # east — лицо справа
        if heavy:
            shell = [(1, 13, 17), (2, 11, 19), (3, 10, 20), (4, 10, 20), (5, 10, 20)]
            neck = [(y, 9, 13) for y in range(6, 11)]
        else:
            shell = [(2, 13, 17), (3, 12, 19), (4, 11, 19), (5, 11, 19)]
            neck = [(y, 10, 13) for y in range(6, 10)]
        for y in range(6, 10):
            for x in range(13, 21):
                cv.put(x, y, METAL[0] if x == 20 else mask)
        plate(cv, shell, pal, band=3 if heavy else 6, lacing=f['lacing'], laced=False, light='right')
        plate(cv, neck, pal, band=3, lacing=f['lacing'], light='right')
        if heavy:
            cv.put(19, 6, lens)
            cv.put(19, 7, lens)
            cv.rect(20, 8, 21, 10, METAL[2])
            cv.put(21, 9, METAL[0])
            cv.rect(14, 6, 15, 9, pal[2])
        else:
            cv.rect(17, 6, 20, 6, lens)
            cv.put(19, 8, METAL[2])
            cv.put(20, 9, METAL[2])
    return cv.image()


def helmet_layers(cls, f):
    s, n, e = helmet(cls, f, 'south'), helmet(cls, f, 'north'), helmet(cls, f, 'east')
    return four(s, n, e, mirror(e))


# ----------------------------------------------------------------------------------------------------------------

def icon_from(layer, only=0, prefer_side=False, main_piece=False):
    """Иконка: кусок с направления, обрезанный и увеличенный целым коэффициентом под 32x32, с тёмным контуром."""
    frs = [layer.crop((x, y, x + 32, y + 32)) for (x, y) in ((0, 0), (32, 0), (0, 32), (32, 32))]
    candidates = [frs[only]] if only is not None else frs
    best, area = None, -1
    for i, fr in enumerate(candidates):
        box = fr.getbbox()
        if not box:
            continue
        a = (box[2] - box[0]) * (box[3] - box[1]) * (1.3 if prefer_side and i >= 2 else 1)
        if a > area:
            best, area = fr.crop(box), a
    out = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    if best is None:
        return out
    w, h = best.size
    k = max(1, min(28 // max(w, h), 4))
    piece = best.resize((w * k, h * k), Image.NEAREST)
    outline = Image.new('RGBA', (piece.width + 2, piece.height + 2), (0, 0, 0, 0))
    alpha = piece.split()[3]
    dark = Image.new('RGBA', piece.size, (18, 20, 16, 255))
    for dx, dy in ((0, 1), (2, 1), (1, 0), (1, 2)):
        outline.paste(dark, (dx, dy), alpha)
    outline.alpha_composite(piece, (1, 1))
    out.alpha_composite(outline, ((32 - outline.width) // 2, (32 - outline.height) // 2))
    return out


def write_rsi(path, states):
    os.makedirs(path, exist_ok=True)
    for name in os.listdir(path):
        os.remove(os.path.join(path, name))
    meta_states = []
    for name, (img, dirs) in states.items():
        img.save(os.path.join(path, name + '.png'))
        st = {'name': name}
        if dirs == 4:
            st['directions'] = 4
        meta_states.append(st)
    meta = {'version': 1, 'license': 'CC-BY-SA-3.0', 'copyright': COPYRIGHT, 'size': {'x': 32, 'y': 32},
            'states': meta_states}
    with open(os.path.join(path, 'meta.json'), 'w', encoding='utf-8') as fh:
        json.dump(meta, fh, indent=4, ensure_ascii=False)
        fh.write('\n')


EXO_PLATES = {'light': 'composite', 'medium': 'light', 'heavy': 'heavy'}


def main():
    preview = []
    for fid, f in FACTIONS.items():
        plates = {}
        for cls in ('composite', 'light', 'heavy'):
            layers = part_layers(cls, f)
            plates[cls] = layers
            states = {}
            for part in PARTS:
                states[part] = (layers[part], 4)
                states['icon_' + part] = (icon_from(layers[part], only=None if part != 'torso' else 0,
                                                    prefer_side=part.startswith('arm')), 1)
            write_rsi(os.path.join(OUT_OUTER, '%s_%s.rsi' % (fid, cls)), states)

        for cls in ('light', 'medium', 'heavy'):
            frame = four(exo_frame(cls, f, 'south'), exo_frame(cls, f, 'north'), exo_frame(cls, f, 'east'),
                         mirror(exo_frame(cls, f, 'east')))
            write_rsi(os.path.join(OUT_OUTER, '%s_exo_%s.rsi' % (fid, cls)),
                      {'icon': (icon_from(frame, only=0), 1), 'equipped-OUTERCLOTHING': (frame, 4)})
            helm = helmet_layers(cls, f)
            empty4 = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
            write_rsi(os.path.join(OUT_HEAD, '%s_helmet_%s.rsi' % (fid, cls)),
                      {'icon': (icon_from(helm, only=2), 1), 'icon-unshaded': (EMPTY, 1),
                       'equipped-head': (helm, 4), 'equipped-head-unshaded': (empty4, 4)})
            full = frame.copy()
            for part in PARTS:
                full.alpha_composite(plates[EXO_PLATES[cls]][part])
            full.alpha_composite(helm)
            preview.append(('%s %s' % (f['name'], cls), frame, full))

    # превью: на фигуре человека (по маскам частей тела), голая рама и полный комплект
    base = os.path.join(TEX, 'Mobs/Species/Human/parts.rsi')
    body = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
    for st in ('torso_m', 'head_m', 'l_arm', 'r_arm', 'l_hand', 'r_hand', 'l_leg', 'r_leg', 'l_foot', 'r_foot'):
        im = Image.open(os.path.join(base, st + '.png')).convert('RGBA').crop((0, 0, 64, 64))
        tint = Image.new('RGBA', im.size, (196, 160, 130, 255))
        tint.putalpha(im.split()[3])
        body.alpha_composite(tint)
    uniform = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
    uni_px = body.load()
    for y in range(64):
        for x in range(64):
            if uni_px[x, y][3] and y % 32 >= 10:
                uniform.putpixel((x, y), (92, 98, 70, 255))
    Z = 4
    rows = []
    for name, frame, full in preview:
        a = body.copy()
        a.alpha_composite(uniform)
        a.alpha_composite(frame)
        b = body.copy()
        b.alpha_composite(uniform)
        b.alpha_composite(full)
        rows.append((name, a, b))
    W = 4 * 32 * Z
    sheet = Image.new('RGBA', (W * 2 + 12, len(rows) * (32 * Z + 16)), (52, 54, 60, 255))
    d = ImageDraw.Draw(sheet)
    for r, (name, a, b) in enumerate(rows):
        y = r * (32 * Z + 16)
        d.text((4, y + 2), name + ' — frame / full', fill=(235, 235, 235))
        for c, img in enumerate((a, b)):
            frs = [img.crop((x, yy, x + 32, yy + 32)) for (x, yy) in ((0, 0), (32, 0), (0, 32), (32, 32))]
            for i, fr in enumerate(frs):
                sheet.alpha_composite(fr.resize((32 * Z, 32 * Z), Image.NEAREST),
                                      (c * (W + 12) + i * 32 * Z, y + 14))
    sheet.save(os.path.join(HERE, 'preview.png'))


if __name__ == '__main__':
    main()
