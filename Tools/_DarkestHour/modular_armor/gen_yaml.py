"""Prototypes of the modular armour IMB-1 / RMB-1: body suits, helmets, modules, special systems and the suit presets
that the squad kits hand out.

Run from the repository root:  python3 Tools/_DarkestHour/modular_armor/gen_yaml.py
Writes Resources/Prototypes/_DarkestHour/Clothing/ModularArmor/{imb1,rmb1}.yml (the shared parts — special systems,
built-in weapons, the maintenance rack — live in special.yml, written by hand)."""
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
OUT = os.path.join(ROOT, 'Resources/Prototypes/_DarkestHour/Clothing/ModularArmor')

FACTIONS = {
    'imb1': dict(Id='IMB1', name='IMB-1', who='Imperial Marine Corps', contra='BaseSyndicateContraband',
                 body_desc='The sealed black body suit of the IMB-1 modular armour of the Imperial Marine Corps. White '
                           'armour modules lock onto it at the arms, legs and torso; a special system fits on the back.',
                 helmet_desc='The IMB-1 helmet: a white combat helmet over a twin-filter gas mask. Seals the suit.'),
    'rmb1': dict(Id='RMB1', name='RMB-1', who='Republican Army Corps', contra='BaseTaskForceContraband',
                 body_desc='The sealed green body suit of the RMB-1 modular armour of the Republican Army Corps. Khaki '
                           'armour modules lock onto it at the arms, legs and torso; a special system fits on the back.',
                 helmet_desc='The RMB-1 helmet: a khaki combat helmet over a twin-filter gas mask. Seals the suit.'),
}

# «Штурм» (решение автора 2026-10-24): урон оружия −25%, резисты брони выше — защита модулей и шлема ×1.25
# (коэффициент урона = 1 − (1 − база) × RESIST_BOOST, не ниже 0.25).
RESIST_BOOST = 1.25


def boost(c):
    return round(max(0.25, 1 - (1 - c) * RESIST_BOOST), 2)


# class: (name, base coefficients — boosted on output, speed per module, integrity, swap note)
CLASSES = {
    'Light': ('light', dict(Blunt=0.8, Slash=0.8, Piercing=0.8, Heat=0.85), 0.99, 100),
    'Heavy': ('heavy', dict(Blunt=0.45, Slash=0.45, Piercing=0.45, Heat=0.7, Caustic=0.8), 0.95, 260),
    'Composite': ('composite', dict(Blunt=0.6, Slash=0.6, Piercing=0.55, Heat=0.55, Caustic=0.7), 0.98, 170),
}
CLASS_DESC = {
    'Light': 'A light armour module: thin plates, barely slows you down. Can be swapped in the field.',
    'Heavy': 'A heavy armour module: thick plates that stop rifle rounds but weigh you down. Can only be fitted or '
             'removed at an armour maintenance rack.',
    'Composite': 'A composite armour module: layered plates, good against fire and bullets alike, light enough to '
                 'swap in the field.',
}

# slot: (enum, part state, item name, torso integrity multiplier)
SLOTS = {
    'Torso': ('Torso', 'torso', 'torso plate', 1.5),
    'ArmLeft': ('ArmLeft', 'arm_left', 'left arm guard', 1.0),
    'ArmRight': ('ArmRight', 'arm_right', 'right arm guard', 1.0),
    'LegLeft': ('LegLeft', 'leg_left', 'left leg guard', 1.0),
    'LegRight': ('LegRight', 'leg_right', 'right leg guard', 1.0),
}

# preset id suffix: (torso, arms, legs, special or None)
PRESETS = {
    'imb1': {
        'Composite': ('Composite', 'Composite', 'Composite', None),
        'Standard': ('Composite', 'Light', 'Light', None),
        'StandardAutoMed': ('Composite', 'Light', 'Light', 'DHArmorSpecialAutoMed'),
        'StandardStorage': ('Composite', 'Light', 'Light', 'DHArmorSpecialStorage'),
        'StandardTanks': ('Composite', 'Light', 'Light', 'DHArmorSpecialTankStorage'),
        'LightFlamer': ('Light', 'Light', 'Light', 'DHArmorSpecialFlamer'),
        'LightShotgun': ('Light', 'Light', 'Light', 'DHArmorSpecialShotgun'),
        'LightAutoMed': ('Light', 'Light', 'Light', 'DHArmorSpecialAutoMed'),
        'HeavyAutoMed': ('Heavy', 'Heavy', 'Heavy', 'DHArmorSpecialAutoMed'),
    },
    'rmb1': {
        'Composite': ('Composite', 'Composite', 'Composite', None),
        'CompositeAutoMed': ('Composite', 'Composite', 'Composite', 'DHArmorSpecialAutoMed'),
        'StandardAutoMed': ('Composite', 'Light', 'Light', 'DHArmorSpecialAutoMed'),
        'LightAutoMed': ('Light', 'Light', 'Light', 'DHArmorSpecialAutoMed'),
        'HeavyAutoMed': ('Heavy', 'Heavy', 'Heavy', 'DHArmorSpecialAutoMed'),
        'HeavyTanks': ('Heavy', 'Heavy', 'Heavy', 'DHArmorSpecialTankStorage'),
    },
}


def module_id(f, cls, slot):
    return 'DHArmorModule%s%s%s' % (FACTIONS[f]['Id'], cls, slot)


def gen(f):
    F = FACTIONS[f]
    out = ['# DarkestHour: модульная броня %s (%s). Сгенерировано Tools/_DarkestHour/modular_armor/gen_yaml.py —\n'
           '# правьте генератор. Спрайты — Tools/_DarkestHour/modular_armor/build.py.\n' % (F['name'], F['who'])]
    outer = '_DarkestHour/Clothing/OuterClothing/ModularArmor'
    out.append('''
- type: entity
  parent: [ ClothingOuterHardsuitBase, %(contra)s ]
  id: DHClothingOuterModular%(Id)s
  name: %(name)s body suit
  description: "%(body_desc)s"
  components:
  - type: Sprite
    sprite: %(outer)s/%(f)s_body.rsi
  - type: Clothing
    sprite: %(outer)s/%(f)s_body.rsi
  - type: Item
    size: Huge
    shape:
    - 0,0,3,3
  - type: PressureProtection
    highPressureMultiplier: 0.1
    lowPressureMultiplier: 1000
  - type: ExplosionResistance
    damageCoefficient: 0.8
  - type: Armor
    modifiers:
      coefficients:
        Blunt: 0.95
        Slash: 0.95
        Piercing: 0.95
        Heat: 0.9
        Radiation: 0.6
        Caustic: 0.6
  - type: ClothingSpeedModifier
    walkModifier: 1
    sprintModifier: 1
  - type: HeldSpeedModifier
  - type: ToggleableClothing
    clothingPrototype: DHClothingHeadHelmetModular%(Id)s
  - type: DHModularArmor
    faction: %(f)s
  - type: ContainerContainer
    containers:
      toggleable-clothing: !type:ContainerSlot {}
      dh_armor_torso: !type:ContainerSlot {}
      dh_armor_armleft: !type:ContainerSlot {}
      dh_armor_armright: !type:ContainerSlot {}
      dh_armor_legleft: !type:ContainerSlot {}
      dh_armor_legright: !type:ContainerSlot {}
      dh_armor_special: !type:ContainerSlot {}
  - type: Tag
    tags:
    - Hardsuit
    - WhitelistChameleon
  - type: StaticPrice
    price: 4000

- type: entity
  parent: [ ClothingHeadHardsuitBase, %(contra)s ]
  id: DHClothingHeadHelmetModular%(Id)s
  name: %(name)s helmet
  description: "%(helmet_desc)s"
  components:
  - type: Sprite
    sprite: _DarkestHour/Clothing/Head/ModularArmor/%(f)s_helmet.rsi
  - type: Clothing
    sprite: _DarkestHour/Clothing/Head/ModularArmor/%(f)s_helmet.rsi
  - type: PressureProtection
    highPressureMultiplier: 0.1
    lowPressureMultiplier: 1000
  # Шлем защищает только голову (часть тела выбирают травмы), общей брони у него нет.
  - type: Armor
    modifiers:
      coefficients:
        Blunt: 1
        Slash: 1
        Piercing: 1
        Heat: 1
        Radiation: 1
  - type: DHPartArmor
    parts: [ Head ]
    modifiers:
      coefficients:
        Blunt: %(helmet_kinetic)s
        Slash: %(helmet_kinetic)s
        Piercing: %(helmet_kinetic)s
        Heat: %(helmet_heat)s
''' % dict(F, f=f, outer=outer, helmet_kinetic=boost(0.6), helmet_heat=boost(0.7)))

    for cls, (cname, coef, speed, integrity) in CLASSES.items():
        for slot, (enum, state, iname, mult) in SLOTS.items():
            hp = int(integrity * mult)
            coefs = '\n'.join('        %s: %s' % (k, boost(v)) for k, v in coef.items())
            out.append('''
- type: entity
  parent: [ BaseItem, %(contra)s ]
  id: %(mid)s
  name: %(name)s %(cname)s %(iname)s
  description: "%(desc)s"
  components:
  - type: Sprite
    sprite: %(outer)s/%(f)s_%(cname)s.rsi
    state: icon_%(state)s
  - type: Item
    size: %(size)s
  - type: DHArmorModule
    slot: %(enum)s
    class: %(cls)s
    faction: %(f)s
    speed: %(speed)s
    maxIntegrity: %(hp)s
    integrity: %(hp)s
    equippedRsi: %(outer)s/%(f)s_%(cname)s.rsi
    equippedState: %(state)s
    modifiers:
      coefficients:
%(coefs)s
  - type: StaticPrice
    price: %(price)s
''' % dict(F, mid=module_id(f, cls, slot), cname=cname, iname=iname, desc=CLASS_DESC[cls], outer=outer, f=f,
                state=state, size='Large' if cls == 'Heavy' else 'Normal', enum=enum, cls=cls, speed=speed, hp=hp,
                coefs=coefs, price={'Light': 300, 'Composite': 700, 'Heavy': 900}[cls]))

    for preset, (torso, arms, legs, special) in PRESETS[f].items():
        lines = [
            '      Torso: %s' % module_id(f, torso, 'Torso'),
            '      ArmLeft: %s' % module_id(f, arms, 'ArmLeft'),
            '      ArmRight: %s' % module_id(f, arms, 'ArmRight'),
            '      LegLeft: %s' % module_id(f, legs, 'LegLeft'),
            '      LegRight: %s' % module_id(f, legs, 'LegRight'),
        ]
        if special:
            lines.append('      Special: %s' % special)
        out.append('''
- type: entity
  parent: DHClothingOuterModular%(Id)s
  id: DHClothingOuterModular%(Id)s%(preset)s
  suffix: %(preset)s
  components:
  - type: DHModularArmor
    faction: %(f)s
    startingModules:
%(lines)s
''' % dict(F, f=f, preset=preset, lines='\n'.join(lines)))
    return ''.join(out)


def main():
    os.makedirs(OUT, exist_ok=True)
    for f in FACTIONS:
        with open(os.path.join(OUT, f + '.yml'), 'w', encoding='utf-8') as fh:
            fh.write(gen(f).rstrip('\n') + '\n')


if __name__ == '__main__':
    main()
