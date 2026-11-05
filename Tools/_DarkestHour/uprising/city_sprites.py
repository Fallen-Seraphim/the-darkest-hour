"""Спрайты «Восстания»: ключ от квартиры, банковская карта, телефон, взломщик антенны.
Запуск из корня репозитория: python3 Tools/_DarkestHour/uprising/city_sprites.py"""
import json, os
from PIL import Image, ImageDraw

OUT = 'Resources/Textures/_DarkestHour/Objects/City'
COPY = 'Drawn by Claude (Anthropic) for The Darkest Hour'


def save(name, states):
    d = os.path.join(OUT, name + '.rsi')
    os.makedirs(d, exist_ok=True)
    meta = {'version': 1, 'license': 'CC-BY-SA-3.0', 'copyright': COPY, 'size': {'x': 32, 'y': 32}, 'states': []}
    for st, img, dirs in states:
        if dirs == 4:
            sheet = Image.new('RGBA', (64, 64))
            for i, im in enumerate(img):
                sheet.paste(im, ((i % 2) * 32, (i // 2) * 32))
            sheet.save(os.path.join(d, st + '.png'))
            meta['states'].append({'name': st, 'directions': 4})
        else:
            img.save(os.path.join(d, st + '.png'))
            meta['states'].append({'name': st})
    json.dump(meta, open(os.path.join(d, 'meta.json'), 'w'), indent=2)


def canvas():
    return Image.new('RGBA', (32, 32), (0, 0, 0, 0))


def outline(img, col=(20, 20, 24, 255)):
    src = img.copy()
    px = src.load()
    out = img.load()
    for y in range(32):
        for x in range(32):
            if px[x, y][3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < 32 and 0 <= ny < 32 and px[nx, ny][3] and px[nx, ny] != col:
                    out[x, y] = col
                    break
    return img


def inhand(color):
    """Маленький предмет в руке: точка цвета у кисти (S, N, E, W)."""
    frames = []
    for pos in ((12, 18), (19, 18), (14, 18), (17, 18)):
        im = canvas()
        d = ImageDraw.Draw(im)
        d.rectangle([pos[0], pos[1], pos[0] + 2, pos[1] + 1], fill=color)
        frames.append(outline(im))
    return frames


def key():
    im = canvas()
    d = ImageDraw.Draw(im)
    brass, dark, light = (196, 156, 64, 255), (130, 98, 36, 255), (236, 204, 120, 255)
    d.ellipse([7, 10, 15, 18], fill=brass)
    d.ellipse([10, 13, 12, 15], fill=(0, 0, 0, 0))
    d.rectangle([15, 13, 26, 15], fill=brass)
    d.line([16, 13, 25, 13], fill=light)
    d.rectangle([21, 16, 22, 18], fill=dark)
    d.rectangle([24, 16, 25, 19], fill=dark)
    d.point([8, 12], fill=light)
    # кольцо и бирка
    d.ellipse([3, 17, 8, 22], outline=(150, 150, 160, 255))
    d.rectangle([2, 22, 6, 26], fill=(214, 74, 58, 255))
    return outline(im)


def card(stripe):
    im = canvas()
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([5, 10, 26, 22], radius=2, fill=(46, 92, 64, 255))
    d.rectangle([5, 13, 26, 14], fill=stripe)
    d.rectangle([8, 16, 11, 18], fill=(222, 196, 110, 255))
    d.line([14, 19, 24, 19], fill=(200, 230, 210, 255))
    d.line([14, 17, 20, 17], fill=(160, 200, 180, 255))
    return outline(im)


def phone(screen_on=True):
    im = canvas()
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([11, 5, 21, 26], radius=2, fill=(36, 38, 44, 255))
    if screen_on:
        d.rectangle([12, 7, 20, 22], fill=(70, 150, 190, 255))
        d.rectangle([13, 8, 19, 9], fill=(150, 210, 235, 255))
        for y in (11, 14, 17):
            d.rectangle([13, y, 15, y + 1], fill=(220, 240, 250, 255))
            d.rectangle([17, y, 19, y + 1], fill=(220, 240, 250, 255))
    else:
        d.rectangle([12, 7, 20, 22], fill=(20, 22, 26, 255))
    d.rectangle([15, 24, 17, 24], fill=(120, 124, 132, 255))
    return outline(im)


def hacker():
    im = canvas()
    d = ImageDraw.Draw(im)
    d.rectangle([8, 11, 23, 22], fill=(58, 62, 52, 255))
    d.rectangle([10, 13, 18, 17], fill=(40, 160, 70, 255))
    d.point([11, 14], fill=(170, 255, 180, 255))
    d.point([13, 15], fill=(170, 255, 180, 255))
    d.rectangle([20, 13, 21, 14], fill=(220, 60, 50, 255))
    d.rectangle([20, 16, 21, 17], fill=(230, 190, 60, 255))
    d.line([23, 14, 28, 9], fill=(180, 60, 50, 255))
    d.line([23, 18, 28, 23], fill=(50, 80, 180, 255))
    d.rectangle([27, 8, 29, 9], fill=(150, 150, 150, 255))
    d.rectangle([27, 23, 29, 24], fill=(150, 150, 150, 255))
    return outline(im)


save('apartment_key', [('icon', key(), 1), ('inhand-left', inhand((196, 156, 64, 255)), 4),
                       ('inhand-right', inhand((196, 156, 64, 255)), 4)])
save('bank_card', [('icon', card((230, 230, 230, 255)), 1), ('corporate', card((166, 139, 232, 255)), 1),
                   ('police', card((91, 138, 214, 255)), 1),
                   ('inhand-left', inhand((46, 92, 64, 255)), 4), ('inhand-right', inhand((46, 92, 64, 255)), 4)])
save('phone', [('icon', phone(), 1), ('off', phone(False), 1),
               ('inhand-left', inhand((36, 38, 44, 255)), 4), ('inhand-right', inhand((36, 38, 44, 255)), 4)])
save('antenna_hacker', [('icon', hacker(), 1), ('inhand-left', inhand((58, 62, 52, 255)), 4),
                        ('inhand-right', inhand((58, 62, 52, 255)), 4)])
print('ok')


# ------------------------------------------------------------------ иконки должностей (8×8) и паспорт

def icons():
    """LA-V — фиолетовые с монограммой «V» (по мотивам иконок Ве-Я в RMC), ОФСК — тёмно-красные со звездой."""
    d = 'Resources/Textures/_DarkestHour/Interface/uprising_job_icons.rsi'
    os.makedirs(d, exist_ok=True)
    meta = {'version': 1, 'license': 'CC-BY-SA-3.0', 'copyright': COPY, 'size': {'x': 8, 'y': 8}, 'states': []}
    black = (16, 14, 18, 255)

    def base(fill, rim):
        im = Image.new('RGBA', (8, 8), (0, 0, 0, 0))
        dr = ImageDraw.Draw(im)
        dr.rectangle([0, 0, 7, 7], fill=black)
        dr.rectangle([1, 1, 6, 6], fill=fill)
        dr.line([1, 1, 6, 1], fill=rim)
        return im, dr

    lav = (104, 72, 176, 255)
    for name, rim in (('lav_representative', (236, 200, 96, 255)), ('lav_assistant', (200, 200, 220, 255)),
                      ('lav_security', (214, 74, 58, 255)), ('lav_employee', (150, 130, 210, 255))):
        im, dr = base(lav, rim)
        white = (240, 240, 250, 255)
        for x, y in ((2, 2), (2, 3), (3, 4), (3, 5), (4, 5), (4, 4), (5, 3), (5, 2)):
            dr.point((x, y), fill=white)
        if name == 'lav_representative':
            dr.rectangle([0, 0, 7, 7], outline=rim)
        im.save(os.path.join(d, name + '.png'))
        meta['states'].append({'name': name})

    red = (140, 20, 20, 255)
    star = (236, 196, 60, 255)
    marks = {'front_leader': (236, 196, 60, 255), 'front_saboteur': (240, 120, 40, 255),
             'front_specialist': (120, 200, 120, 255), 'front_machinegunner': (200, 200, 200, 255),
             'front_rebel': None}
    for name, mark in marks.items():
        im, dr = base(red, (190, 50, 40, 255))
        for x, y in ((3, 2), (4, 2), (2, 3), (3, 3), (4, 3), (5, 3), (3, 4), (4, 4), (2, 5), (5, 5)):
            dr.point((x, y), fill=star)
        if mark:
            dr.line([1, 6, 6, 6], fill=mark)
        im.save(os.path.join(d, name + '.png'))
        meta['states'].append({'name': name})
    json.dump(meta, open(os.path.join(d, 'meta.json'), 'w'), indent=2)


def passport():
    im = canvas()
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([9, 6, 23, 25], radius=1, fill=(36, 74, 52, 255))
    d.rectangle([9, 6, 10, 25], fill=(24, 52, 36, 255))
    d.ellipse([13, 10, 19, 16], outline=(214, 186, 96, 255))
    d.point([16, 13], fill=(214, 186, 96, 255))
    d.line([13, 19, 19, 19], fill=(214, 186, 96, 255))
    d.line([14, 21, 18, 21], fill=(214, 186, 96, 255))
    return outline(im)


def blank():
    return [canvas() for _ in range(4)]


icons()
save('passport', [('passport', passport(), 1), ('equipped-IDCARD', blank(), 4),
                  ('default-inhand-left', inhand((36, 74, 52, 255)), 4),
                  ('default-inhand-right', inhand((36, 74, 52, 255)), 4)])
print('icons ok')
