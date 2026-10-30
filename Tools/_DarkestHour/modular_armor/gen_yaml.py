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

# Классы брони (решение автора 2026-10-30): без брони 0, композитный 1, лёгкий 2, тяжёлый 3. Резистов у модулей нет:
# пуля с классом бронепробития не ниже класса модуля проходит полностью, иначе — 15% тупым уроном (заброневая травма);
# холодное оружие и взрывы — ×0,5 физического урона. Логика — DHModularArmorDamageSystem.
# class: (name, speed per module, integrity)
CLASSES = {
    'Light': ('light', 0.99, 100),
    'Heavy': ('heavy', 0.95, 260),
    'Composite': ('composite', 0.98, 170),
}
HELMET_CLASS = 1
CLASS_DESC = {
    'Light': 'A light armour module, protection class 2: stops ordinary rounds, barely slows you down. Can be swapped '
             'in the field.',
    'Heavy': 'A heavy armour module, protection class 3: only the heaviest armour-piercing rounds get through, but it '
             'weighs you down. Can only be fitted or removed at an armour maintenance rack.',
    'Composite': 'A composite armour module, protection class 1: layered plates that stop only the weakest rounds, '
                 'light enough to swap in the field.',
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
        'StandardStorage': ('Composite', 'Light', 'Light', 'DHArmorSpecialStorage'),
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
  # Физическую защиту дают только модули (классы брони); корпус — от радиации и кислоты.
  - type: Armor
    modifiers:
      coefficients:
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
    armorClass: %(helmet_class)s
''' % dict(F, f=f, outer=outer, helmet_class=HELMET_CLASS))

    for cls, (cname, speed, integrity) in CLASSES.items():
        for slot, (enum, state, iname, mult) in SLOTS.items():
            hp = int(integrity * mult)
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
  - type: StaticPrice
    price: %(price)s
''' % dict(F, mid=module_id(f, cls, slot), cname=cname, iname=iname, desc=CLASS_DESC[cls], outer=outer, f=f,
                state=state, size='Large' if cls == 'Heavy' else 'Normal', enum=enum, cls=cls, speed=speed, hp=hp,
                price={'Light': 300, 'Composite': 700, 'Heavy': 900}[cls]))

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
