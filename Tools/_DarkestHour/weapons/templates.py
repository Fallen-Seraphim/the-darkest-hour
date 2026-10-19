"""Derive in-hand / equipped placement templates from a vanilla rifle (AK) RSI.
For each state and direction: centre, muzzle direction angle (degrees, screen space, 0 = +x, CCW positive with y up),
length of the gun silhouette, plus the strap pixels (for back/suit storage)."""
import json, math, sys
from PIL import Image
import numpy as np

STATES = ['inhand-left', 'inhand-right', 'wielded-inhand-left', 'wielded-inhand-right', 'equipped-BACKPACK', 'equipped-SUITSTORAGE', 'equipped-BELT']

def frames(path):
    im = Image.open(path).convert('RGBA')
    out = []
    for d in range(4):
        fx, fy = (d % 2) * 32, (d // 2) * 32
        out.append(im.crop((fx, fy, fx + 32, fy + 32)))
    return out

def is_strap(p):
    r, g, b, a = p
    return a > 0 and r > 90 and b / r > 0.2 and 0.42 < g / r < 0.62

def derive(rsi):
    res = {}
    for s in STATES:
        import os
        if not os.path.exists(f'{rsi}/{s}.png'):
            continue
        res[s] = []
        for d, f in enumerate(frames(f'{rsi}/{s}.png')):
            px = f.load()
            pts = []; strap = []
            for y in range(32):
                for x in range(32):
                    p = px[x, y]
                    if p[3] == 0: continue
                    if s.startswith('equipped') and is_strap(p):
                        strap.append((x, y, p)); continue
                    pts.append((x, y))
            if len(pts) < 3:
                res[s].append(None); continue
            a = np.array(pts, float)
            c = a.mean(0)
            cov = np.cov((a - c).T)
            w, v = np.linalg.eigh(cov)
            axis = v[:, -1]
            proj = (a - c) @ axis
            lo, hi = proj.min(), proj.max()
            length = hi - lo + 1
            # thickness near each end
            perp = (a - c) @ np.array([-axis[1], axis[0]])
            def thick(mask):
                sel = perp[mask]
                return (sel.max() - sel.min() + 1) if len(sel) else 0
            end = length * 0.3
            t_lo = thick(proj < lo + end); t_hi = thick(proj > hi - end)
            # muzzle = thinner end
            muzzle_dir = axis if t_hi < t_lo else -axis
            # screen y is down; angle with y up
            ang = math.degrees(math.atan2(-muzzle_dir[1], muzzle_dir[0]))
            mid = (lo + hi) / 2
            centre = c + axis * mid
            res[s].append({'cx': float(centre[0]), 'cy': float(centre[1]), 'angle': ang, 'length': float(length),
                           'thin': float(min(t_lo, t_hi)), 'thick': float(max(t_lo, t_hi)),
                           'strap': [(x, y, list(p)) for x, y, p in strap]})
    return res

if __name__ == '__main__':
    r = derive(sys.argv[1])
    json.dump(r, open(sys.argv[2], 'w'))
    for s, lst in r.items():
        print(s, [None if t is None else (round(t['cx'],1), round(t['cy'],1), round(t['angle']), round(t['length']), len(t['strap'])) for t in lst])
