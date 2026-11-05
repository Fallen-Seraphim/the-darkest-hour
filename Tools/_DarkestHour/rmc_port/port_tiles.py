"""Порт тайлов RMC-14 (Гибрисса-Проспера: полы, асфальт, бетон) для городских карт The Darkest Hour.

Убирает поля, которых у нас нет (canDig, weedsSpreadable), заменяет плитку RMC на ванильную (CMTileItemSteel →
FloorTileItemSteel, CMFloorPlating → Plating) и копирует текстуры.
Запуск: python3 Tools/_DarkestHour/rmc_port/port_tiles.py (RMC-клон в /home/claude/rmc).
"""
import os
import re
import shutil

RMC = '/home/claude/rmc/Resources'
DH = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'Resources')
FILES = ['base.yml', 'Planet/base.yml', 'asphalt.yml'] + ['Hybrisa/' + f for f in sorted(
    os.listdir(RMC + '/Prototypes/_RMC14/Tiles/Hybrisa'))]

DROP_FIELDS = ('canDig', 'weedsSpreadable')
SWAP = {'CMTileItemSteel': 'FloorTileItemSteel', 'CMFloorPlating': 'Plating'}

textures = set()
names = set()
for rel in FILES:
    text = open(RMC + '/Prototypes/_RMC14/Tiles/' + rel, encoding='utf-8-sig').read().replace('\r\n', '\n')
    lines = [l for l in text.split('\n') if not re.match(r'^  (%s):' % '|'.join(DROP_FIELDS), l)]
    text = '\n'.join(lines)
    for a, b in SWAP.items():
        text = re.sub(r'\b%s\b' % a, b, text)
    # Planet/base.yml: только база CMPlanetBase (остальные тайлы планеты не нужны)
    if rel == 'Planet/base.yml':
        blocks = re.split(r'(?m)^(?=- type:)', text)
        text = ''.join(b for b in blocks if 'id: CMPlanetBase' in b)
    textures.update(re.findall(r'/Textures/(_RMC14/Tiles/[^\s]+\.png)', text))
    names.update(re.findall(r'(?m)^  name: (tiles-\S+)', text))
    dst = DH + '/Prototypes/_RMC14/Tiles/' + rel
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    open(dst, 'w', encoding='utf-8').write(text.rstrip('\n') + '\n')

missing = []
for t in sorted(textures):
    a, b = RMC + '/Textures/' + t, DH + '/Textures/' + t
    if not os.path.exists(a):
        missing.append(t)
        continue
    os.makedirs(os.path.dirname(b), exist_ok=True)
    shutil.copy(a, b)
    # лицензия/авторство: meta рядом (у тайлов RMC — attributions.yml в папке)
    for extra in ('attributions.yml',):
        src = os.path.join(os.path.dirname(a), extra)
        if os.path.exists(src):
            shutil.copy(src, os.path.join(os.path.dirname(b), extra))
print('tiles files', len(FILES), 'textures', len(textures), 'missing', len(missing))
print('names', sorted(names))
open('/tmp/claude-0/-home-claude/b98f3588-2b1f-59b9-a305-05f4e1959a09/scratchpad/la/tiles_missing.txt', 'w').write(
    '\n'.join('/Resources/Textures/' + os.path.dirname(t) + '/' for t in sorted(set(missing))))
