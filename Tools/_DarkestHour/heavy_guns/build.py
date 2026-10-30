"""Sprites of the heavy guns of the Assault mode: breeches and barrels (artillery and anti-orbital guns), shells with
four warheads, air-defence interceptors (missile launcher, missile silo, twin 30 mm autocannon), their ammunition,
the red target marker of an incoming strike and the paradrop beacon.

All art faces north (rotation 0); barrels are drawn centred on their tile and reach forward over the tiles ahead.

Run from the repository root:  python3 Tools/_DarkestHour/heavy_guns/build.py"""
import json
import os
from PIL import Image, ImageDraw

ROOT = 'Resources/Textures/_DarkestHour/Structures/HeavyGuns'
ITEMS = 'Resources/Textures/_DarkestHour/Objects/Weapons/HeavyGuns'
COPYRIGHT = 'Made for The Darkest Hour (Tools/_DarkestHour/heavy_guns/build.py)'

OUT = (26, 28, 31, 255)
STEEL = (96, 102, 108, 255)
STEEL_L = (140, 147, 153, 255)
STEEL_D = (62, 66, 71, 255)
STEEL_DD = (44, 47, 51, 255)
OLIVE = (86, 92, 60, 255)
OLIVE_L = (118, 126, 84, 255)
BRASS = (184, 148, 72, 255)
BRASS_L = (222, 190, 110, 255)
BRASS_D = (132, 102, 48, 255)


def save_rsi(path, size, states):
    os.makedirs(path, exist_ok=True)
    meta = {'version': 1, 'license': 'CC-BY-SA-3.0', 'copyright': COPYRIGHT,
            'size': {'x': size[0], 'y': size[1]}, 'states': []}
    for name, img, dirs in states:
        img.save(os.path.join(path, name + '.png'))
        entry = {'name': name}
        if dirs != 1:
            entry['directions'] = dirs
        meta['states'].append(entry)
    with open(os.path.join(path, 'meta.json'), 'w', encoding='utf-8') as f:
        json.dump(meta, f, indent=4)
        f.write('\n')


def rect(d, box, fill, outline=None):
    d.rectangle(box, fill=fill, outline=outline)


# ---------------------------------------------------------------- breech

def breech(heavy):
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    # base plate
    rect(d, (2, 4, 29, 29), STEEL_DD, OUT)
    for x in (4, 27):
        for y in (6, 27):
            d.point((x, y), STEEL_L)
    # breech block
    w = 10 if heavy else 8
    rect(d, (16 - w, 8, 15 + w, 26), STEEL, OUT)
    rect(d, (17 - w, 9, 14 + w, 11), STEEL_L)
    rect(d, (17 - w, 23, 14 + w, 25), STEEL_D)
    # recoil cylinders on the sides
    for x0 in (16 - w - 3, 15 + w + 1):
        rect(d, (x0, 10, x0 + 2, 24), OLIVE, OUT)
        d.line((x0 + 1, 11, x0 + 1, 23), OLIVE_L)
    # breech door with handle
    rect(d, (12, 14, 19, 21), STEEL_D, OUT)
    d.line((13, 17, 18, 17), STEEL_L)
    rect(d, (19, 17, 22, 18), BRASS)
    # trunnion towards the barrel (north): as wide as the barrel jacket and flush with the tile edge,
    # so an assembled gun reads as one piece (the jacket overlaps it by a few pixels)
    jx0, jx1 = (7, 24) if heavy else (9, 22)
    rect(d, (jx0, 0, jx1, 8), STEEL_D, OUT)
    d.line((jx0 + 1, 1, jx0 + 1, 7), STEEL)
    if heavy:
        for x in range(8, 24, 3):
            d.point((x, 28), BRASS_D)
    return img


def light(color):
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    rect(d, (6, 26, 8, 28), color)
    d.point((7, 27), (255, 255, 255, 255))
    return img


def loaded_light():
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    rect(d, (23, 26, 25, 28), (230, 170, 50, 255))
    d.point((24, 27), (255, 240, 200, 255))
    return img


# ---------------------------------------------------------------- barrel

def barrel(length_tiles, width):
    """Centred on its tile; reaches from the back edge of the tile forward over `length_tiles` tiles."""
    total = (length_tiles * 2 + 1) * 32  # symmetric sprite around the tile centre
    img = Image.new('RGBA', (32, total))
    d = ImageDraw.Draw(img)
    centre = total // 2
    bottom = centre + 16          # back edge of the barrel tile
    top = centre + 16 - (length_tiles + 1) * 32 + 2
    x0, x1 = 16 - width // 2, 15 + width // 2
    # jacket near the breech: runs to the back edge of the tile and a little into the breech tile,
    # over the breech trunnion, so there is no gap between barrel and breech
    rect(d, (x0 - 2, bottom - 30, x1 + 2, bottom + 3), STEEL_D, OUT)
    d.line((x0 - 1, bottom - 29, x0 - 1, bottom + 2), STEEL)
    rect(d, (x0 - 3, bottom - 2, x1 + 3, bottom + 3), STEEL_DD, OUT)  # mounting flange
    # tube
    rect(d, (x0, top + 10, x1, bottom - 30), STEEL, OUT)
    d.line((x0 + 1, top + 11, x0 + 1, bottom - 31), STEEL_L)
    d.line((x1 - 1, top + 11, x1 - 1, bottom - 31), STEEL_D)
    # reinforcing bands
    for y in range(bottom - 44, top + 14, -18):
        rect(d, (x0 - 1, y, x1 + 1, y + 2), STEEL_D, OUT)
    # muzzle brake
    rect(d, (x0 - 3, top, x1 + 3, top + 10), STEEL_DD, OUT)
    for y in (top + 3, top + 6):
        d.line((x0 - 2, y, x0, y), OUT)
        d.line((x1, y, x1 + 2, y), OUT)
    rect(d, (16 - 2, top, 15 + 2, top + 1), OUT)
    return img


# ---------------------------------------------------------------- shells

WARHEADS = {
    'he': ((196, 178, 64, 255), (120, 104, 30, 255)),      # high explosive: yellow band
    'ap': ((40, 40, 44, 255), (176, 54, 48, 255)),         # armour piercing: black tip, red band
    'inc': ((214, 96, 40, 255), (150, 54, 26, 255)),       # incendiary: orange
    'cluster': ((70, 120, 176, 255), (210, 214, 220, 255)),  # cluster: blue with white stripes
}


def shell(heavy, warhead):
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    w = 9 if heavy else 7
    x0, x1 = 16 - w // 2 - 1, 16 + w // 2
    top, bottom = (2, 30) if heavy else (5, 28)
    band, accent = WARHEADS[warhead]
    # casing
    rect(d, (x0, top + 9, x1, bottom), BRASS, OUT)
    d.line((x0 + 1, top + 10, x0 + 1, bottom - 1), BRASS_L)
    d.line((x1 - 1, top + 10, x1 - 1, bottom - 1), BRASS_D)
    rect(d, (x0 - 1, bottom - 2, x1 + 1, bottom), BRASS_D, OUT)
    # projectile with ogive
    rect(d, (x0, top + 4, x1, top + 9), OLIVE, OUT)
    d.polygon([(x0, top + 4), (16, top - 1), (x1, top + 4)], fill=band, outline=OUT)
    # warhead band
    rect(d, (x0 + 1, top + 7, x1 - 1, top + 8), accent)
    if warhead == 'cluster':
        for y in range(top + 11, bottom - 3, 3):
            d.line((x0 + 1, y, x1 - 1, y), accent)
    return img


def held(color):
    """In-hand sprite for a big two-handed item: four directions in a row."""
    sheet = Image.new('RGBA', (128, 32))
    spots = [(16, 20), (16, 20), (13, 20), (19, 20)]
    for i, (x, y) in enumerate(spots):
        frame = Image.new('RGBA', (32, 32))
        d = ImageDraw.Draw(frame)
        rect(d, (x - 3, y - 6, x + 2, y + 4), color, OUT)
        sheet.alpha_composite(frame, (32 * i, 0))
    return sheet


# ---------------------------------------------------------------- interceptors

def launcher():
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    rect(d, (3, 6, 28, 29), STEEL_DD, OUT)
    rect(d, (6, 3, 25, 26), OLIVE, OUT)
    for cx in (10, 16, 22):
        for cy in (9, 17):
            rect(d, (cx - 2, cy - 3, cx + 2, cy + 3), STEEL_DD, OUT)
            d.point((cx, cy), (210, 70, 50, 255))
    rect(d, (6, 23, 25, 25), OLIVE_L)
    return img


def silo():
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    rect(d, (1, 1, 30, 30), (128, 120, 104, 255), OUT)   # concrete collar
    d.ellipse((4, 4, 27, 27), fill=STEEL_D, outline=OUT)
    d.line((15, 5, 15, 26), OUT)
    d.line((16, 5, 16, 26), STEEL_L)
    for x, y in ((8, 8), (23, 8), (8, 23), (23, 23)):
        d.point((x, y), (230, 190, 60, 255))
    return img


def autocannon():
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    d.ellipse((5, 9, 26, 30), fill=STEEL_DD, outline=OUT)  # ring mount
    rect(d, (9, 13, 22, 26), OLIVE, OUT)                     # turret
    rect(d, (10, 14, 21, 15), OLIVE_L)
    for x in (12, 18):                                       # twin barrels forward
        rect(d, (x, 0, x + 1, 13), STEEL, OUT)
        rect(d, (x - 1, 0, x + 2, 2), STEEL_DD, OUT)
    rect(d, (14, 18, 17, 21), STEEL_DD, OUT)                 # sight
    return img


def missile():
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    rect(d, (13, 6, 18, 27), (210, 212, 205, 255), OUT)
    d.polygon([(13, 6), (15, 2), (16, 2), (18, 6)], fill=(176, 54, 48, 255), outline=OUT)
    for x0, x1 in ((10, 13), (18, 21)):
        d.polygon([(x0, 27), (x1, 22), (x1, 27)] if x0 == 10 else [(x1, 27), (x0, 22), (x0, 27)],
                  fill=STEEL_D, outline=OUT)
    rect(d, (13, 12, 18, 13), (176, 54, 48, 255))
    return img


def ammo_box():
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    rect(d, (6, 11, 25, 26), OLIVE, OUT)
    rect(d, (7, 12, 24, 13), OLIVE_L)
    rect(d, (13, 9, 18, 11), STEEL_D, OUT)
    for x in range(9, 23, 3):
        rect(d, (x, 16, x + 1, 22), BRASS)
    return img


def beacon():
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    # tripod legs, body and antenna
    d.line((16, 18, 9, 28), OUT, 2)
    d.line((16, 18, 23, 28), OUT, 2)
    d.line((16, 18, 16, 29), OUT, 2)
    rect(d, (12, 11, 19, 19), OLIVE, OUT)
    rect(d, (13, 12, 18, 13), OLIVE_L)
    d.line((16, 3, 16, 11), STEEL_D)
    rect(d, (15, 2, 17, 4), STEEL_DD)
    return img


def beacon_light():
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    rect(d, (14, 14, 17, 16), (255, 70, 50, 255))
    rect(d, (15, 2, 17, 4), (255, 120, 90, 255))
    return img


def marker():
    img = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(img)
    red = (230, 40, 30, 220)
    d.ellipse((2, 2, 29, 29), outline=red, width=2)
    d.ellipse((10, 10, 21, 21), outline=red, width=1)
    for a, b in (((15, 0), (16, 8)), ((15, 23), (16, 31)), ((0, 15), (8, 16)), ((23, 15), (31, 16))):
        rect(d, a + b, red)
    return img


def main():
    save_rsi(os.path.join(ROOT, 'breech.rsi'), (32, 32), [
        ('medium', breech(False), 1),
        ('heavy', breech(True), 1),
        ('assembled', light((80, 220, 90, 255)), 1),
        ('loaded', loaded_light(), 1),
    ])
    save_rsi(os.path.join(ROOT, 'barrel_medium.rsi'), (32, 32 * 5), [('icon', barrel(2, 10), 1)])
    save_rsi(os.path.join(ROOT, 'barrel_heavy.rsi'), (32, 32 * 7), [('icon', barrel(3, 14), 1)])
    save_rsi(os.path.join(ROOT, 'interceptors.rsi'), (32, 32), [
        ('launcher', launcher(), 1),
        ('silo', silo(), 1),
        ('autocannon', autocannon(), 1),
    ])
    save_rsi(os.path.join(ROOT, 'strike_marker.rsi'), (32, 32), [('marker', marker(), 1)])

    states = []
    for heavy in (False, True):
        for warhead in WARHEADS:
            states.append(('%s_%s' % ('heavy' if heavy else 'medium', warhead), shell(heavy, warhead), 1))
    states.append(('inhand-left', held(BRASS), 4))
    states.append(('inhand-right', held(BRASS), 4))
    save_rsi(os.path.join(ITEMS, 'shells.rsi'), (32, 32), states)

    save_rsi(os.path.join(ITEMS, 'drop_beacon.rsi'), (32, 32), [
        ('icon', beacon(), 1),
        ('light', beacon_light(), 1),
        ('inhand-left', held(OLIVE), 4),
        ('inhand-right', held(OLIVE), 4),
    ])
    save_rsi(os.path.join(ITEMS, 'interceptor_ammo.rsi'), (32, 32), [
        ('missile', missile(), 1),
        ('box', ammo_box(), 1),
        ('inhand-left', held((200, 200, 196, 255)), 4),
        ('inhand-right', held((200, 200, 196, 255)), 4),
    ])

    # preview (not committed)
    prev = Image.new('RGBA', (32 * 14, 32 * 7), (58, 60, 64, 255))
    prev.alpha_composite(Image.open(os.path.join(ROOT, 'barrel_heavy.rsi/icon.png')), (0, 0))
    prev.alpha_composite(Image.open(os.path.join(ROOT, 'breech.rsi/heavy.png')), (0, 32 * 4))
    prev.alpha_composite(Image.open(os.path.join(ROOT, 'breech.rsi/assembled.png')), (0, 32 * 4))
    prev.alpha_composite(Image.open(os.path.join(ROOT, 'barrel_medium.rsi/icon.png')), (32, 32))
    prev.alpha_composite(Image.open(os.path.join(ROOT, 'breech.rsi/medium.png')), (32, 32 * 4))
    prev.alpha_composite(Image.open(os.path.join(ROOT, 'breech.rsi/loaded.png')), (32, 32 * 4))
    x = 64
    for name, img, _ in states[:8]:
        prev.alpha_composite(img, (x, 0))
        x += 32
    for i, name in enumerate(('launcher', 'silo', 'autocannon')):
        prev.alpha_composite(Image.open(os.path.join(ROOT, 'interceptors.rsi/%s.png' % name)), (64 + 32 * i, 64))
    prev.alpha_composite(missile(), (64, 128))
    prev.alpha_composite(ammo_box(), (96, 128))
    prev.alpha_composite(marker(), (128, 128))
    prev.resize((prev.width * 3, prev.height * 3), Image.NEAREST).save(
        os.path.join(os.path.dirname(__file__), 'preview.png'))


if __name__ == '__main__':
    main()
