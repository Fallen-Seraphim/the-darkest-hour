"""Anti-orbital railway guns of the Republic (RAO-400, RAO-700): a miniature "Dora" - a long barrel on a cradle
between two girder carriages that stand on two double railway tracks with a row of bogies under each side.

The gun is drawn whole, facing north (muzzle up), then cut in two along the front edge of the breech tile:
  * breech part - everything from that edge backwards (carriages, cradle, breech, loading platform);
  * barrel part - everything from that edge forwards (the barrel and the front ends of the carriages).
Both parts get a canvas symmetric about their own tile centre (the barrel stands on the tile in front of the breech),
so the 1-direction sprites rotate with the entity like the old barrels did and meet seamlessly when assembled.

Run from the repository root:  python3 Tools/_DarkestHour/heavy_guns/railway_gun.py [preview.png]"""
import json
import os
import sys

from PIL import Image, ImageDraw

ROOT = 'Resources/Textures/_DarkestHour/Structures/HeavyGuns'
COPYRIGHT = 'Made for The Darkest Hour (Tools/_DarkestHour/heavy_guns/railway_gun.py)'
T = 32

OUT = (22, 24, 26, 255)
RAIL = (150, 152, 150, 255)
RAIL_D = (92, 94, 92, 255)
SLEEPER = (74, 58, 44, 255)
SLEEPER_D = (54, 42, 32, 255)
BALLAST = (88, 84, 76, 255)
STEEL = (92, 100, 96, 255)       # Republic grey-green
STEEL_L = (132, 142, 134, 255)
STEEL_LL = (168, 178, 170, 255)
STEEL_D = (64, 70, 67, 255)
STEEL_DD = (44, 48, 46, 255)
WHEEL = (34, 36, 36, 255)
WHEEL_L = (86, 88, 86, 255)
BRASS = (176, 142, 70, 255)
BRASS_L = (214, 182, 104, 255)
HAZARD_Y = (212, 176, 52, 255)
HAZARD_B = (30, 30, 30, 255)
RED = (176, 52, 44, 255)
SHADOW = (0, 0, 0, 70)


def rect(d, box, fill, outline=None):
    d.rectangle(box, fill=fill, outline=outline)


def draw_gun(w_tiles, len_tiles, barrel_tiles, calibre):
    """Whole gun facing north. Returns (image, y of the front edge of the breech tile)."""
    W, H = w_tiles * T, len_tiles * T
    img = Image.new('RGBA', (W, H))
    d = ImageDraw.Draw(img)
    cx = W // 2
    s = W / 128  # scale against the 4-tile design

    def X(v):
        return int(round(cx + v * s))

    body_top = barrel_tiles * T - int(10 * s)   # front ends of the carriages
    body_bot = H - 4

    # --- two double tracks under the carriages (ballast, sleepers, rails)
    for side in (-1, 1):
        x0, x1 = sorted((X(side * 30), X(side * 58)))
        rect(d, (x0 - 3, body_top - int(26 * s), x1 + 3, H - 1), BALLAST)
        for y in range(body_top - int(24 * s), H - 2, 6):
            rect(d, (x0 - 2, y, x1 + 2, y + 2), SLEEPER)
            d.line((x0 - 2, y + 2, x1 + 2, y + 2), SLEEPER_D)
        for rx in (x0 + 2, x1 - 2):
            d.line((rx, body_top - int(26 * s), rx, H - 1), RAIL)
            d.line((rx + 1, body_top - int(26 * s), rx + 1, H - 1), RAIL_D)

    # --- shadow of the whole mass (to the south-east)
    sh = Image.new('RGBA', (W, H))
    sd = ImageDraw.Draw(sh)
    sd.rectangle((X(-52) + 4, body_top + 6, X(52) + 4, body_bot + 2), SHADOW)
    img.alpha_composite(sh)

    # --- bogies: rows of wheel sets along each carriage
    n = max(4, (body_bot - body_top) // int(26 * s))
    step = (body_bot - body_top - int(10 * s)) / n
    for side in (-1, 1):
        for i in range(n):
            y = int(body_top + 8 * s + i * step)
            bx0, bx1 = sorted((X(side * 28), X(side * 60)))
            rect(d, (bx0, y, bx1, y + int(step) - 3), STEEL_DD, OUT)
            for wx in (bx0 + 3, bx1 - 6):
                for wy in (y + 3, y + int(step) - 9):
                    rect(d, (wx, wy, wx + 3, wy + 4), WHEEL, OUT)
                    d.point((wx + 1, wy + 1), WHEEL_L)

    # --- girder carriages (left/right) with rivets and walkways
    for side in (-1, 1):
        gx0, gx1 = sorted((X(side * 34), X(side * 54)))
        rect(d, (gx0, body_top, gx1, body_bot - 6), STEEL, OUT)
        d.line((gx0 + 1, body_top + 1, gx0 + 1, body_bot - 7), STEEL_L)
        d.line((gx1 - 1, body_top + 1, gx1 - 1, body_bot - 7), STEEL_D)
        # walkway grating
        for y in range(body_top + 4, body_bot - 8, 3):
            d.line((gx0 + 3, y, gx1 - 3, y), STEEL_D)
        for y in range(body_top + 6, body_bot - 8, int(12 * s) or 6):
            d.point((gx0 + 2, y), STEEL_LL)
            d.point((gx1 - 2, y), STEEL_LL)
        # handrail on the outer edge
        ox = gx0 if side < 0 else gx1
        d.line((ox, body_top + 2, ox, body_bot - 8), STEEL_LL)

    # --- transverse beams joining the carriages
    for y in (body_top + int(14 * s), body_bot - int(30 * s)):
        rect(d, (X(-36), y, X(36), y + int(8 * s)), STEEL_D, OUT)

    # --- cradle (gun house walls) in the middle
    cr_top = body_top + int(36 * s)
    cr_bot = body_bot - int(16 * s)
    rect(d, (X(-24), cr_top, X(24), cr_bot), STEEL_D, OUT)
    rect(d, (X(-22), cr_top + 2, X(-14), cr_bot - 2), STEEL, OUT)
    rect(d, (X(14), cr_top + 2, X(22), cr_bot - 2), STEEL, OUT)
    d.line((X(-21), cr_top + 3, X(-21), cr_bot - 3), STEEL_L)
    d.line((X(15), cr_top + 3, X(15), cr_bot - 3), STEEL_L)
    # trunnions
    ty = cr_top + int(44 * s)
    for side in (-1, 1):
        rect(d, (X(side * 28) - 4, ty - 5, X(side * 28) + 4, ty + 5), STEEL_DD, OUT)
        d.point((X(side * 28), ty), STEEL_LL)

    # --- barrel: tapered tube from the cradle to the muzzle, with recoil cylinders
    b_bot = cr_bot - int(30 * s)
    b_top = 6
    w_back = int(30 * s) // 2
    w_front = max(5, int(17 * s) // 2)
    for y in range(b_top + 8, b_bot + 1):
        t = (y - b_top) / max(1, b_bot - b_top)
        hw = int(round(w_front + (w_back - w_front) * t))
        d.line((cx - hw, y, cx + hw, y), STEEL)
        d.point((cx - hw, y), OUT)
        d.point((cx + hw, y), OUT)
        d.point((cx - hw + 1, y), STEEL_LL)
        d.point((cx - hw + 2, y), STEEL_L)
        d.point((cx + hw - 1, y), STEEL_D)
        d.point((cx + hw - 2, y), STEEL_D)
    # reinforcing hoops
    for y in range(b_bot - int(20 * s), b_top + 20, -int(28 * s) or -14):
        t = (y - b_top) / max(1, b_bot - b_top)
        hw = int(round(w_front + (w_back - w_front) * t)) + 1
        rect(d, (cx - hw, y, cx + hw, y + 2), STEEL_D, OUT)
    # muzzle
    rect(d, (cx - w_front - 2, b_top, cx + w_front + 2, b_top + 8), STEEL_DD, OUT)
    rect(d, (cx - 2, b_top, cx + 1, b_top + 1), OUT)
    # recoil cylinders along the cradle
    for side in (-1, 1):
        rx = cx + side * (w_back + 4)
        rect(d, (rx - 2, cr_top - int(4 * s), rx + 2, b_bot), STEEL_L, OUT)

    # --- breech block and loading platform at the back
    br_top = b_bot - 2
    rect(d, (cx - w_back - 2, br_top, cx + w_back + 2, br_top + int(18 * s)), BRASS, OUT)
    d.line((cx - w_back - 1, br_top + 1, cx + w_back + 1, br_top + 1), BRASS_L)
    rect(d, (cx - 3, br_top + 4, cx + 2, br_top + int(18 * s) - 4), OUT)
    pl_top = br_top + int(20 * s)
    rect(d, (X(-24), pl_top, X(24), body_bot - 4), STEEL_DD, OUT)
    for x in range(X(-23), X(24), 4):
        d.line((x, pl_top + 1, x + 2, pl_top + 1), HAZARD_Y)
        d.line((x + 2, pl_top + 1, x + 4, pl_top + 1), HAZARD_B)
    # shell hoist (crane) on the platform
    rect(d, (X(10), pl_top + 4, X(20), body_bot - 8), STEEL_D, OUT)
    d.line((X(10), pl_top + 6, X(-6), pl_top + 6), STEEL_LL)
    # calibre stencil on the left carriage and a red band on the barrel jacket
    stencil = str(calibre)
    sx, sy = X(-50), body_top + int(60 * s)
    for i, ch in enumerate(stencil):
        rect(d, (sx, sy + i * 6, sx + 3, sy + i * 6 + 4), STEEL_LL)
    rect(d, (cx - w_back, cr_top - 6, cx + w_back, cr_top - 4), RED)

    front_edge = barrel_tiles * T  # front edge of the breech tile = back edge of the barrel tile
    return img, front_edge


def split(img, front_edge):
    """Cut along the front edge of the breech tile and centre both halves on their own tile."""
    W, H = img.size
    rear = img.crop((0, front_edge, W, H))          # breech tile is the first tile of this part
    front = img.crop((0, 0, W, front_edge))         # barrel tile is the last tile of this part
    # breech: tile centre at y = 16 of `rear`; make canvas symmetric about it
    rh = rear.height
    half = max(16, rh - 16)
    breech = Image.new('RGBA', (W, half * 2))
    breech.alpha_composite(rear, (0, half - 16))
    # barrel: tile centre at y = front.height - 16
    fh = front.height
    half = max(16, fh - 16)
    barrel = Image.new('RGBA', (W, half * 2))
    barrel.alpha_composite(front, (0, half - (fh - 16)))
    return breech, barrel


def indicators(size, heavy):
    """'assembled' (green) and 'loaded' (amber) lamps on the loading platform of the breech part."""
    W, H = size
    out = []
    for colour in ((80, 220, 110, 255), (240, 170, 60, 255)):
        im = Image.new('RGBA', (W, H))
        d = ImageDraw.Draw(im)
        x = W // 2 - (10 if colour[0] < 200 else 4)
        y = H // 2 + 20
        rect(d, (x, y, x + 3, y + 3), colour, OUT)
        out.append(im)
    return out


def save_rsi(path, size, states):
    os.makedirs(path, exist_ok=True)
    meta = {'version': 1, 'license': 'CC-BY-SA-3.0', 'copyright': COPYRIGHT,
            'size': {'x': size[0], 'y': size[1]}, 'states': []}
    for name, img in states:
        img.save(os.path.join(path, name + '.png'))
        meta['states'].append({'name': name})
    with open(os.path.join(path, 'meta.json'), 'w', encoding='utf-8') as f:
        json.dump(meta, f, indent=4)
        f.write('\n')


GUNS = {
    # calibre: (width tiles, length tiles, tiles the barrel reaches in front of the breech tile)
    400: (3, 8, 4),
    700: (4, 11, 6),
}


def main():
    preview = sys.argv[1] if len(sys.argv) > 1 else None
    whole = {}
    for cal, (w, l, b) in GUNS.items():
        img, edge = draw_gun(w, l, b, cal)
        whole[cal] = img
        breech, barrel = split(img, edge)
        assembled, loaded = indicators(breech.size, cal == 700)
        if not preview:
            save_rsi(f'{ROOT}/railway_{cal}_breech.rsi', breech.size,
                     [('icon', breech), ('assembled', assembled), ('loaded', loaded)])
            save_rsi(f'{ROOT}/railway_{cal}_barrel.rsi', barrel.size, [('icon', barrel)])
    if preview:
        gap = 24
        W = sum(i.width for i in whole.values()) + gap * 3
        H = max(i.height for i in whole.values()) + gap * 2
        sheet = Image.new('RGBA', (W, H), (120, 104, 78, 255))
        x = gap
        for img in whole.values():
            sheet.alpha_composite(img, (x, H - gap - img.height))
            x += img.width + gap
        sheet.resize((W * 2, H * 2), Image.NEAREST).save(preview)


if __name__ == '__main__':
    main()
