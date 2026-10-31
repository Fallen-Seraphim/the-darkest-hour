#!/usr/bin/env python3
"""DarkestHour: спрайт окопа (Resources/Textures/_DarkestHour/Structures/trench.rsi).

Окоп соединяется с соседними (CornerSpriteSmoothing, base «trench»): состояния trench0..trench7, в каждом 4 направления —
четверти тайла (S = юго-восток, N = северо-запад, E = северо-восток, W = юго-запад). Номер — флаги соседей угла:
1 — против часовой, 2 — по диагонали, 4 — по часовой. Рисуется процедурно: тёмное дно, песчаный бруствер по краям,
где соседнего окопа нет, тень у стенок. Лицензия — CC-BY-SA-3.0 (как у остальных спрайтов сервера).
"""
import json
import os
import random

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
OUT = os.path.join(ROOT, 'Resources/Textures/_DarkestHour/Structures/trench.rsi')
S = 32
H = S // 2

FLOOR = (78, 62, 42)
FLOOR_DARK = (60, 47, 31)
WALL = (112, 88, 58)
RIM = (196, 164, 104)
RIM_DARK = (164, 134, 82)
RIM_W = 4   # ширина бруствера
WALL_W = 3  # стенка под бруствером


def noise(rng, c, k=6):
    d = rng.randint(-k, k)
    return tuple(max(0, min(255, v + d)) for v in c) + (255,)


def quadrant(edge_a, edge_b, corner_notch, rng, ax, ay):
    """Четверть 16×16 в локальных координатах, где угол тайла — (0,0) после отражения ax/ay.
    edge_a — бруствер вдоль верхней кромки (y=0), edge_b — вдоль боковой (x=0), notch — внутренний угол."""
    q = Image.new('RGBA', (H, H))
    px = q.load()
    for y in range(H):
        for x in range(H):
            c = noise(rng, FLOOR_DARK if rng.random() < 0.18 else FLOOR, 5)
            da = y if edge_a else 99
            db = x if edge_b else 99
            dn = max(x, y) if corner_notch else 99
            d = min(da, db, dn)
            if d < RIM_W:
                c = noise(rng, RIM if d < RIM_W - 1 else RIM_DARK, 8)
            elif d < RIM_W + WALL_W:
                c = noise(rng, WALL, 6)
            elif d < RIM_W + WALL_W + 2:
                c = noise(rng, FLOOR_DARK, 4)
            px[x, y] = c
    if ax:
        q = q.transpose(Image.FLIP_LEFT_RIGHT)
    if ay:
        q = q.transpose(Image.FLIP_TOP_BOTTOM)
    return q


# Угол → (позиция четверти в тайле, отражения, кромка флага 1, кромка флага 4) по CornerSpriteSmoothing:
# SE: 1 — юг, 4 — восток; NE: 1 — восток, 4 — север; NW: 1 — север, 4 — запад; SW: 1 — запад, 4 — юг.
# Кромка «a» — горизонтальная (север/юг), «b» — вертикальная (восток/запад).
CORNERS = {
    # dir index в RSI: 0 = South(SE), 1 = North(NW), 2 = East(NE), 3 = West(SW)
    'SE': dict(dir=0, pos=(H, H), flip=(True, True), ccw='a', cw='b'),
    'NW': dict(dir=1, pos=(0, 0), flip=(False, False), ccw='a', cw='b'),
    'NE': dict(dir=2, pos=(H, 0), flip=(True, False), ccw='b', cw='a'),
    'SW': dict(dir=3, pos=(0, H), flip=(False, True), ccw='b', cw='a'),
}


def corner_image(name, fill):
    c = CORNERS[name]
    ccw = bool(fill & 1)
    diag = bool(fill & 2)
    cw = bool(fill & 4)
    has = {c['ccw']: ccw, c['cw']: cw}
    edge_a = not has['a']
    edge_b = not has['b']
    notch = has['a'] and has['b'] and not diag
    rng = random.Random(sum(map(ord, name)) * 31 + fill)
    q = quadrant(edge_a, edge_b, notch, rng, *c['flip'])
    img = Image.new('RGBA', (S, S))
    img.paste(q, c['pos'])
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    states = []
    for fill in range(8):
        sheet = Image.new('RGBA', (S * 2, S * 2))
        # порядок кадров направлений: S, N, E, W — слева направо, сверху вниз
        for name, c in CORNERS.items():
            i = c['dir']
            sheet.paste(corner_image(name, fill), ((i % 2) * S, (i // 2) * S))
        sheet.save(os.path.join(OUT, 'trench%d.png' % fill))
        states.append({'name': 'trench%d' % fill, 'directions': 4})

    full = Image.new('RGBA', (S, S))
    for name in CORNERS:
        full.alpha_composite(corner_image(name, 0))
    full.save(os.path.join(OUT, 'full.png'))
    states.insert(0, {'name': 'full'})

    # Повреждённый окоп: тот же одиночный окоп, наполовину засыпанный — бруствер с одной стороны осыпался внутрь
    # неровным языком песка, по дну раскиданы комья.
    damaged = full.copy()
    damaged_px = damaged.load()
    rng = random.Random(1337)
    edge = [rng.uniform(-3, 3) for _ in range(S)]
    for i in range(1, S):  # сгладить кромку осыпи
        edge[i] = edge[i - 1] * 0.6 + edge[i] * 0.4
    for y in range(S):
        for x in range(S):
            # Осыпь идёт от северо-западного угла: чем дальше по диагонали, тем тоньше слой.
            depth = 31 + edge[(x - y) % S] * 5 - (x + y)
            if depth <= 0:
                continue
            if depth < 3 and rng.random() < 0.45:
                continue
            base = RIM if depth > 9 else RIM_DARK
            damaged_px[x, y] = noise(rng, base, 7)
    for _ in range(10):  # комья на открытой части дна
        x, y = rng.randrange(18, S - 5), rng.randrange(18, S - 5)
        if x + y > 40:
            damaged_px[x, y] = noise(rng, RIM_DARK, 6)
            damaged_px[x + 1, y] = noise(rng, RIM_DARK, 6)
    damaged.save(os.path.join(OUT, 'damaged.png'))
    states.insert(1, {'name': 'damaged'})

    meta = {
        'version': 1,
        'license': 'CC-BY-SA-3.0',
        'copyright': 'Procedurally drawn for The Darkest Hour (Tools/_DarkestHour/trench/build.py)',
        'size': {'x': S, 'y': S},
        'states': states,
    }
    with open(os.path.join(OUT, 'meta.json'), 'w') as f:
        json.dump(meta, f, indent=4)


if __name__ == '__main__':
    main()
