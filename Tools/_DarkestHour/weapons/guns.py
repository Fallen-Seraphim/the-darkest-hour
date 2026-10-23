"""Weapon designs v2 (pixel grids, 48x32 icon canvas, muzzle right, y down).

Republic: colonial-marine look (CM-SS13 / RMC-14): chunky gunmetal housings, shrouded barrels, under-barrel tubes,
red ammo displays, coyote-tan furniture.
Empire: PDV look (Monolith): black angular steel, white polymer, yellow markings, green emitters. No wood.
Station security (Metallistick): gunmetal, navy polymer, white trim.
All drawings are original; references were only looked at."""
from pixart import Art

GUNS = {}

MET = '1234'   # metal ramp
DMET = '0123'  # darker metal
FUR = 'abcd'   # main furniture
ACC = 'uvxy'   # accent furniture


class Gun:
    def __init__(self, gid, faction, kind, base, mag=None, bolt_open=None, muzzle=None, rail=None, under=None,
                 stock_at=None, width=48):
        self.id = gid
        self.faction = faction
        self.kind = kind
        self.base = base                 # Art, bolt closed, without magazine
        self.mag = mag or Art()          # Art, the magazine as seen in the gun (mag-0 state)
        self.bolt_open = bolt_open or Art()  # Art overrides for the open bolt (' ' erases)
        self.muzzle = muzzle             # (x, y) first pixel past the muzzle, y = top row of a 2px bore
        self.rail = rail                 # (x0, x1, y) top of the optic rail (optics sit at y-1 and above)
        self.under = under               # (x, y) top of an under-barrel grip
        self.stock_at = stock_at         # (x, y) where an add-on stock attaches (IMP-17)
        self.width = width               # icon canvas width (48, or 64 for long guns)
        full = base.copy().stamp(self.mag)
        x0, y0, x1, y1 = full.bounds()
        self.length = x1 - x0 + 1

    def full(self, open_bolt=False):
        a = self.base.copy()
        if open_bolt:
            a.stamp(self.bolt_open)
        return a.stamp(self.mag)


def add(g):
    GUNS[g.id] = g
    return g


# ------------------------------------------------------------------ shared parts
def grip(a, x, y, h=6, w=4, slant=2, r=FUR):
    """Pistol grip hanging from (x, y) (top-left), raked back by `slant` px."""
    a.shade_poly([(x, y), (x + w, y), (x + w - slant, y + h), (x - slant, y + h)], r)
    a.vline(x + w - 1, y + 1, y + h - 2, r[1])
    return a


def guard(a, x0, x1, y, top, c='1', trig='3'):
    """Trigger guard: bottom bar at y from x0..x1, front post at x1 from top..y, trigger at x0+1."""
    a.hline(x0, x1, y, c)
    a.vline(x1, top, y, c)
    a.dot(x0 + 1, top + 1, trig).dot(x0 + 2, top, trig)
    return a


def box_mag(x, y, w, h, curve=0, r=DMET, ribs=True):
    """Magazine hanging from (x, y) (w+1 px wide, h+1 tall); `curve` shifts the bottom forward."""
    m = Art()
    m.shade_poly([(x, y), (x + w, y), (x + w + curve, y + h), (x + curve, y + h)], r)
    for k in range(1, h):
        off = (curve * k) // h
        m.dot(x + off, y + k, r[3])                       # lit back edge
        if ribs and k % 2 == 0 and k < h - 1:
            m.hline(x + 1 + off, x + w - 1 + off, y + k, r[1])
    m.hline(x + curve, x + w + curve, y + h, r[0])       # base plate
    m.dot(x + w + curve, y + h - 1, r[0])
    return m


def drum(x, y, rad, r=DMET):
    m = Art()
    for yy in range(-rad, rad + 1):
        for xx in range(-rad, rad + 1):
            d = (xx * xx + yy * yy) ** 0.5
            if d <= rad + 0.3:
                c = r[2]
                if yy < -rad // 2 and d > rad - 1.2:
                    c = r[3]
                elif yy > 0 and d > rad - 1.2:
                    c = r[0]
                m.dot(x + xx, y + yy, c)
    m.dot(x, y, r[0]).dot(x - 1, y, r[1]).dot(x + 1, y, r[1]).dot(x, y - 1, r[1]).dot(x, y + 1, r[1])
    return m


def ammo_box(x, y, w, h, r=DMET, strap='v'):
    m = Art()
    m.bevel(x, y, x + w, y + h, r, left=True, right=True)
    m.hline(x + 1, x + w - 1, y + 2, r[0])
    m.vline(x + w // 2, y + 3, y + h - 1, strap)
    return m


# ================================================================== Republic
def rar_m11_1():
    """RAR-M-11-1 - 5.56x45 assault rifle. Pulse-rifle style: one-piece receiver, perforated shroud,
    under-barrel tube with tan pump, red ammo display."""
    a = Art()
    # stock
    a.shade_poly([(2, 10), (10, 9), (13, 10), (13, 15), (9, 15), (5, 18), (2, 18)], FUR)
    a.erase(6, 12, 9, 13).erase(6, 14, 7, 14)
    a.vline(2, 10, 18, '1').vline(3, 11, 17, 'a')
    # receiver
    a.bevel(12, 8, 28, 14, MET, left=True)
    a.rail(15, 26, 7)
    a.hline(13, 27, 13, '1')
    a.rect(13, 10, 16, 11, '0').dot(14, 10, 'R').dot(15, 10, 'R').dot(14, 11, 'r')
    a.hline(19, 23, 10, '0').hline(19, 23, 11, '2')          # ejection port
    a.dot(25, 10, '0').dot(26, 10, '0')
    # shroud
    a.bevel(28, 9, 38, 14, MET)
    a.erase(38, 9, 38, 9)
    a.pattern(29, 37, 11, '0.')
    a.pattern(30, 36, 12, '1.')
    a.hline(28, 38, 14, '0')
    # barrel
    a.bevel(39, 11, 40, 12, MET)
    a.bevel(41, 10, 42, 13, DMET, right=True)
    # under-barrel tube + pump
    a.bevel(22, 15, 37, 16, MET)
    a.vline(37, 15, 16, '0')
    a.bevel(28, 15, 34, 17, FUR, left=True, right=True)
    a.pattern(29, 33, 17, 'b.')
    # front sight
    a.dot(36, 8, '2').dot(36, 7, 'R')
    # grip + guard
    grip(a, 13, 15, h=6, w=4, slant=2)
    guard(a, 16, 20, 18, 15)
    mag = box_mag(21, 15, 3, 8, curve=2)
    bolt = Art().hline(19, 23, 10, '0').hline(19, 23, 11, '0').dot(20, 11, 'Y').dot(21, 11, 'Y')
    bolt.hline(24, 26, 7, '3').hline(24, 26, 8, '2')
    return Gun('rar_m11_1', 'rep', 'rifle', a, mag, bolt, muzzle=(43, 11), rail=(15, 26, 6), under=(31, 18))


def rar_m11_2():
    """RAR-M-11-2 - 7.62x51 battle rifle. Long rounded shroud with cooling ports, cheek-rest stock, long rail."""
    a = Art()
    a.shade_poly([(1, 9), (9, 9), (12, 10), (12, 15), (8, 15), (4, 18), (1, 18)], FUR)
    a.hline(2, 8, 10, 'b').hline(2, 8, 11, 'd')                    # cheek rest
    a.erase(5, 13, 8, 14)
    a.vline(1, 9, 18, '1').vline(2, 12, 17, 'a')
    a.bevel(12, 8, 24, 14, MET, left=True)
    a.rail(13, 31, 7)
    a.hline(16, 21, 10, '0').hline(16, 21, 11, '2')
    a.dot(13, 10, 'R').dot(13, 11, 'r')
    a.hline(13, 23, 13, '1')
    # long shroud
    a.bevel(24, 9, 36, 14, MET)
    for x in (26, 29, 32):
        a.rect(x, 11, x + 1, 12, '0').dot(x, 11, '1')
    a.dot(36, 9, '3')
    a.hline(24, 36, 14, '0')
    a.bevel(37, 10, 38, 13, DMET)
    a.bevel(39, 11, 40, 12, MET)
    a.bevel(41, 10, 42, 13, DMET, right=True)
    a.dot(35, 7, '2').dot(35, 6, 'R')
    grip(a, 13, 15, h=6, w=4, slant=2)
    guard(a, 16, 20, 18, 15)
    # bipod folded under the shroud
    a.hline(29, 35, 15, '1').dot(35, 15, '0')
    mag = box_mag(21, 15, 3, 7, curve=0)
    bolt = Art().hline(16, 21, 10, '0').hline(16, 21, 11, '0').dot(18, 11, 'Y').dot(19, 11, 'Y')
    return Gun('rar_m11_2', 'rep', 'rifle', a, mag, bolt, muzzle=(43, 11), rail=(13, 31, 6), under=(27, 16))


def rar_m11_3():
    """RAR-M-11-3 - 9x51 liquidator rifle. Compact boxy all-black housing, thick integral barrel sleeve,
    folding wire stock."""
    a = Art()
    # folding wire stock
    a.hline(4, 12, 10, '2').hline(4, 12, 11, '1')
    a.vline(4, 10, 17, '2').vline(5, 11, 17, '1')
    a.hline(5, 11, 16, '1').hline(4, 9, 17, '0')
    a.dot(11, 15, '1').dot(10, 16, '1')
    # receiver
    a.bevel(12, 8, 27, 15, DMET, left=True)
    a.rail(14, 26, 7, '0', '2')
    a.hline(17, 21, 10, '0').hline(17, 21, 11, '1')
    a.dot(13, 10, 'R')
    a.hline(13, 26, 13, '0')
    # thick barrel sleeve
    a.bevel(27, 9, 39, 14, MET)
    for x in (29, 32, 35):
        a.rect(x, 11, x + 1, 12, '0').dot(x, 11, '1')
    a.hline(27, 39, 14, '0').vline(27, 9, 14, '0')
    a.bevel(40, 9, 42, 14, DMET, right=True)
    a.vline(41, 10, 13, '1')
    grip(a, 13, 16, h=5, w=4, slant=2, r=FUR)
    guard(a, 16, 19, 18, 16)
    mag = box_mag(20, 16, 3, 7, curve=3)
    bolt = Art().hline(17, 21, 10, '0').hline(17, 21, 11, '0').dot(18, 11, 'Y')
    return Gun('rar_m11_3', 'rep', 'rifle', a, mag, bolt, muzzle=(43, 11), rail=(14, 26, 6), under=(31, 15))




def ramg_m14_1():
    """RAMG-M-14-1 - 5.56x45 squad automatic weapon. Smartgun-like frame, carry handle, olive 100-round box."""
    a = Art()
    a.shade_poly([(1, 10), (9, 9), (12, 10), (12, 16), (8, 16), (4, 18), (1, 18)], FUR)
    a.erase(5, 12, 8, 14)
    a.vline(1, 10, 18, '1')
    a.bevel(11, 7, 30, 15, MET, left=True)
    a.hline(12, 29, 9, '2').hline(12, 29, 14, '1')
    a.rect(13, 10, 15, 11, '0').dot(13, 10, 'R').dot(14, 10, 'R')
    a.hline(18, 24, 11, '0').hline(18, 24, 12, '2')
    # carry handle
    a.hline(16, 26, 4, '3').hline(16, 26, 5, '2').vline(16, 5, 6, '2').vline(26, 5, 6, '2')
    # perforated shroud
    a.bevel(30, 9, 41, 14, MET)
    a.pattern(31, 40, 11, '0.').pattern(32, 40, 12, '0.')
    a.hline(30, 41, 14, '0')
    a.bevel(42, 11, 44, 12, MET)
    a.bevel(45, 10, 46, 13, DMET, right=True)
    # folded bipod
    a.hline(33, 41, 15, '1').dot(41, 16, '1').dot(33, 15, '0')
    grip(a, 12, 16, h=5, w=4, slant=2)
    guard(a, 15, 18, 18, 16)
    mag = ammo_box(19, 15, 9, 6, r='uvxy', strap='u')
    mag.dot(23, 16, '2').dot(24, 16, '2')
    bolt = Art().hline(18, 24, 11, '0').hline(18, 24, 12, '0').dot(20, 12, 'Y').dot(21, 12, 'Y').dot(22, 12, 'Y')
    return Gun('ramg_m14_1', 'rep', 'lmg', a, mag, bolt, muzzle=(47, 11), rail=(17, 25, 3), under=(36, 16))


def ramg_m14_2():
    """RAMG-M-14-2 - 7.62x51 machine gun. Long cooling jacket, top handle, deployed bipod, box."""
    a = Art()
    a.shade_poly([(0, 10), (8, 9), (11, 10), (11, 16), (7, 16), (3, 18), (0, 18)], FUR)
    a.erase(4, 12, 7, 14)
    a.vline(0, 10, 18, '1')
    a.bevel(10, 7, 26, 15, MET, left=True)
    a.hline(11, 25, 14, '1')
    a.rect(12, 9, 14, 10, '0').dot(12, 9, 'R').dot(13, 9, 'R')
    a.hline(16, 22, 11, '0').hline(16, 22, 12, '2')
    a.hline(14, 23, 4, '3').hline(14, 23, 5, '2').vline(14, 5, 6, '2').vline(23, 5, 6, '2')
    # long jacket with round ports
    a.bevel(26, 8, 42, 14, MET)
    for x in range(28, 42, 3):
        a.rect(x, 10, x + 1, 12, '0').dot(x, 10, '1')
    a.hline(26, 42, 14, '0')
    a.bevel(43, 10, 44, 12, MET)
    a.bevel(45, 9, 47, 13, DMET, right=True)
    a.pattern(45, 47, 11, '0')
    # deployed bipod
    for i in range(6):
        a.dot(38 - i // 2, 15 + i, '1').dot(41 + i // 2, 15 + i, '1')
    a.hline(37, 42, 15, '2')
    grip(a, 11, 16, h=5, w=4, slant=2)
    guard(a, 14, 17, 18, 16)
    mag = ammo_box(18, 15, 7, 6, r=DMET, strap='3')
    bolt = Art().hline(16, 22, 11, '0').hline(16, 22, 12, '0').dot(18, 12, 'Y').dot(19, 12, 'Y')
    return Gun('ramg_m14_2', 'rep', 'lmg', a, mag, bolt, muzzle=(48, 11), rail=(15, 22, 3), under=(33, 15))


def raptor_m12_16():
    """RAP-TOR-M-12-16 - 10x24 compact automatic weapon. Rounded SMG body, folded stock, long magazine."""
    a = Art()
    # folded stock along the left side
    a.hline(6, 15, 9, '2').hline(6, 15, 10, '1').vline(6, 9, 15, '2').vline(7, 10, 15, '1').hline(7, 10, 15, '1')
    a.shade_poly([(12, 9), (28, 8), (31, 10), (31, 15), (12, 15)], MET)
    a.rail(15, 26, 7)
    a.rect(14, 10, 16, 11, '0').dot(14, 10, 'R').dot(15, 10, 'R')
    a.hline(19, 23, 10, '0').hline(19, 23, 11, '2')
    a.hline(13, 30, 14, '1')
    a.bevel(31, 10, 36, 14, FUR, right=True)
    a.pattern(32, 35, 12, 'a.')
    a.bevel(37, 11, 39, 12, MET)
    a.bevel(40, 10, 40, 13, DMET)
    grip(a, 15, 16, h=5, w=4, slant=2)
    guard(a, 18, 21, 18, 16)
    mag = box_mag(22, 16, 3, 9, curve=1)
    bolt = Art().hline(19, 23, 10, '0').hline(19, 23, 11, '0').dot(20, 11, 'Y')
    return Gun('raptor_m12_16', 'rep', 'smg', a, mag, bolt, muzzle=(41, 11), rail=(15, 26, 6), under=(33, 15))


def rmar_as_m15_18():
    """RMAR-AS-M-15-18 - 12.7x105 anti-materiel rifle. Massive housing, fluted barrel, big box brake, monopod stock."""
    a = Art()
    a.shade_poly([(0, 9), (9, 9), (12, 10), (12, 16), (8, 16), (5, 19), (0, 19)], FUR)
    a.hline(1, 8, 10, 'd').erase(4, 13, 8, 15)
    a.vline(0, 9, 19, '1').vline(3, 19, 21, '1').hline(2, 4, 21, '0')    # monopod
    a.bevel(12, 8, 27, 16, MET, left=True)
    a.hline(13, 26, 15, '1')
    a.rail(13, 32, 7)
    a.rect(14, 10, 16, 11, '0').dot(14, 10, 'R')
    a.hline(19, 24, 10, '0').hline(19, 24, 11, '2')
    a.bevel(27, 10, 30, 14, MET)
    # fluted barrel
    a.bevel(31, 10, 41, 13, MET)
    a.pattern(31, 41, 11, '24').pattern(31, 41, 12, '1')
    # box muzzle brake
    a.bevel(42, 8, 47, 15, DMET, left=True, right=True)
    a.vline(44, 9, 10, '0').vline(44, 13, 14, '0').vline(46, 9, 10, '0').vline(46, 13, 14, '0')
    # folded bipod
    a.hline(31, 40, 14, '1').dot(40, 15, '1')
    grip(a, 13, 17, h=5, w=4, slant=2)
    guard(a, 16, 19, 19, 17)
    mag = box_mag(20, 17, 5, 5, curve=0)
    bolt = Art().hline(19, 24, 10, '0').hline(19, 24, 11, '0').dot(20, 11, 'Y').dot(21, 11, 'Y').dot(22, 11, 'Y')
    bolt.hline(25, 27, 9, '3')
    return Gun('rmar_as_m15_18', 'rep', 'sniper', a, mag, bolt, muzzle=(48, 11), rail=(13, 32, 6), under=(35, 15))


def rmad_as_m15_17():
    """RMAD-AS-M-15-17 - 10x51 marksman rifle. Thumbhole stock, long tan handguard, full-length rail."""
    a = Art()
    a.shade_poly([(1, 9), (11, 9), (15, 11), (16, 15), (14, 21), (10, 21), (11, 16), (5, 16), (3, 18), (1, 18)], FUR)
    a.erase(7, 12, 11, 14).erase(12, 15, 12, 17)                        # thumbhole
    a.vline(1, 9, 18, '1')
    a.bevel(15, 8, 25, 14, MET)
    a.rail(13, 36, 7)
    a.hline(18, 22, 10, '0').hline(18, 22, 11, '2')
    a.dot(16, 10, 'R')
    a.hline(16, 24, 13, '1')
    a.bevel(25, 9, 37, 14, FUR)
    for x in (27, 30, 33):
        a.hline(x, x + 1, 11, 'a').hline(x, x + 1, 12, 'b')
    a.hline(25, 37, 14, 'a')
    a.bevel(38, 11, 40, 12, MET)
    a.bevel(41, 10, 42, 13, DMET, right=True)
    guard(a, 15, 18, 18, 15)
    mag = box_mag(19, 15, 3, 6, curve=1)
    bolt = Art().hline(18, 22, 10, '0').hline(18, 22, 11, '0').dot(19, 11, 'Y').dot(20, 11, 'Y')
    return Gun('rmad_as_m15_17', 'rep', 'rifle', a, mag, bolt, muzzle=(43, 11), rail=(13, 36, 6), under=(30, 15))


def rmad_in_m91_19():
    """RMAD-IN-M-91-19 - 10.66 iridium bullpup in the M4RA mould: pale sage housing with dark panels,
    stepped top rail, long slotted handguard glowing with the iridium coil, ring trigger guard,
    magazine behind the grip, heavy muzzle brake. 64 px canvas."""
    L = '2345'                                                                    # pale housing
    a = Art()
    # butt + rear housing (bullpup)
    a.shade_poly([(3, 10), (9, 9), (9, 17), (3, 18)], L)
    a.vline(3, 10, 18, '1').vline(4, 11, 17, '2')
    a.bevel(9, 8, 32, 16, L, left=True)
    a.shade_poly([(10, 6), (20, 6), (22, 8), (10, 8)], L)                         # raised cheek hump
    a.hline(10, 31, 12, '1').hline(10, 31, 13, '2')                              # dark side panel
    a.rect(11, 9, 16, 10, '1').hline(11, 16, 9, '0')                             # ejection window
    a.dot(29, 10, 'R').dot(30, 10, 'R').dot(29, 11, 'r')                          # ammo display
    a.hline(9, 32, 16, '1')
    # top rail over the whole length
    a.rail(20, 48, 7)
    a.hline(20, 48, 8, '3')
    # long slotted handguard, slots glow with the iridium coil
    a.bevel(32, 9, 50, 15, L)
    for x in range(34, 49, 3):
        a.rect(x, 11, x + 1, 12, 'B').hline(x, x + 1, 12, 'C')
    a.hline(32, 50, 14, '2').hline(32, 50, 15, '1')
    a.vline(32, 9, 15, '1')
    # barrel + brake
    a.bevel(51, 11, 54, 12, '1234')
    a.bevel(55, 9, 58, 14, '0123', left=True, right=True)
    a.vline(56, 10, 13, '0').vline(57, 10, 13, '1').dot(58, 11, 'B').dot(58, 12, 'C')
    # forward grip with a big rounded trigger guard
    grip(a, 33, 16, h=6, w=4, slant=2, r='1234')
    for x, y in [(37, 16), (38, 17), (39, 18), (39, 19), (39, 20), (38, 21), (37, 21), (36, 21)]:
        a.dot(x, y, '2')
    a.dot(37, 17, '4').dot(38, 18, '3')
    mag = box_mag(19, 17, 5, 7, curve=1)
    mag.vline(21, 18, 22, 'C').vline(22, 18, 22, 'B')
    bolt = Art().rect(11, 9, 16, 10, '0').dot(12, 10, 'B').dot(13, 10, 'B').dot(14, 10, 'B')
    return Gun('rmad_in_m91_19', 'rep', 'rifle', a, mag, bolt, muzzle=(59, 11), rail=(20, 48, 6), under=(44, 16),
               width=64)

def rpr_m9_02():
    """RPR-M-9-02 - 10x24 police carbine. Compact rounded black body, white police stripe, stub stock."""
    a = Art()
    a.shade_poly([(8, 10), (14, 9), (14, 16), (11, 16), (8, 17)], FUR)
    a.vline(8, 10, 17, '1')
    a.shade_poly([(14, 9), (31, 8), (34, 10), (34, 15), (14, 15)], DMET)
    a.hline(15, 33, 12, 'W')
    a.rail(17, 28, 7, '0', '2')
    a.dot(16, 10, 'R').dot(31, 9, 'B')
    a.hline(15, 33, 14, '0')
    a.bevel(35, 11, 37, 12, MET)
    a.bevel(38, 10, 39, 13, DMET, right=True)
    grip(a, 16, 16, h=5, w=4, slant=2, r=DMET)
    guard(a, 19, 22, 18, 16)
    mag = box_mag(23, 16, 3, 7, curve=0)
    bolt = Art().hline(21, 25, 10, '0').dot(22, 10, 'Y')
    return Gun('rpr_m9_02', 'rep', 'smg', a, mag, bolt, muzzle=(40, 11), rail=(17, 28, 6), under=(30, 16))


for f in (rar_m11_1, rar_m11_2, rar_m11_3, ramg_m14_1, ramg_m14_2, raptor_m12_16, rmar_as_m15_18, rmad_as_m15_17,
          rmad_in_m91_19, rpr_m9_02):
    add(f())


# ================================================================== Empire (PDV look: black steel, white polymer, yellow)
EM = '1234'      # black steel
WH = 'abcd'      # white polymer
YE = 'uvxy'      # yellow


def emp_stock(a, x, y, w=11, h=9, hole=True):
    """Big white skeleton stock, butt at x, top at y, joining the receiver at x + w."""
    a.shade_poly([(x, y), (x + w - 2, y), (x + w + 1, y + 2), (x + w + 1, y + 6), (x + w - 3, y + 6),
                  (x + 3, y + h), (x, y + h)], WH)
    if hole:
        a.poly([(x + 3, y + 3), (x + w - 3, y + 3), (x + w - 3, y + 4), (x + 4, y + 6), (x + 3, y + 6)], ' ')
        for k in [k for k, c in a.px.items() if c == ' ']:
            del a.px[k]
    a.vline(x, y, y + h, '1').vline(x + 1, y + 1, y + h - 1, 'a')
    a.dot(x + w - 1, y + 1, 'x').dot(x + w, y + 2, 'x')          # yellow marking
    return a


def ir17():
    """IR-17 - 8.42x54 ImpS battle rifle. White skeleton stock, black angular receiver,
    long slotted white handguard with a yellow band, integrated slotted compensator, green emitters."""
    a = Art()
    emp_stock(a, 1, 9)
    a.shade_poly([(12, 8), (27, 8), (29, 10), (29, 15), (12, 15)], EM)
    a.rail(14, 27, 7, '0', '2')
    a.hline(13, 28, 13, '0')
    a.hline(18, 23, 10, '0').hline(18, 23, 11, '2')
    a.dot(14, 10, 'R').dot(15, 10, 'r')
    a.bevel(29, 9, 37, 14, WH)
    for x in (30, 33):
        a.hline(x, x + 1, 11, '2').hline(x, x + 1, 12, '1')
    a.rect(36, 9, 37, 14, 'x').vline(37, 10, 13, 'v')
    a.bevel(38, 10, 42, 13, EM, right=True)
    a.vline(40, 10, 11, '0').vline(42, 10, 11, '0')
    a.dot(14, 6, 'G').dot(36, 8, '2').dot(36, 7, 'G')
    grip(a, 13, 15, h=6, w=4, slant=2, r=WH)
    guard(a, 16, 20, 18, 15, c='1')
    mag = box_mag(21, 15, 3, 8, curve=2)
    bolt = Art().hline(18, 23, 10, '0').hline(18, 23, 11, '0').dot(19, 11, 'Y').dot(20, 11, 'Y')
    return Gun('ir17', 'emp', 'rifle', a, mag, bolt, muzzle=(43, 11), rail=(14, 27, 6), under=(32, 15))


def img17l():
    """IMG-17L - 10.4x16 ImpB submachine gun. Angular black block, short white skeleton stock, flash hider."""
    a = Art()
    emp_stock(a, 6, 9, w=8, h=7)
    a.shade_poly([(14, 8), (29, 8), (31, 10), (31, 15), (14, 15)], EM)
    a.rail(16, 28, 7, '0', '2')
    a.hline(20, 24, 10, '0').hline(20, 24, 11, '2')
    a.dot(16, 10, 'R')
    a.hline(15, 30, 13, '0')
    a.bevel(31, 10, 36, 14, WH)
    a.hline(32, 35, 12, 'b').dot(36, 10, 'x').dot(36, 11, 'x')
    a.bevel(37, 11, 38, 12, EM)
    a.bevel(39, 10, 41, 13, EM, right=True)
    a.dot(40, 11, '0').dot(40, 12, '0')
    a.dot(16, 6, 'G').dot(35, 9, 'G')
    grip(a, 15, 16, h=5, w=4, slant=2, r=WH)
    guard(a, 18, 21, 18, 16)
    mag = box_mag(22, 16, 3, 9, curve=0)
    bolt = Art().hline(20, 24, 10, '0').hline(20, 24, 11, '0').dot(21, 11, 'Y')
    return Gun('img17l', 'emp', 'smg', a, mag, bolt, muzzle=(42, 11), rail=(16, 28, 6), under=(33, 15))


def img17t():
    """IMG-17T - 8.42x54 ImpS light machine gun. IR-17 family: heavier receiver with yellow handle,
    long white handguard, bipod, 45-round drum."""
    a = Art()
    emp_stock(a, 0, 9)
    a.shade_poly([(11, 7), (28, 7), (30, 9), (30, 16), (11, 16)], EM)
    a.hline(15, 24, 4, 'x').hline(15, 24, 5, 'v').vline(15, 5, 6, '2').vline(24, 5, 6, '2')
    a.hline(12, 29, 14, '0')
    a.hline(17, 23, 10, '0').hline(17, 23, 11, '2')
    a.dot(13, 9, 'R').dot(14, 9, 'r')
    a.bevel(30, 9, 42, 14, WH)
    for x in range(32, 41, 2):
        a.vline(x, 11, 12, '2')
    a.rect(41, 9, 42, 14, 'x')
    a.bevel(43, 11, 44, 12, EM)
    a.bevel(45, 10, 47, 13, EM, right=True)
    a.dot(46, 11, '0').dot(46, 12, '0')
    a.dot(13, 6, 'G').dot(41, 8, 'G')
    # folded bipod
    a.hline(33, 41, 15, '1').dot(41, 16, '1')
    grip(a, 12, 17, h=5, w=4, slant=2, r=WH)
    guard(a, 15, 18, 19, 17)
    mag = drum(23, 20, 4)
    mag.dot(23, 20, 'x').dot(23, 16, '1').dot(22, 16, '1').dot(24, 16, '1')
    bolt = Art().hline(17, 23, 10, '0').hline(17, 23, 11, '0').dot(18, 11, 'Y').dot(19, 11, 'Y').dot(20, 11, 'Y')
    return Gun('img17t', 'emp', 'lmg', a, mag, bolt, muzzle=(48, 11), rail=(15, 24, 3), under=(36, 16))


def imp17():
    """IMP-17 - 14.5x114 modular pistol. Huge angular black slide with a yellow stripe, white grip,
    slotted compensator, green emitter."""
    a = Art()
    a.shade_poly([(14, 9), (32, 9), (33, 10), (33, 14), (14, 14)], EM)          # slide
    a.hline(15, 32, 12, 'x').hline(15, 32, 13, 'v')
    a.vline(17, 10, 11, '0').vline(19, 10, 11, '0').vline(21, 10, 11, '0')    # cocking serrations
    a.hline(25, 28, 10, '0')                                                   # ejection port
    a.dot(15, 8, 'G').dot(31, 8, '2')
    a.bevel(34, 10, 37, 13, EM, right=True)                                    # compensator
    a.vline(35, 10, 11, '0')
    a.bevel(16, 15, 30, 16, EM)                                                # frame + rail
    a.pattern(22, 30, 16, '0.')
    a.shade_poly([(15, 15), (21, 15), (19, 23), (14, 23), (13, 22)], WH)       # grip
    a.pattern(14, 18, 19, 'b.').pattern(14, 18, 21, 'b.')
    guard(a, 20, 24, 18, 16)
    mag = Art().bevel(13, 23, 19, 24, '0123').dot(13, 23, '2')
    bolt = Art().erase(14, 9, 16, 14)
    bolt.bevel(11, 9, 13, 14, EM).hline(11, 13, 12, 'x').hline(11, 13, 13, 'v')
    bolt.hline(25, 28, 10, '0').dot(26, 10, 'Y')
    return Gun('imp17', 'emp', 'pistol', a, mag, bolt, muzzle=(38, 11), rail=(18, 30, 8), under=(26, 17),
               stock_at=(14, 15))


def imsh17():
    """IMSH-17 - 14 gauge automatic shotgun. Tall white top shroud over a black body and barrel,
    skeleton stock, 12-round drum."""
    a = Art()
    emp_stock(a, 1, 9)
    a.shade_poly([(12, 9), (30, 9), (30, 16), (12, 16)], EM)
    a.shade_poly([(14, 9), (16, 6), (36, 6), (39, 8), (39, 10), (14, 10)], WH)  # top shroud
    a.hline(17, 35, 8, 'b').pattern(18, 34, 8, 'b.')
    a.rect(33, 7, 35, 8, 'x')
    a.dot(37, 6, 'G')
    a.hline(13, 29, 15, '0')
    a.dot(14, 12, 'R')
    a.bevel(30, 11, 43, 13, EM)
    a.bevel(30, 14, 39, 15, EM)                                                 # under tube
    a.bevel(44, 10, 45, 14, EM, right=True)
    grip(a, 13, 16, h=5, w=4, slant=2, r=WH)
    guard(a, 16, 19, 18, 16)
    mag = drum(24, 20, 4)
    mag.dot(24, 20, 'x').hline(22, 26, 16, '1')
    bolt = Art().hline(19, 25, 12, '0').hline(19, 25, 13, '0').dot(21, 13, 'r').dot(22, 13, 'r')
    return Gun('imsh17', 'emp', 'rifle', a, mag, bolt, muzzle=(46, 11), rail=(18, 33, 5), under=None)


def impsh17p():
    """IMPSH-17P - 14 gauge pump shotgun. Long and slim, white skeleton stock, yellow pump, choke."""
    a = Art()
    emp_stock(a, 1, 9)
    a.shade_poly([(12, 9), (24, 9), (25, 10), (25, 15), (12, 15)], EM)
    a.rail(14, 24, 8, '0', '2')
    a.hline(16, 21, 11, '0')
    a.dot(13, 11, 'R')
    a.hline(13, 24, 14, '0')
    a.bevel(25, 10, 43, 11, EM)                                                 # barrel
    a.bevel(25, 12, 40, 13, EM)                                                 # magazine tube
    a.bevel(28, 12, 35, 14, YE, left=True, right=True)                         # pump
    a.pattern(29, 34, 14, 'u.')
    a.bevel(44, 9, 46, 12, EM, right=True)                                      # choke
    a.dot(45, 10, '0')
    a.dot(42, 9, 'G')
    grip(a, 13, 15, h=6, w=4, slant=2, r=WH)
    guard(a, 16, 20, 18, 15)
    bolt = Art().erase(28, 12, 35, 14).bevel(25, 12, 32, 14, YE, left=True, right=True).bevel(33, 12, 40, 13, EM)
    bolt.hline(16, 21, 11, '0').dot(17, 11, 'r').dot(18, 11, 'r')
    return Gun('impsh17p', 'emp', 'rifle', a, None, bolt, muzzle=(47, 10), rail=(14, 24, 7), under=None)


# ================================================================== Station security (Metallistick Group)
NV = 'abcd'   # navy polymer
TR = 'uvxy'   # white trim


def vympel():
    """Vympel - 5.56x45 bullpup carbine. Slim navy shell with a gunmetal top rail and white trim,
    long magazine behind the grip, short shrouded barrel."""
    a = Art()
    a.shade_poly([(5, 10), (8, 9), (32, 9), (35, 11), (35, 14), (24, 14), (22, 15), (9, 15), (5, 15)], NV)
    a.vline(5, 10, 15, '1').vline(6, 10, 14, 'a')
    a.rail(10, 31, 8)
    a.hline(8, 33, 12, 'x').hline(8, 33, 13, 'a')
    a.hline(16, 20, 10, '1').hline(16, 20, 11, '0')                               # ejection port
    a.dot(29, 10, 'R')
    a.bevel(36, 11, 39, 12, '1234')
    a.bevel(40, 10, 41, 13, '0123', right=True)
    a.dot(31, 7, '2').dot(31, 6, 'R')
    grip(a, 24, 15, h=6, w=4, slant=2, r=NV)
    guard(a, 27, 30, 18, 15, c='a')
    mag = box_mag(10, 15, 4, 10, curve=2)
    bolt = Art().hline(16, 20, 10, '0').hline(16, 20, 11, '0').dot(17, 11, 'Y').dot(18, 11, 'Y')
    return Gun('vympel', 'corp', 'rifle', a, mag, bolt, muzzle=(42, 11), rail=(10, 31, 7), under=(32, 15))

def wspr():
    """WSPR - 9x51 integrally suppressed rifle. Navy stock, gunmetal receiver, long thick suppressor."""
    a = Art()
    a.shade_poly([(1, 10), (9, 9), (12, 10), (12, 15), (8, 15), (4, 18), (1, 18)], NV)
    a.erase(5, 12, 8, 13)
    a.vline(1, 10, 18, '1').hline(2, 8, 10, 'x')
    a.bevel(12, 8, 24, 14, '1234', left=True)
    a.rail(13, 26, 7)
    a.hline(16, 20, 10, '0').hline(16, 20, 11, '2')
    a.dot(13, 10, 'R')
    a.hline(13, 23, 13, '1')
    a.bevel(24, 9, 29, 14, NV)
    a.bevel(30, 8, 46, 14, '0123', left=True, right=True)                      # suppressor
    for x in range(32, 46, 4):
        a.vline(x, 9, 13, '1')
    a.hline(31, 45, 11, '3').hline(30, 46, 14, '0')
    a.hline(33, 43, 9, 'x')
    grip(a, 13, 15, h=6, w=4, slant=2, r=NV)
    guard(a, 16, 19, 18, 15)
    mag = box_mag(20, 15, 3, 7, curve=2)
    bolt = Art().hline(16, 20, 10, '0').hline(16, 20, 11, '0').dot(17, 11, 'Y')
    return Gun('wspr', 'corp', 'rifle', a, mag, bolt, muzzle=(47, 11), rail=(13, 26, 6), under=(27, 15))


def ravenger():
    """Ravenger - 7.62x39 assault rifle of Metallistick Group security, after the M16/M4A1 pattern:
    fixed navy stock, flat-top upper with a flip-up sight, ribbed round handguard behind a delta ring,
    tall front sight post, birdcage flash hider, curved 7.62x39 magazine. 64 px canvas."""
    a = Art()
    # A2-style stock
    a.shade_poly([(2, 10), (12, 10), (17, 12), (17, 15), (12, 16), (2, 18)], NV)
    a.vline(2, 10, 18, '1').vline(3, 11, 17, '0')
    a.hline(5, 13, 11, 'c').hline(4, 11, 15, 'a')
    a.dot(8, 13, 'x').dot(9, 13, 'x').dot(10, 13, 'x')                              # white trim
    # lower + upper receiver
    a.bevel(17, 13, 29, 16, '1234', left=True)
    a.bevel(17, 9, 33, 13, '1234', left=True)
    a.rail(18, 32, 8)
    a.rect(19, 5, 21, 7, '2').dot(19, 5, '3').dot(21, 5, '3').dot(20, 6, '0')       # flip-up rear sight
    a.hline(23, 28, 11, '0').hline(23, 28, 12, '2')                                 # dust cover / port
    a.dot(18, 10, '3').dot(18, 11, '2')                                              # charging handle
    a.dot(31, 11, 'R')
    a.hline(18, 32, 13, '0')
    # magazine well
    a.bevel(26, 14, 31, 16, '1234')
    # delta ring + ribbed handguard
    a.bevel(33, 9, 34, 15, '1234', left=True, right=True)
    a.bevel(35, 9, 47, 15, NV)
    for x in range(36, 47, 2):
        a.vline(x, 10, 14, 'a')
    a.bevel(48, 10, 48, 14, '1234')
    # barrel, front sight, birdcage
    a.bevel(49, 11, 56, 12, '1234')
    a.rect(49, 9, 53, 10, '2').hline(49, 53, 9, '3').hline(50, 52, 8, '2')
    a.vline(51, 6, 7, '2').dot(51, 5, '3')
    a.bevel(57, 10, 59, 13, '0123', right=True)
    a.vline(58, 10, 10, '0').vline(58, 13, 13, '0')
    # pistol grip + guard
    grip(a, 19, 16, h=6, w=4, slant=2, r=NV)
    guard(a, 22, 26, 19, 16)
    mag = box_mag(27, 17, 4, 8, curve=3)
    bolt = Art().hline(23, 28, 11, '0').hline(23, 28, 12, '0').dot(24, 12, 'Y').dot(25, 12, 'Y')
    bolt.hline(16, 18, 10, '3')
    return Gun('ravenger', 'corp', 'rifle', a, mag, bolt, muzzle=(60, 11), rail=(18, 32, 7), under=(42, 16),
               width=64)


def mk58():
    """Mk 58 - Metallistick security sidearm, rechambered for 10x24mm. Gunmetal slide with rear serrations,
    khaki polymer frame with an accessory rail and a textured grip, beige trim line."""
    a = Art()
    a.shade_poly([(14, 9), (34, 9), (35, 10), (35, 13), (14, 13)], '1234')       # slide
    a.vline(16, 10, 12, '1').vline(18, 10, 12, '1').vline(20, 10, 12, '1')     # rear serrations
    a.hline(25, 29, 10, '0').hline(25, 29, 11, '2')                            # ejection port
    a.dot(15, 8, '2').dot(33, 8, '3')                                          # sights
    a.vline(36, 11, 12, '0')                                                   # muzzle
    a.hline(15, 34, 13, '1')
    a.bevel(17, 14, 34, 15, 'abcd')                                            # frame
    a.hline(17, 34, 14, 'x')                                                   # beige trim
    a.pattern(27, 34, 15, 'a.')                                                # rail slots
    a.shade_poly([(15, 14), (21, 14), (19, 23), (14, 23), (13, 22)], 'abcd')   # grip
    a.pattern(14, 19, 17, 'a.').pattern(14, 18, 19, 'a.').pattern(14, 18, 21, 'a.')
    a.vline(20, 15, 17, 'x')
    guard(a, 21, 25, 18, 16, c='a')
    mag = Art().bevel(13, 23, 19, 24, '0123').dot(13, 23, '2')
    bolt = Art().erase(14, 9, 16, 13)
    bolt.bevel(11, 9, 13, 13, '1234').vline(12, 10, 12, '1')
    bolt.hline(25, 29, 10, '0').dot(26, 11, 'Y').dot(27, 11, 'Y')
    return Gun('mk58', 'corp', 'pistol', a, mag, bolt, muzzle=(37, 11), rail=(18, 30, 8), under=(28, 16))


for f in (ir17, img17l, img17t, imp17, imsh17, impsh17p, vympel, wspr, ravenger, mk58):
    add(f())


# ================================================================== Earth General Association (ZGA)
# TSF look (Monolith reference): black angular steel, graphite polymer, steel-blue stripes, cyan status lights.
CELL = 'CCBB'   # energy cell / emitter glass ramp
KANE = 'ttTT'   # Kane cartridge (EAR-20/LR)


def zga_stripe(a, x0, x1, y):
    """Steel-blue Association stripe with a lighter top edge."""
    a.hline(x0, x1, y, 'v')
    return a


def tit_mg10():
    """TIT/MG-10 - 5.56x45 assault rifle of the Association Army (Titan). Skeletal stock, angular receiver,
    a wide magazine well that holds two magazines side by side (switched in a second), slotted graphite handguard."""
    a = Art()
    # skeletal stock
    a.shade_poly([(2, 10), (12, 9), (15, 11), (15, 15), (11, 15), (6, 18), (2, 18)], FUR)
    a.erase(6, 12, 10, 13).erase(5, 14, 7, 15)
    a.vline(2, 10, 18, '0').vline(3, 11, 17, 'a')
    # receiver
    a.bevel(15, 8, 34, 14, MET, left=True)
    a.rail(17, 33, 7)
    a.hline(22, 27, 10, '0').hline(22, 27, 11, '2')             # ejection port
    a.dot(32, 10, 'B').dot(31, 10, 'C')                            # status light
    zga_stripe(a, 16, 34, 13)
    # double magazine well
    a.bevel(25, 15, 34, 16, MET, left=True, right=True)
    a.stamp(box_mag(25, 17, 3, 8, curve=2, r='0012'))            # second (standby) magazine
    # pistol grip + guard
    grip(a, 17, 15, h=6, w=4, slant=2)
    guard(a, 20, 24, 18, 15)
    # handguard
    a.bevel(35, 9, 49, 14, FUR)
    a.pattern(36, 48, 11, 'a..')
    zga_stripe(a, 35, 49, 13)
    a.rail(37, 47, 8)
    a.vline(47, 6, 7, '2').dot(47, 5, 'B')                        # front sight
    # barrel + muzzle device
    a.bevel(50, 11, 56, 12, MET)
    a.bevel(57, 10, 59, 13, DMET, right=True)
    a.vline(58, 10, 10, '0').vline(58, 13, 13, '0')
    mag = box_mag(30, 17, 3, 8, curve=2)
    bolt = Art().hline(22, 27, 10, '0').hline(22, 27, 11, '0').dot(23, 11, 'Y').dot(24, 11, 'Y')
    bolt.hline(13, 15, 9, '3')
    return Gun('tit_mg10', 'zga', 'rifle', a, mag, bolt, muzzle=(60, 11), rail=(17, 33, 6), under=(42, 15), width=64)


def tit_r15():
    """TIT/R-15 - 7.62x39 second-line automatic rifle (Titan): the old Eurasian pattern rebuilt in black steel
    and graphite polymer — folding stock, dust cover with a side rail, gas tube, curved magazine, slant brake."""
    a = Art()
    a.shade_poly([(2, 10), (10, 10), (13, 11), (13, 14), (9, 14), (4, 17), (2, 17)], FUR)
    a.erase(5, 12, 9, 12)
    a.vline(2, 10, 17, '0')
    a.bevel(13, 9, 27, 14, MET, left=True)
    a.hline(14, 26, 9, '3')                                        # dust cover
    a.rail(15, 26, 8)
    a.hline(17, 21, 11, '0').dot(23, 12, '4').dot(24, 12, '4')       # port, selector
    zga_stripe(a, 14, 27, 13)
    grip(a, 15, 15, h=6, w=4, slant=2)
    guard(a, 18, 21, 18, 15)
    a.bevel(28, 9, 35, 14, FUR)
    a.pattern(29, 34, 11, 'a.')
    zga_stripe(a, 28, 35, 13)
    a.hline(28, 37, 8, '2')                                        # gas tube
    a.bevel(36, 11, 41, 12, MET)
    a.rect(37, 8, 38, 10, '2').dot(37, 7, '3')                      # front sight block
    a.bevel(42, 10, 44, 13, DMET, right=True)
    a.dot(43, 10, '0')
    mag = box_mag(22, 15, 3, 8, curve=3)
    bolt = Art().hline(17, 21, 11, '0').dot(18, 11, 'Y').dot(19, 11, 'Y').hline(24, 26, 10, '4')
    return Gun('tit_r15', 'zga', 'rifle', a, mag, bolt, muzzle=(45, 11), rail=(15, 26, 7), under=(31, 15))


def alt_r16():
    """ALT/R-16 - 7.62x51 marksman rifle (Altair): cheek-riser stock, long receiver with a full rail, straight
    20/30-round magazine, long slotted handguard and a heavy brake."""
    a = Art()
    a.shade_poly([(2, 9), (14, 9), (17, 11), (17, 15), (13, 16), (2, 18)], FUR)
    a.hline(4, 12, 8, 'c').hline(4, 12, 9, 'b')                    # cheek riser
    a.erase(7, 13, 12, 14)
    a.vline(2, 9, 18, '0').vline(3, 10, 17, 'a')
    a.bevel(17, 8, 36, 14, MET, left=True)
    a.rail(19, 35, 7)
    a.hline(24, 29, 10, '0').hline(24, 29, 11, '2')
    a.dot(34, 10, 'B')
    zga_stripe(a, 18, 36, 13)
    grip(a, 20, 15, h=6, w=4, slant=2)
    guard(a, 23, 26, 18, 15)
    a.bevel(37, 9, 52, 14, FUR)
    a.pattern(38, 51, 11, 'a..')
    zga_stripe(a, 37, 52, 13)
    a.rail(39, 50, 8)
    a.bevel(53, 11, 59, 12, MET)
    a.bevel(60, 10, 62, 13, DMET, right=True)
    a.vline(61, 10, 13, '0')
    mag = box_mag(27, 15, 4, 8, curve=0)
    bolt = Art().hline(24, 29, 10, '0').hline(24, 29, 11, '0').dot(25, 11, 'Y').dot(26, 11, 'Y')
    return Gun('alt_r16', 'zga', 'rifle', a, mag, bolt, muzzle=(63, 11), rail=(19, 35, 6), under=(45, 15), width=64)


def pro_lr17():
    """PRO/LR-17 - energy rifle (Procyon): smooth bullpup shell, energy cell behind the grip, finned emitter with
    10 mm focusing lenses and a cyan glow, heat vents along the top."""
    a = Art()
    a.shade_poly([(2, 10), (16, 8), (40, 8), (44, 10), (44, 15), (30, 16), (14, 16), (6, 18), (2, 18)], MET)
    a.vline(2, 10, 18, '0')
    a.vents(24, 40, 9, 10, '0', step=3)
    a.rail(20, 38, 7)
    zga_stripe(a, 14, 43, 14)
    # energy cell in the stock
    a.bevel(7, 12, 14, 15, CELL, left=True, right=True)
    a.hline(8, 13, 13, 'B')
    a.dot(36, 11, 'B').dot(37, 11, 'B').dot(38, 11, 'C')             # charge display
    grip(a, 29, 16, h=6, w=4, slant=2, r=FUR)
    guard(a, 32, 36, 19, 16, c='0')
    # emitter
    a.bevel(45, 10, 51, 13, DMET, right=True)
    a.vline(47, 9, 14, '2').vline(49, 9, 14, '2')                    # cooling fins
    a.vline(52, 11, 12, 'B').vline(53, 11, 12, 'b')                   # lens glow
    return Gun('pro_lr17', 'zga', 'energy', a, Art(), Art(), muzzle=(54, 11), rail=(20, 38, 6), under=(46, 15),
               width=64)


def pro_r12():
    """PRO/R-12 - 12.7x105 anti-materiel rifle (Procyon): long fluted barrel, heavy box brake, folded bipod,
    wide 10-round magazine."""
    a = Art()
    a.shade_poly([(2, 9), (12, 9), (16, 11), (16, 16), (11, 16), (2, 18)], FUR)
    a.erase(6, 12, 11, 13)
    a.vline(2, 9, 18, '0').vline(3, 10, 17, 'a')
    a.bevel(16, 8, 34, 15, MET, left=True)
    a.rail(18, 33, 7)
    a.hline(23, 29, 10, '0').hline(23, 29, 11, '2')
    a.dot(32, 10, 'B')
    zga_stripe(a, 17, 34, 14)
    grip(a, 19, 16, h=6, w=4, slant=2)
    guard(a, 22, 25, 19, 16)
    a.bevel(35, 10, 56, 12, MET)
    a.pattern(36, 55, 11, '2.')
    a.hline(37, 49, 13, '1').dot(37, 14, '1').dot(49, 14, '1')        # folded bipod
    a.bevel(57, 9, 61, 13, DMET, right=True)
    a.vline(58, 9, 13, '0').vline(60, 9, 13, '0')
    mag = box_mag(25, 16, 5, 6, curve=0)
    bolt = Art().hline(23, 29, 10, '0').hline(23, 29, 11, '0').dot(24, 11, 'Y').dot(25, 11, 'Y')
    bolt.hline(30, 32, 9, '4')
    return Gun('pro_r12', 'zga', 'sniper', a, mag, bolt, muzzle=(62, 10), rail=(18, 33, 6), under=(44, 14),
               width=64)


def pro_amr16():
    """PRO/AMR-16 - experimental 18.9x110 hybrid rifle (Procyon): powder chamber plus an electromagnetic stage —
    twin accelerator rails with cyan coil rings, a side battery pack, a short 5-round magazine."""
    a = Art()
    a.shade_poly([(2, 9), (12, 9), (15, 11), (15, 16), (2, 18)], FUR)
    a.erase(5, 12, 10, 13)
    a.vline(2, 9, 18, '0')
    a.bevel(15, 7, 34, 15, MET, left=True)
    a.rail(16, 33, 6)
    a.bevel(17, 9, 23, 13, CELL, left=True, right=True)              # battery pack
    a.hline(18, 22, 11, 'B')
    a.hline(26, 31, 9, '0').hline(26, 31, 10, '2')
    zga_stripe(a, 16, 34, 14)
    grip(a, 18, 16, h=6, w=4, slant=2)
    guard(a, 21, 25, 19, 16)
    # accelerator rails
    a.bevel(35, 8, 58, 9, MET)
    a.bevel(35, 12, 58, 13, MET)
    a.hline(35, 57, 10, '0').hline(35, 57, 11, '0')
    for x in range(37, 57, 4):
        a.vline(x, 10, 11, 'B')
    a.vline(58, 10, 11, '1')
    mag = box_mag(27, 16, 4, 5, curve=0)
    bolt = Art().hline(26, 31, 9, '0').hline(26, 31, 10, '0').dot(27, 10, 'Y').dot(28, 10, 'Y')
    return Gun('pro_amr16', 'zga', 'sniper', a, mag, bolt, muzzle=(59, 10), rail=(16, 33, 5), under=(44, 14),
               width=64)


def ear20():
    """EAR-20/LR - pre-Collapse Association energy assault rifle: worn blue-grey enamel, a carry handle, a Kane
    cartridge seated on top, a ringed focusing emitter and a two-position selector (stun / burn)."""
    a = Art()
    a.shade_poly([(2, 10), (14, 9), (17, 11), (17, 16), (12, 17), (2, 18)], FUR)
    a.vline(2, 10, 18, '1').vline(3, 11, 17, 'c')
    a.dot(6, 12, '4').dot(10, 15, '4')                                # scuffs
    a.bevel(17, 8, 38, 15, MET, left=True)
    a.bevel(19, 9, 36, 13, FUR)
    a.dot(22, 10, '3').dot(31, 12, '3').dot(27, 11, 'a')
    # carry handle + Kane cartridge on top
    a.vline(20, 5, 7, '2').vline(35, 5, 7, '2').hline(20, 35, 5, '3').hline(21, 34, 6, '1')
    a.bevel(25, 3, 30, 7, KANE, left=True, right=True)
    a.hline(26, 29, 5, 'T')
    a.dot(33, 10, 'B').dot(33, 12, 'R')                               # selector lights: stun / burn
    grip(a, 21, 16, h=6, w=4, slant=2)
    guard(a, 24, 28, 19, 16)
    # emitter
    a.bevel(39, 10, 50, 13, DMET)
    a.vline(42, 9, 14, '3').vline(45, 9, 14, '3').vline(48, 9, 14, '3')
    a.bevel(51, 10, 52, 13, MET, right=True)
    a.vline(53, 11, 12, 'T')
    return Gun('ear20', 'zgaold', 'energy', a, Art(), Art(), muzzle=(54, 11), rail=(22, 33, 4), under=(44, 15),
               width=64)


for f in (tit_mg10, tit_r15, alt_r16, pro_lr17, pro_r12, pro_amr16, ear20):
    add(f())


# ================================================================== Kane beam weapons (station, Metallistick)
def kl1():
    """KL-1 - Kane beam pistol of station security: compact gunmetal body, khaki grip, a Kane cell in the grip
    (teal window) and a ringed emitter with a teal lens."""
    a = Art()
    a.shade_poly([(13, 9), (31, 9), (33, 11), (33, 14), (13, 14)], '1234')
    a.hline(15, 30, 10, '4')
    a.vline(24, 10, 13, '1').vline(27, 10, 13, '1')                         # emitter rings
    a.hline(14, 32, 14, '0')
    a.vline(34, 11, 12, 'T').vline(35, 11, 12, 't')                         # lens
    a.dot(15, 8, '2').dot(30, 8, '3')                                       # sights
    a.shade_poly([(15, 15), (21, 15), (19, 23), (14, 23), (13, 22)], 'abcd')
    a.vline(16, 17, 21, 't').vline(17, 17, 21, 'T')                         # Kane cell window
    a.hline(15, 21, 15, 'x')
    guard(a, 21, 25, 18, 15, c='a')
    return Gun('kl1', 'corp', 'pistol', a, Art(), Art(), muzzle=(36, 11), rail=(16, 28, 8), under=(27, 15))


def kr3():
    """KR-3 - Kane beam rifle of station security: navy/khaki furniture like the Ravenger, a long gunmetal body
    with cooling slots, a Kane cell housing behind the grip and a finned emitter with a teal lens."""
    a = Art()
    a.shade_poly([(2, 10), (12, 10), (16, 12), (16, 15), (11, 16), (2, 18)], 'abcd')
    a.vline(2, 10, 18, '1').hline(4, 11, 15, 'x')
    a.bevel(16, 8, 38, 14, '1234', left=True)
    a.rail(18, 36, 7)
    a.vents(22, 36, 10, 11, '0', step=3)
    a.hline(17, 38, 13, 'x')
    a.bevel(18, 15, 23, 18, 'ttTT', left=True, right=True)                 # Kane cell housing
    a.dot(36, 12, 'T')
    grip(a, 25, 15, h=6, w=4, slant=2)
    guard(a, 28, 32, 18, 15)
    a.bevel(39, 10, 50, 13, '0123')
    a.vline(42, 9, 14, '2').vline(45, 9, 14, '2').vline(48, 9, 14, '2')     # fins
    a.vline(51, 11, 12, 'T').vline(52, 11, 12, 't')
    return Gun('kr3', 'corp', 'rifle', a, Art(), Art(), muzzle=(53, 11), rail=(18, 36, 6), under=(44, 14),
               width=64)


for f in (kl1, kr3):
    add(f())
