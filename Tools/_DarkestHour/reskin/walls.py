"""Ванильные стены -> стены RMC-14 (cmss13), план осады п. 24 (решение автора 2026-11-26: RMC metal / rwall).

Ванильный rsi копируется в Resources/Textures/_DarkestHour/<тот же путь>, и в копии состояния стены заменяются на
состояния донора RMC (формат стыковки тот же: full + base0..7 по 4 направления). Остальные состояния (каркас,
стадии разборки) остаются ванильными. Ржавые стены — тот же донор с ржавым тоном. Прототипы указывают на копии
(walls.yml, пометка DarkestHour).

Запуск из корня репозитория:  python3 Tools/_DarkestHour/reskin/walls.py"""
import json
import os
import shutil
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
TEX = os.path.join(ROOT, 'Resources/Textures')

METAL = '_RMC14/Structures/Walls/metal.rsi'
RWALL = '_RMC14/Structures/Walls/rwall.rsi'

# целевой ванильный rsi -> {состояние в цели: (донор, состояние донора)}
def wall_map():
    m = {'full': (METAL, 'full'), 'rgeneric': (RWALL, 'full')}
    for i in range(8):
        m[f'solid{i}'] = (METAL, f'metal{i}')
        m[f'reinf_over{i}'] = (RWALL, f'rwall{i}')
    return m


TARGETS = {
    'Structures/Walls/solid.rsi': (wall_map(), None),
    # ржавые: тот же металл, тёплый ржавый тон
    'Structures/Walls/solid-rust.rsi': (wall_map(), (122, 82, 52, 0.28)),
}


def tint(im, t):
    if t is None:
        return im
    r, g, b, k = t
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            p = px[x, y]
            if p[3]:
                px[x, y] = (int(p[0] + (r * p[0] / 160 - p[0]) * k), int(p[1] + (g * p[1] / 160 - p[1]) * k),
                            int(p[2] + (b * p[2] / 160 - p[2]) * k), p[3])
    return im


def meta(rsi):
    with open(os.path.join(TEX, rsi, 'meta.json'), encoding='utf-8-sig') as f:
        return json.load(f)


def build():
    for target, (mapping, tone) in TARGETS.items():
        src = os.path.join(TEX, target)
        dst = os.path.join(TEX, '_DarkestHour', target)
        if os.path.exists(dst):
            shutil.rmtree(dst)
        shutil.copytree(src, dst)
        m = meta(target)
        dirs = {s['name']: s.get('directions', 1) for s in m['states']}
        donors = set()
        for state, (donor, dstate) in mapping.items():
            if state not in dirs:
                continue
            ddirs = {s['name']: s.get('directions', 1) for s in meta(donor)['states']}
            assert ddirs.get(dstate) == dirs[state], (target, state, donor, dstate)
            im = Image.open(os.path.join(TEX, donor, dstate + '.png')).convert('RGBA')
            tint(im, tone).save(os.path.join(dst, state + '.png'))
            donors.add(meta(donor)['copyright'])
        m['copyright'] = (m.get('copyright', '') + ' | Wall states replaced for The Darkest Hour '
                          '(Tools/_DarkestHour/reskin/walls.py): ' + ' / '.join(sorted(donors)))
        m['license'] = 'CC-BY-SA-3.0'
        with open(os.path.join(dst, 'meta.json'), 'w', encoding='utf-8') as f:
            json.dump(m, f, indent=4, ensure_ascii=False)
            f.write('\n')


if __name__ == '__main__':
    build()
