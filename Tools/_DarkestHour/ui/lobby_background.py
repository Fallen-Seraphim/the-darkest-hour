#!/usr/bin/env python3
"""Фон лобби «Ночной Лост-Энджелс» (выбор автора 2026-11-11 по макетам «Интерфейс TDH»).

Пиксель-арт 480×270, увеличенный в 4 раза без сглаживания до 1920×1080: несколько планов небоскрёбов со светом
окон (янтарный и холодный), антенны с красными огнями, неоновые вывески, дымка у земли, прожекторы ПВО, косой дождь.
Два варианта: night (ясная ночь) и rain (ливень, туман, зарево на горизонте).

Запуск из корня репозитория:  python3 Tools/_DarkestHour/ui/lobby_background.py
Выход: Resources/Textures/_DarkestHour/LobbyScreens/lost_angels_{night,rain}.webp
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..')
OUT = ROOT + '/Resources/Textures/_DarkestHour/LobbyScreens'
W, H, SCALE = 480, 270, 4


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))


def sky(img, top, bottom, glow):
    d = ImageDraw.Draw(img)
    for y in range(H):
        t = y / (H - 1)
        c = lerp(top, bottom, t ** 1.4)
        d.line([(0, y), (W, y)], fill=c + (255,))
    # зарево города у горизонта
    g = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    gd = ImageDraw.Draw(g)
    for i in range(60):
        a = int(glow[3] * (1 - i / 60))
        gd.rectangle([0, H - 70 - i, W, H - 70 - i], fill=glow[:3] + (a,))
    img.alpha_composite(g)


def stars(img, rnd, n):
    d = ImageDraw.Draw(img)
    for _ in range(n):
        x, y = rnd.randrange(W), rnd.randrange(0, 120)
        v = rnd.randrange(90, 200)
        d.point((x, y), fill=(v, v, min(255, v + 20), 255))


def tower(d, rnd, x, w, h, base, body, lit_rate, warm, cold, top_style):
    y0 = base - h
    d.rectangle([x, y0, x + w - 1, base], fill=body)
    # грани: светлее левая кромка
    edge = tuple(min(255, c + 10) for c in body[:3]) + (255,)
    d.line([(x, y0), (x, base)], fill=edge)
    # окна сеткой 2×2 с шагом 4×5
    for wy in range(y0 + 4, base - 2, 5):
        for wx in range(x + 2, x + w - 2, 4):
            r = rnd.random()
            if r < lit_rate:
                c = warm if rnd.random() < 0.7 else cold
                d.rectangle([wx, wy, wx + 1, wy + 1], fill=c)
            elif r < lit_rate + 0.05:
                d.rectangle([wx, wy, wx + 1, wy + 1], fill=(40, 46, 58, 255))
    # верх
    if top_style == 'antenna':
        ax = x + w // 2
        d.line([(ax, y0 - 14), (ax, y0)], fill=body)
        d.point((ax, y0 - 15), fill=(255, 60, 50, 255))
    elif top_style == 'stepped' and w >= 16:
        d.rectangle([x + 3, y0 - 6, x + w - 4, y0], fill=body)
        d.rectangle([x + 6, y0 - 11, x + w - 7, y0 - 6], fill=body)
        d.point((x + w // 2, y0 - 12), fill=(255, 60, 50, 255))
    elif top_style == 'pad':
        d.rectangle([x - 2, y0 - 2, x + w + 1, y0], fill=body)
        for i in range(0, w + 4, 6):
            d.point((x - 2 + i, y0 - 3), fill=(90, 160, 255, 255))


def skyline(img, rnd, base, count, hmin, hmax, wmin, wmax, body, lit, warm, cold, tops=True):
    layer = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    x = -rnd.randrange(0, 20)
    while x < W:
        w = rnd.randrange(wmin, wmax)
        h = rnd.randrange(hmin, hmax)
        style = rnd.choice(['flat', 'antenna', 'stepped', 'pad', 'flat']) if tops else 'flat'
        tower(d, rnd, x, w, h, base, body, lit, warm, cold, style)
        x += w + rnd.randrange(0, 4)
    img.alpha_composite(layer)
    return layer


def neon(img, rnd, n, base):
    d = ImageDraw.Draw(img)
    colors = [(255, 70, 160), (70, 220, 255), (255, 176, 46), (160, 110, 255)]
    for _ in range(n):
        c = rnd.choice(colors)
        x, y = rnd.randrange(10, W - 30), rnd.randrange(base - 60, base - 12)
        w, h = rnd.randrange(8, 18), rnd.randrange(3, 6)
        glow = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        ImageDraw.Draw(glow).rectangle([x - 3, y - 3, x + w + 3, y + h + 3], fill=c + (70,))
        img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(2)))
        d.rectangle([x, y, x + w, y + h], outline=c + (255,))
        for i in range(x + 2, x + w - 1, 3):
            d.point((i, y + h // 2), fill=c + (255,))


def searchlights(img, rnd, n):
    layer = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for _ in range(n):
        x0 = rnd.randrange(40, W - 40)
        ang = math.radians(rnd.uniform(-35, 35))
        length = 260
        x1 = x0 + math.sin(ang) * length
        y1 = H - 60 - math.cos(ang) * length
        spread = 14
        d.polygon([(x0, H - 60), (x1 - spread, y1), (x1 + spread, y1)], fill=(200, 210, 230, 28))
    img.alpha_composite(layer.filter(ImageFilter.GaussianBlur(3)))


def haze(img, color, height, alpha):
    layer = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for i in range(height):
        a = int(alpha * (i / height))
        d.line([(0, H - height + i), (W, H - height + i)], fill=color + (a,))
    img.alpha_composite(layer)


def rain(img, rnd, n, alpha):
    layer = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for _ in range(n):
        x, y = rnd.randrange(-20, W), rnd.randrange(-10, H)
        ln = rnd.randrange(4, 9)
        d.line([(x, y), (x - ln // 3, y + ln)], fill=(150, 175, 200, alpha))
    img.alpha_composite(layer)


def flashes(img, rnd, n):
    layer = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for _ in range(n):
        x = rnd.randrange(0, W)
        r = rnd.randrange(14, 30)
        d.ellipse([x - r, H - 90 - r // 2, x + r, H - 90 + r // 2], fill=(255, 150, 60, 60))
    img.alpha_composite(layer.filter(ImageFilter.GaussianBlur(6)))


def street(img):
    d = ImageDraw.Draw(img)
    d.rectangle([0, H - 14, W, H], fill=(10, 11, 14, 255))
    for x in range(0, W, 24):
        d.rectangle([x + 4, H - 8, x + 14, H - 7], fill=(140, 110, 50, 255))
    for x in range(10, W, 46):
        d.line([(x, H - 14), (x, H - 34)], fill=(20, 22, 26, 255))
        d.point((x + 1, H - 34), fill=(255, 200, 120, 255))
        g = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        ImageDraw.Draw(g).ellipse([x - 10, H - 40, x + 12, H - 14], fill=(255, 180, 90, 40))
        img.alpha_composite(g.filter(ImageFilter.GaussianBlur(4)))


def scene(variant):
    rnd = random.Random(2318 if variant == 'night' else 1404)
    img = Image.new('RGBA', (W, H), (0, 0, 0, 255))
    if variant == 'night':
        sky(img, (6, 9, 18), (34, 30, 40), (255, 150, 70, 70))
        stars(img, rnd, 70)
    else:
        sky(img, (12, 14, 20), (48, 40, 40), (255, 120, 50, 110))
        flashes(img, rnd, 4)
    searchlights(img, rnd, 3 if variant == 'night' else 2)
    warm, cold = (226, 170, 80, 255), (120, 170, 220, 255)
    skyline(img, rnd, H - 40, 0, 90, 190, 10, 22, (16, 19, 26, 255), 0.10,
            (150, 115, 60, 255), (80, 110, 150, 255))
    haze(img, (60, 55, 60), 90, 120 if variant == 'night' else 170)
    skyline(img, rnd, H - 26, 0, 60, 150, 14, 30, (11, 13, 18, 255), 0.22, warm, cold)
    neon(img, rnd, 7, H - 26)
    skyline(img, rnd, H - 14, 0, 30, 90, 20, 40, (7, 8, 11, 255), 0.30, warm, cold, tops=False)
    street(img)
    haze(img, (90, 80, 70), 40, 70 if variant == 'night' else 120)
    rain(img, rnd, 260 if variant == 'night' else 700, 55 if variant == 'night' else 85)
    return img.resize((W * SCALE, H * SCALE), Image.NEAREST).convert('RGB')


def main():
    os.makedirs(OUT, exist_ok=True)
    for v in ('night', 'rain'):
        path = '%s/lost_angels_%s.webp' % (OUT, v)
        scene(v).save(path, 'WEBP', quality=90)
        print(os.path.relpath(path, ROOT))


if __name__ == '__main__':
    main()
