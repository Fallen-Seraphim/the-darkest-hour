import os
# Генерирует Resources/Prototypes/_DarkestHour/Research/arsenal.yml (ветка «Арсенал»: рецепты боеприпасов). python3 gen_arsenal.py
K = {'10x24': 10, '104x16': 10, '556x45': 12, '762x39': 12, '9x51': 12, '762x51': 18, '842x54': 18, '10x51': 18,
     '1066': 18, '127x105': 60, '145x114': 60, '14g': 20}
N = {'556x45': 60, '762x51': 40, '9x51': 30, '762x39': 60, '10x24': 60, '10x51': 40, '1066': 28, '127x105': 10,
     '842x54': 40, '104x16': 60, '145x114': 12, '14g': 16}
AP = {'BB', 'SVP', 'EB', 'ImpR', 'Flech', 'EPP', 'EPPP'}
SOFT = {'E', 'EP', 'ImpZ', 'Slug'}

def box(cal, t):
    n, k = N[cal], K[cal]
    m = {'Steel': n * k}
    if t in AP:
        m['Plasteel'] = n * k // 4
    if t in SOFT:
        m['Plastic'] = n * k // 4
    return f'DHAmmoBox{cal}{t}', m

def mag(mid):
    return mid, {'Steel': 50, 'Plastic': 25}

TECHS = [
    # id, name, desc-less, icon(sprite,state), tier, cost, pos, prereqs, boxes, mags
    ('DHAmmoProduction', 'research-technology-dh-ammo-production', ('_Mono/Objects/Weapons/Guns/Ammunition/Boxes/45_ACP.rsi', 'base'),
     1, 5000, (13, 0), [], [('10x24', 'E'), ('762x39', 'FMG')],
     ['DHMagazine10x24Empty', 'DHMagazine10x24ShortEmpty', 'DHMagazine762x39Empty']),
    ('DHArmorPiercingAmmo', 'research-technology-dh-armor-piercing-ammo', None,
     2, 8000, (14, 2), ['DHAmmoProduction'], [('10x24', 'SVP'), ('762x39', 'BB'), ('762x39', 'E')], []),
    ('DHRepublicCalibers', 'research-technology-dh-republic-calibers', None,
     2, 10000, (15, 2), ['DHAmmoProduction'],
     [('556x45', 'FMG'), ('762x51', 'FMG'), ('9x51', 'FMG'), ('10x51', 'FMG')],
     ['DHMagazine556x45Empty', 'DHMagazine556x45ShortEmpty', 'DHMagazine762x51Empty', 'DHMagazine762x51ShortEmpty',
      'DHMagazine9x51Empty', 'DHMagazine9x51ShortEmpty', 'DHMagazine10x51Empty']),
    ('DHRepublicSpecialAmmo', 'research-technology-dh-republic-special-ammo', None,
     3, 15000, (15, 4), ['DHRepublicCalibers', 'DHArmorPiercingAmmo'],
     [('556x45', 'BB'), ('556x45', 'E'), ('762x51', 'BB'), ('9x51', 'EB'), ('9x51', 'DZ'), ('10x51', 'SVP'),
      ('1066', 'EPP'), ('127x105', 'SVP')],
     ['DHMagazine1066Empty', 'DHMagazine127x105Empty']),
    ('DHImperialCalibers', 'research-technology-dh-imperial-calibers', None,
     2, 12000, (16, 2), ['DHAmmoProduction'],
     [('842x54', 'ImpS'), ('104x16', 'ImpB'), ('14g', 'Shot')],
     ['DHMagazine842x54Empty', 'DHMagazine104x16Empty', 'DHMagazineDrum14gEmpty']),
    ('DHImperialSpecialAmmo', 'research-technology-dh-imperial-special-ammo', None,
     3, 15000, (16, 4), ['DHImperialCalibers'],
     [('842x54', 'ImpR'), ('842x54', 'ImpZ'), ('14g', 'Flech'), ('14g', 'Slug'), ('145x114', 'Std')],
     ['DHMagazineDrum842x54Empty', 'DHMagazine145x114Empty']),
]

SHIP = [
    ('DHShipMagazine20mm', {'Steel': 1500, 'Plasteel': 200}),
    ('DHShipMagazine30mmAP', {'Steel': 1200, 'Plasteel': 500}),
    ('DHShipMagazine30mmHE', {'Steel': 1200, 'Plastic': 400}),
]
SHIP_HEAVY = [
    ('DHShipShell90mmAP', {'Steel': 600, 'Plasteel': 300}),
    ('DHShipShell90mmHE', {'Steel': 600, 'Plastic': 300}),
]

out = ['# DarkestHour: ветка «Арсенал» дерева технологий (этап 4). Ванильное оружие, калибры и лазеры вырезаны;',
       '# здесь — производство боеприпасов наших калибров в оружейном техфабе СБ и снарядов корабельных орудий.',
       '# Сгенерировано по таблице калибров (Tools/_DarkestHour/weapons/gen_yaml.py): цена коробки = патроны × ставка калибра;',
       '# бронебойные добавляют пластсталь (материаловедение), экспансивные и зажигательные — пластик.', '']
packs = {'DHSecurityAmmo': [], 'DHShipAmmo': []}
recipes = []
for tid, name, icon, tier, cost, pos, pre, boxes, mags in TECHS:
    unl = []
    for cal, t in boxes:
        rid, m = box(cal, t)
        recipes.append(('BaseAmmoBoxRecipe', rid, m))
        unl.append(rid)
    for mid in mags:
        rid, m = mag(mid)
        recipes.append(('BaseEmptyAmmoRecipe', rid, m))
        unl.append(rid)
    packs['DHSecurityAmmo'] += unl
    if icon is None:
        rsi, st = {
            'DHArmorPiercingAmmo': ('45_ACP.rsi', 'synthAP'), 'DHRepublicCalibers': ('5.56x45mm.rsi', 'base'),
            'DHRepublicSpecialAmmo': ('12.7x99mm.rsi', 'base'), 'DHImperialCalibers': ('7.62x54mmR.rsi', 'base'),
            'DHImperialSpecialAmmo': ('12_gauge.rsi', 'flechette')}[tid]
        icon = ('_Mono/Objects/Weapons/Guns/Ammunition/Boxes/' + rsi, st)
    out += ['- type: technology', f'  id: {tid}', f'  name: {name}', '  icon:', f'    sprite: {icon[0]}',
            f'    state: {icon[1]}', '  discipline: Arsenal', f'  tier: {tier}', f'  cost: {cost}', '  recipeUnlocks:']
    out += [f'  - {r}' for r in unl]
    if pre:
        out += ['  technologyPrerequisites:'] + [f'  - {p}' for p in pre]
    out += [f'  position: {pos[0]}, {pos[1]}', '']

for rid, m in SHIP + SHIP_HEAVY:
    recipes.append(('BaseAmmoRecipe', rid, m))
packs['DHShipAmmo'] = [r for r, _ in SHIP + SHIP_HEAVY]
out += ['- type: technology', '  id: DHShipMunitions', '  name: research-technology-dh-ship-munitions', '  icon:',
        '    sprite: _Mono/Objects/ShuttleWeapons/L85.rsi', '    state: icon', '  discipline: Arsenal', '  tier: 2',
        '  cost: 10000', '  recipeUnlocks:'] + [f'  - {r}' for r, _ in SHIP] + \
       ['  technologyPrerequisites:', '  - BasicShuttleArmament', '  position: 19, 2', '']

out += ['# --- Рецепты ---', '']
for parent, rid, m in recipes:
    out += ['- type: latheRecipe', f'  parent: {parent}', f'  id: {rid}', f'  result: {rid}', '  materials:']
    out += [f'    {k}: {v}' for k, v in m.items()]
    out.append('')
out += ['# Оружейный техфаб СБ (dynamicPacks): боеприпасы наших калибров и снаряды корабельных орудий.', '']
for pid, rs in packs.items():
    out += ['- type: latheRecipePack', f'  id: {pid}', '  recipes:'] + [f'  - {r}' for r in rs] + ['']
open(os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../Resources/Prototypes/_DarkestHour/Research/arsenal.yml'), 'w').write('\n'.join(out).rstrip('\n') + '\n')
print(len(recipes), 'recipes')
