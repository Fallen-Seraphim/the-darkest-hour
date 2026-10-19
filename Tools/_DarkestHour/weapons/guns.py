"""Weapon designs. Icon space: 32px design grid (shifted into a 48x32 canvas), muzzle right, y down, bore line y=11..12.
Parts are drawn in order; later parts cover earlier ones."""
from gunart import Gun, R, P, rail, trigger

GUNS = {}

def add(g):
    GUNS[g.id] = g
    return g

# ============================================================== Republic (black receivers, coyote-tan polymer)

# RAR-M-11-1 - 5.56x45 assault rifle, 30/15 rounds. Collapsible stock, flat-top rail, M-LOK handguard.
add(Gun('rar_m11_1', 'rep', [
    ('poly', P((1, 10), (5, 10), (7, 12), (7, 15), (5, 15), (1, 17))),          # collapsible stock
    ('poly_d', R(1, 10, 1, 17)),                                                 # butt pad
    ('poly_d', R(3, 12, 4, 13)),                                                 # stock cut-out
    ('metal_d', R(7, 12, 8, 13)),                                                # buffer tube
    ('metal', P((8, 10), (18, 10), (18, 14), (14, 14), (13, 15), (8, 15))),      # lower + upper receiver
    ('metal_d', R(15, 12, 17, 12)),                                              # ejection port
    ('metal_l', R(9, 11, 12, 11)),                                               # receiver highlight
    ('poly', P((9, 15), (12, 15), (11, 19), (8, 19))),                           # pistol grip
    ('metal_d', P((12, 15), (15, 15), (15, 16), (12, 16))),                      # trigger guard
    *trigger(13, 15),
    ('poly', R(19, 10, 25, 13)),                                                 # handguard
    ('poly_d', R(20, 12, 21, 12)), ('poly_d', R(23, 12, 24, 12)),                # M-LOK slots
    ('metal_d', R(26, 11, 28, 12)),                                              # barrel
    ('metal', R(28, 10, 30, 13)),                                                # muzzle brake
    ('metal_d', R(29, 10, 29, 10)),
    *rail(9, 25, 9),
    ('metal_d', R(24, 8, 25, 8)),                                                # folding front sight
], muzzle=(31, 11.5), rail=(9, 23, 9), under=(22, 14),
    mag_parts=[('mag', P((14, 15), (17, 15), (18, 21), (15, 22))), ('metal_d', P((15, 21), (18, 20), (18, 21), (15, 22)))],
    bolt=[('metal_d', R(8, 9, 9, 9))],
    bolt_open=[('metal_d', R(5, 9, 6, 9)), ('metal_d', R(14, 11, 17, 12)), ('brass', R(16, 11, 16, 11))]))

# RAR-M-11-2 - 7.62x51 marksman rifle, 20/10 rounds. Fixed stock with cheek riser, long handguard.
add(Gun('rar_m11_2', 'rep', [
    ('poly', P((0, 10), (6, 10), (8, 12), (8, 15), (5, 15), (2, 17), (0, 17))),
    ('poly_d', R(0, 10, 0, 17)),
    ('poly_d', R(2, 9, 5, 9)),                                                   # cheek riser
    ('metal', P((8, 10), (18, 10), (18, 14), (14, 14), (13, 15), (8, 15))),
    ('metal_d', R(15, 12, 17, 12)),
    ('metal_l', R(9, 11, 12, 11)),
    ('poly', P((9, 15), (12, 15), (11, 19), (8, 19))),
    ('metal_d', P((12, 15), (15, 15), (15, 16), (12, 16))),
    *trigger(13, 15),
    ('poly', R(19, 10, 27, 13)),
    ('poly_d', R(20, 12, 21, 12)), ('poly_d', R(23, 12, 24, 12)), ('poly_d', R(26, 12, 26, 12)),
    ('metal_d', R(28, 11, 30, 12)),
    ('metal', R(30, 10, 31, 13)),
    *rail(9, 27, 9),
], muzzle=(32, 11.5), rail=(9, 25, 9), under=(23, 14),
    mag_parts=[('mag', R(14, 15, 17, 20)), ('metal_d', R(14, 20, 17, 20)), ('metal_l', R(15, 16, 15, 18))],
    bolt=[('metal_d', R(8, 9, 9, 9))],
    bolt_open=[('metal_d', R(5, 9, 6, 9)), ('metal_d', R(14, 11, 17, 12)), ('brass', R(16, 11, 16, 11))]))

# RAR-M-11-3 - 9x51 special purpose rifle, 15/5 rounds. Skeleton stock, short handguard, threaded barrel.
add(Gun('rar_m11_3', 'rep', [
    ('metal_d', P((2, 10), (8, 10), (8, 11), (3, 11), (3, 15), (8, 13), (8, 14), (2, 16))),
    ('poly2', R(2, 14, 3, 16)),                                                  # butt pad
    ('metal', P((8, 10), (17, 10), (17, 14), (14, 14), (13, 15), (8, 15))),
    ('metal_d', R(14, 12, 16, 12)),
    ('metal_l', R(9, 11, 11, 11)),
    ('poly2', P((9, 15), (12, 15), (11, 19), (8, 19))),
    ('metal_d', P((12, 15), (15, 15), (15, 16), (12, 16))),
    *trigger(13, 15),
    ('poly2', R(18, 10, 22, 13)),
    ('poly_d', R(19, 12, 21, 12)),
    ('metal_d', R(23, 11, 25, 12)),
    ('metal_l', R(24, 11, 25, 11)),                                              # thread
    *rail(9, 22, 9),
], muzzle=(26, 11.5), rail=(9, 20, 9), under=(20, 14),
    mag_parts=[('mag', R(13, 15, 16, 19)), ('metal_d', R(13, 19, 16, 19))],
    bolt=[('metal_d', R(8, 9, 9, 9))],
    bolt_open=[('metal_d', R(5, 9, 6, 9)), ('metal_d', R(13, 11, 16, 12)), ('brass', R(15, 11, 15, 11))]))

# RAMG-M-14-1 - 5.56x45 squad automatic weapon, 100-round box, takes RAR-M-11-1 magazines.
add(Gun('ramg_m14_1', 'rep', [
    ('poly', P((0, 10), (5, 10), (6, 12), (6, 16), (3, 16), (0, 18))),
    ('poly_d', R(0, 10, 0, 18)),
    ('metal', R(6, 10, 19, 15)),
    ('metal_d', R(7, 13, 18, 13)),
    ('metal_d', R(9, 7, 16, 7)), ('metal_d', R(9, 8, 9, 9)), ('metal_d', R(16, 8, 16, 9)),   # carry handle
    ('poly', P((8, 16), (11, 16), (10, 20), (7, 20))),
    ('metal_d', P((11, 16), (13, 16), (13, 17), (11, 17))),
    ('poly', R(20, 10, 25, 14)),
    ('poly_d', R(21, 12, 24, 12)),
    ('metal_d', R(26, 11, 29, 12)),
    ('metal', R(29, 10, 31, 13)),
    ('metal_d', R(30, 10, 30, 10)),
    ('metal_d', P((21, 15), (23, 15), (28, 18), (26, 18))),                      # folded bipod
    ('metal_d', R(28, 8, 28, 9)),                                                # front sight
], muzzle=(32, 11.5), rail=(10, 15, 7), under=None,
    mag_parts=[('poly2', R(13, 16, 20, 21)), ('accent', R(14, 17, 19, 17)), ('metal_d', R(13, 21, 20, 21))],
    bolt=[('metal_d', R(18, 9, 19, 9))],
    bolt_open=[('metal_d', R(15, 9, 16, 9)), ('metal_d', R(14, 11, 18, 12)), ('brass', R(16, 11, 17, 11))],
    kind='lmg'))

# RAMG-M-14-2 - 7.62x51 machine gun, 75-round box, takes RAR-M-11-2 magazines. Ventilated barrel shroud.
add(Gun('ramg_m14_2', 'rep', [
    ('poly', P((0, 10), (6, 10), (6, 16), (3, 16), (0, 19))),
    ('poly_d', R(0, 10, 0, 19)),
    ('metal', R(6, 10, 20, 15)),
    ('metal_d', R(7, 13, 19, 13)),
    ('poly', P((8, 16), (11, 16), (10, 20), (7, 20))),
    ('metal_d', P((11, 16), (13, 16), (13, 17), (11, 17))),
    ('metal_d', R(21, 10, 26, 14)),                                              # ventilated shroud
    ('metal_l', R(22, 11, 22, 13)), ('metal_l', R(24, 11, 24, 13)), ('metal_l', R(26, 11, 26, 13)),
    ('metal_d', R(27, 11, 29, 12)),
    ('metal', R(29, 10, 31, 13)),
    ('metal_d', P((23, 15), (25, 15), (29, 18), (27, 18))),
    *rail(7, 20, 9),
], muzzle=(32, 11.5), rail=(7, 19, 9), under=None,
    mag_parts=[('poly2', R(13, 16, 20, 20)), ('accent', R(14, 17, 19, 17)), ('metal_d', R(13, 20, 20, 20))],
    bolt=[('metal_d', R(19, 8, 20, 8))],
    bolt_open=[('metal_d', R(16, 8, 17, 8)), ('metal_d', R(15, 11, 19, 12)), ('brass', R(17, 11, 18, 11))],
    kind='lmg'))

# RAP-TOR-M-12-16 - 10x24 SVP compact automatic weapon, 30 rounds. Folding wire stock, long magazine.
add(Gun('raptor_m12_16', 'rep', [
    ('metal_d', P((3, 10), (8, 10), (8, 11), (4, 11), (4, 14), (8, 12), (8, 13), (3, 15))),
    ('metal', P((8, 10), (18, 10), (18, 14), (8, 14))),
    ('metal_d', R(14, 12, 16, 12)),
    ('metal_l', R(9, 11, 12, 11)),
    ('poly', P((9, 15), (12, 15), (11, 19), (8, 19))),
    ('metal', R(8, 14, 13, 14)),
    ('metal_d', P((12, 15), (14, 15), (14, 16), (12, 16))),
    *trigger(13, 15),
    ('poly', R(19, 11, 21, 13)),
    ('metal_d', R(22, 11, 23, 12)),
    ('metal', R(23, 10, 24, 13)),
    *rail(9, 17, 9),
], muzzle=(25, 11.5), rail=(9, 17, 9), under=None,
    mag_parts=[('mag', P((15, 15), (17, 15), (17, 23), (15, 23))), ('metal_d', R(15, 23, 17, 23))],
    bolt=[('metal_d', R(17, 9, 18, 9))],
    bolt_open=[('metal_d', R(13, 9, 14, 9)), ('metal_d', R(13, 11, 16, 12)), ('brass', R(15, 11, 15, 11))],
    kind='smg'))

# RMAR-AS-M-15-18 - 12.7x105 anti-materiel rifle, 10 rounds. Long fluted barrel, big muzzle brake, bipod.
add(Gun('rmar_as_m15_18', 'rep', [
    ('poly', P((0, 9), (5, 9), (6, 11), (6, 16), (2, 16), (0, 18))),
    ('poly_d', R(0, 9, 0, 18)),
    ('poly_d', R(2, 13, 4, 13)),
    ('metal', R(6, 9, 16, 15)),
    ('metal_d', R(7, 13, 15, 13)),
    ('metal_l', R(7, 10, 10, 10)),
    ('poly', P((8, 16), (11, 16), (10, 20), (7, 20))),
    ('metal_d', P((11, 16), (13, 16), (13, 17), (11, 17))),
    ('metal', R(17, 10, 21, 14)),
    ('metal_d', R(22, 11, 28, 13)),
    ('metal_l', R(23, 12, 27, 12)),                                              # flutes
    ('metal', R(28, 9, 31, 14)),                                                 # muzzle brake
    ('metal_d', R(29, 9, 29, 14)), ('metal_d', R(31, 10, 31, 13)),
    ('metal_d', P((18, 15), (20, 15), (25, 19), (23, 19))),
    *rail(7, 20, 8),
], muzzle=(32, 11.5), rail=(7, 19, 8), under=None,
    mag_parts=[('mag', R(12, 16, 15, 20)), ('metal_d', R(12, 20, 15, 20))],
    bolt=[('metal_l', R(13, 8, 14, 8))],
    bolt_open=[('metal_l', R(10, 8, 11, 8)), ('metal_d', R(12, 10, 15, 12)), ('brass', R(13, 11, 15, 11))],
    kind='amr'))

# RMAD-AS-M-15-17 - 10x51 SVP marksman rifle, 20 rounds. Thumbhole stock, full-length rail.
add(Gun('rmad_as_m15_17', 'rep', [
    ('poly', P((0, 10), (9, 10), (9, 15), (7, 15), (5, 13), (4, 13), (3, 17), (0, 17))),
    ('poly_d', R(0, 10, 0, 17)),
    ('metal', P((9, 10), (18, 10), (18, 14), (9, 14))),
    ('metal_d', R(14, 12, 16, 12)),
    ('metal_l', R(10, 11, 12, 11)),
    ('poly', P((9, 15), (11, 15), (10, 19), (8, 19))),
    ('metal_d', P((11, 14), (14, 14), (14, 15), (11, 15))),
    ('poly', R(19, 10, 26, 13)),
    ('poly_d', R(20, 12, 25, 12)),
    ('metal_d', R(27, 11, 29, 12)),
    ('metal', R(29, 10, 31, 13)),
    *rail(9, 26, 9),
], muzzle=(32, 11.5), rail=(9, 24, 9), under=(23, 14),
    mag_parts=[('mag', R(14, 15, 17, 20)), ('metal_d', R(14, 20, 17, 20))],
    bolt=[('metal_d', R(16, 8, 17, 8))],
    bolt_open=[('metal_d', R(13, 8, 14, 8)), ('metal_d', R(14, 11, 17, 12)), ('brass', R(16, 11, 16, 11))]))

# RMAD-IN-M-91-19 - 10.66 iridium bullpup, 14 rounds. Angular shroud, blue iridium accents.
add(Gun('rmad_in_m91_19', 'rep', [
    ('poly', P((1, 10), (13, 10), (13, 15), (9, 16), (1, 16))),                  # bullpup shell
    ('poly_d', R(1, 10, 1, 16)),
    ('metal', P((13, 9), (25, 9), (27, 11), (27, 13), (13, 14))),                # angular shroud
    ('glass', R(15, 11, 24, 11)),                                                # iridium coil line
    ('metal_d', R(4, 12, 7, 12)),                                                # ejection port (rear)
    ('poly', P((16, 15), (18, 15), (17, 19), (15, 19))),
    ('metal_d', P((18, 15), (20, 15), (20, 16), (18, 16))),
    ('metal_d', R(28, 11, 30, 12)),
    *rail(3, 22, 8),
], muzzle=(31, 11.5), rail=(3, 21, 8), under=None,
    mag_parts=[('mag', P((8, 16), (12, 16), (12, 20), (8, 20))), ('glass', R(9, 17, 11, 17))],
    bolt=[('metal_d', R(10, 9, 11, 9))],
    bolt_open=[('metal_d', R(6, 9, 7, 9)), ('metal_d', R(3, 11, 7, 12)), ('glass', R(5, 11, 5, 11))],
    kind='bullpup'))

# RPR-M-9-02 - 10x24 police carbine, 15 rounds. Compact, stub stock, white police stripe.
add(Gun('rpr_m9_02', 'rep', [
    ('poly2', P((5, 10), (8, 10), (8, 15), (5, 15))),
    ('poly_d', R(5, 10, 5, 15)),
    ('metal', P((8, 10), (17, 10), (17, 14), (8, 14))),
    ('metal_d', R(13, 12, 15, 12)),
    ('poly2', P((9, 15), (12, 15), (11, 19), (8, 19))),
    ('metal', R(8, 14, 12, 14)),
    ('metal_d', P((12, 15), (14, 15), (14, 16), (12, 16))),
    *trigger(13, 15),
    ('poly2', R(18, 10, 21, 13)),
    ('metal_d', R(22, 11, 24, 12)),
    ('accent', R(9, 13, 16, 13)),                                                # police stripe
    *rail(9, 16, 9),
], muzzle=(25, 11.5), rail=(9, 16, 9), under=None,
    mag_parts=[('mag', R(14, 15, 16, 20)), ('metal_d', R(14, 20, 16, 20))],
    bolt=[('metal_d', R(16, 9, 17, 9))],
    bolt_open=[('metal_d', R(12, 9, 13, 9)), ('metal_d', R(12, 11, 15, 12)), ('brass', R(14, 11, 14, 11))],
    kind='smg'))

# ============================================================== Empire (blued steel, crimson furniture, brass)

# IR-17 - 8.42x54 ImpS battle rifle, 20 rounds. Massive receiver, integral recoil compensator.
add(Gun('ir17', 'emp', [
    ('poly', P((0, 9), (7, 10), (7, 15), (4, 15), (0, 18))),
    ('accent', R(0, 9, 0, 18)),                                                  # brass butt plate
    ('metal', P((7, 9), (20, 9), (20, 14), (15, 14), (14, 15), (7, 15))),
    ('accent', R(8, 9, 19, 9)),                                                  # brass top strip
    ('metal_d', R(15, 11, 18, 11)),                                              # ejection port
    ('metal_l', R(8, 11, 11, 11)),
    ('metal_d', R(8, 13, 13, 13)),
    ('poly', P((9, 15), (12, 15), (10, 19), (7, 19))),
    ('metal_d', P((12, 15), (14, 15), (14, 16), (12, 16))),
    ('poly', R(21, 10, 25, 14)),
    ('poly_d', R(22, 12, 24, 12)),
    ('metal_d', R(26, 11, 27, 12)),
    ('metal', R(27, 10, 31, 13)),                                                # integral compensator
    ('metal_d', R(28, 10, 28, 10)), ('metal_d', R(30, 10, 30, 10)),
    ('metal_d', R(28, 13, 28, 13)), ('metal_d', R(30, 13, 30, 13)),
    ('metal_d', R(9, 8, 18, 8)),                                                 # optic mount
], muzzle=(32, 11.5), rail=(9, 18, 8), under=(22, 15),
    mag_parts=[('mag', P((14, 15), (18, 15), (19, 21), (15, 21))), ('accent', P((15, 20), (19, 20), (19, 21), (15, 21)))],
    bolt=[('metal_l', R(18, 12, 19, 12))],
    bolt_open=[('metal_l', R(14, 12, 15, 12)), ('metal_d', R(15, 11, 19, 12)), ('brass', R(17, 11, 18, 11))]))

# IMG-17L - 10.4x16 ImpB submachine gun, 20 rounds. Boxy, top ejection, integral flash hider.
add(Gun('img17l', 'emp', [
    ('metal_d', P((3, 10), (8, 10), (8, 14), (3, 15))),
    ('poly', R(3, 11, 6, 13)),
    ('metal', R(8, 9, 18, 14)),
    ('accent', R(11, 9, 14, 9)),                                                 # top ejection port frame
    ('metal_l', R(9, 11, 10, 11)),
    ('metal_d', R(9, 13, 17, 13)),
    ('poly', P((10, 15), (13, 15), (12, 19), (9, 19))),
    ('metal_d', P((13, 15), (15, 15), (15, 16), (13, 16))),
    ('poly', R(19, 10, 21, 13)),
    ('metal_d', R(22, 11, 23, 12)),
    ('metal', P((23, 10), (26, 9), (26, 14), (23, 13))),                         # flash hider cone
    ('metal_d', R(25, 10, 25, 13)),
], muzzle=(27, 11.5), rail=(8, 11, 8), under=None,
    mag_parts=[('mag', R(15, 15, 17, 21)), ('accent', R(15, 21, 17, 21))],
    bolt=[('metal_d', R(12, 8, 13, 8))],
    bolt_open=[('metal_d', R(11, 9, 14, 10)), ('brass', R(12, 8, 13, 8))],
    kind='smg'))

# IMG-17T - 8.42x54 ImpS light machine gun, 45-round drum, bipod. IR-17 family.
add(Gun('img17t', 'emp', [
    ('poly', P((0, 9), (7, 10), (7, 15), (4, 15), (0, 18))),
    ('accent', R(0, 9, 0, 18)),
    ('metal', P((7, 9), (20, 9), (20, 14), (7, 15))),
    ('accent', R(8, 9, 19, 9)),
    ('metal_d', R(15, 11, 18, 11)),
    ('metal_d', R(8, 13, 19, 13)),
    ('poly', P((9, 15), (12, 15), (10, 19), (7, 19))),
    ('metal_d', P((12, 15), (14, 15), (14, 16), (12, 16))),
    ('metal_d', R(21, 10, 25, 14)),                                              # heavy barrel jacket
    ('metal_l', R(22, 11, 22, 13)), ('metal_l', R(24, 11, 24, 13)),
    ('metal_d', R(26, 11, 28, 12)),
    ('metal', R(28, 10, 31, 13)),
    ('metal_d', R(29, 10, 29, 10)), ('metal_d', R(29, 13, 29, 13)),
    ('metal_d', P((22, 15), (24, 15), (28, 19), (26, 19))),                      # bipod
    ('metal_d', R(9, 8, 18, 8)),
], muzzle=(32, 11.5), rail=(9, 18, 8), under=None,
    mag_parts=[('mag', P((14, 15), (19, 15), (20, 17), (20, 20), (18, 22), (15, 22), (13, 20), (13, 17))),
               ('accent', R(16, 18, 17, 19)), ('metal_l', R(15, 16, 18, 16))],
    bolt=[('metal_l', R(18, 12, 19, 12))],
    bolt_open=[('metal_l', R(14, 12, 15, 12)), ('metal_d', R(15, 11, 19, 12)), ('brass', R(17, 11, 18, 11))],
    kind='lmg'))

# IMP-17 - 14.5x114 modular pistol, 4 rounds. Huge slide, brass accents, rail for modules.
add(Gun('imp17', 'emp', [
    ('metal', R(8, 9, 22, 13)),                                                  # slide
    ('accent', R(9, 9, 20, 9)),
    ('metal_d', R(10, 12, 21, 12)),
    ('metal_l', R(17, 10, 20, 10)),                                              # serrations
    ('metal_d', R(23, 10, 24, 12)),
    ('poly', P((9, 14), (14, 14), (13, 21), (8, 21))),                           # grip
    ('poly_d', R(10, 16, 12, 16)), ('poly_d', R(10, 18, 12, 18)),
    ('metal_d', P((14, 14), (17, 14), (17, 16), (14, 16))),                      # trigger guard
    *trigger(15, 14),
    ('metal', R(15, 13, 22, 13)),                                                # dust cover
], muzzle=(25, 11.5), rail=(11, 20, 8), under=(19, 14),
    mag_parts=[('mag', R(9, 21, 12, 22))],
    bolt=[], bolt_open=[('metal_d', R(17, 10, 21, 12)), ('brass', R(19, 11, 20, 11))], kind='pistol'))

# IMSH-17 - 14 gauge automatic shotgun, 12-round drum. Bulky, wide bore.
add(Gun('imsh17', 'emp', [
    ('poly', P((0, 9), (7, 10), (7, 15), (4, 15), (0, 18))),
    ('accent', R(0, 9, 0, 18)),
    ('metal', P((7, 9), (19, 9), (19, 14), (7, 15))),
    ('accent', R(8, 9, 18, 9)),
    ('metal_d', R(14, 11, 17, 11)),
    ('poly', P((9, 15), (12, 15), (10, 19), (7, 19))),
    ('metal_d', P((12, 15), (14, 15), (14, 16), (12, 16))),
    ('poly', R(20, 10, 24, 14)),
    ('poly_d', R(21, 12, 23, 12)),
    ('metal', R(25, 10, 28, 13)),                                                # wide barrel
    ('metal_d', R(25, 12, 28, 12)),
    ('metal_d', R(28, 10, 28, 13)),
], muzzle=(29, 11.5), rail=(8, 17, 8), under=None,
    mag_parts=[('mag', P((14, 15), (19, 15), (20, 17), (20, 20), (18, 22), (15, 22), (13, 20), (13, 17))),
               ('brass', R(15, 18, 18, 19)), ('metal_l', R(15, 16, 18, 16))],
    bolt=[('metal_l', R(17, 12, 18, 12))],
    bolt_open=[('metal_l', R(13, 12, 14, 12)), ('metal_d', R(14, 11, 17, 12)), ('brass', R(15, 11, 16, 11))]))

# IMPSH-17P - 14 gauge pump shotgun, 8-round tube. Folding stock, choke.
add(Gun('impsh17p', 'emp', [
    ('metal_d', P((1, 10), (8, 10), (8, 11), (2, 11), (2, 15), (8, 13), (8, 14), (1, 16))),
    ('accent', R(1, 14, 2, 16)),
    ('metal', R(8, 9, 15, 14)),
    ('accent', R(9, 9, 14, 9)),
    ('metal_d', R(11, 11, 13, 11)),
    ('poly', P((9, 15), (12, 15), (10, 19), (7, 19))),
    ('metal_d', P((12, 15), (14, 15), (14, 16), (12, 16))),
    ('metal_d', R(16, 10, 28, 11)),                                              # barrel
    ('metal', R(16, 12, 27, 13)),                                                # magazine tube
    ('metal_l', R(28, 9, 29, 12)),                                               # choke
], muzzle=(30, 10.5), rail=(9, 14, 8), under=None,
    mag_parts=[],
    bolt=[('poly', R(19, 12, 24, 14)), ('poly_d', R(20, 14, 23, 14))],          # pump forward
    bolt_open=[('poly', R(16, 12, 21, 14)), ('poly_d', R(17, 14, 20, 14)), ('brass', R(12, 11, 13, 11))],
    kind='pump'))

# ============================================================== Station security (Metallistick Group), from Monolith

# Vympel - 5.56x45 bullpup carbine, takes RAR-M-11-1 magazines. Own sprite (Monolith's art is non-commercial).
add(Gun('vympel', 'corp', [
    ('poly', P((2, 10), (14, 10), (14, 15), (10, 16), (2, 16))),                 # bullpup shell
    ('poly_d', R(2, 10, 2, 16)),
    ('poly_d', R(4, 13, 8, 13)),
    ('metal', P((14, 10), (22, 10), (22, 14), (14, 14))),
    ('poly', R(22, 10, 25, 13)),
    ('metal_d', R(26, 11, 28, 12)),
    ('metal', R(28, 10, 29, 13)),
    ('metal_d', R(5, 11, 8, 11)),                                                # ejection port (rear)
    ('poly', P((16, 14), (19, 14), (18, 19), (15, 19))),                         # grip forward of mag
    ('metal_d', P((19, 14), (21, 14), (21, 15), (19, 15))),
    *rail(6, 22, 9),
], muzzle=(30, 11.5), rail=(6, 20, 9), under=(23, 14),
    mag_parts=[('mag', P((9, 16), (12, 16), (13, 21), (10, 22))), ('metal_d', P((10, 21), (13, 20), (13, 21), (10, 22)))],
    bolt=[('metal_d', R(12, 9, 13, 9))],
    bolt_open=[('metal_d', R(8, 9, 9, 9)), ('metal_d', R(4, 11, 8, 12)), ('brass', R(6, 11, 6, 11))],
    kind='bullpup'))

# WSPR - 9x51 integrally suppressed rifle, takes RAR-M-11-3 magazines. Own sprite.
add(Gun('wspr', 'corp', [
    ('poly', P((1, 10), (6, 10), (8, 12), (8, 15), (5, 15), (1, 17))),
    ('poly_d', R(1, 10, 1, 17)),
    ('metal_d', R(7, 12, 8, 13)),
    ('metal', P((8, 10), (17, 10), (17, 14), (13, 14), (12, 15), (8, 15))),
    ('metal_d', R(14, 12, 16, 12)),
    ('metal_l', R(9, 11, 11, 11)),
    ('poly', P((9, 15), (12, 15), (11, 19), (8, 19))),
    ('metal_d', P((12, 15), (14, 15), (14, 16), (12, 16))),
    ('metal', R(18, 9, 29, 14)),                                                 # integral suppressor
    ('metal_d', R(18, 9, 18, 14)),
    ('metal_d', R(21, 10, 21, 13)), ('metal_d', R(24, 10, 24, 13)), ('metal_d', R(27, 10, 27, 13)),
    ('metal_d', R(29, 10, 29, 13)),
    *rail(9, 17, 9),
], muzzle=(30, 11.5), rail=(9, 17, 9), under=None,
    mag_parts=[('mag', R(13, 15, 16, 19)), ('metal_d', R(13, 19, 16, 19))],
    bolt=[('metal_d', R(8, 9, 9, 9))],
    bolt_open=[('metal_d', R(5, 9, 6, 9)), ('metal_d', R(13, 11, 16, 12)), ('brass', R(15, 11, 15, 11))]))
