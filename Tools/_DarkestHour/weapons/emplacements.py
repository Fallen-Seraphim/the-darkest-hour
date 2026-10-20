"""Mounted machine guns for The Darkest Hour: RSPMG-10-54 (Republic, 7.62x51) and IHMG-17 (Empire, 14.5x114).

One RSI per gun, 32x32:
  folded                 the packed gun lying on the floor (item icon)
  deployed               gun on its tripod, 4 directions (south, north, east, west)
  ammo                   belt box overlay, 4 directions (shown while a belt is loaded)
  heat                   glowing barrel overlay, 4 directions (alpha follows the barrel heat)
  inhand-left/-right, equipped-BACKPACK   the packed gun carried or slung

Usage: python3 emplacements.py <out_dir> <repo>   -> <out_dir>/<id>.rsi"""
import os
import sys
from types import SimpleNamespace

sys.path.insert(0, os.path.dirname(__file__))
from pixart import Art, PAL, render  # noqa: E402
import build  # noqa: E402

S = (32, 32)


def line(a, x0, y0, x1, y1, c):
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err = dx + dy
    while True:
        a.dot(x0, y0, c)
        if x0 == x1 and y0 == y1:
            return a
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy


def mirror(a, w=32):
    return Art({(w - 1 - x, y): c for (x, y), c in a.px.items()})


def tripod_side(a, x, y, heavy=False):
    """Tripod seen from the side; (x, y) is the cradle under the receiver."""
    a.vline(x, y, y + 3, '1').dot(x, y, '2')
    line(a, x, y + 3, x + 8, 26, '1')           # front leg
    line(a, x, y + 3, x - 7, 26, '1')           # rear leg
    line(a, x, y + 3, x - 3, 27, '0')           # far leg
    for fx, fy in ((x + 8, 26), (x - 7, 26), (x - 3, 27)):
        a.dot(fx, fy, '2' if not heavy else '3')
    if heavy:
        line(a, x + 1, y + 3, x + 9, 26, '0')
        line(a, x - 1, y + 3, x - 8, 26, '0')
    return a


def tripod_front(a, x, y, rear_up=True, heavy=False):
    """Tripod seen along the barrel; (x, y) is the cradle (centre of two pixels x, x+1)."""
    line(a, x, y, x - 8, 25, '1')
    line(a, x + 1, y, x + 9, 25, '1')
    a.dot(x - 8, 25, '2').dot(x + 9, 25, '2')
    if heavy:
        line(a, x - 1, y, x - 9, 25, '0')
        line(a, x + 2, y, x + 10, 25, '0')
    if rear_up:
        a.vline(x, y - 6, y - 1, '0').vline(x + 1, y - 6, y - 1, '1')
    return a


# ================================================================== RSPMG-10-54 (Republic, M2/M240 look)
def rspmg():
    pal = 'rep'
    # ---- east (muzzle to the right)
    e = Art()
    e.vline(4, 11, 15, '1').dot(3, 11, '2').dot(3, 15, '2').dot(5, 13, '0')    # spade grips + trigger
    e.bevel(6, 10, 17, 15, '1234', left=True, right=True)                     # receiver
    e.hline(7, 16, 10, '4').hline(7, 16, 11, '3')                             # feed cover
    e.dot(8, 9, '2').dot(9, 9, '2')                                           # rear sight
    e.hline(7, 15, 14, '0')
    e.dot(15, 12, 'R')                                                        # status light
    e.bevel(18, 11, 25, 14, '0123', right=True)                               # perforated jacket
    e.pattern(19, 24, 12, '0.').pattern(19, 24, 13, '.0')
    e.hline(18, 21, 9, '2').vline(18, 9, 10, '2').vline(21, 9, 10, '2')       # carry handle
    e.hline(26, 28, 12, '3').hline(26, 28, 13, '2')                           # barrel
    e.dot(27, 11, '2')                                                        # front sight
    e.bevel(29, 11, 30, 14, '0123', right=True)                               # flash hider
    tripod_side(e, 12, 16)
    e_ammo = Art().bevel(8, 16, 13, 19, 'abcd', left=True, right=True)
    e_ammo.hline(9, 12, 17, 'b').dot(13, 15, 'Y').dot(12, 15, 'Y')           # belt into the feed
    e_heat = Art().rect(18, 11, 30, 14, 'O').rect(26, 12, 28, 13, 'R')
    # ---- north (muzzle up; we stand behind it)
    n = Art()
    n.bevel(14, 1, 17, 3, '0123', left=True, right=True)                      # flash hider
    n.vline(15, 4, 6, '3').vline(16, 4, 6, '2')                               # barrel
    n.bevel(14, 7, 17, 11, '0123', left=True, right=True)                     # jacket
    n.dot(15, 8, '0').dot(16, 10, '0')
    n.bevel(12, 12, 19, 17, '1234', left=True, right=True)                    # receiver from behind
    n.hline(13, 18, 12, '4').dot(15, 14, 'R')
    n.dot(12, 18, '1').dot(19, 18, '1').hline(13, 18, 19, '1')                # spade grips
    tripod_front(n, 15, 17, rear_up=False)
    n_ammo = Art().bevel(7, 12, 11, 16, 'abcd', left=True, right=True).dot(11, 13, 'Y')
    n_heat = Art().rect(14, 1, 17, 11, 'O').vline(15, 4, 6, 'R').vline(16, 4, 6, 'R')
    # ---- south (muzzle towards the viewer)
    s = Art()
    s.hline(13, 18, 6, '1').dot(12, 6, '2').dot(19, 6, '2')                   # spade grips behind
    s.bevel(12, 7, 19, 12, '1234', left=True, right=True)                     # receiver
    s.hline(13, 18, 7, '4').dot(16, 9, 'R')
    s.bevel(14, 13, 17, 18, '0123', left=True, right=True)                    # jacket
    s.dot(15, 14, '0').dot(16, 16, '0')
    s.vline(15, 19, 20, '3').vline(16, 19, 20, '2')
    s.bevel(14, 21, 17, 23, '0123', left=True, right=True)                    # flash hider (front)
    s.dot(15, 22, '0').dot(16, 22, '0')
    tripod_front(s, 15, 12, rear_up=True)
    s_ammo = Art().bevel(20, 8, 24, 12, 'abcd', left=True, right=True).dot(20, 9, 'Y')
    s_heat = Art().rect(14, 13, 17, 23, 'O').vline(15, 19, 20, 'R').vline(16, 19, 20, 'R')
    # ---- packed (item)
    f = Art()
    f.vline(4, 12, 15, '1').dot(3, 12, '2').dot(3, 15, '2')
    f.bevel(5, 11, 16, 15, '1234', left=True, right=True)
    f.hline(6, 15, 11, '4')
    f.bevel(17, 12, 24, 14, '0123', right=True)
    f.pattern(18, 23, 13, '0.')
    f.hline(25, 27, 13, '2').bevel(28, 12, 29, 14, '0123', right=True)
    f.hline(8, 25, 17, '1').hline(9, 24, 18, '0').dot(7, 17, '2').dot(26, 17, '2')   # folded legs
    f.vline(12, 16, 16, '1')
    return 'rspmg_10_54', pal, dict(S=(s, s_ammo, s_heat), N=(n, n_ammo, n_heat), E=(e, e_ammo, e_heat)), f


# ================================================================== IHMG-17 (Empire, DShK look, PDV colours)
def ihmg():
    pal = 'emp'
    # ---- east
    e = Art()
    e.vline(3, 11, 15, '1').dot(2, 11, '2').dot(2, 15, '2').dot(4, 13, '0')    # spade grips
    e.bevel(5, 10, 15, 15, '1234', left=True, right=True)                     # receiver
    e.bevel(6, 9, 14, 10, 'abcd')                                             # white feed cover
    e.hline(6, 14, 13, 'x')                                                   # yellow stripe
    e.dot(13, 12, 'G')                                                        # green emitter
    e.hline(16, 27, 12, '3').hline(16, 27, 13, '2')                           # barrel
    for x in range(16, 25, 2):                                                # cooling fins
        e.vline(x, 10, 15, '2').dot(x, 10, '3')
    e.dot(20, 7, '2').dot(19, 8, '2').dot(21, 8, '2').dot(20, 9, '2')        # ring AA sight
    e.bevel(27, 10, 30, 15, '0123', left=True, right=True)                    # muzzle brake
    e.vline(28, 11, 14, '0').vline(29, 11, 14, '1')
    e.dot(30, 12, '0').dot(30, 13, '0')
    tripod_side(e, 11, 16, heavy=True)
    e_ammo = Art().bevel(6, 16, 11, 21, '1234', left=True, right=True)
    e_ammo.hline(7, 10, 17, 'x').dot(11, 15, 'Y').dot(10, 15, 'Y')
    e_heat = Art().rect(16, 10, 30, 15, 'O').rect(16, 12, 27, 13, 'R')
    # ---- north
    n = Art()
    n.bevel(13, 0, 18, 2, '0123', left=True, right=True)                      # muzzle brake
    n.dot(15, 1, '0').dot(16, 1, '0')
    n.vline(15, 3, 9, '3').vline(16, 3, 9, '2')
    for y in range(4, 10, 2):                                                 # fins seen end-on
        n.hline(13, 18, y, '2')
    n.dot(15, 2, '2')
    n.bevel(12, 10, 19, 16, '1234', left=True, right=True)
    n.bevel(13, 10, 18, 11, 'abcd').hline(13, 18, 14, 'x').dot(15, 13, 'G')
    n.dot(12, 17, '1').dot(19, 17, '1').hline(13, 18, 18, '1')
    tripod_front(n, 15, 16, rear_up=False, heavy=True)
    n_ammo = Art().bevel(6, 11, 11, 16, '1234', left=True, right=True).hline(7, 10, 12, 'x')
    n_heat = Art().rect(13, 0, 18, 9, 'O').vline(15, 3, 9, 'R').vline(16, 3, 9, 'R')
    # ---- south
    s = Art()
    s.hline(13, 18, 5, '1').dot(12, 5, '2').dot(19, 5, '2')
    s.bevel(12, 6, 19, 11, '1234', left=True, right=True)
    s.bevel(13, 6, 18, 7, 'abcd').hline(13, 18, 10, 'x').dot(16, 9, 'G')
    s.vline(15, 12, 20, '3').vline(16, 12, 20, '2')
    for y in range(13, 20, 2):
        s.hline(13, 18, y, '2')
    s.bevel(13, 21, 18, 24, '0123', left=True, right=True)                    # muzzle brake (front)
    s.rect(15, 22, 16, 23, '0')
    tripod_front(s, 15, 11, rear_up=True, heavy=True)
    s_ammo = Art().bevel(20, 7, 25, 12, '1234', left=True, right=True).hline(21, 24, 8, 'x')
    s_heat = Art().rect(13, 12, 18, 24, 'O').vline(15, 12, 20, 'R').vline(16, 12, 20, 'R')
    # ---- packed
    f = Art()
    f.vline(3, 12, 15, '1').dot(2, 12, '2').dot(2, 15, '2')
    f.bevel(4, 11, 14, 15, '1234', left=True, right=True)
    f.bevel(5, 10, 13, 11, 'abcd').hline(5, 13, 13, 'x')
    f.hline(15, 26, 12, '3').hline(15, 26, 13, '2')
    for x in range(15, 24, 2):
        f.vline(x, 11, 14, '2')
    f.bevel(26, 11, 29, 14, '0123', left=True, right=True)
    f.hline(7, 25, 17, '1').hline(8, 24, 18, '0').hline(8, 24, 19, '1').dot(6, 17, '2').dot(26, 17, '2')
    f.vline(10, 16, 16, '1')
    return 'ihmg_17', pal, dict(S=(s, s_ammo, s_heat), N=(n, n_ammo, n_heat), E=(e, e_ammo, e_heat)), f


def dirs(views, k):
    s, n, e = views['S'][k], views['N'][k], views['E'][k]
    return [s, n, e, mirror(e)]


def build_one(out, gid, palname, views, packed, body_s, body_n):
    pal = PAL[palname]
    heat_pal = dict(pal, O=(255, 120, 40), R=(255, 200, 90))
    deployed = [render(a, pal, S) for a in dirs(views, 0)]
    ammo = [render(a, pal, S) for a in dirs(views, 1)]
    heat = [render(a, heat_pal, S, outline=False) for a in dirs(views, 2)]
    x0, y0, x1, y1 = packed.bounds()
    folded = render(packed.moved(0, 16 - (y0 + y1 + 1) // 2), pal, S)
    g = SimpleNamespace(faction=palname, length=x1 - x0 + 1)
    worn = [(st, build.worn(g, packed, build.TPL_RIFLE[st], build.REF_RIFLE, 0.75, 1.12, body_s, body_n))
            for st in ('inhand-left', 'inhand-right', 'equipped-BACKPACK')]
    build.save_rsi(os.path.join(out, gid + '.rsi'), S,
                   [('folded', [folded]), ('deployed', deployed), ('ammo', ammo), ('heat', heat)] + worn)


if __name__ == '__main__':
    out_dir, repo = sys.argv[1], sys.argv[2]
    bs, bn = build.body_mask(repo), build.body_mask(repo, 32, 0)
    for fn in (rspmg, ihmg):
        gid, palname, views, packed = fn()
        build_one(out_dir, gid, palname, views, packed, bs, bn)
    print('built 2')
