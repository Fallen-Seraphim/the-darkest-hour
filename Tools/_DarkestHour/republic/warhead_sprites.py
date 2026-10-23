"""Sprites of the Republican tactical nuclear warhead and its keys (drawn pixel by pixel, CC-BY-SA-3.0).

Run from the repository root:  python3 Tools/_DarkestHour/republic/warhead_sprites.py
Writes Resources/Textures/_DarkestHour/Objects/Devices/tactical_warhead.rsi, warhead_auth_key.rsi, warhead_panel_key.rsi
and preview.png next to this script (not committed)."""
import json
import os
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
OUT = os.path.join(ROOT, 'Resources/Textures/_DarkestHour/Objects/Devices')
COPY = 'Drawn for The Darkest Hour (Tools/_DarkestHour/republic/warhead_sprites.py)'


def hx(h, a=255):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (a,)


OUTLINE = hx('0d120c')
G0, G1, G2, G3, G4 = hx('18241a'), hx('243722'), hx('35502f'), hx('46643d'), hx('5e7e50')   # republic green
K0, K1, K2 = hx('7d7556'), hx('a99f74'), hx('d2c79a')                                       # khaki
M0, M1, M2, M3 = hx('1c1d20'), hx('33353a'), hx('55585e'), hx('8a8e94')                     # steel
Y, YD, BK = hx('e3bd2c'), hx('9c7f17'), hx('17171a')
RED, RED2, AMB, GRN = hx('e03030'), hx('7a1414'), hx('e8a43a'), hx('4fbf5a')
WHITE = hx('ece8dc')


class Canvas:
    def __init__(self, w=32, h=32):
        self.im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        self.px = self.im.load()
        self.w, self.h = w, h

    def put(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = c

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.put(x, y, c)

    def outline(self, c=OUTLINE):
        """Dark outline around every opaque pixel."""
        src = self.im.copy().load()
        for y in range(self.h):
            for x in range(self.w):
                if src[x, y][3]:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    xx, yy = x + dx, y + dy
                    if 0 <= xx < self.w and 0 <= yy < self.h and src[xx, yy][3] and src[xx, yy] != c:
                        self.px[x, y] = c
                        break


# --- warhead -----------------------------------------------------------------------------------------------------

def body():
    c = Canvas()
    # cradle: rails and two wheel pairs
    c.rect(4, 23, 27, 24, M1)
    c.rect(4, 23, 27, 23, M2)
    for x in (6, 22):
        c.rect(x, 25, x + 3, 28, M0)
        c.rect(x + 1, 26, x + 2, 27, M2)
    c.rect(8, 21, 9, 22, M1)
    c.rect(22, 21, 23, 22, M1)
    # warhead casing: horizontal capsule x 3..26, y 12..21 with shading
    for x in range(3, 27):
        top, bot = 12, 21
        if x < 5:                       # rounded tail
            top, bot = 13, 20
        for y in range(top, bot + 1):
            t = (y - 12) / 9
            col = G4 if t < 0.12 else G3 if t < 0.35 else G2 if t < 0.7 else G1 if t < 0.9 else G0
            c.put(x, y, col)
    # nose cone (right) tapering to x 30
    for i, x in enumerate(range(27, 31)):
        top, bot = 12 + i + (i > 1), 21 - i - (i > 1)
        for y in range(top, bot + 1):
            t = (y - top) / max(1, bot - top)
            c.put(x, y, K2 if t < 0.2 else K1 if t < 0.7 else K0)
    # tail fins (left)
    for y, x0 in ((10, 2), (11, 2), (22, 2), (23, 2)):
        c.rect(x0, y, x0 + 3, y, M2)
    c.rect(1, 14, 2, 19, M1)
    # khaki band and hazard band
    for x in (8, 9):
        for y in range(12, 22):
            c.put(x, y, K1 if y > 13 else K2)
    for y in range(12, 22):
        for x in (23, 24, 25):
            c.put(x, y, Y if (x + y) % 4 < 2 else BK)
    # radiation trefoil on a yellow disc
    tre = ['.YYYYY.', 'YKKYKKY', 'YKKYKKY', 'YYYKYYY', 'YYKKKYY', 'YYKKKYY', '.YYYYY.']
    for j, row in enumerate(tre):
        for i, ch in enumerate(row):
            if ch != '.':
                c.put(11 + i, 13 + j, Y if ch == 'Y' else BK)
    # stencil stripe of the Republic: khaki star-ish mark
    c.put(19, 16, K2); c.put(18, 17, K2); c.put(19, 17, K2); c.put(20, 17, K2); c.put(19, 18, K2)
    # control box on top
    c.rect(11, 6, 21, 11, M1)
    c.rect(11, 6, 21, 6, M2)
    c.rect(12, 7, 16, 10, M0)            # screen bezel
    c.outline()
    return c.im


def panel(open_):
    c = Canvas()
    if open_:
        # cover swung up, keypad and key slot exposed
        c.rect(17, 2, 21, 5, M2)
        c.rect(17, 2, 21, 2, M3)
        for y in (7, 9):
            for x in (18, 20):
                c.put(x, y, K2)
        c.put(19, 8, K2)
        c.put(18, 10, RED); c.put(20, 10, GRN)
    else:
        c.rect(17, 7, 21, 10, M2)
        c.rect(17, 7, 21, 7, M3)
        c.put(19, 9, Y)                  # key slot
    return c.im


def screen(colour, blink=None):
    frames = []
    for col in ([colour] if blink is None else [colour, blink]):
        c = Canvas()
        c.rect(13, 8, 15, 9, col)
        frames.append(c.im)
    return frames


def strip(frames):
    if len(frames) == 1:
        return frames[0]
    im = Image.new('RGBA', (32 * len(frames), 32), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        im.paste(f, (32 * i, 0))
    return im


def save_rsi(name, states, extra=None):
    path = os.path.join(OUT, name + '.rsi')
    os.makedirs(path, exist_ok=True)
    for f in os.listdir(path):
        os.remove(os.path.join(path, f))
    meta = {'version': 1, 'license': 'CC-BY-SA-3.0', 'copyright': COPY, 'size': {'x': 32, 'y': 32}, 'states': []}
    for st, img, info in states:
        img.save(os.path.join(path, st + '.png'))
        entry = {'name': st}
        entry.update(info or {})
        meta['states'].append(entry)
    with open(os.path.join(path, 'meta.json'), 'w', encoding='utf-8') as fh:
        json.dump(meta, fh, indent=4)
        fh.write('\n')
    return path


def warhead():
    b = body()
    closed, opened = panel(False), panel(True)
    icon = b.copy()
    icon.alpha_composite(closed)
    dim = hx('2c5a32')
    states = [
        ('base', b, None),
        ('icon', icon, None),
        ('panel_closed', closed, None),
        ('panel_open', opened, None),
        ('screen_idle', strip(screen(dim)), None),
        ('screen_auth', strip(screen(AMB)), None),
        ('screen_armed', strip(screen(RED, RED2)), {'delays': [[0.5, 0.5]]}),
        ('screen_final', strip(screen(RED, Y)), {'delays': [[0.15, 0.15]]}),
    ]
    save_rsi('tactical_warhead', states)
    return icon, b, opened


# --- keys --------------------------------------------------------------------------------------------------------

def auth_icon():
    c = Canvas()
    # lanyard loop
    for x, y in ((11, 6), (12, 5), (13, 4), (14, 4), (15, 4), (16, 4), (17, 4), (18, 4), (19, 5), (20, 6),
                 (11, 7), (11, 8), (12, 9), (13, 10), (20, 7), (20, 8), (19, 9), (18, 10)):
        c.put(x, y, G2)
    c.rect(15, 11, 16, 12, M3)          # clip
    # card
    c.rect(11, 13, 20, 26, WHITE)
    c.rect(11, 13, 20, 16, G2)          # republic green header
    c.rect(11, 17, 20, 17, K1)
    c.rect(12, 19, 15, 23, K1)          # photo
    c.rect(17, 19, 19, 19, M2); c.rect(17, 21, 19, 21, M2); c.rect(17, 23, 18, 23, M2)
    c.rect(11, 25, 20, 26, RED)         # red authority stripe
    c.put(15, 14, K2); c.put(16, 14, K2); c.put(15, 15, Y); c.put(16, 15, Y)
    c.outline()
    return c.im


def auth_neck():
    frames = []
    # south: lanyard from the collar to a card on the chest
    c = Canvas()
    for x, y in ((12, 9), (12, 10), (13, 11), (13, 12), (14, 13), (19, 9), (19, 10), (18, 11), (18, 12), (17, 13)):
        c.put(x, y, G2)
    c.rect(14, 14, 17, 18, WHITE)
    c.rect(14, 14, 17, 15, G2)
    c.rect(14, 18, 17, 18, RED)
    frames.append(c.im)
    # north: the strap behind the neck
    c = Canvas()
    for x in range(12, 20):
        c.put(x, 9, G2)
    c.put(12, 10, G2); c.put(19, 10, G2)
    frames.append(c.im)
    # east: side view, card edge on the chest
    c = Canvas()
    for x, y in ((14, 9), (15, 10), (16, 11), (17, 12)):
        c.put(x, y, G2)
    c.rect(17, 13, 18, 17, WHITE)
    c.put(17, 17, RED); c.put(18, 17, RED)
    frames.append(c.im)
    # west: mirror of east
    frames.append(frames[2].transpose(Image.FLIP_LEFT_RIGHT))
    im = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        im.paste(f, ((i % 2) * 32, (i // 2) * 32))
    return im


def panel_key():
    c = Canvas()
    # bow (ring) with a green tag
    for x, y in ((9, 9), (10, 8), (11, 8), (12, 9), (13, 10), (13, 11), (12, 12), (11, 13), (10, 13), (9, 12),
                 (8, 11), (8, 10)):
        c.put(x, y, M3)
    c.rect(5, 13, 8, 16, G3)
    c.put(6, 14, K2)
    # shaft and bit
    for i in range(10):
        c.put(13 + i, 12 + i, M3)
        c.put(14 + i, 12 + i, M2)
    c.rect(20, 21, 21, 22, M3)
    c.rect(22, 19, 23, 20, M3)
    c.outline()
    return c.im


def main():
    icon, b, opened = warhead()
    a_icon, a_neck, p_icon = auth_icon(), auth_neck(), panel_key()
    save_rsi('warhead_auth_key', [('icon', a_icon, None), ('equipped-NECK', a_neck, {'directions': 4})])
    save_rsi('warhead_panel_key', [('icon', p_icon, None)])
    prev = Image.new('RGBA', (32 * 5, 32), (60, 62, 66, 255))
    open_view = b.copy(); open_view.alpha_composite(opened)
    for i, im in enumerate((icon, open_view, a_icon, p_icon, a_neck.crop((0, 0, 32, 32)))):
        prev.alpha_composite(im, (32 * i, 0))
    prev.resize((32 * 5 * 4, 128), Image.NEAREST).save(os.path.join(os.path.dirname(__file__), 'preview.png'))


if __name__ == '__main__':
    main()
