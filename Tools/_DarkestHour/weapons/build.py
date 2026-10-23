"""Build all weapon RSIs from guns2.py. Usage: python3 build.py <out_dir> <repo>

Per gun: <id>.rsi (48x32: icon, base, bolt-open, mag-0, attachment overlays) and <id>_inhands.rsi
(32x32: in hand, wielded, back/suit storage or belt). Attachment item icons go to Attachments/attachments.rsi.
Magazine items use Monolith's sprites (see gen_yaml.py), so no magazine RSIs are built here."""
import json, os, sys
from PIL import Image
sys.path.insert(0, os.path.dirname(__file__))
from pixart import Art, PAL, render, render_mini, compose
from guns import GUNS

HERE = os.path.dirname(__file__)
COPY = "Drawn by Claude (Anthropic) for The Darkest Hour"
ICON_W, ICON_H = 48, 32   # default canvas; long guns set Gun.width = 64
DY = 2  # designs sit a little high on the grid; centre them vertically

TPL_RIFLE = json.load(open(os.path.join(HERE, 'tpl_ak.json')))
TPL_PISTOL = json.load(open(os.path.join(HERE, 'tpl_viper.json')))
REF_RIFLE = 40.0   # gun length that maps to the AK's in-hand size
REF_PISTOL = 22.0


# ---------------------------------------------------------------- attachments (stamps in the 'att' palette)
def stamp(kind, g):
    a = Art()
    if kind in ('suppressed', 'flashhider', 'compensator', 'ext_barrel'):
        mx, my = g.muzzle
        if kind == 'suppressed':
            a.bevel(mx - 3, my - 1, mx + 4, my + 2, '1234', left=True, right=True)
            a.vline(mx - 1, my - 1, my + 2, '2').vline(mx + 2, my - 1, my + 2, '2')
            a.hline(mx - 2, mx + 3, my, '4')
        elif kind == 'flashhider':
            a.bevel(mx - 1, my - 1, mx + 2, my + 2, '1234', right=True)
            a.vline(mx + 1, my - 1, my - 1, '0').vline(mx + 1, my + 2, my + 2, '0')
        elif kind == 'compensator':
            a.bevel(mx - 1, my - 1, mx + 3, my + 2, '1234', right=True)
            a.dot(mx, my - 1, '0').dot(mx + 2, my - 1, '0')
        else:
            a.bevel(mx, my, mx + 5, my + 1, '1234')
            a.bevel(mx + 6, my - 1, mx + 7, my + 2, '1234', right=True)
    elif kind.startswith('optic'):
        x0, x1, ry = g.rail
        c = (x0 + x1) // 2
        if kind == 'optic2x':          # red-dot sight
            a.bevel(c - 2, ry - 3, c + 2, ry - 1, '1234', left=True, right=True)
            a.dot(c + 2, ry - 2, 'B').dot(c - 2, ry - 2, '0')
            a.hline(c - 3, c + 3, ry, '1')
        elif kind == 'optic4x':        # scope
            a.bevel(c - 5, ry - 3, c + 4, ry - 2, '1234')
            a.bevel(c + 4, ry - 4, c + 6, ry - 1, '1234', right=True)
            a.bevel(c - 7, ry - 4, c - 6, ry - 1, '1234', left=True)
            a.dot(c + 6, ry - 3, 'B').dot(c + 6, ry - 2, 'b')
            a.dot(c - 3, ry - 1, '1').dot(c + 2, ry - 1, '1')
            a.dot(c - 1, ry - 4, '2')
        else:                          # long-range scope
            a.bevel(c - 7, ry - 3, c + 5, ry - 2, '1234')
            a.bevel(c + 5, ry - 5, c + 8, ry - 1, '1234', right=True)
            a.bevel(c - 9, ry - 4, c - 7, ry - 1, '1234', left=True)
            a.dot(c + 8, ry - 4, 'B').dot(c + 8, ry - 3, 'B').dot(c + 8, ry - 2, 'b')
            a.dot(c - 4, ry - 1, '1').dot(c + 3, ry - 1, '1')
            a.rect(c - 1, ry - 5, c, ry - 4, '2')
    elif kind in ('vgrip', 'agrip', 'gyro'):
        ux, uy = g.under
        if kind == 'vgrip':
            a.hline(ux - 1, ux + 2, uy, '1')
            a.bevel(ux, uy + 1, ux + 1, uy + 5, 'abcd', left=True, right=True)
            a.hline(ux, ux + 1, uy + 5, 'a')
        elif kind == 'agrip':
            a.shade_poly([(ux - 2, uy), (ux + 4, uy), (ux + 1, uy + 3), (ux - 2, uy + 2)], 'abcd')
        else:
            a.bevel(ux - 3, uy, ux + 3, uy + 3, '1234', left=True, right=True)
            a.hline(ux - 2, ux + 2, uy + 1, 'B').dot(ux, uy + 2, 'G')
    elif kind == 'stock':              # add-on wire stock (IMP-17)
        sx, sy = g.stock_at
        a.hline(sx - 9, sx, sy, '2').hline(sx - 9, sx, sy + 1, '1')
        a.vline(sx - 9, sy, sy + 6, '2').vline(sx - 8, sy + 1, sy + 6, '1')
        a.hline(sx - 8, sx - 4, sy + 6, '1')
    return a


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
    'ravenger': ['suppressed', 'flashhider', 'compensator', 'vgrip', 'agrip', 'optic2x', 'optic4x', 'optic8x'],
    'mk58': [],
    'tit_mg10': ['suppressed', 'flashhider', 'compensator', 'vgrip', 'agrip', 'optic2x', 'optic4x', 'optic8x'],
    'tit_r15': ['suppressed', 'flashhider', 'compensator', 'vgrip', 'agrip', 'optic2x', 'optic4x'],
    'alt_r16': ['suppressed', 'flashhider', 'compensator', 'vgrip', 'agrip', 'optic2x', 'optic4x', 'optic8x'],
    'pro_r12': ['optic4x', 'optic8x'],
    'pro_amr16': ['optic4x', 'optic8x'],
    'pro_lr17': ['optic2x', 'optic4x'],
    'ear20': [],
    'kl1': [],
    'kr3': ['optic2x', 'optic4x'],
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
def body_mask(repo, fx=0, fy=0):
    m = Image.new('L', (32, 32), 0)
    for part in ('torso_m', 'head_m', 'l_arm', 'r_arm', 'l_leg', 'r_leg', 'l_hand', 'r_hand'):
        im = Image.open(f'{repo}/Resources/Textures/Mobs/Species/Human/parts.rsi/{part}.png').convert('RGBA')
        f = im.crop((fx, fy, fx + 32, fy + 32))
        px = f.load(); mp = m.load()
        for y in range(32):
            for x in range(32):
                if px[x, y][3] > 0:
                    mp[x, y] = 255
    return m


def occlude(im, mask):
    mp = mask.load(); ip = im.load()
    for y in range(32):
        for x in range(32):
            if mp[x, y]:
                ip[x, y] = (0, 0, 0, 0)


def worn(g, art, tpl_state, ref, lo, hi, body_s, body_n, wielded=False):
    pal = PAL[g.faction]
    f = max(lo, min(hi, g.length / ref))
    out = []
    for d, t in enumerate(tpl_state):
        if t is None:
            out.append(Image.new('RGBA', (32, 32), (0, 0, 0, 0))); continue
        im = render_mini(art, pal, t['cx'], t['cy'], t['angle'], t['length'] * f)
        if wielded and d == 1:   # facing north: the wielded gun is in front of the body, hidden from view
            occlude(im, body_n)
        if t.get('strap'):
            strap = Image.new('RGBA', (32, 32), (0, 0, 0, 0)); sp = strap.load()
            for x, y, c in t['strap']:
                sp[x, y] = tuple(c)
            if d == 0:   # facing south: the gun is behind the body, only the strap is in front
                occlude(im, body_s)
                im.alpha_composite(strap)
            elif d != 1:
                im.alpha_composite(strap)
        out.append(im)
    return out


def build_gun(g, out, body_s, body_n):
    pal = PAL[g.faction]
    base = g.base.moved(0, DY)
    mag = g.mag.moved(0, DY)
    bolt = g.bolt_open.moved(0, DY)
    shifted = type('S', (), {})()
    shifted.muzzle = (g.muzzle[0], g.muzzle[1] + DY) if g.muzzle else None
    shifted.rail = (g.rail[0], g.rail[1], g.rail[2] + DY) if g.rail else None
    shifted.under = (g.under[0], g.under[1] + DY) if g.under else None
    shifted.stock_at = (g.stock_at[0], g.stock_at[1] + DY) if g.stock_at else None

    full = base.copy().stamp(mag)
    opened = base.copy().stamp(bolt)
    size = (g.width, ICON_H)
    states = [('icon', [render(full, pal, size)]),
              ('base', [render(base, pal, size)]),
              ('bolt-open', [render(opened, pal, size)])]
    if mag.px:
        states.append(('mag-0', [render(mag, pal, size)]))
    for kind in SLOTS[g.id]:
        states.append((kind, [render(stamp(kind, shifted), PAL['att'], size)]))
    save_rsi(os.path.join(out, g.id + '.rsi'), size, states)

    pistol = g.kind == 'pistol'
    tpl, ref, lo, hi = (TPL_PISTOL, REF_PISTOL, 0.9, 1.25) if pistol else (TPL_RIFLE, REF_RIFLE, 0.75, 1.12)
    worn_states = ['inhand-left', 'inhand-right']
    if not pistol:
        worn_states += ['wielded-inhand-left', 'wielded-inhand-right', 'equipped-BACKPACK', 'equipped-SUITSTORAGE']
    else:
        worn_states += ['equipped-BELT', 'equipped-SUITSTORAGE']
    inh = [(s, worn(g, full, tpl[s], ref, lo, hi, body_s, body_n, wielded=s.startswith('wielded')))
           for s in worn_states]
    save_rsi(os.path.join(out, g.id + '_inhands.rsi'), (32, 32), inh)
    return states, inh


def attachment_items(out):
    """Item icons for attachments, 32x32, centred."""
    dummy = type('D', (), {})()
    dummy.muzzle = (12, 15); dummy.rail = (8, 24, 18); dummy.under = (16, 13); dummy.stock_at = (20, 13)
    items = {}
    for kind in ['suppressed', 'flashhider', 'compensator', 'vgrip', 'agrip', 'optic2x', 'optic4x', 'optic8x',
                 'ext_barrel', 'gyro', 'stock']:
        a = stamp(kind, dummy)
        x0, y0, x1, y1 = a.bounds()
        a = a.moved(16 - (x0 + x1 + 1) // 2, 16 - (y0 + y1 + 1) // 2)
        items[kind] = render(a, PAL['att'], size=(32, 32))
    save_rsi(os.path.join(out, 'Attachments', 'attachments.rsi'), (32, 32), [(k, [v]) for k, v in items.items()])
    return items


def preview(allstates, path):
    rows = list(GUNS.values())
    cell = 64
    sheet = Image.new('RGBA', (cell * 3 + 32 * 6 + 12, len(rows) * 36 + 4), (66, 66, 72, 255))
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
        x = 6 + cell * 3
        for s, frames4 in inh:
            if s in ('inhand-left', 'wielded-inhand-left', 'equipped-BACKPACK', 'equipped-BELT'):
                for fr in frames4[:2]:
                    sheet.alpha_composite(fr, (x, y)); x += 32
    sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST).save(path)


if __name__ == '__main__':
    out_dir, repo = sys.argv[1], sys.argv[2]
    guns_out = os.path.join(out_dir, 'Objects/Weapons/Guns')
    body_s = body_mask(repo)
    body_n = body_mask(repo, 32, 0)
    allstates = {g.id: build_gun(g, guns_out, body_s, body_n) for g in GUNS.values()}
    attachment_items(guns_out)
    preview(allstates, os.path.join(out_dir, 'preview.png'))
    print('built', len(GUNS))
