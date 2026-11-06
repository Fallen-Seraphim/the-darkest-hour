"""Sprites of the modular armour IMB-1 (Empire) and RMB-1 (Republic).

The body suit and the modules are cut out of existing hardsuit sprites by body part: the human body part sprites
(Mobs/Species/Human/parts.rsi) tell which pixel of a suit belongs to the torso, an arm or a leg. Every module class
takes its shape from its own source suit and is recoloured into the faction palette:
  body suit  - Monolith TSFMC experimental (CC0)        IMB-1 black,  RMB-1 green
  light      - Monolith USSP combat light (CC-BY-SA)    IMB-1 white,  RMB-1 khaki
  heavy      - Monolith Viper Group juggernaut (CC-BY-SA)
  composite  - Monolith TSFMC combat (CC0), with a faction accent (IMB-1 imperial yellow, RMB-1 republic green)
  helmet     - vanilla merc helmet over the explorer gas mask (CC-BY-SA)

RMC look (author's decision 2026-11-05): with an RMC-14 checkout the body suit is the faction's RMC fatigues
(RMB-1 - USCM jungle fatigues, IMB-1 - TWE Royal Marines uniform), the torso and arm modules are cut from RMC armour
without recolouring (RMB-1 - M3 light / M3 padded / B12, IMB-1 - Royal Marines light / padded / Kestrel heavy) and
the helmet is the RMC helmet over the faction gas mask. RMC armour does not cover the legs, so the leg modules stay
cut from the Monolith suits, recoloured into an RMC-like palette; what the RMC armour has below the belt goes to
the torso module.

Run from the repository root with the path to a Monolith checkout (and an RMC-14 checkout for the RMC look):
    python3 Tools/_DarkestHour/modular_armor/build.py /path/to/monolith [/path/to/RMC-14]
Writes Resources/Textures/_DarkestHour/Clothing/OuterClothing/ModularArmor/*.rsi and
Resources/Textures/_DarkestHour/Clothing/Head/ModularArmor/*.rsi; preview.png next to this script is not committed."""
import colorsys
import json
import os
import sys
from collections import deque
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
TEX = os.path.join(ROOT, 'Resources/Textures')
OUT_OUTER = os.path.join(TEX, '_DarkestHour/Clothing/OuterClothing/ModularArmor')
OUT_HEAD = os.path.join(TEX, '_DarkestHour/Clothing/Head/ModularArmor')


def hexc(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def ramp(*hexes):
    return [hexc(h) for h in hexes]


FACTIONS = {
    'imb1': {
        'name': 'IMB-1',
        'body': ramp('08080a', '111114', '1a1b1f', '25272c', '33363c', '45484f'),
        'module': ramp('4a4741', '6f6b64', '938f88', 'b7b3ab', 'd8d4cc', 'f3f1ea'),
        # RMC look: legs in the dark Royal Marines armour colours
        'rmc_module': ramp('0b0c10', '15171d', '20232b', '2d3039', '3d414c', '545966'),
        'rmc_body': '_RMC14/Objects/Clothing/Uniforms/TSE/RCM/standard.rsi',
        'rmc_helmet': '_RMC14/Objects/Clothing/Head/Royal/helmet.rsi',
        'rmc_classes': {
            'light': '_RMC14/Objects/Clothing/OuterClothing/Royal/light.rsi',
            'composite': '_RMC14/Objects/Clothing/OuterClothing/Royal/padded.rsi',
            'heavy': '_RMC14/Objects/Clothing/OuterClothing/PVE/Royal/kestrelheavy.rsi',
        },
        'accent': ramp('4d3a08', '7a5c10', 'a57e1a', 'cfa22c', 'ecc548', 'f8e27c'),
        'helmet': ramp('4a4741', '77736c', 'a3a098', 'c7c3bb', 'e4e1da', 'f6f4ee'),
        'mask': ramp('09090b', '141417', '202226', '2e3136', '41454c', '5a5f68'),
        'lens': ramp('5a4206', 'a87c12', 'e8b628', 'ffe27a'),
    },
    'rmb1': {
        'name': 'RMB-1',
        'body': ramp('0c140b', '142113', '1d2e1b', '283d24', '35502f', '46643d'),
        'module': ramp('2e2c1f', '4f4a33', '746c4a', '978e66', 'b9ae86', 'ddd2b2'),
        # RMC look: legs in the olive drab of the M3 armour
        'rmc_module': ramp('1b1d12', '2a2d1c', '3b3f27', '4e5333', '646a42', '7e8556'),
        'rmc_body': '_RMC14/Objects/Clothing/Uniforms/Marine/standard/jungle.rsi',
        'rmc_helmet': '_RMC14/Objects/Clothing/Head/Helmets/m10/standard/jungle.rsi',
        'rmc_classes': {
            'light': '_RMC14/Objects/Clothing/OuterClothing/Armor/m3/light/padded/jungle.rsi',
            'composite': '_RMC14/Objects/Clothing/OuterClothing/Armor/m3/standard/padded/jungle.rsi',
            'heavy': '_RMC14/Objects/Clothing/OuterClothing/Armor/m3/b12/jungle.rsi',
        },
        'accent': ramp('1c2b0c', '2f4714', '43641d', '5e8c2a', '7fae46', 'a9cf72'),
        'helmet': ramp('2e2c1f', '524c35', '7a714e', '9f966f', 'c1b78f', 'e0d7b6'),
        'mask': ramp('101a10', '1a2a19', '273b24', '354d30', '46623f', '5a7a51'),
        'lens': ramp('1c3a12', '3a6b22', '6fae3c', 'b5e48a'),
    },
}

# module class: (source rsi relative to the Monolith Resources/Textures, keep accent)
CLASSES = {
    'light': ('_Mono/Clothing/OuterClothing/Hardsuits/ussp_combat_light.rsi', False),
    'heavy': ('_Mono/Clothing/OuterClothing/Hardsuits/viper_group_juggernaut.rsi', False),
    'composite': ('_Mono/Clothing/OuterClothing/Hardsuits/tsfmc_combat.rsi', True),
}
BODY_SOURCE = '_Mono/Clothing/OuterClothing/Hardsuits/tsfmc_experimental.rsi'

PARTS = ['torso', 'arm_left', 'arm_right', 'leg_left', 'leg_right']
DIRS = 4  # south, north, east, west — 2x2 grid of 32x32 frames


def lum(c):
    r, g, b = c[:3]
    return (0.299 * r + 0.587 * g + 0.114 * b) / 255


def from_ramp(rmp, y):
    y = max(0.0, min(1.0, y))
    pos = y * (len(rmp) - 1)
    i = min(int(pos), len(rmp) - 2)
    t = pos - i
    a, b = rmp[i], rmp[i + 1]
    return tuple(int(round(a[k] + (b[k] - a[k]) * t)) for k in range(3))


def frames(im):
    """The first animation frame of each direction (32x32)."""
    return [im.crop((x, y, x + 32, y + 32)) for (x, y) in ((0, 0), (32, 0), (0, 32), (32, 32))]


def assemble(frs):
    out = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
    for (x, y), f in zip(((0, 0), (32, 0), (0, 32), (32, 32)), frs):
        out.alpha_composite(f, (x, y))
    return out


def load_masks():
    base = os.path.join(TEX, 'Mobs/Species/Human/parts.rsi')
    groups = {
        'torso': ['torso_m', 'head_m'],
        'arm_left': ['l_arm', 'l_hand'],
        'arm_right': ['r_arm', 'r_hand'],
        'leg_left': ['l_leg', 'l_foot'],
        'leg_right': ['r_leg', 'r_foot'],
    }
    masks = {}
    for part, states in groups.items():
        per_dir = [set() for _ in range(DIRS)]
        for st in states:
            for d, f in enumerate(frames(Image.open(os.path.join(base, st + '.png')).convert('RGBA'))):
                px = f.load()
                for y in range(32):
                    for x in range(32):
                        if px[x, y][3] > 0:
                            per_dir[d].add((x, y))
        masks[part] = per_dir
    return masks


# which part wins where body part sprites overlap, per direction (east shows the left side, west the right side)
PRIORITY = [
    ['arm_left', 'arm_right', 'leg_left', 'leg_right', 'torso'],
    ['arm_left', 'arm_right', 'leg_left', 'leg_right', 'torso'],
    ['arm_left', 'torso', 'leg_left', 'leg_right', 'arm_right'],
    ['arm_right', 'torso', 'leg_right', 'leg_left', 'arm_left'],
]
# far limbs in side views: hidden behind the torso / near leg
FAR = {2: {'arm_right': 'torso', 'leg_right': 'leg_left'}, 3: {'arm_left': 'torso', 'leg_left': 'leg_right'}}


def assign(frame, d, masks):
    """Body part of every opaque pixel of a suit frame: the mask it lies on, else the nearest mask."""
    px = frame.load()
    owner = {}
    for part in PRIORITY[d]:
        for p in masks[part][d]:
            owner.setdefault(p, part)
    result = {}
    # multi-source BFS from all mask pixels for the pixels outside the body
    dist = dict(owner)
    queue = deque(owner.keys())
    while queue:
        x, y = queue.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (x + dx, y + dy)
            if 0 <= q[0] < 32 and 0 <= q[1] < 32 and q not in dist:
                dist[q] = dist[(x, y)]
                queue.append(q)
    for y in range(32):
        for x in range(32):
            if px[x, y][3] > 0:
                result[(x, y)] = dist.get((x, y), 'torso')
    return result


def lum_range(im):
    """10th and 95th percentile of brightness of the opaque pixels: sources differ a lot in brightness."""
    ys = sorted(lum(p) for p in im.convert('RGBA').getdata() if p[3])
    if not ys:
        return 0.0, 1.0
    return ys[int(len(ys) * 0.1)], ys[min(len(ys) - 1, int(len(ys) * 0.95))]


def recolor_px(c, faction, accent, keep_accent, lo, hi):
    h, s, v = colorsys.rgb_to_hsv(*(k / 255 for k in c[:3]))
    t = (lum(c) - lo) / max(0.05, hi - lo)
    if keep_accent and s >= 0.35 and v >= 0.35:
        return from_ramp(accent, 0.25 + 0.75 * t)
    # two-tone: seams and recesses take the body colour, plates the module colour
    if t < 0.32:
        return from_ramp(faction['body'], 0.15 + t / 0.32 * 0.75)
    return from_ramp(faction['module'], (t - 0.32) / 0.68 * 0.9 + 0.1)


def recolor_image(im, fn):
    im = im.convert('RGBA')
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            p = px[x, y]
            if p[3]:
                px[x, y] = fn(p) + (p[3],)
    return im


def cut_modules(src, masks, faction, keep_accent, recolor=True, module_key='module'):
    """part -> 64x64 four-direction image of that part of the source suit, recoloured (unless recolor=False)."""
    im = Image.open(src).convert('RGBA')
    palette = dict(faction)
    palette['module'] = faction[module_key]
    lo, hi = lum_range(im.crop((0, 0, 64, 64)))
    fr = frames(im)
    layers = {p: [Image.new('RGBA', (32, 32), (0, 0, 0, 0)) for _ in range(DIRS)] for p in PARTS}
    for d, f in enumerate(fr):
        owner = assign(f, d, masks)
        rc = recolor_image(f.copy(), lambda c: recolor_px(c, palette, faction['accent'], keep_accent, lo, hi)) \
            if recolor else f.copy()
        src_px = rc.load()
        for (x, y), part in owner.items():
            far_behind = FAR.get(d, {}).get(part)
            if far_behind and (x, y) in masks[far_behind][d]:
                continue
            layers[part][d].putpixel((x, y), src_px[x, y])
    return {p: assemble(fs) for p, fs in layers.items()}


def largest_piece(frame):
    """Только самый крупный связный кусок кадра (у торса RMC снизу висят обрывки ремней — в иконке они лишние)."""
    px = frame.load()
    w, h = frame.size
    seen, best = set(), []
    for y in range(h):
        for x in range(w):
            if px[x, y][3] == 0 or (x, y) in seen:
                continue
            comp, stack = [], [(x, y)]
            seen.add((x, y))
            while stack:
                cx, cy = stack.pop()
                comp.append((cx, cy))
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in seen and px[nx, ny][3]:
                        seen.add((nx, ny))
                        stack.append((nx, ny))
            if len(comp) > len(best):
                best = comp
    out = Image.new('RGBA', frame.size, (0, 0, 0, 0))
    opx = out.load()
    for x, y in best:
        opx[x, y] = px[x, y]
    return out


def icon_from(layer, prefer_side=False, front_only=False, main_piece=False):
    """
    Иконка предмета: кусок с того направления, где он виден крупнее (рука спереди — узкая полоска, сбоку — вся),
    обрезанный по содержимому и увеличенный под холст 32x32 (до 26 px по большей стороне, целым коэффициентом),
    с тёмным контуром — чтобы в окне инвентаря читался силуэт (решение автора 2026-11-06).
    """
    frames = [layer.crop((x, y, x + 32, y + 32)) for (x, y) in ((0, 0), (32, 0), (0, 32), (32, 32))]
    if front_only:
        frames = frames[:1]
    if main_piece:
        frames = [largest_piece(f) for f in frames]
    best, best_area = None, -1
    for i, frame in enumerate(frames):
        box = frame.getbbox()
        if not box:
            continue
        area = (box[2] - box[0]) * (box[3] - box[1])
        if prefer_side and i >= 2:
            area *= 1.3
        if area > best_area:
            best, best_area = frame.crop(box), area
    out = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    if best is None:
        return out
    w, h = best.size
    k = max(1, min(26 // max(w, h), 4))
    piece = best.resize((w * k, h * k), Image.NEAREST)
    # контур: тёмная обводка по альфе
    outline = Image.new('RGBA', (piece.width + 2, piece.height + 2), (0, 0, 0, 0))
    alpha = piece.split()[3]
    dark = Image.new('RGBA', piece.size, (18, 20, 16, 255))
    for dx, dy in ((0, 1), (2, 1), (1, 0), (1, 2)):
        outline.paste(dark, (dx, dy), alpha)
    outline.alpha_composite(piece, (1, 1))
    out.alpha_composite(outline, ((32 - outline.width) // 2, (32 - outline.height) // 2))
    return out


def write_rsi(path, states, copyright_, license_='CC-BY-SA-3.0'):
    os.makedirs(path, exist_ok=True)
    for f in os.listdir(path):
        os.remove(os.path.join(path, f))
    meta_states = []
    for name, (img, dirs) in states.items():
        img.save(os.path.join(path, name + '.png'))
        st = {'name': name}
        if dirs == 4:
            st['directions'] = 4
        meta_states.append(st)
    meta = {'version': 1, 'license': license_, 'copyright': copyright_, 'size': {'x': 32, 'y': 32},
            'states': meta_states}
    with open(os.path.join(path, 'meta.json'), 'w', encoding='utf-8') as f:
        json.dump(meta, f, indent=4, ensure_ascii=False)
        f.write('\n')


def build_helmet(faction, rmc_tex=None):
    helm_rsi = os.path.join(TEX, 'Clothing/Head/Helmets/merc_helmet.rsi')
    mask_rsi = os.path.join(TEX, 'Clothing/Mask/gasexplorer.rsi')

    def paint(im, part):
        def fn(c):
            h, s, v = colorsys.rgb_to_hsv(*(k / 255 for k in c[:3]))
            y = lum(c)
            if part == 'mask' and s >= 0.3 and 120 <= h * 360 <= 200 and v > 0.35:
                return from_ramp(faction['lens'], (y - 0.2) / 0.6)
            if y < 0.08:
                return from_ramp(faction['mask'], 0.0)
            return from_ramp(faction[part], (y - 0.05) / 0.55)
        return recolor_image(im, fn)

    mask_eq = paint(Image.open(os.path.join(mask_rsi, 'equipped-MASK.png')), 'mask')
    helm_eq = paint(Image.open(os.path.join(helm_rsi, 'equipped-HELMET.png')), 'helmet')
    if rmc_tex:
        helm_rsi = os.path.join(rmc_tex, faction['rmc_helmet'])
        helm_eq = Image.open(os.path.join(helm_rsi, 'equipped-HELMET.png')).convert('RGBA')
    eq = Image.new('RGBA', helm_eq.size, (0, 0, 0, 0))
    eq.alpha_composite(mask_eq.crop((0, 0) + helm_eq.size))
    eq.alpha_composite(helm_eq)
    eq = eq.crop((0, 0, 64, 64))

    mask_ic = paint(Image.open(os.path.join(mask_rsi, 'icon.png')), 'mask').crop((0, 0, 32, 32))
    helm_ic = (Image.open(os.path.join(helm_rsi, 'icon.png')).convert('RGBA') if rmc_tex
               else paint(Image.open(os.path.join(helm_rsi, 'icon.png')), 'helmet')).crop((0, 0, 32, 32))
    icon = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    icon.alpha_composite(mask_ic, (0, 3))
    icon.alpha_composite(helm_ic, (0, -2))
    empty4 = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
    empty1 = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    return {
        'icon': (icon, 1),
        'icon-unshaded': (empty1, 1),
        'equipped-head': (eq, 4),
        'equipped-head-unshaded': (empty4, 4),
    }


def rmc_frames(path, state):
    return Image.open(os.path.join(path, state + '.png')).convert('RGBA').crop((0, 0, 64, 64))


def rmc_copyright(path):
    with open(os.path.join(path, 'meta.json'), encoding='utf-8-sig') as f:
        return json.load(f).get('copyright', 'RMC-14')


def main(mono, rmc=None):
    mono_tex = os.path.join(mono, 'Resources/Textures')
    rmc_tex = os.path.join(rmc, 'Resources/Textures') if rmc else None
    masks = load_masks()
    preview = []
    for fid, faction in FACTIONS.items():
        # body suit
        if rmc_tex:
            body_rsi = os.path.join(rmc_tex, faction['rmc_body'])
            body = rmc_frames(body_rsi, 'equipped-INNERCLOTHING')
            body_icon = Image.open(os.path.join(body_rsi, 'icon.png')).convert('RGBA').crop((0, 0, 32, 32))
            body_copy = rmc_copyright(body_rsi) + ' | Used as the modular armour body suit for The Darkest Hour ' \
                '(Tools/_DarkestHour/modular_armor)'
        else:
            body = Image.open(os.path.join(mono_tex, BODY_SOURCE, 'equipped-OUTERCLOTHING.png')).convert('RGBA').crop((0, 0, 64, 64))
            body = recolor_image(body, lambda c: from_ramp(faction['body'], lum(c) / 0.55))
            body_icon = recolor_image(Image.open(os.path.join(mono_tex, BODY_SOURCE, 'icon.png')).convert('RGBA').crop((0, 0, 32, 32)),
                                      lambda c: from_ramp(faction['body'], lum(c) / 0.55))
            body_copy = 'Body suit cut from Monolith TSFMC experimental hardsuit (CC0, original work by TJohnson), ' \
                'recoloured for The Darkest Hour (Tools/_DarkestHour/modular_armor)'
        # иконка корпуса — сам корпус на теле (южный вид), а не сложенная форма RMC
        body_icon = icon_from(body, front_only=True)
        write_rsi(os.path.join(OUT_OUTER, fid + '_body.rsi'),
                  {'icon': (body_icon, 1), 'equipped-OUTERCLOTHING': (body, 4)}, body_copy)
        for cls, (src, keep_accent) in CLASSES.items():
            with open(os.path.join(mono_tex, src, 'meta.json'), encoding='utf-8-sig') as f:
                src_meta = json.load(f)
            copyright_ = src_meta.get('copyright', '')
            if rmc_tex:
                legs = cut_modules(os.path.join(mono_tex, src, 'equipped-OUTERCLOTHING.png'), masks, faction, False,
                                   module_key='rmc_module')
                rmc_src = os.path.join(rmc_tex, faction['rmc_classes'][cls])
                layers = cut_modules(os.path.join(rmc_src, 'equipped-OUTERCLOTHING.png'), masks, faction, False,
                                     recolor=False)
                # RMC armour stops at the belt: what it has below goes to the torso, the legs come from Monolith
                for part in ('leg_left', 'leg_right'):
                    layers['torso'].alpha_composite(layers[part])
                    layers[part] = legs[part]
                copyright_ = 'Torso and arms: ' + rmc_copyright(rmc_src) + '; legs: ' + copyright_
            else:
                layers = cut_modules(os.path.join(mono_tex, src, 'equipped-OUTERCLOTHING.png'), masks, faction, keep_accent)
            states = {}
            for part, img in layers.items():
                states[part] = (img, 4)
                states['icon_' + part] = (icon_from(img, prefer_side=part.startswith('arm'),
                                                    front_only=part == 'torso', main_piece=part == 'torso'), 1)
            write_rsi(os.path.join(OUT_OUTER, '%s_%s.rsi' % (fid, cls)), states,
                      (copyright_ + ' | Cut into armour modules by body part' + ('' if rmc_tex else ' and recoloured') +
                       ' for The Darkest Hour (Tools/_DarkestHour/modular_armor)').strip(' |'),
                      'CC-BY-SA-3.0' if rmc_tex else src_meta.get('license', 'CC-BY-SA-3.0'))
            full = body.copy()
            for part in PARTS:
                full.alpha_composite(layers[part])
            preview.append(('%s %s' % (faction['name'], cls), full))
        helmet = build_helmet(faction, rmc_tex)
        write_rsi(os.path.join(OUT_HEAD, fid + '_helmet.rsi'), helmet,
                  (rmc_copyright(os.path.join(rmc_tex, faction['rmc_helmet'])) + ' | Explorer gas mask from '
                   'space-station-14 (CC-BY-SA-3.0)' if rmc_tex else
                   'Merc helmet and explorer gas mask from space-station-14 (CC-BY-SA-3.0)') +
                  ', combined and recoloured for The Darkest Hour (Tools/_DarkestHour/modular_armor)')
        with_helmet = []
        for name, img in preview[-3:]:
            h = img.copy()
            h.alpha_composite(helmet['equipped-head'][0])
            with_helmet.append((name, h))
        preview[-3:] = with_helmet

    Z = 3
    sheet = Image.new('RGBA', (len(preview) * 64 * Z, 64 * Z + 14), (52, 54, 60, 255))
    d = ImageDraw.Draw(sheet)
    for i, (name, img) in enumerate(preview):
        sheet.alpha_composite(img.resize((64 * Z, 64 * Z), Image.NEAREST), (i * 64 * Z, 14))
        d.text((i * 64 * Z + 4, 1), name, fill=(235, 235, 235))
    sheet.save(os.path.join(HERE, 'preview.png'))


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None)
