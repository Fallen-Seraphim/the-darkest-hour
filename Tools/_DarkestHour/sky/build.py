"""Sky of the atmosphere layer (the map a ship hovers in above a world): a hazy desert sky and two layers of clouds
for the parallax DHSky. All textures tile seamlessly.

Run from the repository root:  python3 Tools/_DarkestHour/sky/build.py"""
import os
import random
from PIL import Image, ImageDraw, ImageFilter

OUT = 'Resources/Textures/_DarkestHour/Parallaxes'
SIZE = 512


def seamless(draw_fn, blur, seed, clear=(244, 238, 226, 0)):
    """Draws on a 3x3 tiled canvas, blurs and crops the centre: the result tiles without seams. The transparent
    background has the colour of the shapes, so the blur does not darken their edges."""
    rnd = random.Random(seed)
    big = Image.new('RGBA', (SIZE * 3, SIZE * 3), clear)
    d = ImageDraw.Draw(big)
    shapes = draw_fn(rnd)
    for ox in (0, SIZE, SIZE * 2):
        for oy in (0, SIZE, SIZE * 2):
            for (x0, y0, x1, y1), color in shapes:
                d.ellipse((x0 + ox, y0 + oy, x1 + ox, y1 + oy), fill=color)
    big = big.filter(ImageFilter.GaussianBlur(blur))
    return big.crop((SIZE, SIZE, SIZE * 2, SIZE * 2))


def haze(rnd):
    shapes = []
    for _ in range(140):
        x, y = rnd.uniform(0, SIZE), rnd.uniform(0, SIZE)
        r = rnd.uniform(30, 110)
        tone = rnd.choice([(232, 196, 150), (214, 170, 122), (240, 214, 176), (204, 160, 118)])
        shapes.append(((x - r, y - r * 0.6, x + r, y + r * 0.6), tone + (90,)))
    return shapes


def clouds(count, rmin, rmax, alpha):
    def fn(rnd):
        shapes = []
        for _ in range(count):
            cx, cy = rnd.uniform(0, SIZE), rnd.uniform(0, SIZE)
            # a cloud is a cluster of puffs, wider than tall
            for _ in range(rnd.randint(5, 11)):
                r = rnd.uniform(rmin, rmax)
                x = cx + rnd.uniform(-rmax * 1.6, rmax * 1.6)
                y = cy + rnd.uniform(-rmax * 0.5, rmax * 0.5)
                shade = rnd.randint(236, 252)
                shapes.append(((x - r, y - r * 0.55, x + r, y + r * 0.55), (shade, shade - 6, shade - 16, alpha)))
        return shapes
    return fn


def main():
    os.makedirs(OUT, exist_ok=True)

    base = Image.new('RGBA', (SIZE, SIZE), (222, 184, 138, 255))
    base.alpha_composite(seamless(haze, 28, 7, clear=(222, 184, 138, 0)))
    base.save(os.path.join(OUT, 'sky_desert_base.png'))

    seamless(clouds(9, 18, 40, 120), 9, 11).save(os.path.join(OUT, 'sky_desert_clouds_far.png'))
    seamless(clouds(6, 30, 64, 170), 12, 23).save(os.path.join(OUT, 'sky_desert_clouds_near.png'))

    with open(os.path.join(OUT, 'attributions.yml'), 'w', encoding='utf-8') as f:
        f.write('- files: ["sky_desert_base.png", "sky_desert_clouds_far.png", "sky_desert_clouds_near.png"]\n'
                '  license: "CC-BY-SA-3.0"\n'
                '  copyright: "Made for The Darkest Hour (Tools/_DarkestHour/sky/build.py)"\n'
                '  source: "https://github.com/Fallen-Seraphim/the-darkest-hour"\n')


if __name__ == '__main__':
    main()
