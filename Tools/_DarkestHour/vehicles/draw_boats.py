#!/usr/bin/env python3
# DarkestHour: спрайты лодок (план осады п. 10) — рисуются этим скриптом, вид сверху, нос — вниз (на юг: так у машин
# нос в их системе координат). Одно направление: спрайт поворачивается вместе с лодкой. Запуск из корня репозитория.
# Лицензия спрайтов — как у репозитория (AGPL-3.0 код, спрайты CC-BY-SA-3.0), автор — Claude по заданию автора.

import json
import os

from PIL import Image, ImageDraw

OUT = 'Resources/Textures/_DarkestHour/Vehicles/boats.rsi'


def shade(color, k):
    return tuple(max(0, min(255, int(c * k))) for c in color[:3]) + (255,)


def inflatable():
    img = Image.new('RGBA', (96, 96), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    tube = (74, 82, 60)
    floor = (104, 108, 88)
    x0, x1, y0, y1 = 28, 68, 10, 88          # корпус 40×78, корма сверху, нос снизу
    # баллон: корма прямая, нос скруглён
    d.rounded_rectangle((x0, y0, x1, y1), radius=18, fill=shade(tube, 1.0), outline=shade(tube, 0.55), width=2)
    d.rectangle((x0, y0, x1, y0 + 14), fill=shade(tube, 1.0))
    d.line((x0, y0, x1, y0), fill=shade(tube, 0.55), width=2)
    # блик на баллонах
    d.line((x0 + 3, y0 + 6, x0 + 3, y1 - 14), fill=shade(tube, 1.35), width=2)
    d.line((x1 - 4, y0 + 6, x1 - 4, y1 - 14), fill=shade(tube, 1.2), width=1)
    # дно
    d.rounded_rectangle((x0 + 7, y0 + 5, x1 - 7, y1 - 8), radius=11, fill=shade(floor, 1.0),
                        outline=shade(floor, 0.7), width=1)
    for y in range(y0 + 12, y1 - 12, 6):     # решётка пайола
        d.line((x0 + 9, y, x1 - 9, y), fill=shade(floor, 0.85), width=1)
    # банки (сиденья)
    for y in (y0 + 30, y0 + 52):
        d.rectangle((x0 + 7, y, x1 - 7, y + 4), fill=shade(tube, 0.8))
    # подвесной мотор на корме
    d.rectangle((44, 1, 52, 11), fill=(36, 38, 40, 255), outline=(18, 18, 20, 255))
    d.rectangle((46, 3, 50, 6), fill=(70, 72, 76, 255))
    # леер по борту
    for y in range(y0 + 16, y1 - 18, 10):
        d.point((x0 + 1, y), fill=(30, 30, 30, 255))
        d.point((x1 - 1, y), fill=(30, 30, 30, 255))
    return img


def heavy():
    img = Image.new('RGBA', (128, 128), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    hull = (88, 98, 84)
    deck = (118, 124, 108)
    x0, x1 = 38, 90                          # ширина 52
    y0, y1 = 6, 122                          # длина 116, нос снизу
    bow = [(x0, y1 - 30), (64, y1), (x1, y1 - 30)]
    d.polygon([(x0, y0), (x1, y0), (x1, y1 - 30), (64, y1), (x0, y1 - 30)], fill=shade(hull, 1.0),
              outline=shade(hull, 0.5))
    # палуба
    d.polygon([(x0 + 5, y0 + 5), (x1 - 5, y0 + 5), (x1 - 5, y1 - 32), (64, y1 - 8), (x0 + 5, y1 - 32)],
              fill=shade(deck, 1.0), outline=shade(deck, 0.7))
    # фальшборт с бликом
    d.line((x0 + 2, y0 + 4, x0 + 2, y1 - 31), fill=shade(hull, 1.3), width=2)
    # рубка с ветровым стеклом (середина)
    d.rectangle((x0 + 12, 44, x1 - 12, 66), fill=shade(hull, 0.75), outline=shade(hull, 0.45))
    d.rectangle((x0 + 15, 62, x1 - 15, 66), fill=(120, 150, 170, 255))
    # штурвал
    d.ellipse((60, 52, 68, 60), outline=(40, 40, 40, 255), width=2)
    # пулемёт на носу: тумба и ствол вперёд (вниз)
    d.ellipse((56, 84, 72, 100), fill=shade(hull, 0.6), outline=shade(hull, 0.35))
    d.rectangle((62, 92, 66, 112), fill=(40, 42, 44, 255))
    d.rectangle((60, 86, 68, 94), fill=(55, 58, 60, 255))
    # бронещиток
    d.arc((52, 96, 76, 112), 20, 160, fill=(50, 54, 50, 255), width=3)
    # люк машинного отделения на корме
    d.rectangle((52, 12, 76, 30), fill=shade(deck, 0.82), outline=shade(deck, 0.6))
    for x in range(55, 75, 4):
        d.line((x, 14, x, 28), fill=shade(deck, 0.7))
    # банки для десанта
    for y in (34, 72):
        d.rectangle((x0 + 7, y, x0 + 13, y + 6), fill=shade(hull, 0.7))
        d.rectangle((x1 - 13, y, x1 - 7, y + 6), fill=shade(hull, 0.7))
    return img


def folded():
    img = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    tube = (74, 82, 60)
    d.rounded_rectangle((4, 9, 28, 25), radius=7, fill=shade(tube, 1.0), outline=shade(tube, 0.5))
    for x in (10, 16, 22):
        d.line((x, 10, x, 24), fill=shade(tube, 0.7))
    d.line((5, 13, 27, 13), fill=shade(tube, 1.3))
    d.rectangle((13, 6, 19, 9), fill=(36, 38, 40, 255))       # мотор, пристёгнут сверху
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    states = {'inflatable': inflatable(), 'heavy': heavy(), 'folded': folded()}
    for name, img in states.items():
        img.save(os.path.join(OUT, f'{name}.png'))
    meta = {
        'version': 1,
        'license': 'CC-BY-SA-3.0',
        'copyright': 'Drawn for The Darkest Hour (Tools/_DarkestHour/vehicles/draw_boats.py)',
        'size': {'x': 128, 'y': 128},
        'states': [],
    }
    # RSI требует один размер кадра: все кадры — 128×128, мелкие по центру.
    for name, img in states.items():
        canvas = Image.new('RGBA', (128, 128), (0, 0, 0, 0))
        canvas.paste(img, ((128 - img.width) // 2, (128 - img.height) // 2))
        canvas.save(os.path.join(OUT, f'{name}.png'))
        meta['states'].append({'name': name})
    with open(os.path.join(OUT, 'meta.json'), 'w') as f:
        json.dump(meta, f, indent=2)

    # Иконка сложенной лодки — отдельный RSI 32×32.
    item = 'Resources/Textures/_DarkestHour/Vehicles/boat_folded.rsi'
    os.makedirs(item, exist_ok=True)
    states['folded'].save(os.path.join(item, 'icon.png'))
    with open(os.path.join(item, 'meta.json'), 'w') as f:
        json.dump({'version': 1, 'license': 'CC-BY-SA-3.0',
                   'copyright': 'Drawn for The Darkest Hour (Tools/_DarkestHour/vehicles/draw_boats.py)',
                   'size': {'x': 32, 'y': 32}, 'states': [{'name': 'icon'}]}, f, indent=2)
    os.remove(os.path.join(OUT, 'folded.png'))
    meta['states'] = [s for s in meta['states'] if s['name'] != 'folded']
    with open(os.path.join(OUT, 'meta.json'), 'w') as f:
        json.dump(meta, f, indent=2)


if __name__ == '__main__':
    main()
