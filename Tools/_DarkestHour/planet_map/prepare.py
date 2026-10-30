#!/usr/bin/env python3
"""
DarkestHour: подготовка нарисованной вручную карты планеты (DHPlanetBase mapped: true) после savemap.

Что делает (повторный запуск безопасен):
  * переводит строки в LF;
  * снимает с карты сохранённый при маппинге биом (Biome) — на нарисованной карте его быть не должно;
  * ставит BecomesStation (id станции из gameMap, по умолчанию Terminus);
  * застилает грунтом (FloorAsteroidSand) все пустые тайлы в прямоугольнике карты — дыр в земле не остаётся;
  * обносит прямоугольник карты неразрушимым краем (DHWorldEdgeSand); старый край, оказавшийся внутри после
    расширения карты, убирает; скалы, попавшие на линию края, заменяет краем;
  * печатает отчёт: точки появления, маяки ЗП и их имена, размер, предупреждения.

Запуск:
  python3 prepare.py ПУТЬ_К_КАРТЕ.yml                 — подготовить карту на месте
  python3 prepare.py ПУТЬ.yml -o Resources/Maps/_DarkestHour/Assault/terminus.yml
  python3 prepare.py ПУТЬ.yml --check                  — только отчёт, без записи
"""
import argparse
import base64
import math
import random
import re
import struct
import sys

CHUNK = 16
TILE_BYTES = 7  # формат тайлов version 7: int32 тип, byte флаги, byte вариант, byte поворот/отражение
EDGE = 'DHWorldEdgeSand'
ROCK = re.compile(r'(WallRock|AsteroidRock|FloraRock)')


class MapFile:
    def __init__(self, text):
        self.lines = text.replace('\r\n', '\n').replace('\r', '\n').split('\n')
        self._parse_header()

    # ---------- структура файла ----------

    def _parse_header(self):
        L = self.lines
        self.map_ids = []
        self.grid_ids = []
        i = 0
        while i < len(L):
            if L[i] == 'maps:':
                i += 1
                while i < len(L) and L[i].startswith('- '):
                    self.map_ids.append(int(L[i][2:]))
                    i += 1
                continue
            if L[i] == 'grids:':
                i += 1
                while i < len(L) and L[i].startswith('- '):
                    self.grid_ids.append(int(L[i][2:]))
                    i += 1
                continue
            if L[i] == 'entities:':
                break
            i += 1
        if len(self.map_ids) != 1 or self.map_ids[0] not in self.grid_ids:
            sys.exit('Ожидается карта, у которой сама карта — грид (одна карта, она же в списке grids). '
                     'Сохраняйте всю планету одним гридом через savemap.')
        self.root = self.map_ids[0]

    def tilemap(self):
        L = self.lines
        i = L.index('tilemap:') + 1
        out = {}
        while L[i].startswith('  '):
            k, v = L[i].strip().split(': ', 1)
            out[int(k)] = v
            i += 1
        return out

    def set_tilemap(self, tm):
        L = self.lines
        i = L.index('tilemap:') + 1
        j = i
        while L[j].startswith('  '):
            j += 1
        L[i:j] = [f'  {k}: {v}' for k, v in tm.items()]

    def root_component_range(self, ctype):
        """Строки [начало, конец) компонента корневой сущности."""
        L = self.lines
        start = self._root_start()
        i = start
        while i < len(L) and not (L[i].startswith('- proto:') and i > start) and not (L[i].startswith('  - uid:') and i > start):
            if L[i] == f'    - type: {ctype}':
                j = i + 1
                while j < len(L) and L[j].startswith('      '):
                    j += 1
                return i, j
            i += 1
        return None

    def _root_start(self):
        for i, l in enumerate(self.lines):
            if l == f'  - uid: {self.root}':
                return i
        sys.exit(f'Не найдена корневая сущность uid {self.root}')

    def root_components_end(self):
        L = self.lines
        i = self._root_start() + 1
        while i < len(L) and not L[i].startswith('- proto:') and not L[i].startswith('  - uid:'):
            i += 1
        return i

    # ---------- тайлы ----------

    def chunks(self):
        r = self.root_component_range('MapGrid')
        L = self.lines
        out = {}
        for i in range(r[0], r[1]):
            m = re.match(r'^          ind: (-?\d+),(-?\d+)$', L[i])
            if m:
                data = base64.b64decode(L[i + 1].strip()[len('tiles: '):])
                ver = L[i + 2].strip()
                if ver != 'version: 7' or len(data) != CHUNK * CHUNK * TILE_BYTES:
                    sys.exit(f'Неожиданный формат чанка ({ver}, {len(data)} байт)')
                out[(int(m.group(1)), int(m.group(2)))] = bytearray(data)
        return out

    def set_chunks(self, chunks):
        r = self.root_component_range('MapGrid')
        L = self.lines
        i = r[0] + 1
        assert L[i] == '      chunks:', L[i]
        j = i + 1
        while j < r[1] and L[j].startswith('        '):
            j += 1
        new = []
        for (cx, cy) in sorted(chunks):
            new += [f'        {cx},{cy}:', f'          ind: {cx},{cy}',
                    '          tiles: ' + base64.b64encode(bytes(chunks[(cx, cy)])).decode(),
                    '          version: 7']
        L[i + 1:j] = new

    # ---------- сущности ----------

    def entities(self):
        """[(proto, uid, parent, pos, name, line_start, line_end)] для всех сущностей кроме корня."""
        L = self.lines
        out = []
        proto = None
        cur = None
        for i, l in enumerate(L):
            if l.startswith('- proto: '):
                proto = l[len('- proto: '):]
                if cur:
                    cur['end'] = i
                    cur = None
                continue
            if l.startswith('  - uid: '):
                if cur:
                    cur['end'] = i
                cur = {'proto': proto, 'uid': int(l[len('  - uid: '):]), 'parent': None, 'pos': None,
                       'name': None, 'start': i, 'end': None}
                out.append(cur)
                continue
            if cur:
                m = re.match(r'^      (parent|pos|name): (.*)$', l)
                if m and cur[m.group(1)] is None:
                    cur[m.group(1)] = m.group(2)
        if cur:
            cur['end'] = len(L)
            while cur['end'] > cur['start'] and L[cur['end'] - 1] == '':
                cur['end'] -= 1
        return [e for e in out if e['uid'] != self.root]

    def remove_entities(self, uids):
        if not uids:
            return
        uids = set(uids)
        L = self.lines
        keep = []
        drop = set()
        for e in self.entities():
            if e['uid'] in uids:
                drop.update(range(e['start'], e['end']))
        self.lines = [l for i, l in enumerate(L) if i not in drop]
        # пустые группы прототипов
        L = self.lines
        out = []
        i = 0
        while i < len(L):
            if L[i].startswith('- proto: ') and i + 2 <= len(L) and L[i + 1] == '  entities:' and \
                    (i + 2 >= len(L) or not L[i + 2].startswith('  - uid:')):
                i += 2
                continue
            out.append(L[i])
            i += 1
        self.lines = out

    def max_uid(self):
        return max([self.root] + [e['uid'] for e in self.entities()])

    def add_entities(self, proto, items):
        """items: [(x, y, name|None)] в координатах карты (центр тайла: +0.5)."""
        if not items:
            return []
        uid = self.max_uid()
        block = []
        uids = []
        for x, y, name in items:
            uid += 1
            uids.append(uid)
            block.append(f'  - uid: {uid}')
            block.append('    components:')
            if name:
                block += ['    - type: MetaData', f'      name: {name}']
            block += ['    - type: Transform', f'      parent: {self.root}', f'      pos: {fmt(x)},{fmt(y)}']
        L = self.lines
        # в существующую группу
        for i, l in enumerate(L):
            if l == f'- proto: {proto}':
                j = i + 2
                while j < len(L) and not L[j].startswith('- proto:') and L[j] != '':
                    j += 1
                L[j:j] = block
                return uids
        # новая группа — по алфавиту
        groups = [(i, l[len('- proto: '):]) for i, l in enumerate(L) if l.startswith('- proto: ')]
        at = None
        for i, name in groups:
            if name and name > proto:
                at = i
                break
        if at is None:
            at = len(L)
            while at > 0 and L[at - 1] == '':
                at -= 1
        L[at:at] = [f'- proto: {proto}', '  entities:'] + block
        return uids

    def update_entity_count(self):
        n = len(self.entities()) + 1
        for i, l in enumerate(self.lines):
            if l.startswith('  entityCount: '):
                self.lines[i] = f'  entityCount: {n}'
                break

    def text(self):
        t = '\n'.join(self.lines)
        return t if t.endswith('\n') else t + '\n'


def fmt(v):
    s = repr(float(v))
    return s[:-2] if s.endswith('.0') else s


def tile_at(chunks, x, y):
    c = chunks.get((x // CHUNK, y // CHUNK))
    if c is None:
        return None
    k = ((y % CHUNK) * CHUNK + (x % CHUNK)) * TILE_BYTES
    return struct.unpack_from('<i', c, k)[0]


def pos_of(e):
    if not e['pos'] or e['parent'] is None:
        return None
    x, y = (float(v) for v in e['pos'].split(','))
    return x, y


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('map')
    ap.add_argument('-o', '--output')
    ap.add_argument('--check', action='store_true', help='только отчёт')
    ap.add_argument('--station', default='Terminus', help='id BecomesStation (ключ stations в gameMap)')
    ap.add_argument('--ground', default='FloorAsteroidSand', help='тайл грунта для дыр')
    ap.add_argument('--no-edge', action='store_true', help='не ставить край карты')
    args = ap.parse_args()

    mf = MapFile(open(args.map, encoding='utf-8-sig').read())
    warnings = []

    # биом
    r = mf.root_component_range('Biome')
    if r:
        print(f'Биом на карте: {"будет снят" if not args.check else "есть — снять!"}')
        if not args.check:
            del mf.lines[r[0]:r[1]]

    # станция
    if not mf.root_component_range('BecomesStation'):
        print(f'BecomesStation: {"ставлю id " + args.station if not args.check else "НЕТ — карта не станет станцией!"}')
        if not args.check:
            end = mf.root_components_end()
            mf.lines[end:end] = ['    - type: BecomesStation', f'      id: {args.station}']

    # тайлы
    tm = mf.tilemap()
    empty_ids = {k for k, v in tm.items() if v == 'Space'}
    if not empty_ids:
        empty_ids = {max(tm) + 1}
        tm[next(iter(empty_ids))] = 'Space'
    ground_id = next((k for k, v in tm.items() if v == args.ground), None)
    if ground_id is None:
        ground_id = max(tm) + 1
        tm[ground_id] = args.ground
    if not args.check:
        mf.set_tilemap(tm)

    chunks = mf.chunks()
    used = [c for c, data in chunks.items()
            if any(struct.unpack_from('<i', data, k * TILE_BYTES)[0] not in empty_ids for k in range(CHUNK * CHUNK))]
    if not used:
        sys.exit('На карте нет тайлов')
    cx0 = min(c[0] for c in used); cx1 = max(c[0] for c in used)
    cy0 = min(c[1] for c in used); cy1 = max(c[1] for c in used)
    x0, x1, y0, y1 = cx0 * CHUNK, (cx1 + 1) * CHUNK - 1, cy0 * CHUNK, (cy1 + 1) * CHUNK - 1
    print(f'Прямоугольник карты: x {x0}..{x1}, y {y0}..{y1} ({x1 - x0 + 1}×{y1 - y0 + 1} тайлов)')
    if (x1 - x0 + 1) * (y1 - y0 + 1) > 400 * 400:
        warnings.append('карта больше 400×400 — проверьте нагрузку сервера')

    holes = 0
    for cx in range(cx0, cx1 + 1):
        for cy in range(cy0, cy1 + 1):
            data = chunks.get((cx, cy))
            if data is None:
                data = bytearray(CHUNK * CHUNK * TILE_BYTES)
                for k in range(CHUNK * CHUNK):
                    struct.pack_into('<i', data, k * TILE_BYTES, next(iter(empty_ids)))
                chunks[(cx, cy)] = data
            for k in range(CHUNK * CHUNK):
                if struct.unpack_from('<i', data, k * TILE_BYTES)[0] in empty_ids:
                    holes += 1
                    x = cx * CHUNK + k % CHUNK
                    y = cy * CHUNK + k // CHUNK
                    variant = random.Random(x * 73856093 ^ y * 19349663).randrange(7)
                    struct.pack_into('<iBBB', data, k * TILE_BYTES, ground_id, 0, variant, 0)
    for c in list(chunks):
        if not (cx0 <= c[0] <= cx1 and cy0 <= c[1] <= cy1):
            warnings.append(f'чанк {c} вне прямоугольника — пустой, удалён')
            del chunks[c]
    print(f'Пустых тайлов (дыр) застелено грунтом: {holes}' if not args.check else f'Пустых тайлов (дыр): {holes}')
    if not args.check:
        mf.set_chunks(chunks)

    # край карты
    if not args.no_edge:
        def border(x, y):
            return x in (x0, x1) or y in (y0, y1)

        drop = []
        have = set()
        for e in mf.entities():
            p = pos_of(e)
            if p is None or e['parent'] != str(mf.root):
                continue
            t = (math.floor(p[0]), math.floor(p[1]))
            if e['proto'] == EDGE:
                if border(*t) and t not in have:
                    have.add(t)
                else:
                    drop.append(e['uid'])
            elif border(*t):
                if ROCK.search(e['proto'] or ''):
                    drop.append(e['uid'])
                else:
                    warnings.append(f'{e["proto"]} (uid {e["uid"]}) стоит на краю карты {t}')
        need = [(x, y) for x in range(x0, x1 + 1) for y in range(y0, y1 + 1)
                if border(x, y) and (x, y) not in have]
        print(f'Край карты: убрать {len(drop)}, поставить {len(need)}')
        if not args.check:
            mf.remove_entities(drop)
            mf.add_entities(EDGE, [(x + 0.5, y + 0.5, None) for x, y in need])

    # отчёт
    ents = mf.entities()
    count = lambda p: sum(1 for e in ents if e['proto'] == p)
    anyjob, late = count('SpawnPointAnyJob'), count('SpawnPointLatejoin')
    print(f'Точки появления: SpawnPointAnyJob {anyjob}, SpawnPointLatejoin {late}')
    if anyjob == 0:
        warnings.append('нет SpawnPointAnyJob — гарнизону негде появиться в начале раунда')
    if late == 0:
        warnings.append('нет SpawnPointLatejoin — опоздавшим негде появиться')
    for e in ents:
        if e['proto'] and e['proto'].startswith('SpawnPoint') and e['proto'] not in ('SpawnPointAnyJob', 'SpawnPointLatejoin'):
            warnings.append(f'лишняя точка появления {e["proto"]} {e["pos"]}')
    pads = [e for e in ents if e['proto'] == 'DHLandingZonePad']
    print(f'Маяки ЗП: {len(pads)}')
    for e in pads:
        print(f'  {e["pos"]}: {e["name"] or "(без имени — получит «ЗП-n»)"}')
    if not pads:
        warnings.append('нет маяков ЗП (DHLandingZonePad) — челнокам некуда садиться')
    for e in ents:
        p = pos_of(e)
        if p is None or e['parent'] != str(mf.root):
            continue
        tx, ty = math.floor(p[0]), math.floor(p[1])
        if not (x0 <= tx <= x1 and y0 <= ty <= y1):
            warnings.append(f'{e["proto"]} (uid {e["uid"]}) за пределами карты {p}')

    for w in warnings:
        print('ВНИМАНИЕ:', w)

    if not args.check:
        mf.update_entity_count()
        out = args.output or args.map
        with open(out, 'w', encoding='utf-8', newline='\n') as f:
            f.write(mf.text())
        print('Записано:', out)


if __name__ == '__main__':
    main()
