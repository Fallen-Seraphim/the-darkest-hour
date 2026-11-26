"""Imperial weapons v3 (plan item 24, 2026-11-26): rebuild the six imperial guns (guns.py) and the IHMG-17
(emplacements.py) in the imperial palette, without touching the attachment overlay states that attachments_v2.py
added to the gun RSIs. Only the gun states (icon, base, bolt-open, mag-0) and the worn states are replaced.

Run from the repository root:  python3 Tools/_DarkestHour/weapons/imperial_v3.py"""
import json
import os
import shutil
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import build  # noqa: E402
import emplacements  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
GUNS = os.path.join(ROOT, 'Resources/Textures/_DarkestHour/Objects/Weapons/Guns')
IDS = ['ir17', 'img17l', 'img17t', 'imp17', 'imsh17', 'impsh17p']
GUN_STATES = {'icon', 'base', 'bolt-open', 'mag-0'}


def splice(src_rsi, dst_rsi, only=None):
    """Copy state PNGs from src into dst; the dst meta keeps its states (all of them must already exist)."""
    meta = json.load(open(os.path.join(dst_rsi, 'meta.json')))
    have = {s['name'] for s in meta['states']}
    for st in json.load(open(os.path.join(src_rsi, 'meta.json')))['states']:
        name = st['name']
        if only is not None and name not in only:
            continue
        assert name in have, (dst_rsi, name)
        shutil.copy(os.path.join(src_rsi, name + '.png'), os.path.join(dst_rsi, name + '.png'))


def main():
    tmp = tempfile.mkdtemp()
    body_s, body_n = build.body_mask(ROOT), build.body_mask(ROOT, 32, 0)
    for gid in IDS:
        build.build_gun(build.GUNS[gid], tmp, body_s, body_n)
        splice(os.path.join(tmp, gid + '.rsi'), os.path.join(GUNS, gid + '.rsi'), GUN_STATES)
        splice(os.path.join(tmp, gid + '_inhands.rsi'), os.path.join(GUNS, gid + '_inhands.rsi'))
    gid, pal, views, packed = emplacements.ihmg()
    emplacements.build_one(tmp, gid, pal, views, packed, body_s, body_n)
    splice(os.path.join(tmp, gid + '.rsi'), os.path.join(GUNS, 'Emplacements', gid + '.rsi'))
    shutil.rmtree(tmp)
    print('imperial v3:', ', '.join(IDS + [gid]))


if __name__ == '__main__':
    main()
