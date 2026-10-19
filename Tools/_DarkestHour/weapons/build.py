"""Build all weapon RSIs from guns.py. Usage: python3 build.py <out_dir> <repo>"""
import json, os, sys, math
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(__file__))
from gunart import Gun, R, P, PAL, raster, finish, render_mini, compose
from guns import GUNS

HERE = os.path.dirname(__file__)
OUT = sys.argv[1]
REPO = sys.argv[2]
COPY = "Drawn by Claude (Anthropic) for The Darkest Hour"
ICON_W, ICON_H = 48, 32
SHIFT = 6  # designs are drawn on a 32px grid; shift right inside the 48px canvas

TPL_RIFLE = json.load(open(os.path.join(HERE, 'tpl_ak.json')))
TPL_PISTOL = json.load(open(os.path.join(HERE, 'tpl_viper.json')))
REF_RIFLE = 32.0   # AK icon length
REF_PISTOL = 22.0  # Viper icon length

def shift(parts, dx=SHIFT, dy=0):
    return [(role, [(x + dx, y + dy) for x, y in poly]) for role, poly in parts]

def icon(parts, pal):
    img, roles = raster(shift(parts), pal, (ICON_W, ICON_H))
    return finish(img, roles)

# ---------------------------------------------------------------- attachments (overlays, drawn in neutral black)
def overlay_parts(kind, g):
    mx, my = g.muzzle
    by = math.floor(my)
    parts = []
    if kind == 'suppressed':
        parts = [('metal', R(mx, by - 1, mx + 7, by + 2)), ('metal_d', R(mx + 2, by - 1, mx + 2, by + 2)),
                 ('metal_d', R(mx + 5, by - 1, mx + 5, by + 2))]
    elif kind == 'flashhider':
        parts = [('metal_d', R(mx, by, mx + 2, by + 1)), ('metal', R(mx + 1, by - 1, mx + 2, by - 1)),
                 ('metal', R(mx + 1, by + 2, mx + 2, by + 2))]
    elif kind == 'compensator':
        parts = [('metal', R(mx, by - 1, mx + 3, by + 2)), ('metal_d', R(mx + 1, by - 1, mx + 1, by - 1)),
                 ('metal_d', R(mx + 3, by - 1, mx + 3, by - 1))]
    elif kind == 'ext_barrel':
        parts = [('metal_d', R(mx, by, mx + 5, by + 1)), ('metal', R(mx + 5, by - 1, mx + 6, by + 2))]
    elif kind in ('vgrip', 'agrip', 'gyro'):
        ux, uy = g.under
        if kind == 'vgrip':
            parts = [('poly2', R(ux, uy, ux + 1, uy + 4)), ('metal_d', R(ux - 1, uy, ux + 2, uy))]
        elif kind == 'agrip':
            parts = [('poly2', P((ux - 2, uy), (ux + 3, uy), (ux + 1, uy + 3), (ux - 2, uy + 2)))]
        else:
            parts = [('metal', R(ux - 2, uy, ux + 2, uy + 2)), ('accent', R(ux - 1, uy + 1, ux + 1, uy + 1))]
    elif kind.startswith('optic'):
        x0, x1, ry = g.rail
        c = (x0 + x1) // 2
        if kind == 'optic2x':
            parts = [('metal_d', R(c - 1, ry - 1, c + 2, ry - 1)), ('metal', R(c - 1, ry - 4, c + 2, ry - 2)),
                     ('glass', R(c + 2, ry - 3, c + 2, ry - 3))]
        elif kind == 'optic4x':
            parts = [('metal_d', R(c - 2, ry - 1, c - 2, ry - 1)), ('metal_d', R(c + 2, ry - 1, c + 2, ry - 1)),
                     ('metal', R(c - 4, ry - 3, c + 4, ry - 2)), ('metal', R(c + 3, ry - 4, c + 5, ry - 2)),
                     ('glass', R(c + 5, ry - 3, c + 5, ry - 3))]
        else:
            parts = [('metal_d', R(c - 3, ry - 1, c - 3, ry - 1)), ('metal_d', R(c + 3, ry - 1, c + 3, ry - 1)),
                     ('metal', R(c - 6, ry - 3, c + 5, ry - 2)), ('metal', R(c - 7, ry - 4, c - 6, ry - 2)),
                     ('metal', R(c + 4, ry - 5, c + 7, ry - 2)), ('glass', R(c + 7, ry - 4, c + 7, ry - 3))]
    elif kind == 'stock':
        # wire stock for the IMP-17, attached behind the grip
        parts = [('metal_d', P((g.x0 - 7, 12), (g.x0, 12), (g.x0, 13), (g.x0 - 6, 13), (g.x0 - 6, 17),
                               (g.x0 - 7, 17)))]
    return parts

# which overlay states each gun gets
SLOTS = {
    'rar_m11_1': ['suppressed', 'flashhider', 'compensator', 'vgrip', 'agrip', 'optic2x', 'optic4x', 'optic8x'],
    'rar_m11_2': ['suppressed', 'flashhider', 'compensator', 'vgrip', 'agrip', 'optic2x', 'optic4x', 'optic8x'],
    'rar_m11_3': ['suppressed', 'flashhider', 'compensator', 'vgrip', 'agrip', 'optic2x', 'optic4x', 'optic8x'],
    'ramg_m14_1': ['optic2x', 'optic4x'],
    'ramg_m14_2': ['optic2x', 'optic4x'],
    'raptor_m12_16': ['suppressed', 'flashhider', 'compensator', 'optic2x'],
    'rmar_as_m15_18': ['optic4x', 'optic8x'],
    'rmad_as_m15_17': ['suppressed', 'flashhider', 'compensator', 'vgrip', 'agrip', 'optic2x', 'optic4x', 'optic8x'],
    'rmad_in_m91_19': ['optic2x', 'optic4x', 'optic8x'],
    'rpr_m9_02': ['suppressed', 'flashhider', 'compensator', 'optic2x'],
    'ir17': ['suppressed', 'vgrip', 'agrip', 'optic2x', 'optic4x', 'optic8x'],
    'img17l': ['suppressed', 'optic2x'],
    'img17t': ['optic2x', 'optic4x'],
    'imp17': ['ext_barrel', 'stock', 'optic2x', 'optic4x', 'gyro'],
    'imsh17': ['optic2x'],
    'impsh17p': [],
    'vympel': ['suppressed', 'flashhider', 'compensator', 'vgrip', 'agrip', 'optic2x', 'optic4x'],
    'wspr': ['optic2x', 'optic4x', 'optic8x'],
}

def save_rsi(path, size, states):
    os.makedirs(path, exist_ok=True)
    meta = {"version": 1, "license": "CC-BY-SA-3.0", "copyright": COPY,
            "size": {"x": size[0], "y": size[1]}, "states": []}
    for name, imgs in states:
        if len(imgs) == 1:
            imgs[0].save(os.path.join(path, name + '.png'))
            meta['states'].append({"name": name})
        else:  # 4 directions in a 2x2 grid
            w, h = size
            sheet = Image.new('RGBA', (w * 2, h * 2), (0, 0, 0, 0))
            for i, im in enumerate(imgs):
                sheet.paste(im, ((i % 2) * w, (i // 2) * h))
            sheet.save(os.path.join(path, name + '.png'))
            meta['states'].append({"name": name, "directions": 4})
    json.dump(meta, open(os.path.join(path, 'meta.json'), 'w'), indent=2)

# ---------------------------------------------------------------- body occlusion for things worn on the back
def body_mask(fx=0, fy=0):
    m = Image.new('L', (32, 32), 0)
    for part in ('torso_m', 'head_m', 'l_arm', 'r_arm', 'l_leg', 'r_leg', 'l_hand', 'r_hand'):
        im = Image.open(f'{REPO}/Resources/Textures/Mobs/Species/Human/parts.rsi/{part}.png').convert('RGBA')
        f = im.crop((fx, fy, fx + 32, fy + 32))
        px = f.load(); mp = m.load()
        for y in range(32):
            for x in range(32):
                if px[x, y][3] > 0:
                    mp[x, y] = 255
    return m
BODY_S = None
BODY_N = None

def occlude(im, mask):
    mp = mask.load(); ip = im.load()
    for y in range(32):
        for x in range(32):
            if mp[x, y]:
                ip[x, y] = (0, 0, 0, 0)

def worn(g, parts, tpl_state, ref, wielded=False):
    out = []
    for d, t in enumerate(tpl_state):
        if t is None:
            out.append(Image.new('RGBA', (32, 32), (0, 0, 0, 0))); continue
        length = t['length'] * g.length / ref
        im = render_mini(g, parts, t['cx'], t['cy'], t['angle'], length)
        if wielded and d == 1:   # facing north: the wielded gun is in front of the body, hidden from view
            occlude(im, BODY_N)
        if t.get('strap'):
            strap = Image.new('RGBA', (32, 32), (0, 0, 0, 0)); sp = strap.load()
            for x, y, c in t['strap']:
                sp[x, y] = tuple(c)
            if d == 0:   # facing south: the gun is behind the body, only the strap is in front
                occlude(im, BODY_S)
                im.alpha_composite(strap)
            else:
                im.alpha_composite(strap) if d != 1 else None
        out.append(im)
    return out

def build_gun(g):
    pal = g.pal
    full = g.parts + g.bolt + g.mag
    states = [('icon', [icon(g.parts + g.bolt + g.mag, pal)]),
              ('base', [icon(g.parts + g.bolt, pal)]),
              ('bolt-open', [icon(g.parts + g.bolt_open, pal)])]
    if g.mag:
        states.append(('mag-0', [icon(g.mag, pal)]))
    for kind in SLOTS[g.id]:
        states.append((kind, [icon(overlay_parts(kind, g), PAL['att'])]))
    save_rsi(os.path.join(OUT, 'Objects/Weapons/Guns', g.id + '.rsi'), (ICON_W, ICON_H), states)

    # in-hand / worn sprites
    pistol = g.kind == 'pistol'
    tpl, ref = (TPL_PISTOL, REF_PISTOL) if pistol else (TPL_RIFLE, REF_RIFLE)
    worn_states = ['inhand-left', 'inhand-right']
    if not pistol:
        worn_states += ['wielded-inhand-left', 'wielded-inhand-right', 'equipped-BACKPACK', 'equipped-SUITSTORAGE']
    else:
        worn_states += ['equipped-BELT', 'equipped-SUITSTORAGE']
    inh = [(s, worn(g, full, tpl[s], ref, wielded=s.startswith('wielded'))) for s in worn_states]
    save_rsi(os.path.join(OUT, 'Objects/Weapons/Guns', g.id + '_inhands.rsi'), (32, 32), inh)

    # magazine item
    if g.mag:
        xs = [x for _, poly in g.mag for x, _ in poly]; ys = [y for _, poly in g.mag for _, y in poly]
        dx = 16 - (min(xs) + max(xs)) / 2; dy = 16 - (min(ys) + max(ys)) / 2
        mag = [(r, [(x + dx, y + dy) for x, y in poly]) for r, poly in g.mag]
        img, roles = raster(mag, pal, (32, 32)); base = finish(img, roles)
        top = min(y for _, poly in mag for _, y in poly)
        lx = [x for _, poly in mag for x, y in poly if y == top]
        cart = Image.new('RGBA', (32, 32), (0, 0, 0, 0)); d = ImageDraw.Draw(cart)
        if lx:
            d.line([(min(lx) + 1, top - 1), (max(lx) - 1, top - 1)], fill=g.pal['brass'] + (255,))
        save_rsi(os.path.join(OUT, 'Objects/Weapons/Guns/Magazines', g.id + '_mag.rsi'), (32, 32),
                 [('base', [base]), ('mag-1', [cart])])
    return states, inh

def attachment_items():
    """Item icons for attachments, 32x32."""
    dummy = Gun('dummy', 'att', [('metal', R(4, 14, 5, 15))], muzzle=(8, 15.5), rail=(8, 24, 17), under=(16, 13))
    items = {}
    def centred(parts):
        xs = [x for _, p in parts for x, _ in p]; ys = [y for _, p in parts for _, y in p]
        dx = 16 - (min(xs) + max(xs)) / 2; dy = 16 - (min(ys) + max(ys)) / 2
        img, roles = raster([(r, [(x + dx, y + dy) for x, y in p]) for r, p in parts], PAL['att'], (32, 32))
        return finish(img, roles)
    for kind in ['suppressed', 'flashhider', 'compensator', 'vgrip', 'agrip', 'optic2x', 'optic4x', 'optic8x',
                 'ext_barrel', 'gyro']:
        items[kind] = centred(overlay_parts(kind, dummy))
    stock = [('metal_d', P((8, 14), (22, 14), (22, 15), (10, 15), (10, 20), (8, 20)))]
    items['stock'] = centred(stock)
    save_rsi(os.path.join(OUT, 'Objects/Weapons/Guns/Attachments', 'attachments.rsi'), (32, 32),
             [(k, [v]) for k, v in items.items()])
    return items

if __name__ == '__main__':
    BODY_S = body_mask()
    BODY_N = body_mask(32, 0)
    allstates = {}
    for g in GUNS.values():
        allstates[g.id] = build_gun(g)
    att = attachment_items()
    # preview sheet
    rows = list(GUNS.values())
    cell = 48
    sheet = Image.new('RGBA', (cell * 6 + 32 * 4 + 20, len(rows) * 36 + 4), (66, 66, 72, 255))
    for i, g in enumerate(rows):
        st, inh = allstates[g.id]
        names = dict(st)
        y = 2 + i * 36
        sheet.alpha_composite(names['icon'][0], (2, y))
        sheet.alpha_composite(names['bolt-open'][0], (2 + cell, y))
        ov = names['base'][0].copy()
        for k in SLOTS[g.id]:
            if k.startswith('optic4') or k in ('suppressed', 'vgrip', 'ext_barrel', 'stock', 'gyro'):
                ov.alpha_composite(names[k][0])
        if 'mag-0' in names:
            ov.alpha_composite(names['mag-0'][0])
        sheet.alpha_composite(ov, (2 + cell * 2, y))
        x = 2 + cell * 3
        for s, frames4 in inh:
            if s in ('wielded-inhand-left', 'equipped-BACKPACK', 'inhand-left'):
                for f in frames4[:2]:
                    sheet.alpha_composite(f, (x, y)); x += 32
    sheet = sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST)
    sheet.save(os.path.join(OUT, 'preview.png'))
    print('built', len(GUNS))
