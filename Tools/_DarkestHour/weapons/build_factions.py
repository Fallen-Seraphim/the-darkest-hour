"""Build only the uprising weapons (guns_factions.py) into <out_dir>/Objects/Weapons/Guns.
Usage: python3 build_factions.py <out_dir> <repo>"""
import os, sys
import build
import guns_factions as gf

if __name__ == '__main__':
    out_dir, repo = sys.argv[1], sys.argv[2]
    guns_out = os.path.join(out_dir, 'Objects/Weapons/Guns')
    build.SLOTS.update(gf.SLOTS)
    body_s = build.body_mask(repo)
    body_n = build.body_mask(repo, 32, 0)
    states = {gid: build.build_gun(build.GUNS[gid], guns_out, body_s, body_n) for gid in gf.NEW}
    keep = dict(build.GUNS)
    build.GUNS.clear()
    build.GUNS.update({gid: keep[gid] for gid in gf.NEW})
    build.preview(states, os.path.join(out_dir, 'preview_factions.png'))
    print('built', len(gf.NEW))
