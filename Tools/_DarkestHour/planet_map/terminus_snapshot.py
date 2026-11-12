#!/usr/bin/env python3
"""
DarkestHour: снимок нарисованной вручную базы «Терминуса» для генератора новой карты (terminus_build.py).

Берёт старую карту поверхности и сохраняет в исходных координатах всё, что внутри прямоугольника базы:
тайлы постройки (не голую землю) и сущности (кроме скал, флоры, воды и края карты). Генератор переносит снимок
на новое место со смещением BASE_SHIFT. Запускать только на карте, где база стоит на старом месте:
  python3 Tools/_DarkestHour/planet_map/terminus_snapshot.py OLD_terminus.yml \
      -o Tools/_DarkestHour/planet_map/terminus_base.snapshot
"""
import argparse
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from prepare import MapFile, pos_of, tile_at  # noqa: E402

# Забор базы x −16.5…17.5, y −25.5…8.5 и колючка на y 10; техника стоит западнее забора.
REGION = (-28, -34, 20, 11)        # x0, y0, x1, y1 включительно (клетки)
FENCE = (-17, -26, 18, 9)          # внутри — все тайлы; снаружи — только постройки
GROUND = {'FloorAsteroidSand', 'FloorAsteroidSandBorderless', 'FloorDesert', 'FloorLowDesert', 'FloorDirt',
          'FloorPlanetDirt', 'FloorPlanetGrass', 'FloorAsteroidSandRed', 'Space'}
SKIP_PREFIX = ('WallRock', 'AsteroidRock', 'FloraRock', 'RMCBoulder', 'RMCCact', 'RMCGrassDesert', 'RMCTreeJoshua',
               'DHWater', 'DHWorldEdge', 'DHLightPostGrid', 'DHPowerLineWire', 'DHPowerPylon')
# Переезжают в новые постройки (РЛС и связь гарнизона).
SKIP = {'DHRadarRepublic', 'DHCommsRelayRepublic'}


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('map')
    ap.add_argument('-o', '--output', required=True)
    args = ap.parse_args()

    mf = MapFile(open(args.map, encoding='utf-8-sig').read())
    tm = mf.tilemap()
    ch = mf.chunks()
    x0, y0, x1, y1 = REGION
    fx0, fy0, fx1, fy1 = FENCE
    tiles = {}
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            t = tile_at(ch, x, y)
            name = tm.get(t)
            inside = fx0 <= x <= fx1 and fy0 <= y <= fy1
            if name and (inside and name != 'Space' or name not in GROUND):
                tiles[f'{x},{y}'] = name

    ents = []
    root = str(mf.root)
    for e in mf.entities():
        p = pos_of(e)
        if p is None or e['parent'] != root or not e['proto']:
            continue
        if e['proto'].startswith(SKIP_PREFIX) or e['proto'] in SKIP:
            continue
        cx, cy = math.floor(p[0]), math.floor(p[1])
        if not (x0 <= cx <= x1 and y0 <= cy <= y1):
            continue
        inside = fx0 - 1 <= cx <= fx1 + 1 and fy0 - 1 <= cy <= fy1 + 2
        if not inside and e['proto'].startswith('Cable'):
            continue          # кабели к старой орудийной галерее
        block = mf.lines[e['start'] + 2:e['end']]   # без «- uid»/«components:»
        ents.append({'proto': e['proto'], 'lines': block})
    json.dump({'region': REGION, 'tiles': tiles, 'entities': ents}, open(args.output, 'w', encoding='utf-8'),
              ensure_ascii=False, indent=0)
    print(f'Снимок базы: тайлов {len(tiles)}, сущностей {len(ents)} → {args.output}')


if __name__ == '__main__':
    main()
