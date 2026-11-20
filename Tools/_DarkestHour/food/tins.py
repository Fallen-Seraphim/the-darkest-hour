"""Консервы «Осады» (план осады п. 27): перекраска ванильных банок SS14 (CC-BY-SA-3.0) под рыбу и кашу.
Запуск из корня: python3 Tools/_DarkestHour/food/tins.py"""
import colorsys, json, os, shutil
from PIL import Image

SRC = 'Resources/Textures/Objects/Consumable/Food/Tins/'
OUT = 'Resources/Textures/_DarkestHour/Objects/Consumable/Food/Tins/'
# имя → (исходная банка, сдвиг тона, множитель насыщенности)
RECOLORS = {'fish': ('meat', 0.55, 1.0), 'porridge': ('beans', 0.6, 0.85)}


def shift(im, dh, ds):
    im = im.convert('RGBA')
    px = im.load()
    for y in range(im.size[1]):
        for x in range(im.size[0]):
            r, g, b, a = px[x, y]
            if not a:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if s < 0.15:  # металл банки не трогаем
                continue
            r2, g2, b2 = colorsys.hsv_to_rgb((h + dh) % 1, min(1, s * ds), v)
            px[x, y] = (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)
    return im


for name, (src, dh, ds) in RECOLORS.items():
    out = OUT + name + '.rsi'
    os.makedirs(out, exist_ok=True)
    meta = json.load(open(SRC + src + '.rsi/meta.json'))
    for state in meta['states']:
        shift(Image.open(f"{SRC}{src}.rsi/{state['name']}.png"), dh, ds).save(f"{out}/{state['name']}.png")
    meta['copyright'] = f"Recoloured for The Darkest Hour (2026-11-20) from SS14 Tins/{src}.rsi: " + meta.get('copyright', '')
    json.dump(meta, open(out + '/meta.json', 'w'), indent=4)
print('ok')
