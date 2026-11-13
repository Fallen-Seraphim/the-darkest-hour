#!/usr/bin/env python3
"""DarkestHour: республиканские кредиты — купюры URC (United Republic Credit), решение автора 2026-11-13.

Форма и ступени — как у пачек морпехов RMC-14/cmss13 (spacecash): одна купюра до 50, пачка в бандероли с 100,
дальше больше пачек. Купюры свои: зелёно-хаки цвета Республики, на лицевой стороне «URC».
Запуск из корня репозитория: python3 Tools/_DarkestHour/economy/urc_sprites.py
Пишет Resources/Textures/_DarkestHour/Objects/Economy/urc.rsi (состояния urc_1 … urc_10000).
"""
import json
import os
from PIL import Image

OUT = 'Resources/Textures/_DarkestHour/Objects/Economy/urc.rsi'
OUTLINE = (32, 38, 28, 255)
INK = (28, 46, 24, 255)

# номинал купюры: (бумага, светлая кайма)
NOTES = {
    1: ((150, 170, 140), (182, 198, 172)),
    10: ((118, 160, 104), (156, 192, 140)),
    20: ((160, 160, 100), (192, 190, 132)),
    50: ((104, 160, 146), (142, 192, 178)),
    100: ((180, 166, 112), (210, 196, 146)),
}
BAND = (64, 96, 40, 255)      # бандероль — тёмно-зелёная, как РМБ-1
BAND_LIGHT = (96, 132, 62, 255)

FONT = {
    'U': ['101', '101', '101', '101', '111'],
    'R': ['110', '101', '110', '101', '101'],
    'C': ['011', '100', '100', '100', '011'],
}


def note(img, x, y, value, band=False):
    """Купюра 16×8: контур, бумага, кайма, «URC»; band — бандероль пачки."""
    paper, light = NOTES[value]
    px = img.load()
    for i in range(16):
        for j in range(8):
            edge = i in (0, 15) or j in (0, 7)
            if edge:
                c = OUTLINE
            elif j == 1 or i == 1:
                c = light + (255,)
            elif j == 6 or i == 14:
                c = tuple(int(v * 0.82) for v in paper) + (255,)
            else:
                c = paper + (255,)
            px[x + i, y + j] = c
    tx = x + 2
    for letter in 'URC':
        for row, bits in enumerate(FONT[letter]):
            for col, bit in enumerate(bits):
                if bit == '1':
                    px[tx + col, y + 1 + row] = INK
        tx += 4
    # номинал — точками справа (печать)
    px[x + 13, y + 2] = INK
    if band:
        for j in range(8):
            px[x + 12, y + j] = BAND if j not in (0, 7) else OUTLINE
            px[x + 13, y + j] = BAND_LIGHT if j not in (0, 7) else OUTLINE


def bundle(img, x, y, thick=1):
    """Пачка в бандероли; thick — сколько пачек стопкой (видны края снизу)."""
    for k in range(thick - 1, -1, -1):
        note(img, x, y + k * 2, 100, band=True)


LAYOUTS = {
    1: lambda im: note(im, 8, 14, 1),
    10: lambda im: note(im, 8, 14, 10),
    20: lambda im: note(im, 8, 14, 20),
    50: lambda im: note(im, 8, 14, 50),
    100: lambda im: bundle(im, 8, 14),
    200: lambda im: (bundle(im, 7, 15), bundle(im, 10, 11)),
    500: lambda im: (bundle(im, 6, 10, 2), bundle(im, 12, 19)),
    1000: lambda im: (bundle(im, 5, 18, 2), bundle(im, 12, 20), bundle(im, 6, 9, 2), bundle(im, 13, 6)),
    5000: lambda im: (bundle(im, 2, 18, 3), bundle(im, 14, 19, 3), bundle(im, 4, 7, 3), bundle(im, 15, 5, 3)),
    10000: lambda im: (bundle(im, 1, 19, 3), bundle(im, 15, 20, 3), bundle(im, 2, 7, 3), bundle(im, 15, 4, 3),
                       bundle(im, 8, 13, 3)),
}


def main():
    os.makedirs(OUT, exist_ok=True)
    states = []
    for value, draw in LAYOUTS.items():
        im = Image.new('RGBA', (32, 32))
        draw(im)
        name = f'urc_{value}'
        im.save(f'{OUT}/{name}.png')
        states.append({'name': name})
    json.dump({'version': 1, 'license': 'CC-BY-SA-3.0',
               'copyright': 'Drawn by Claude (Anthropic) for The Darkest Hour; stack layout after spacecash.rsi from '
                            'RMC-14 (cmss13, CC-BY-SA-3.0)',
               'size': {'x': 32, 'y': 32}, 'states': states}, open(f'{OUT}/meta.json', 'w'), indent=2)
    print('состояний', len(states))


if __name__ == '__main__':
    main()
