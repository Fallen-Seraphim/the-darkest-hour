"""Vector-described pixel-art guns for The Darkest Hour.
Each gun is a list of parts: (layer, role, polygon) in icon pixel space (32x32, muzzle to the right, y down).
Rasterised with PIL (no anti-aliasing) at any scale and rotation, then shaded and outlined."""
import math
from PIL import Image, ImageDraw

# ---------- palettes ----------
PAL = {
    'rep': {  # Republic: black receivers, coyote-tan polymer, steel
        'metal': (64, 68, 74), 'metal_d': (40, 43, 48), 'metal_l': (104, 110, 118),
        'poly': (146, 124, 88), 'poly_d': (112, 94, 66), 'poly2': (58, 60, 56),
        'accent': (176, 150, 104), 'glass': (90, 170, 200), 'brass': (190, 150, 70), 'mag': (52, 54, 58),
    },
    'emp': {  # Empire: blued steel, dark crimson furniture, brass details
        'metal': (62, 66, 80), 'metal_d': (38, 40, 50), 'metal_l': (110, 116, 134),
        'poly': (112, 42, 38), 'poly_d': (82, 30, 28), 'poly2': (54, 50, 52),
        'accent': (182, 142, 66), 'glass': (200, 90, 70), 'brass': (190, 150, 70), 'mag': (48, 50, 60),
    },
    'corp': {  # Metallistick Group station security: gunmetal and navy polymer
        'metal': (70, 74, 80), 'metal_d': (44, 47, 52), 'metal_l': (118, 124, 132),
        'poly': (52, 66, 96), 'poly_d': (38, 48, 72), 'poly2': (60, 62, 66),
        'accent': (200, 200, 205), 'glass': (90, 170, 200), 'brass': (190, 150, 70), 'mag': (54, 56, 62),
    },
    'att': {  # attachments: neutral black
        'metal': (56, 58, 64), 'metal_d': (34, 36, 40), 'metal_l': (100, 104, 112),
        'poly': (70, 72, 70), 'poly_d': (48, 50, 50), 'poly2': (60, 62, 60),
        'accent': (150, 150, 150), 'glass': (90, 170, 200), 'brass': (190, 150, 70), 'mag': (52, 54, 58),
    },
}

def R(x0, y0, x1, y1):
    """Rectangle covering pixels x0..x1, y0..y1 inclusive."""
    return [(x0, y0), (x1 + 1, y0), (x1 + 1, y1 + 1), (x0, y1 + 1)]

def P(*pts):
    return list(pts)

def rail(x0, x1, y, role_a='metal_d', role_b='metal_l'):
    """Picatinny-style rail: dark strip with light teeth every other pixel."""
    parts = [(role_a, R(x0, y, x1, y))]
    parts += [(role_b, R(x, y, x, y)) for x in range(x0 + 1, x1, 2)]
    return parts

def trigger(x, y):
    return [('metal_d', R(x, y, x, y))]

def shade(c, k):
    return tuple(max(0, min(255, int(round(v * k)))) for v in c[:3])

class Gun:
    def __init__(self, gid, faction, parts, muzzle, rail=None, under=None, mag_parts=None, bolt=None, bolt_open=None,
                 length=None, kind='rifle'):
        self.id = gid
        self.pal = PAL[faction]
        self.parts = parts            # list of (role, polygon) for the base sprite
        self.mag = mag_parts or []    # list of (role, polygon) for mag-0
        self.bolt = bolt or []        # parts only in closed state
        self.bolt_open = bolt_open or []  # parts only in open state
        self.muzzle = muzzle          # (x, y) pixel just past the muzzle, on the bore line
        self.rail = rail              # (x0, x1, y_top) for optics
        self.under = under            # (x, y) underbarrel grip mount (top of grip)
        self.kind = kind
        xs = [x for _, poly in parts + self.mag for x, _ in poly]
        self.x0, self.x1 = min(xs), max(xs)
        self.length = length or (self.x1 - self.x0)

def raster(parts, pal, size, transform=None):
    """Rasterise parts into an RGBA image. Returns (image, rolemap) where rolemap stores the role per pixel."""
    w, h = size
    img = Image.new('RGBA', size, (0, 0, 0, 0))
    roles = {}
    for role, poly in parts:
        pts = [transform(x, y) for x, y in poly] if transform else poly
        mask = Image.new('L', size, 0)
        d = ImageDraw.Draw(mask)
        if len(pts) == 2:
            d.line(pts, fill=255)
        else:
            d.polygon(pts, fill=255)
        mp = mask.load(); ip = img.load()
        col = pal[role]
        for y in range(h):
            for x in range(w):
                if mp[x, y]:
                    ip[x, y] = col + (255,)
                    roles[(x, y)] = role
    return img, roles

def finish(img, roles, light=(0, -1), outline=True):
    """Shade: lighten pixels whose neighbour towards the light is empty or another role, darken the opposite side,
    then darken the outer silhouette edge."""
    w, h = img.size
    src = img.copy().load(); px = img.load()
    lx, ly = light
    for (x, y), role in roles.items():
        c = src[x, y]
        if c[3] == 0:
            continue
        up = (round(x + lx), round(y + ly)); dn = (round(x - lx), round(y - ly))
        k = 1.0
        if roles.get(up) != role:
            k = 1.28
        elif roles.get(dn) != role:
            k = 0.78
        px[x, y] = shade(c, k) + (255,)
    if outline:
        src2 = img.copy().load()
        for (x, y) in roles:
            edge = False
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if not (0 <= nx < w and 0 <= ny < h) or src2[nx, ny][3] == 0:
                    edge = True; break
            if edge:
                px[x, y] = shade(src2[x, y], 0.55) + (255,)
    return img

def render_icon(parts, pal, size=(32, 32)):
    img, roles = raster(parts, pal, size)
    return finish(img, roles)

def render_mini(gun, parts, cx, cy, angle_deg, length, size=(32, 32)):
    """Render the gun scaled to `length` px, centred at (cx, cy), muzzle pointing at angle_deg
    (screen space, 0 = right, 90 = up). The gun is mirrored when needed so the magazine stays underneath."""
    s = length / max(gun.length, 1)
    gx = (gun.x0 + gun.x1 + 1) / 2
    gy = 13.5
    a = math.radians(angle_deg)
    d = (math.cos(a), -math.sin(a))          # muzzle direction on screen (y down)
    n = (math.sin(a), math.cos(a))           # gun's "down" side
    if n[1] < 0 or (abs(n[1]) < 1e-6 and n[0] < 0):
        n = (-n[0], -n[1])

    def tf(x, y):
        u = (x - gx) * s
        v = (y - gy) * s
        return (cx + u * d[0] + v * n[0], cy + u * d[1] + v * n[1])

    img, roles = raster(parts, gun.pal, size, transform=tf)
    return finish(img, roles, light=(-n[0], -n[1]))

def compose(*imgs):
    out = imgs[0].copy()
    for im in imgs[1:]:
        out.alpha_composite(im)
    return out
