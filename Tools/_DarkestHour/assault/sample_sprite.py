"""Sprite of the experimental sample (the Assault objective): a sealed cryogenic container with a glowing core.

Run from the repository root:  python3 Tools/_DarkestHour/assault/sample_sprite.py
Writes Resources/Textures/_DarkestHour/Objects/Specific/experimental_sample.rsi"""
import json
import os
from PIL import Image

OUT = 'Resources/Textures/_DarkestHour/Objects/Specific/experimental_sample.rsi'

STEEL = (120, 126, 132, 255)
STEEL_LIGHT = (168, 174, 180, 255)
STEEL_DARK = (72, 76, 82, 255)
OUTLINE = (34, 36, 40, 255)
GREEN = (78, 107, 46, 255)
KHAKI = (210, 199, 154, 255)
GLASS = (34, 70, 84, 255)
CORE = (92, 224, 255, 255)
CORE_HOT = (210, 250, 255, 255)
CORE_DIM = (36, 132, 170, 255)


def canister(px, x0, y0, w, h, glow_only=False):
    """Vertical canister with caps, a green band and a viewport; glow_only draws only the core."""
    cx0, cx1 = x0, x0 + w - 1
    vx0, vx1 = x0 + 2, x0 + w - 3
    vy0, vy1 = y0 + 3, y0 + h - 5
    for y in range(y0, y0 + h):
        for x in range(cx0, cx1 + 1):
            edge = x in (cx0, cx1) or y in (y0, y0 + h - 1)
            cap = y <= y0 + 1 or y >= y0 + h - 2
            inside_view = vx0 <= x <= vx1 and vy0 <= y <= vy1
            if glow_only:
                if inside_view and x not in (vx0, vx1) and y not in (vy0, vy1):
                    mid = (vx0 + vx1) / 2
                    dist = abs(x - mid)
                    # the specimen: a bright vertical filament with a knot in the middle
                    knot = abs(y - (vy0 + vy1) / 2) <= 1 and dist <= 1.5
                    if dist <= 0.5 or knot:
                        px[x, y] = CORE_HOT
                    elif dist <= 1.5:
                        px[x, y] = CORE
                    else:
                        px[x, y] = CORE_DIM
                continue
            if edge:
                px[x, y] = OUTLINE
            elif cap:
                px[x, y] = STEEL_DARK if y in (y0 + 1, y0 + h - 2) and x % 2 else STEEL
            elif inside_view:
                px[x, y] = OUTLINE if x in (vx0, vx1) or y in (vy0, vy1) else GLASS
            elif y == vy1 + 2:
                px[x, y] = GREEN
            else:
                px[x, y] = STEEL_LIGHT if x <= cx0 + 2 else (STEEL_DARK if x >= cx1 - 1 else STEEL)
    # khaki stencil dots on the band
    for x in range(cx0 + 2, cx1 - 1, 2):
        px[x, vy1 + 2] = KHAKI


def main():
    os.makedirs(OUT, exist_ok=True)
    icon = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    canister(icon.load(), 10, 5, 12, 23)
    icon.save(os.path.join(OUT, 'icon.png'))
    glow = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    canister(glow.load(), 10, 5, 12, 23, glow_only=True)
    glow.save(os.path.join(OUT, 'glow.png'))

    # in-hand: 4 directions in a row (south, north, east, west), small canister at the hand
    hands = {
        # direction: (right hand x, left hand x, y) for a 32x32 human
        'inhand-right': [(9, 18), (21, 18), (14, 19), (16, 19)],
        'inhand-left': [(21, 18), (9, 18), (16, 19), (14, 19)],
    }
    for state, spots in hands.items():
        sheet = Image.new('RGBA', (128, 32), (0, 0, 0, 0))
        for i, (x, y) in enumerate(spots):
            frame = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
            canister(frame.load(), x - 2, y - 4, 6, 11)
            canister(frame.load(), x - 2, y - 4, 6, 11, glow_only=True)
            sheet.alpha_composite(frame, (32 * i, 0))
        sheet.save(os.path.join(OUT, state + '.png'))

    meta = {
        'version': 1,
        'license': 'CC-BY-SA-3.0',
        'copyright': 'Made for The Darkest Hour (Tools/_DarkestHour/assault/sample_sprite.py)',
        'size': {'x': 32, 'y': 32},
        'states': [
            {'name': 'icon'},
            {'name': 'glow'},
            {'name': 'inhand-left', 'directions': 4},
            {'name': 'inhand-right', 'directions': 4},
        ],
    }
    with open(os.path.join(OUT, 'meta.json'), 'w', encoding='utf-8') as f:
        json.dump(meta, f, indent=4)
        f.write('\n')


if __name__ == '__main__':
    main()
