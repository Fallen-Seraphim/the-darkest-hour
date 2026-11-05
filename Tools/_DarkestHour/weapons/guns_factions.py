"""Weapons of the Lost Angels uprising: the Independent Colonies (ONK) school that armed the Front, and the
Republic Military Police. Same kit and conventions as guns.py (48x32 / 64x32 canvas, muzzle right, y down).

ONK: simple, cheap and rugged — stamped warm steel, olive-drab polymer, rust-orange markings, few parts.
Rail variants carry copper coils and a cyan capacitor window. Military Police: blued steel, navy polymer, white trim.
All drawings are original."""
from pixart import Art
from guns import Gun, add, grip, guard, box_mag, drum, MET, DMET, FUR, ACC

NEW = []

BAR = ['suppressed', 'flashhider', 'compensator']
GRP = ['vgrip', 'agrip']
SLOTS = {}


def reg(g, slots):
    add(g)
    NEW.append(g.id)
    SLOTS[g.id] = slots
    return g


def onk_stock(a, x=2, y=10):
    """Fixed stamped-steel skeleton stock with an olive butt pad."""
    a.shade_poly([(x, y), (x + 10, y), (x + 13, y + 1), (x + 13, y + 5), (x + 9, y + 5), (x + 4, y + 8), (x, y + 8)], MET)
    a.erase(x + 4, y + 2, x + 9, y + 3).erase(x + 4, y + 4, x + 6, y + 5)
    a.vline(x, y, y + 8, 'b').vline(x + 1, y, y + 8, 'a')
    return a


def coils(a, x0, x1, y0, y1):
    """Copper rail coils over a dark barrel: every other column a ring."""
    for x in range(x0, x1 + 1, 2):
        a.vline(x, y0, y1, 'O')
        a.dot(x, y0, 'Y')
    return a


# ------------------------------------------------------------------ ONK
def fs2a():
    """Firestarter-2A — main assault rifle: skeleton stock, slab receiver with a rust stripe, ribbed olive handguard,
    integral slotted flash hider and a fixed bayonet under the muzzle, curved 30-round magazine. 64 px."""
    a = Art()
    onk_stock(a)
    a.bevel(15, 9, 31, 14, MET, left=True)
    a.rail(17, 30, 8)
    a.hline(16, 31, 13, 'v')                                     # rust stripe
    a.hline(21, 26, 10, '0').hline(21, 26, 11, '2')             # port
    a.dot(29, 11, '4').dot(30, 11, '4')                          # selector
    grip(a, 17, 15, h=6, w=4, slant=2)
    guard(a, 20, 24, 18, 15)
    a.bevel(32, 9, 44, 14, FUR)
    for x in range(33, 44, 2):
        a.vline(x, 10, 13, 'a')
    a.hline(32, 46, 8, '2')                                      # gas tube
    a.bevel(45, 11, 51, 12, MET)
    a.rect(46, 8, 47, 10, '2').dot(46, 7, '3')                   # front sight
    a.bevel(52, 10, 55, 13, DMET, right=True)                    # integral flash hider
    a.vline(53, 10, 10, '0').vline(53, 13, 13, '0').vline(54, 10, 10, '0').vline(54, 13, 13, '0')
    a.hline(47, 49, 13, '1')                                     # bayonet lug
    a.hline(48, 58, 14, '4').hline(49, 57, 15, '3').dot(59, 14, '5').dot(58, 15, '4')   # bayonet blade
    a.hline(45, 47, 14, 'a').hline(45, 47, 15, 'a')              # bayonet handle
    mag = box_mag(25, 15, 3, 8, curve=3)
    bolt = Art().hline(21, 26, 10, '0').hline(21, 26, 11, '0').dot(22, 11, 'Y').dot(23, 11, 'Y')
    return Gun('fs2a', 'onk', 'rifle', a, mag, bolt, muzzle=(56, 11), rail=(17, 30, 7), under=(38, 15), width=64)


def fs2s():
    """Firestarter-2S — short carbine: folded wire stock, the same receiver, short handguard and barrel."""
    a = Art()
    a.hline(5, 14, 10, '2').hline(5, 14, 15, '1').vline(5, 10, 15, '2')    # folding wire stock
    a.bevel(13, 9, 28, 14, MET, left=True)
    a.rail(15, 27, 8)
    a.hline(14, 28, 13, 'v')
    a.hline(18, 23, 10, '0').hline(18, 23, 11, '2')
    a.dot(26, 11, '4')
    grip(a, 14, 15, h=6, w=4, slant=2)
    guard(a, 17, 21, 18, 15)
    a.bevel(29, 9, 35, 14, FUR)
    a.pattern(30, 34, 11, 'a.')
    a.hline(29, 36, 8, '2')
    a.bevel(36, 11, 39, 12, MET)
    a.rect(36, 8, 37, 10, '2')
    a.bevel(40, 10, 41, 13, DMET, right=True)
    mag = box_mag(22, 15, 3, 7, curve=2)
    bolt = Art().hline(18, 23, 10, '0').hline(18, 23, 11, '0').dot(19, 11, 'Y')
    return Gun('fs2s', 'onk', 'rifle', a, mag, bolt, muzzle=(42, 11), rail=(15, 27, 7), under=(32, 15))


def fs2r():
    """Firestarter-2R — rail rifle: the 2A receiver, a capacitor block with a cyan window behind the magazine,
    a long coil barrel in an open steel cage, straight 15-round magazine of 5.58x63R slugs. 64 px."""
    a = Art()
    onk_stock(a)
    a.bevel(15, 9, 31, 14, MET, left=True)
    a.rail(17, 30, 8)
    a.hline(16, 31, 13, 'v')
    a.dot(29, 11, '4')
    a.bevel(19, 15, 24, 18, MET, left=True, right=True)          # capacitor block
    a.hline(20, 23, 16, 'B').hline(20, 23, 17, 'C')
    grip(a, 15, 15, h=6, w=4, slant=2)
    a.hline(32, 54, 9, '2').hline(32, 54, 14, '1')               # cage rails
    a.bevel(32, 11, 54, 12, DMET)                                # barrel
    coils(a, 33, 53, 10, 13)
    a.bevel(55, 10, 57, 13, MET, right=True)
    a.dot(56, 11, 'B').dot(56, 12, 'C')
    mag = box_mag(26, 15, 3, 6, curve=0)
    bolt = Art().hline(20, 23, 16, 'C').hline(20, 23, 17, 'C')
    return Gun('fs2r', 'onk', 'rifle', a, mag, bolt, muzzle=(58, 11), rail=(17, 30, 7), under=(40, 15), width=64)


def fs2t():
    """Firestarter-2T — light machine gun: heavy finned barrel with a carry handle, folded bipod under the front,
    a 50-round pan on top of the receiver. 64 px."""
    a = Art()
    onk_stock(a)
    a.bevel(15, 10, 33, 15, MET, left=True)
    a.hline(16, 33, 14, 'v')
    a.hline(22, 27, 12, '0')
    grip(a, 18, 16, h=6, w=4, slant=2)
    guard(a, 21, 25, 19, 16)
    a.bevel(34, 10, 42, 15, FUR)
    a.pattern(35, 41, 12, 'a.')
    a.bevel(43, 11, 55, 13, MET)                                 # heavy barrel
    for x in (44, 46, 48, 50):
        a.vline(x, 10, 14, '2')                                  # fins
    a.rect(38, 7, 46, 7, '2').vline(38, 8, 9, '2').vline(46, 8, 9, '2')   # carry handle
    a.hline(44, 54, 15, '1').hline(44, 54, 16, '2')              # folded bipod legs
    a.dot(54, 16, '3')
    a.bevel(56, 10, 58, 14, DMET, right=True)
    mag = Art()                                                  # 50-round pan on top
    mag.bevel(18, 6, 32, 8, DMET, left=True, right=True)
    mag.hline(19, 31, 5, '1').hline(21, 29, 4, '2')
    mag.dot(25, 5, '3').dot(25, 7, 'x')
    mag.vline(24, 9, 9, '0').vline(26, 9, 9, '0')
    bolt = Art().hline(22, 27, 12, '0').dot(23, 12, 'Y').dot(24, 12, 'Y')
    return Gun('fs2t', 'onk', 'lmg', a, mag, bolt, muzzle=(59, 12), rail=(34, 42, 9), under=(40, 16), width=64)


def fs2e():
    """Firestarter-2E Expedition — light expedition carbine: rust-coloured polymer furniture, shrouded short barrel,
    a short straight 17-round magazine, sling loops."""
    a = Art()
    a.shade_poly([(2, 10), (11, 10), (14, 11), (14, 15), (10, 15), (5, 18), (2, 18)], ACC)
    a.erase(5, 12, 9, 13)
    a.vline(2, 10, 18, 'u')
    a.bevel(14, 9, 29, 14, MET, left=True)
    a.rail(16, 28, 8)
    a.hline(19, 24, 10, '0').hline(19, 24, 11, '2')
    grip(a, 15, 15, h=6, w=4, slant=2, r=ACC)
    guard(a, 18, 22, 18, 15)
    a.bevel(30, 9, 39, 14, ACC)
    a.vents(31, 38, 11, 12, 'u', step=2)
    a.bevel(40, 10, 43, 13, MET, right=True)
    a.dot(4, 9, '2').dot(37, 15, '2')                            # sling loops
    mag = box_mag(23, 15, 3, 5, curve=0)
    bolt = Art().hline(19, 24, 10, '0').hline(19, 24, 11, '0').dot(20, 11, 'Y')
    return Gun('fs2e', 'onk', 'rifle', a, mag, bolt, muzzle=(44, 11), rail=(16, 28, 7), under=(34, 15))


def ds3a():
    """Destroyer-3A — marksman rifle in 10x44: adjustable cheek riser, long receiver with a full rail, long free-floated
    barrel in a slotted olive handguard, two-port muzzle brake, straight 15-round magazine. 64 px."""
    a = Art()
    a.shade_poly([(2, 9), (13, 9), (16, 11), (16, 15), (12, 16), (2, 18)], FUR)
    a.hline(4, 11, 8, '2').hline(4, 11, 7, '3')                  # cheek riser
    a.erase(6, 12, 10, 13)
    a.vline(2, 9, 18, 'a')
    a.bevel(16, 9, 32, 14, MET, left=True)
    a.rail(17, 40, 8)
    a.hline(17, 32, 13, 'v')
    a.hline(21, 26, 10, '0').hline(21, 26, 11, '2')
    grip(a, 18, 15, h=6, w=4, slant=2)
    guard(a, 21, 25, 18, 15)
    a.bevel(33, 9, 44, 14, FUR)
    a.pattern(34, 43, 11, 'a..')
    a.bevel(45, 11, 56, 12, MET)
    a.bevel(57, 10, 60, 13, DMET, right=True)
    a.vline(58, 10, 10, '0').vline(58, 13, 13, '0')
    mag = box_mag(27, 15, 3, 7, curve=0)
    bolt = Art().hline(21, 26, 10, '0').hline(21, 26, 11, '0').dot(22, 11, 'Y').dot(23, 11, 'Y')
    return Gun('ds3a', 'onk', 'sniper', a, mag, bolt, muzzle=(61, 11), rail=(17, 40, 7), under=(39, 15), width=64)


def ds3r():
    """Destroyer-3R — rail marksman rifle: the 3A stock and receiver, a twin capacitor block, long coil barrel in a
    closed steel shroud with cyan windows, straight 10-round magazine of 10x44R slugs. 64 px."""
    a = Art()
    a.shade_poly([(2, 9), (13, 9), (16, 11), (16, 15), (12, 16), (2, 18)], FUR)
    a.hline(4, 11, 8, '2').hline(4, 11, 7, '3')
    a.erase(6, 12, 10, 13)
    a.vline(2, 9, 18, 'a')
    a.bevel(16, 9, 32, 14, MET, left=True)
    a.rail(17, 40, 8)
    a.hline(17, 32, 13, 'v')
    grip(a, 18, 15, h=6, w=4, slant=2)
    a.bevel(23, 15, 27, 19, MET, left=True, right=True)          # capacitors
    a.vline(24, 16, 18, 'B').vline(26, 16, 18, 'C')
    a.bevel(33, 9, 57, 14, DMET)                                 # coil shroud
    coils(a, 34, 56, 9, 9)
    a.pattern(35, 56, 11, 'B..')
    a.pattern(35, 56, 12, 'C..')
    a.bevel(58, 10, 60, 13, MET, right=True)
    a.dot(59, 11, 'B')
    mag = box_mag(29, 15, 3, 5, curve=0)
    bolt = Art().vline(24, 16, 18, 'C').vline(26, 16, 18, 'C')
    return Gun('ds3r', 'onk', 'sniper', a, mag, bolt, muzzle=(61, 11), rail=(17, 40, 7), under=(45, 15), width=64)


def abz9():
    """Abuzforse-9 — personal weapon in 4.76 magnum: a boxy steel slide over an olive frame, long 20-round magazine
    that sticks out under the grip, rust-orange front sight."""
    a = Art()
    a.shade_poly([(14, 9), (33, 9), (34, 10), (34, 13), (14, 13)], MET)
    a.vline(16, 10, 12, '1').vline(18, 10, 12, '1')
    a.hline(24, 28, 10, '0').hline(24, 28, 11, '2')
    a.dot(15, 8, '2').dot(32, 8, 'x')
    a.vline(35, 11, 12, '0')
    a.hline(15, 33, 13, '1')
    a.bevel(17, 14, 33, 15, FUR)
    a.shade_poly([(15, 14), (21, 14), (19, 22), (14, 22), (13, 21)], FUR)
    a.pattern(14, 19, 17, 'a.').pattern(14, 18, 19, 'a.')
    guard(a, 21, 25, 18, 16, c='a')
    mag = Art().bevel(13, 22, 18, 26, DMET).dot(13, 22, '2').hline(13, 18, 26, '0')
    bolt = Art().erase(14, 9, 16, 13)
    bolt.bevel(11, 9, 13, 13, MET).hline(24, 28, 10, '0').dot(25, 11, 'Y')
    return Gun('abz9', 'onk', 'pistol', a, mag, bolt, muzzle=(36, 11), rail=(18, 30, 8), under=(28, 16))


# ------------------------------------------------------------------ Republic Military Police
def rmpr0919():
    """RMPR-09-19 — Military Police SMG in 9x19: telescoping wire stock, compact navy lower, blued upper with a
    white MP band, straight 30-round magazine forward of the grip."""
    a = Art()
    a.hline(4, 13, 10, '2').hline(4, 13, 14, '1').vline(4, 10, 14, '3')      # wire stock
    a.bevel(12, 9, 30, 13, MET, left=True)
    a.rail(14, 28, 8)
    a.hline(13, 30, 12, 'x')                                     # white band
    a.hline(18, 22, 10, '0')
    a.bevel(12, 14, 26, 15, FUR, left=True)
    grip(a, 13, 15, h=6, w=4, slant=2)
    guard(a, 16, 19, 18, 15)
    a.bevel(31, 10, 35, 12, FUR, right=True)
    a.bevel(36, 10, 38, 12, MET, right=True)
    a.dot(29, 10, 'R')
    mag = box_mag(21, 16, 2, 8, curve=0)
    bolt = Art().hline(18, 22, 10, 'Y')
    return Gun('rmpr0919', 'rmp', 'smg', a, mag, bolt, muzzle=(39, 11), rail=(14, 28, 7), under=(32, 13))


def rmpr6239():
    """RMPR-62-39 — Military Police rifle in 7.62x39: fixed navy stock with a white MP band, blued receiver, short
    navy handguard, gas tube, curved 30-round magazine, slant brake."""
    a = Art()
    a.shade_poly([(2, 10), (11, 10), (14, 11), (14, 15), (10, 15), (4, 18), (2, 18)], FUR)
    a.hline(4, 11, 13, 'x')
    a.vline(2, 10, 18, 'a')
    a.bevel(14, 9, 29, 14, MET, left=True)
    a.rail(16, 28, 8)
    a.hline(18, 23, 11, '0').hline(18, 23, 12, '2')
    a.dot(26, 11, 'R')
    grip(a, 16, 15, h=6, w=4, slant=2)
    guard(a, 19, 22, 18, 15)
    a.bevel(30, 9, 38, 14, FUR)
    a.pattern(31, 37, 11, 'a.')
    a.hline(30, 40, 8, '2')
    a.bevel(39, 11, 44, 12, MET)
    a.rect(40, 8, 41, 10, '2')
    a.bevel(45, 10, 46, 13, DMET, right=True)
    mag = box_mag(23, 15, 3, 8, curve=3)
    bolt = Art().hline(18, 23, 11, '0').hline(18, 23, 12, '0').dot(19, 12, 'Y').dot(20, 12, 'Y')
    return Gun('rmpr6239', 'rmp', 'rifle', a, mag, bolt, muzzle=(47, 11), rail=(16, 28, 7), under=(34, 15))


reg(fs2a(), GRP + ['optic2x', 'optic4x'])
reg(fs2s(), BAR + GRP + ['optic2x'])
reg(fs2r(), GRP + ['optic2x', 'optic4x', 'optic8x'])
reg(fs2t(), ['optic2x', 'optic4x'])
reg(fs2e(), BAR + ['optic2x', 'optic4x'])
reg(ds3a(), BAR + ['optic4x', 'optic8x'])
reg(ds3r(), ['optic4x', 'optic8x'])
reg(abz9(), [])
reg(rmpr0919(), BAR + ['optic2x'])
reg(rmpr6239(), BAR + GRP + ['optic2x', 'optic4x'])
