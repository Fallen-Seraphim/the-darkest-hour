"""Pixel-art drawing kit for The Darkest Hour weapons (v2).

A gun is drawn on a character grid (one character = one palette colour) with simple primitives
(rectangles, bevelled panels, polygons, vents, rails). Palettes hold 4-step ramps per material so every
panel gets a lit top, a body and a shadowed bottom. Rendering darkens the silhouette edge; in-hand and worn
sprites are rendered by supersampling the rotated grid and taking the most common colour per pixel."""
import math
from collections import Counter
from PIL import Image, ImageDraw

# ------------------------------------------------------------------ palettes
# '0'..'5'  metal, dark -> light        'a'..'d'  main furniture, dark -> light
# 'u'..'y'  second furniture / accent   'R' red LED  'G' green emitter  'B' cyan glass  'O' orange  'Y' brass  'W' white
BASE = {
    'R': (226, 48, 40), 'r': (140, 30, 28), 'G': (96, 240, 96), 'g': (40, 140, 52), 'B': (96, 196, 222),
    'b': (54, 120, 150), 'C': (40, 104, 136), 'O': (244, 150, 48), 'Y': (198, 158, 72), 'W': (236, 238, 240), 'K': (14, 15, 17),
}

def ramp(*cols):
    return cols

PAL = {
    # Republic: colonial-marine gunmetal with a green tint, coyote-tan polymer, red status lights
    'rep': dict(BASE, **dict(zip('012345', [(24, 27, 27), (44, 50, 50), (66, 74, 73), (92, 103, 100), (128, 140, 136), (170, 182, 176)])),
                **dict(zip('abcd', [(90, 72, 48), (122, 100, 68), (152, 128, 90), (184, 160, 118)])),
                **dict(zip('uvxy', [(46, 52, 44), (64, 72, 60), (86, 96, 80), (112, 124, 104)]))),
    # Empire (PDV style): black steel, white polymer, yellow markings, green emitters
    'emp': dict(BASE, **dict(zip('012345', [(16, 16, 19), (28, 28, 32), (42, 42, 48), (60, 60, 68), (86, 86, 96), (124, 124, 134)])),
                **dict(zip('abcd', [(138, 140, 146), (176, 178, 184), (208, 210, 214), (238, 239, 241)])),
                **dict(zip('uvxy', [(146, 106, 14), (196, 148, 24), (232, 188, 40), (252, 222, 98)]))),
    # Metallistick Group station security: gunmetal, navy polymer, white trim
    'corp': dict(BASE, **dict(zip('012345', [(22, 24, 28), (38, 41, 46), (56, 60, 66), (78, 83, 90), (108, 114, 122), (150, 156, 164)])),
                 **dict(zip('abcd', [(30, 38, 62), (44, 56, 88), (62, 78, 118), (88, 106, 150)])),
                 **dict(zip('uvxy', [(150, 152, 156), (190, 192, 196), (218, 220, 224), (242, 243, 245)]))),
    # attachments: neutral matte black
    'att': dict(BASE, **dict(zip('012345', [(30, 32, 35), (54, 57, 61), (76, 80, 85), (100, 105, 111), (130, 136, 143), (166, 172, 179)])),
                **dict(zip('abcd', [(60, 62, 64), (80, 82, 84), (102, 104, 106), (128, 130, 132)])),
                **dict(zip('uvxy', [(60, 62, 64), (80, 82, 84), (102, 104, 106), (128, 130, 132)]))),
}
for p in PAL.values():
    p.setdefault('K', (14, 15, 17))

M = '012345'[1:5]  # default metal ramp '1234'


class Art:
    """Sparse character grid. (x, y) -> palette key."""

    def __init__(self, px=None):
        self.px = dict(px or {})

    def copy(self):
        return Art(self.px)

    # ---- primitives
    def dot(self, x, y, c):
        self.px[(x, y)] = c
        return self

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.px[(x, y)] = c
        return self

    def hline(self, x0, x1, y, c):
        return self.rect(x0, y, x1, y, c)

    def vline(self, x, y0, y1, c):
        return self.rect(x, y0, x, y1, c)

    def erase(self, x0, y0, x1, y1):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.px.pop((x, y), None)
        return self

    def bevel(self, x0, y0, x1, y1, r, left=False, right=False):
        """Panel lit from above: top row r[3], body r[2], lower body r[1], bottom row r[0]."""
        h = y1 - y0 + 1
        for y in range(y0, y1 + 1):
            i = y - y0
            if i == 0 and h > 1:
                c = r[3]
            elif y == y1 and h > 2:
                c = r[0]
            elif h >= 5 and y >= y1 - (h - 2) // 3:
                c = r[1]
            else:
                c = r[2]
            self.hline(x0, x1, y, c)
        if left:
            self.vline(x0, y0 + 1, y1, r[3] if h > 2 else r[2])
        if right:
            self.vline(x1, y0 + 1, y1, r[0])
        return self

    def poly(self, pts, c):
        xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
        ox, oy = min(xs) - 1, min(ys) - 1
        w, h = max(xs) - ox + 3, max(ys) - oy + 3
        m = Image.new('L', (int(w), int(h)), 0)
        ImageDraw.Draw(m).polygon([(x - ox, y - oy) for x, y in pts], fill=255)
        mp = m.load()
        for yy in range(m.height):
            for xx in range(m.width):
                if mp[xx, yy]:
                    self.px[(xx + ox, yy + oy)] = c
        return self

    def shade_poly(self, pts, r):
        """Polygon filled with r[2], then its top edge lit (r[3]) and bottom edge shadowed (r[0])."""
        before = set(self.px)
        tmp = Art().poly(pts, r[2])
        for (x, y) in tmp.px:
            c = r[2]
            if (x, y - 1) not in tmp.px:
                c = r[3]
            elif (x, y + 1) not in tmp.px:
                c = r[0]
            elif (x, y + 2) not in tmp.px:
                c = r[1]
            self.px[(x, y)] = c
        return self

    def pattern(self, x0, x1, y, seq):
        for i, x in enumerate(range(x0, x1 + 1)):
            ch = seq[i % len(seq)]
            if ch != '.':
                self.px[(x, y)] = ch
        return self

    def vents(self, x0, x1, y0, y1, c, step=3, w=1):
        """Vertical slots every `step` pixels."""
        for x in range(x0, x1 + 1, step):
            for k in range(w):
                self.vline(x + k, y0, y1, c)
        return self

    def rail(self, x0, x1, y, dark='1', light='3'):
        self.hline(x0, x1, y, dark)
        for x in range(x0 + 1, x1, 2):
            self.dot(x, y - 1, light)
        return self

    def stamp(self, other, dx=0, dy=0):
        for (x, y), c in other.px.items():
            if c == ' ':
                self.px.pop((x + dx, y + dy), None)
            else:
                self.px[(x + dx, y + dy)] = c
        return self

    def moved(self, dx, dy):
        return Art({(x + dx, y + dy): c for (x, y), c in self.px.items()})

    def bounds(self):
        xs = [x for x, _ in self.px]; ys = [y for _, y in self.px]
        return min(xs), min(ys), max(xs), max(ys)


def text_art(rows, ox=0, oy=0):
    """Art from rows of characters; '.' and ' ' are transparent."""
    a = Art()
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            if ch not in '. ':
                a.dot(ox + i, oy + j, ch)
    return a


def _shade(c, k):
    return tuple(max(0, min(255, int(round(v * k)))) for v in c[:3])


def edge(img, k=0.6, inner=None):
    """Darken silhouette pixels (those touching transparency)."""
    w, h = img.size
    src = img.copy().load(); px = img.load()
    for y in range(h):
        for x in range(w):
            p = src[x, y]
            if p[3] == 0:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if not (0 <= nx < w and 0 <= ny < h) or src[nx, ny][3] == 0:
                    px[x, y] = _shade(p, k) + (255,)
                    break
    return img


def render(art, pal, size=(48, 32), outline=True):
    img = Image.new('RGBA', size, (0, 0, 0, 0))
    px = img.load()
    for (x, y), c in art.px.items():
        if 0 <= x < size[0] and 0 <= y < size[1]:
            px[x, y] = tuple(pal[c]) + (255,)
    return edge(img) if outline else img


def render_mini(art, pal, cx, cy, angle_deg, length, size=(32, 32), ss=4, coverage=0.3):
    """Render `art` rotated so its muzzle (right side) points at angle_deg (screen space, 0 = right, 90 = up),
    scaled to `length` px and centred on (cx, cy). Mirrored when needed so the underside stays down."""
    x0, y0, x1, y1 = art.bounds()
    glen = x1 - x0 + 1
    gx, gy = (x0 + x1 + 1) / 2, (y0 + y1 + 1) / 2
    s = length / glen
    a = math.radians(angle_deg)
    d = (math.cos(a), -math.sin(a))
    n = (math.sin(a), math.cos(a))
    if n[1] < 0 or (abs(n[1]) < 1e-6 and n[0] < 0):
        n = (-n[0], -n[1])
    W, H = size[0] * ss, size[1] * ss
    keys = sorted(set(art.px.values()))
    idx = {k: i + 1 for i, k in enumerate(keys)}
    canvas = Image.new('L', (W, H), 0)
    dr = ImageDraw.Draw(canvas)

    def tf(x, y):
        u = (x - gx) * s; v = (y - gy) * s
        return ((cx + u * d[0] + v * n[0]) * ss, (cy + u * d[1] + v * n[1]) * ss)

    for (x, y), c in art.px.items():
        pts = [tf(x, y), tf(x + 1, y), tf(x + 1, y + 1), tf(x, y + 1)]
        dr.polygon(pts, fill=idx[c])
    cp = canvas.load()
    out = Image.new('RGBA', size, (0, 0, 0, 0)); op = out.load()
    inv = {i: k for k, i in idx.items()}
    need = coverage * ss * ss
    for ty in range(size[1]):
        for tx in range(size[0]):
            cnt = Counter()
            for yy in range(ty * ss, ty * ss + ss):
                for xx in range(tx * ss, tx * ss + ss):
                    v = cp[xx, yy]
                    if v:
                        cnt[v] += 1
            tot = sum(cnt.values())
            if tot < need:
                continue
            # prefer accent colours (LEDs, glass, stripes) if present, else the most common
            best = cnt.most_common(1)[0][0]
            for v in cnt:
                if inv[v] in 'RGBCOWY' and cnt[v] >= ss:
                    best = v
                    break
            op[tx, ty] = tuple(pal[inv[best]]) + (255,)
    return edge(out, 0.62)


def compose(*imgs):
    out = imgs[0].copy()
    for im in imgs[1:]:
        out.alpha_composite(im)
    return out
