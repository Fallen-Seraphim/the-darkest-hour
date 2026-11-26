"""LAVMG-14 (решение автора 2026-11-26): спрайт — корпоративный смарт-ган «M56T» (ml79a) RMC-14 из чекаута RussianCM
(cmss13, CC-BY-SA-4.0); слои окраски «-color» запекаются в фиолетовый LA-V.

Запуск из корня:  python3 Tools/_DarkestHour/lav/lavmg_sprite.py /path/to/russiancm"""
import io
import json
import os
import subprocess
import sys

from PIL import Image

SRC = 'Resources/Textures/_RMC14/Objects/Weapons/Guns/SmartGuns/ml79a.rsi'
OUT = 'Resources/Textures/_DarkestHour/Objects/Weapons/Guns/lavmg14.rsi'
LAV = (138, 106, 208)
PAIRS = {'base': 'camo', 'inhand-left': 'inhand-left-color', 'inhand-right': 'inhand-right-color',
         'wielded-inhand-left': 'wielded-inhand-left-color', 'wielded-inhand-right': 'wielded-inhand-right-color',
         'equipped-BACKPACK': 'equipped-BACK-color', 'equipped-SUITSTORAGE': 'equipped-SUITSTORAGE-color',
         'mag-0': None}


def git(rucm, path):
    return subprocess.run(['git', '-C', rucm, 'show', 'HEAD:' + path], capture_output=True, check=True).stdout


def tint(im):
    lum = im.convert('L')
    r, g, b = (lum.point(lambda v, c=c: v * c // 255) for c in LAV)
    return Image.merge('RGBA', [r, g, b, im.getchannel('A')])


def main():
    rucm = sys.argv[1]
    meta = json.loads(git(rucm, SRC + '/meta.json').decode('utf-8-sig'))
    states = {s['name']: s for s in meta['states']}
    os.makedirs(OUT, exist_ok=True)
    out_states = []
    for name, color in PAIRS.items():
        im = Image.open(io.BytesIO(git(rucm, f'{SRC}/{name}.png'))).convert('RGBA')
        if color:
            im.alpha_composite(tint(Image.open(io.BytesIO(git(rucm, f'{SRC}/{color}.png'))).convert('RGBA')))
        im.save(os.path.join(OUT, name + '.png'))
        out_states.append(states[name])
    # Затвор открыт (GunSystem.ChamberMagazine ставит слою Base «bolt-open», когда патронник пуст) и иконка —
    # у смарт-гана RMC их нет, берём тот же рисунок, что и base.
    for extra in ('bolt-open', 'icon'):
        Image.open(os.path.join(OUT, 'base.png')).save(os.path.join(OUT, extra + '.png'))
        out_states.append({'name': extra})
    meta['states'] = out_states
    meta['copyright'] += '; recoloured for the LA-V LAVMG-14 by The Darkest Hour (Tools/_DarkestHour/lav/lavmg_sprite.py)'
    with open(os.path.join(OUT, 'meta.json'), 'w') as f:
        json.dump(meta, f, indent=2)
        f.write('\n')


if __name__ == '__main__':
    main()
