#!/usr/bin/env python3
"""Спрайты экзоскелетов и пластин модульной брони (решение автора 2026-11-11).

Экзоскелет — силовая рама в духе «Грани будущего»: стойки вдоль рук и ног, приводы в суставах, хребет и силовой
блок на спине; у среднего и тяжёлого под рамой герметичный комбинезон, лёгкий надевается поверх формы.
Пластины (правка автора 2026-11-11) — гладкие формованные, как у штурмовиков и клонов «Звёздных войн»: тонкие,
атлетичные, с округлыми наплечниками; между пластинами виден комбинезон. Тяжёлые — выше и массивнее, в духе силовой
брони Fallout и Комбайна HL2: цельный корпус с высоким воротом, большие наплечники, силовой блок в спине. Империя —
белые пластины, Республика — зелёные. Шлемы — со встроенным противогазом: у тяжёлого две линзы (у Империи жёлтые,
у Республики зелёные) и два фильтра по бокам, шлем выше; у среднего и лёгкого — каска поверх противогаза в духе ПМК-4
(панорамное стекло, круглое переговорное устройство, фильтр на щеке).

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
        accent=hexc('#e2762a'),
        suit=[hexc(c) for c in ('#141519', '#22242a', '#30333a', '#3d4048')],
        lens=hexc('#f2c230'),
        rubber=[hexc(c) for c in ('#1d1e20', '#3a3c40', '#55585d', '#7a7e84')],
    ),
    'rmb1': dict(
        name='RMB-1',
        plate=[hexc(c) for c in ('#1f2617', '#3b4729', '#556638', '#6e8248', '#8b9f5d')],
        accent=hexc('#c9a445'),
        suit=[hexc(c) for c in ('#15180f', '#22281a', '#2f3624', '#3c442e')],
        lens=hexc('#5ee05a'),
        rubber=[hexc(c) for c in ('#1c1f17', '#3a4033', '#555c4a', '#7a8268')],
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


def plate(cv, spans, pal, light='left', **_):
    """
    Гладкая формованная пластина (как у штурмовиков): тёмный контур, блик по верхнему и освещённому краю, тень по
    нижнему и теневому, основной тон внутри. spans — [(y, x0, x1)].
    """
    filled = {(x, y) for y, x0, x1 in spans for x in range(x0, x1 + 1)}

    def edge(x, y):
        return any((x + dx, y + dy) not in filled for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))

    lx = -1 if light == 'left' else 1
    for (x, y) in filled:
        if edge(x, y):
            c = pal[0]
        elif edge(x, y - 1) and (x, y - 1) in filled or edge(x + lx, y) and (x + lx, y) in filled:
            c = pal[4]
        elif edge(x, y + 1) and (x, y + 1) in filled or edge(x - lx, y) and (x - lx, y) in filled:
            c = pal[2]
        else:
            c = pal[3]
        cv.put(x, y, c)


def rows(y0, y1, x0, x1):
    return [(y, x0, x1) for y in range(y0, y1 + 1)]


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
    """Нагрудник и спинные пластины. Средний — грудь и пресс раздельно (между ними виден комбинезон), композитный —
    только грудь, тяжёлый — цельная бочка с высоким воротом, как у силовой брони."""
    cv = Canvas()
    pal = f['plate']
    if view == 'south':
        if cls == 'composite':
            # грудь, сегменты пресса, боковые рёбра и воротник
            plate(cv, [(11, 12, 18), (12, 11, 19), (13, 11, 19), (14, 12, 18), (15, 13, 17)], pal)
            plate(cv, rows(17, 18, 12, 14), pal)
            plate(cv, rows(17, 18, 16, 18), pal)
            plate(cv, rows(20, 21, 13, 17), pal)
            plate(cv, rows(17, 20, 11, 11) + [(16, 11, 11)], pal)
            plate(cv, rows(17, 20, 19, 19) + [(16, 19, 19)], pal)
            cv.rect(13, 10, 17, 10, pal[2])
            cv.put(15, 12, pal[2])
        elif cls == 'light':
            plate(cv, [(11, 12, 18), (12, 11, 19), (13, 11, 19), (14, 11, 19), (15, 12, 18), (16, 13, 17)], pal)
            plate(cv, rows(18, 20, 13, 17), pal)
            cv.put(15, 13, pal[2])
            cv.put(15, 14, pal[2])
        else:
            plate(cv, [(8, 12, 18), (9, 11, 19)] + rows(10, 18, 10, 20) + [(19, 11, 19), (20, 11, 19), (21, 12, 18)],
                  pal)
            cv.rect(13, 9, 17, 9, METAL[1])          # горловина под шлем
            for x in range(12, 19):
                cv.put(x, 16, pal[1])                # шов груди и живота
            for y in range(11, 16):                  # боковые панели груди
                cv.put(12, y, pal[2])
                cv.put(18, y, pal[2])
            for x, y in ((11, 11), (19, 11), (11, 19), (19, 19), (13, 14), (17, 14)):
                cv.put(x, y, pal[1])                 # заклёпки
            for x in (13, 15, 17):                   # вентиляция живота
                cv.rect(x, 17, x, 19, METAL[0])
            cv.put(12, 10, METAL[2])                 # шланги к шлему
            cv.put(18, 10, METAL[2])
            cv.rect(14, 12, 16, 13, METAL[1])        # индикатор питания
            cv.put(15, 12, f['accent'])
            cv.put(14, 13, GLOW)
    elif view == 'north':
        if cls == 'composite':
            plate(cv, rows(12, 14, 11, 13), pal)
            plate(cv, rows(12, 14, 17, 19), pal)
            plate(cv, rows(17, 18, 12, 14), pal)
            plate(cv, rows(17, 18, 16, 18), pal)
            plate(cv, rows(20, 21, 12, 18), pal)
        elif cls == 'light':
            plate(cv, [(11, 12, 13)] + rows(12, 15, 11, 14), pal)
            plate(cv, [(11, 17, 18)] + rows(12, 15, 16, 19), pal)
            plate(cv, rows(18, 20, 12, 18), pal)
        else:
            plate(cv, [(8, 12, 18), (9, 11, 19)] + rows(10, 20, 10, 20) + [(21, 12, 18)], pal)
            # силовой блок, утопленный в спину
            cv.rect(12, 11, 18, 16, METAL[1])
            cv.rect(12, 11, 18, 11, METAL[3])
            for x in (13, 15, 17):
                cv.put(x, 13, METAL[0])
                cv.put(x, 14, METAL[0])
            cv.put(12, 16, GLOW)
            cv.put(18, 16, GLOW)
            for x, y in ((11, 11), (19, 11), (11, 19), (19, 19), (11, 17), (19, 17)):
                cv.put(x, y, pal[1])
            for x in range(12, 19):
                cv.put(x, 18, pal[2])
            cv.rect(13, 8, 13, 9, METAL[3])          # выхлопные патрубки
            cv.rect(17, 8, 17, 9, METAL[3])
    else:  # east: грудь справа, спина слева
        if cls == 'composite':
            plate(cv, [(11, 15, 19), (12, 15, 20), (13, 15, 20), (14, 16, 20), (15, 17, 19)], pal, light='right')
            plate(cv, rows(17, 18, 16, 19), pal, light='right')
            plate(cv, rows(20, 21, 15, 18), pal, light='right')
            plate(cv, rows(12, 15, 10, 11), pal, light='right')
        elif cls == 'light':
            plate(cv, [(11, 15, 19)] + rows(12, 15, 15, 20) + [(16, 16, 19)], pal, light='right')
            plate(cv, rows(18, 20, 16, 19), pal, light='right')
        else:
            plate(cv, [(8, 13, 18), (9, 12, 19)] + rows(10, 19, 10, 21) + [(20, 11, 20), (21, 12, 19)], pal,
                  light='right')
            cv.rect(8, 11, 10, 17, METAL[1])          # силовой блок за спиной
            cv.rect(8, 11, 10, 11, METAL[3])
            cv.put(8, 15, GLOW)
            for y in (17, 18, 19):
                cv.rect(18, y, 19, y, METAL[0] if y % 2 else pal[2])
            cv.put(12, 9, METAL[2])
            cv.put(14, 12, pal[1])
            cv.put(14, 19, pal[1])
            cv.rect(17, 12, 19, 12, pal[2])
    return cv.image()


def arm_plate(cls, f, view):
    """Правая рука персонажа (на юге — слева от зрителя): округлый наплечник и наруч, у тяжёлого — ещё плечо целиком."""
    cv = Canvas()
    pal = f['plate']
    if view == 'south':
        if cls == 'composite':
            plate(cv, [(11, 8, 10), (12, 7, 11), (13, 7, 11), (14, 8, 10)], pal)   # наплечник
            plate(cv, rows(15, 16, 7, 9), pal)                                       # бицепс
            plate(cv, [(18, 7, 9), (19, 7, 9), (20, 8, 9)], pal)                     # наруч
            cv.put(8, 21, pal[2])                                                     # накладка кисти
        elif cls == 'light':
            plate(cv, [(10, 8, 10), (11, 7, 11), (12, 6, 11), (13, 6, 11), (14, 7, 10)], pal)
            plate(cv, [(16, 8, 9), (17, 7, 10), (18, 7, 10), (19, 8, 10)], pal)
        else:
            plate(cv, [(8, 8, 11), (9, 6, 12), (10, 5, 12), (11, 4, 12), (12, 4, 12), (13, 4, 12), (14, 5, 11),
                       (15, 6, 10)], pal)
            plate(cv, rows(16, 20, 5, 10), pal)
            for x in range(5, 12):
                cv.put(x, 13, pal[2])               # кромка наплечника
            cv.put(7, 10, pal[1])
            cv.put(10, 10, pal[1])
            cv.rect(4, 16, 4, 19, METAL[3])          # гидроцилиндр предплечья
            cv.put(4, 20, METAL[1])
            cv.rect(7, 18, 9, 18, pal[2])
            cv.rect(6, 21, 9, 21, METAL[2])          # накладка кисти
            cv.put(6, 17, f['accent'])
    else:  # east — ближняя рука (левая)
        if cls == 'composite':
            plate(cv, [(11, 14, 17), (12, 13, 18), (13, 13, 18), (14, 14, 17)], pal, light='right')
            plate(cv, rows(15, 16, 15, 17), pal, light='right')
            plate(cv, [(18, 17, 19), (19, 17, 19)], pal, light='right')
        elif cls == 'light':
            plate(cv, [(10, 14, 17), (11, 13, 18), (12, 13, 18), (13, 13, 18), (14, 14, 17)], pal, light='right')
            plate(cv, [(16, 17, 18), (17, 16, 19), (18, 16, 19), (19, 17, 19)], pal, light='right')
        else:
            plate(cv, [(8, 13, 17), (9, 12, 18), (10, 11, 19), (11, 11, 19), (12, 11, 19), (13, 11, 19),
                       (14, 12, 18), (15, 13, 17)], pal, light='right')
            plate(cv, rows(16, 20, 16, 20), pal, light='right')
            for x in range(12, 19):
                cv.put(x, 13, pal[2])
            cv.put(15, 10, pal[1])
            cv.rect(21, 16, 21, 19, METAL[3])
            cv.rect(17, 18, 19, 18, pal[2])
            cv.rect(17, 21, 20, 21, METAL[2])
    return cv.image()


def leg_plate(cls, f, view):
    """Правая нога персонажа (на юге — слева от зрителя): набедренник и щиток голени, между ними колено."""
    cv = Canvas()
    pal = f['plate']
    if view in ('south', 'north'):
        if cls == 'composite':
            plate(cv, [(23, 11, 14), (24, 11, 14), (25, 12, 14)], pal)              # набедренник
            plate(cv, [(28, 12, 14), (29, 12, 14), (30, 12, 13)], pal)              # голень
            if view == 'south':
                plate(cv, [(26, 12, 13), (27, 12, 13)], pal)                       # наколенник
            else:
                plate(cv, rows(26, 27, 12, 13), pal)                               # подколенный щиток
        elif cls == 'light':
            plate(cv, [(23, 11, 14), (24, 11, 14), (25, 11, 14), (26, 12, 14)], pal)
            plate(cv, [(28, 11, 14), (29, 11, 14), (30, 12, 13)], pal)
            if view == 'south':
                cv.put(12, 27, pal[3])
                cv.put(13, 27, pal[3])
        else:
            plate(cv, rows(22, 26, 10, 14), pal)
            plate(cv, [(27, 10, 14)] + rows(28, 31, 9, 15), pal)
            cv.rect(8, 23, 8, 28, METAL[3])          # гидроцилиндр бедра и голени
            cv.put(8, 24, METAL[1])
            cv.put(8, 29, METAL[1])
            for y in range(23, 26):
                cv.put(12, y, pal[2])
            cv.put(11, 23, pal[1])
            cv.put(13, 25, pal[1])
            cv.rect(10, 30, 14, 30, pal[2])          # носок ботинка
            if view == 'north':
                cv.rect(11, 29, 13, 29, METAL[1])    # пятка
            if view == 'south':
                cv.rect(11, 27, 13, 27, pal[4])
                cv.put(12, 27, pal[1])
    else:  # east — ближняя нога (левая)
        if cls == 'composite':
            plate(cv, [(23, 14, 17), (24, 14, 17), (25, 15, 17)], pal, light='right')
            plate(cv, [(26, 17, 18), (27, 17, 18)], pal, light='right')
            plate(cv, [(28, 16, 18), (29, 16, 18), (30, 16, 17)], pal, light='right')
        elif cls == 'light':
            plate(cv, [(23, 14, 18), (24, 14, 18), (25, 14, 18), (26, 15, 18)], pal, light='right')
            plate(cv, [(27, 17, 18), (28, 15, 18), (29, 15, 18), (30, 16, 18)], pal, light='right')
        else:
            plate(cv, rows(22, 26, 13, 19), pal, light='right')
            plate(cv, [(27, 14, 19)] + rows(28, 31, 13, 20), pal, light='right')
            cv.rect(18, 27, 19, 27, pal[4])
            cv.rect(12, 23, 12, 28, METAL[3])
            cv.put(12, 29, METAL[1])
            for y in range(23, 26):
                cv.put(16, y, pal[2])
            cv.put(18, 24, pal[1])
            cv.rect(15, 30, 19, 30, pal[2])
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

def pmk4(cv, f, view):
    """
    Противогаз в духе ПМК-4 (правка автора 2026-11-11) под каской: резиновая маска, широкое панорамное стекло-трапеция,
    круглое переговорное устройство с решёткой под ним и фильтр на левой щеке.
    """
    pal, lens, rub = f['plate'], f['lens'], f['rubber']
    glint = tuple(min(255, c + 90) for c in lens[:3]) + (255,)
    if view == 'south':
        face = [(4, 11, 19)] + rows(5, 7, 11, 19) + [(8, 12, 18), (9, 12, 18), (10, 13, 17), (11, 14, 16)]
        for y, x0, x1 in face:
            for x in range(x0, x1 + 1):
                cv.put(x, y, rub[0] if x in (x0, x1) else rub[1])
        # панорамное стекло
        cv.rect(12, 5, 18, 6, lens)
        cv.rect(13, 7, 17, 7, lens)
        cv.put(12, 5, glint)
        cv.put(13, 5, glint)
        cv.put(15, 7, rub[1])                       # перемычка над носом
        # переговорное устройство: кольцо с решёткой
        cv.rect(14, 8, 16, 10, rub[3])
        cv.put(15, 9, rub[0])
        cv.put(14, 8, rub[2])
        cv.put(16, 10, rub[2])
        cv.put(15, 11, rub[2])                      # клапан выдоха
        # фильтр на левой щеке (справа от зрителя)
        cv.rect(19, 7, 20, 9, pal[2])
        cv.put(19, 7, pal[3])
        cv.put(20, 9, pal[1])
        cv.put(21, 8, pal[1])
    elif view == 'north':
        # подшлемник маски закрывает затылок; ремни оголовья; фильтр выглядывает слева (левая щека)
        for y, x0, x1 in rows(5, 7, 11, 19) + [(8, 12, 18), (9, 13, 17)]:
            for x in range(x0, x1 + 1):
                cv.put(x, y, rub[0] if x in (x0, x1) else rub[1])
        cv.rect(12, 6, 18, 6, rub[2])
        cv.rect(13, 8, 17, 8, rub[2])
        cv.put(15, 7, rub[2])
        cv.rect(9, 7, 10, 9, pal[2])
        cv.put(9, 7, pal[3])
    else:  # east — видна левая щека с фильтром
        for y, x0, x1 in [(4, 11, 20)] + rows(5, 7, 11, 20) + [(8, 12, 20), (9, 13, 20), (10, 16, 19)]:
            for x in range(x0, x1 + 1):
                cv.put(x, y, rub[0] if x in (x0, x1) else rub[1])
        cv.rect(12, 6, 14, 6, rub[2])                # ремень оголовья
        cv.rect(18, 5, 20, 6, lens)
        cv.put(19, 7, lens)
        cv.put(18, 5, glint)
        cv.rect(19, 8, 21, 10, rub[3])               # переговорное устройство
        cv.put(21, 9, rub[0])
        cv.rect(15, 7, 17, 9, pal[2])                # фильтр на щеке
        cv.put(15, 7, pal[3])
        cv.put(17, 9, pal[1])
        cv.put(16, 8, pal[1])


def helmet(cls, f, view):
    """
    Тяжёлый — высокий массивный шлем с двумя линзами и двумя фильтрами (Комбайн / силовая броня). Средний и лёгкий —
    каска поверх противогаза в духе ПМК-4.
    """
    cv = Canvas()
    pal = f['plate']
    lens = f['lens']
    if cls != 'heavy':
        pmk4(cv, f, view)
        # каска: купол до бровей, сзади ниже
        if view == 'south':
            plate(cv, [(1, 13, 17), (2, 12, 18), (3, 11, 19), (4, 10, 20)], pal)
        elif view == 'north':
            plate(cv, [(1, 13, 17), (2, 12, 18), (3, 11, 19), (4, 10, 20), (5, 10, 20)], pal)
        else:
            plate(cv, [(1, 13, 17), (2, 12, 18), (3, 11, 19), (4, 10, 20), (5, 10, 13)], pal, light='right')
        return cv.image()

    def filt(x, y):
        side = 1 if x > 15 else -1
        cv.put(x, y, METAL[2])
        cv.put(x, y + 1, METAL[1])
        cv.put(x + side, y, METAL[3])
        cv.put(x + side, y + 1, METAL[1])

    if view in ('south', 'north'):
        dome = [(0, 13, 17), (1, 11, 19), (2, 10, 20)] + rows(3, 8, 10, 20) + [(9, 11, 19), (10, 12, 18)]
        plate(cv, dome, pal)
        if view == 'north':
            cv.rect(13, 8, 17, 8, pal[2])
            for x, y in ((11, 4), (19, 4), (12, 7), (18, 7)):
                cv.put(x, y, pal[1])
            cv.rect(14, 2, 16, 2, METAL[1])         # вентиляция
            return cv.image()
        for lx in ((12, 13), (17, 18)):
            cv.rect(lx[0], 5, lx[1], 6, lens)
            cv.put(lx[0], 5, tuple(min(255, c + 90) for c in lens[:3]) + (255,))
        cv.rect(14, 8, 16, 9, METAL[0])
        cv.put(15, 8, METAL[2])
        cv.rect(14, 10, 16, 10, METAL[1])           # решётка
        filt(12, 8)
        filt(18, 8)
        cv.rect(13, 2, 17, 2, pal[4])
        cv.put(15, 3, f['accent'])
        for x, y in ((11, 3), (19, 3), (11, 7), (19, 7)):
            cv.put(x, y, pal[1])                     # заклёпки
        cv.put(11, 10, METAL[2])                     # шланги вниз, к нагруднику
        cv.put(19, 10, METAL[2])
    else:  # east — лицо справа
        dome = [(0, 12, 17), (1, 11, 19), (2, 10, 20)] + rows(3, 8, 10, 21) + [(9, 11, 20), (10, 12, 19)]
        plate(cv, dome, pal, light='right')
        cv.rect(18, 5, 20, 6, lens)
        cv.rect(19, 8, 21, 9, METAL[0])
        cv.rect(19, 10, 21, 10, METAL[1])
        cv.put(17, 8, METAL[2])
        cv.put(17, 9, METAL[1])
        cv.put(16, 8, METAL[3])
        cv.put(16, 9, METAL[1])
        for x, y in ((12, 4), (12, 7), (15, 3)):
            cv.put(x, y, pal[1])
        cv.rect(11, 6, 13, 6, pal[2])                # кромка назатыльника
        cv.put(13, 10, METAL[2])
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
