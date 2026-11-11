"""Prototypes of the modular armour IMB-1 / RMB-1: exoskeletons (light / medium / heavy), their gas-mask helmets,
armour plates, limb covers and the exoskeleton builds that the squad kits hand out; plus the ru-RU entity names.

Run from the repository root:  python3 Tools/_DarkestHour/modular_armor/gen_yaml.py
Writes Resources/Prototypes/_DarkestHour/Clothing/ModularArmor/{imb1,rmb1}.yml and
Resources/Locale/ru-RU/ss14-ru/prototypes/_DarkestHour/Clothing/ModularArmor/{imb1,rmb1}.ftl (the shared parts — special
systems, built-in weapons, the maintenance rack — live in special.yml, written by hand). Sprites — exo_sprites.py."""
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
OUT = os.path.join(ROOT, 'Resources/Prototypes/_DarkestHour/Clothing/ModularArmor')
OUT_FTL = os.path.join(ROOT, 'Resources/Locale/ru-RU/ss14-ru/prototypes/_DarkestHour/Clothing/ModularArmor')

FACTIONS = {
    'imb1': dict(Id='IMB1', name='IMB-1', ru='ИМБ-1', who='Imperial Marine Corps', contra='BaseSyndicateContraband',
                 suit='black', ru_suit='чёрного'),
    'rmb1': dict(Id='RMB1', name='RMB-1', ru='РМБ-1', who='Republican Army Corps', contra='BaseTaskForceContraband',
                 suit='dark olive', ru_suit='тёмно-оливкового'),
}

# Экзоскелеты (решение автора 2026-11-11): лёгкий — композитные пластины, средний — композитные и лёгкие, тяжёлый —
# тяжёлые. Пластины экзоскелет несёт сам: лёгкий слегка ускоряет, средний компенсирует ровно, тяжёлый замедляет.
# load — доля веса остального снаряжения на носителе, melee — множитель урона в ближнем бою. Все, кроме лёгкого,
# герметичны; у лёгкого нет спецсистемы.
EXOS = {
    'Light': dict(plates=['Composite'], speed=1.05, load=0.85, melee=1.1, sealed=False, special=False, weight=8,
                  price=2500, ru='лёгкий', ru_n='лёгкого'),
    'Medium': dict(plates=['Composite', 'Light'], speed=1.0, load=0.7, melee=1.25, sealed=True, special=True,
                   weight=14, price=4000, ru='средний', ru_n='среднего'),
    'Heavy': dict(plates=['Heavy'], speed=0.9, load=0.55, melee=1.5, sealed=True, special=True, weight=24,
                  price=6000, ru='тяжёлый', ru_n='тяжёлого'),
}
EXO_DESC = {
    'Light': ('A light open-frame exoskeleton worn over the uniform: struts along the limbs, actuators in the joints. '
              'Takes composite plates only, carries itself and its plates and even quickens the step. Not sealed; no '
              'mount for a special system.',
              'Лёгкий открытый экзоскелет поверх формы: стойки вдоль рук и ног, приводы в суставах. Берёт только '
              'композитные пластины, несёт себя и их сам и даже слегка ускоряет шаг. Не герметичен, спецсистему не '
              'поставить.'),
    'Medium': ('A sealed exoskeleton over a %(suit)s pressure suit. Takes composite and light plates and carries them '
               'exactly: the wearer moves as if unarmoured. A special system mounts on the back power unit.',
               'Герметичный экзоскелет поверх %(ru_suit)s комбинезона. Берёт композитные и лёгкие пластины и несёт их '
               'ровно: носитель двигается как без брони. На спинной силовой блок ставится спецсистема.'),
    'Heavy': ('A sealed heavy exoskeleton with massive actuators over a %(suit)s pressure suit. Takes only heavy plates; '
              'slower, but it carries most of the gear and hits hard in melee. A special system mounts on the back '
              'power unit.',
              'Герметичный тяжёлый экзоскелет с мощными приводами поверх %(ru_suit)s комбинезона. Берёт только '
              'тяжёлые пластины; медленнее, зато берёт на себя большую часть снаряжения и сильно бьёт в ближнем бою. '
              'На спинной силовой блок ставится спецсистема.'),
}
HELMET_DESC = {
    'Heavy': ('A tall bulky helmet with an integrated gas mask: two lenses and two side filters. Seals the suit.',
              'Высокий массивный шлем со встроенным противогазом: две линзы и два боковых фильтра. Герметизирует '
              'костюм.'),
    'Medium': ('A combat helmet over an integrated panoramic gas mask: a wide visor, a round speech diaphragm and a '
               'cheek filter. Seals the suit.',
               'Каска поверх встроенного панорамного противогаза: широкое стекло, круглое переговорное устройство и '
               'фильтр на щеке. Герметизирует костюм.'),
    'Light': ('A combat helmet over an integrated panoramic gas mask: a wide visor, a round speech diaphragm and a '
              'cheek filter. The light exoskeleton is not sealed.',
              'Каска поверх встроенного панорамного противогаза: широкое стекло, круглое переговорное устройство и '
              'фильтр на щеке. Лёгкий экзоскелет не герметичен.'),
}

# Классы брони (решение автора 2026-10-30): без брони 0, композитный 1, лёгкий 2, тяжёлый 3. Резистов у пластин нет:
# пуля с классом бронепробития не ниже класса пластины проходит полностью, иначе — 15% тупым уроном (заброневая
# травма); холодное оружие и взрывы — ×0,5 физического урона. Логика — DHModularArmorDamageSystem.
# class: (sprite name, integrity, limb plate weight kg, ru adjective m/f)
CLASSES = {
    'Light': ('light', 100, 1.5, ('лёгкий', 'лёгкая')),
    'Heavy': ('heavy', 260, 4.0, ('тяжёлый', 'тяжёлая')),
    'Composite': ('composite', 170, 2.5, ('композитный', 'композитная')),
}
HELMET_CLASS = 1
CLASS_DESC = {
    'Light': ('A light armour plate, protection class 2: stops ordinary rounds. Fits medium exoskeletons; can be '
              'swapped in the field.',
              'Лёгкая бронепластина, класс защиты 2: держит обычные патроны. Встаёт на средний экзоскелет, меняется '
              'в поле.'),
    'Heavy': ('A heavy armour plate, protection class 3: only the heaviest armour-piercing rounds get through. Fits '
              'heavy exoskeletons only and can only be fitted or removed at an armour maintenance rack.',
              'Тяжёлая бронепластина, класс защиты 3: пробивают только тяжёлые бронебойные патроны. Встаёт только на '
              'тяжёлый экзоскелет, ставится и снимается только на стойке обслуживания брони.'),
    'Composite': ('A composite armour plate, protection class 1: layered plates that stop only the weakest rounds. '
                  'Fits light and medium exoskeletons; can be swapped in the field.',
                  'Композитная бронепластина, класс защиты 1: слоёные пластины держат только самые слабые патроны. '
                  'Встаёт на лёгкий и средний экзоскелет, меняется в поле.'),
}

# slot: (enum, part state, item name, ru name, ru gender 0 m / 1 f, integrity multiplier, weight multiplier)
SLOTS = {
    'Torso': ('Torso', 'torso', 'torso plate', 'пластина торса', 1, 1.5, 2.0),
    'ArmLeft': ('ArmLeft', 'arm_left', 'left arm guard', 'щиток левой руки', 0, 1.0, 1.0),
    'ArmRight': ('ArmRight', 'arm_right', 'right arm guard', 'щиток правой руки', 0, 1.0, 1.0),
    'LegLeft': ('LegLeft', 'leg_left', 'left leg guard', 'щиток левой ноги', 0, 1.0, 1.0),
    'LegRight': ('LegRight', 'leg_right', 'right leg guard', 'щиток правой ноги', 0, 1.0, 1.0),
}

# preset id suffix: (exo, torso, arms, legs, special or None). Id остались со времён корпусов — на них ссылаются
# наборы отрядов.
PRESETS = {
    'imb1': {
        'Composite': ('Light', 'Composite', 'Composite', 'Composite', None),
        'Standard': ('Medium', 'Composite', 'Light', 'Light', None),
        'StandardAutoMed': ('Medium', 'Composite', 'Light', 'Light', 'DHArmorSpecialAutoMed'),
        'StandardStorage': ('Medium', 'Composite', 'Light', 'Light', 'DHArmorSpecialStorage'),
        'StandardTanks': ('Medium', 'Composite', 'Light', 'Light', 'DHArmorSpecialTankStorage'),
        'LightFlamer': ('Medium', 'Light', 'Light', 'Light', 'DHArmorSpecialFlamer'),
        'LightShotgun': ('Medium', 'Light', 'Light', 'Light', 'DHArmorSpecialShotgun'),
        'LightAutoMed': ('Medium', 'Light', 'Light', 'Light', 'DHArmorSpecialAutoMed'),
        'HeavyAutoMed': ('Heavy', 'Heavy', 'Heavy', 'Heavy', 'DHArmorSpecialAutoMed'),
    },
    'rmb1': {
        'Composite': ('Light', 'Composite', 'Composite', 'Composite', None),
        'CompositeAutoMed': ('Medium', 'Composite', 'Composite', 'Composite', 'DHArmorSpecialAutoMed'),
        'StandardAutoMed': ('Medium', 'Composite', 'Light', 'Light', 'DHArmorSpecialAutoMed'),
        'StandardStorage': ('Medium', 'Composite', 'Light', 'Light', 'DHArmorSpecialStorage'),
        'LightAutoMed': ('Medium', 'Light', 'Light', 'Light', 'DHArmorSpecialAutoMed'),
        'HeavyAutoMed': ('Heavy', 'Heavy', 'Heavy', 'Heavy', 'DHArmorSpecialAutoMed'),
        'HeavyTanks': ('Heavy', 'Heavy', 'Heavy', 'Heavy', 'DHArmorSpecialTankStorage'),
    },
}

OUTER = '_DarkestHour/Clothing/OuterClothing/ModularArmor'
HEAD = '_DarkestHour/Clothing/Head/ModularArmor'


def module_id(f, cls, slot):
    return 'DHArmorModule%s%s%s' % (FACTIONS[f]['Id'], cls, slot)


def exo_id(f, exo):
    return 'DHClothingOuterExo%s%s' % (FACTIONS[f]['Id'], exo)


def helmet_id(f, exo):
    return 'DHClothingHeadHelmetExo%s%s' % (FACTIONS[f]['Id'], exo)


def q(s):
    return s.replace('"', '\\"')


def gen_exo(f, exo):
    F, E = FACTIONS[f], EXOS[exo]
    sealed = E['sealed']
    d = dict(F, f=f, outer=OUTER, head=HEAD, cls=exo.lower(), exo=exo, eid=exo_id(f, exo), hid=helmet_id(f, exo),
             desc=q(EXO_DESC[exo][0] % F), hdesc=q(HELMET_DESC[exo][0]), plates=', '.join(E['plates']),
             speed=E['speed'], load=E['load'], melee=E['melee'], special='true' if E['special'] else 'false',
             weight=E['weight'], price=E['price'], helmet_class=HELMET_CLASS,
             parent='ClothingOuterHardsuitBase' if sealed else 'ClothingOuterBase',
             tags='    - Hardsuit\n    - WhitelistChameleon\n' if sealed else '    - WhitelistChameleon\n')
    out = ['''
- type: entity
  parent: [ %(parent)s, %(contra)s ]
  id: %(eid)s
  name: %(name)s %(cls)s exoskeleton
  description: "%(desc)s"
  components:
  - type: Sprite
    sprite: %(outer)s/%(f)s_exo_%(cls)s.rsi
    state: icon
  - type: Clothing
    sprite: %(outer)s/%(f)s_exo_%(cls)s.rsi
  - type: Item
    size: Huge
    shape:
    - 0,0,3,3
  - type: DHWeight
    weight: %(weight)s
''' % d]
    if sealed:
        out.append('''  - type: PressureProtection
    highPressureMultiplier: 0.1
    lowPressureMultiplier: 1000
  - type: ExplosionResistance
    damageCoefficient: 0.8
  # Физическую защиту дают только пластины (классы брони); комбинезон — от радиации и кислоты.
  - type: Armor
    modifiers:
      coefficients:
        Radiation: 0.6
        Caustic: 0.6
''')
    out.append('''  # Скорость задаёт класс экзоскелета (DHExoskeleton), пластины её не меняют.
  - type: ClothingSpeedModifier
    walkModifier: 1
    sprintModifier: 1
  - type: HeldSpeedModifier
  - type: ToggleableClothing
    slot: head
    verbText: toggle-clothing-verb-helmet
    action: ActionToggleSuitPiece
    clothingPrototype: %(hid)s
  - type: DHExoskeleton
    class: %(exo)s
    plates: [ %(plates)s ]
    speed: %(speed)s
    loadFactor: %(load)s
    meleeMultiplier: %(melee)s
    special: %(special)s
  # Пока экзоскелет надет, пластины рук и ног занимают слоты правой/левой руки и ноги (DHArmorLimbCover).
  - type: DHModularArmor
    faction: %(f)s
    covers:
      ArmLeft: DHArmorCover%(Id)sArmLeft
      ArmRight: DHArmorCover%(Id)sArmRight
      LegLeft: DHArmorCover%(Id)sLegLeft
      LegRight: DHArmorCover%(Id)sLegRight
  - type: ContainerContainer
    containers:
      toggleable-clothing: !type:ContainerSlot {}
      dh_armor_torso: !type:ContainerSlot {}
      dh_armor_armleft: !type:ContainerSlot {}
      dh_armor_armright: !type:ContainerSlot {}
      dh_armor_legleft: !type:ContainerSlot {}
      dh_armor_legright: !type:ContainerSlot {}
      dh_armor_special: !type:ContainerSlot {}
  - type: GroupExamine
  - type: Tag
    tags:
%(tags)s  - type: StaticPrice
    price: %(price)s
''' % d)

    # шлем с встроенным противогазом
    if sealed:
        out.append('''
- type: entity
  parent: [ ClothingHeadHardsuitBase, %(contra)s ]
  id: %(hid)s
  name: %(name)s %(cls)s exoskeleton helmet
  description: "%(hdesc)s"
  components:
  - type: Sprite
    sprite: %(head)s/%(f)s_helmet_%(cls)s.rsi
  - type: Clothing
    sprite: %(head)s/%(f)s_helmet_%(cls)s.rsi
  - type: PressureProtection
    highPressureMultiplier: 0.1
    lowPressureMultiplier: 1000
''' % d)
    else:
        out.append('''
- type: entity
  parent: [ ClothingHeadBase, %(contra)s ]
  id: %(hid)s
  name: %(name)s %(cls)s exoskeleton helmet
  description: "%(hdesc)s"
  categories: [ HideSpawnMenu ]
  components:
  - type: Sprite
    sprite: %(head)s/%(f)s_helmet_%(cls)s.rsi
    state: icon
  - type: Clothing
    sprite: %(head)s/%(f)s_helmet_%(cls)s.rsi
    equipSound: /Audio/Mecha/mechmove03.ogg
    unequipSound: /Audio/Mecha/mechmove03.ogg
    quickEquip: false
    slots: [ HEAD ]
  - type: BreathMask
  - type: IngestionBlocker
  - type: IdentityBlocker
  - type: HideLayerClothing
  - type: GroupExamine
''' % d)
    out.append('''  # Шлем защищает только голову (часть тела выбирают травмы), общей брони у него нет.
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
  # Встроенный противогаз занимает и слот маски (решение автора 2026-11-06).
  - type: DHIntegratedMask
  - type: SlotBlock
    slots: [ MASK ]
''' % d)
    ftl = ['ent-%s = %s экзоскелет %s\n    .desc = %s\n' % (d['eid'], E['ru'], F['ru'], EXO_DESC[exo][1] % F),
           'ent-%s = шлем %s экзоскелета %s\n    .desc = %s\n' % (d['hid'], E['ru_n'], F['ru'], HELMET_DESC[exo][1])]
    return ''.join(out), ftl


def gen(f):
    F = FACTIONS[f]
    out = ['# DarkestHour: модульная броня %s (%s) — экзоскелеты и пластины. Сгенерировано\n'
           '# Tools/_DarkestHour/modular_armor/gen_yaml.py — правьте генератор. Спрайты — exo_sprites.py.\n'
           % (F['name'], F['who'])]
    ftl = []
    for exo in EXOS:
        yml, lines = gen_exo(f, exo)
        out.append(yml)
        ftl += lines

    for part, slot_flag, state, pname, ru_part in (('ArmLeft', 'ARMS', 'arm_left', 'left arm module', 'левой руки'),
                                                   ('ArmRight', 'ARMS', 'arm_right', 'right arm module', 'правой руки'),
                                                   ('LegLeft', 'LEGS', 'leg_left', 'left leg module', 'левой ноги'),
                                                   ('LegRight', 'LEGS', 'leg_right', 'right leg module', 'правой ноги')):
        out.append('''
- type: entity
  parent: BaseItem
  id: DHArmorCover%(Id)s%(part)s
  name: %(name)s %(pname)s
  description: "A plate of the worn %(name)s exoskeleton. It comes off together with the exoskeleton or when the plate is taken out; examine for its protection."
  categories: [ HideSpawnMenu ]
  components:
  - type: Sprite
    sprite: %(outer)s/%(f)s_light.rsi
    state: icon_%(state)s
  - type: Item
    size: Normal
  # Вес пластины считается в экзоскелете.
  - type: DHWeight
    weight: 0
  # Саму пластину рисует экзоскелет; здесь — только место в слоте инвентаря.
  - type: Clothing
    slots: [ %(slot_flag)s ]
    clothingVisuals:
      %(slot)s: []
  - type: Unremoveable
    deleteOnDrop: true
  - type: DHArmorLimbCover
''' % dict(F, part=part, pname=pname, outer=OUTER, f=f, state=state, slot_flag=slot_flag,
           slot=part[0].lower() + part[1:]))
        ftl.append('ent-DHArmorCover%s%s = пластина %s %s\n    .desc = Пластина надетого экзоскелета %s. Снимается '
                   'вместе с экзоскелетом или когда пластину вынимают; осмотрите, чтобы увидеть её защиту.\n'
                   % (F['Id'], part, ru_part, F['ru'], F['ru']))

    for cls, (cname, integrity, weight, ru_adj) in CLASSES.items():
        for slot, (enum, state, iname, ru_name, gender, mult, wmult) in SLOTS.items():
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
  - type: DHWeight
    weight: %(weight)s
  - type: DHArmorModule
    slot: %(enum)s
    class: %(cls)s
    faction: %(f)s
    maxIntegrity: %(hp)s
    integrity: %(hp)s
    equippedRsi: %(outer)s/%(f)s_%(cname)s.rsi
    equippedState: %(state)s
  - type: StaticPrice
    price: %(price)s
''' % dict(F, mid=module_id(f, cls, slot), cname=cname, iname=iname, desc=q(CLASS_DESC[cls][0]), outer=OUTER, f=f,
                state=state, size='Large' if cls == 'Heavy' else 'Normal', enum=enum, cls=cls, hp=hp,
                weight=round(weight * wmult, 1), price={'Light': 300, 'Composite': 700, 'Heavy': 900}[cls]))
            ftl.append('ent-%s = %s %s %s\n    .desc = %s\n' % (module_id(f, cls, slot), ru_adj[gender], ru_name,
                                                              F['ru'], CLASS_DESC[cls][1]))

    for preset, (exo, torso, arms, legs, special) in PRESETS[f].items():
        for plate in {torso, arms, legs}:
            assert plate in EXOS[exo]['plates'], (f, preset)
        assert special is None or EXOS[exo]['special'], (f, preset)
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
  parent: %(eid)s
  id: DHClothingOuterModular%(Id)s%(preset)s
  suffix: %(preset)s
  components:
  - type: DHModularArmor
    faction: %(f)s
    covers:
      ArmLeft: DHArmorCover%(Id)sArmLeft
      ArmRight: DHArmorCover%(Id)sArmRight
      LegLeft: DHArmorCover%(Id)sLegLeft
      LegRight: DHArmorCover%(Id)sLegRight
    startingModules:
%(lines)s
''' % dict(F, f=f, preset=preset, lines='\n'.join(lines), eid=exo_id(f, exo)))
        ftl.append('ent-DHClothingOuterModular%s%s = { ent-%s }\n    .desc = { ent-%s.desc }\n'
                   % (F['Id'], preset, exo_id(f, exo), exo_id(f, exo)))
    return ''.join(out), ftl


def main():
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(OUT_FTL, exist_ok=True)
    for f in FACTIONS:
        yml, ftl = gen(f)
        with open(os.path.join(OUT, f + '.yml'), 'w', encoding='utf-8') as fh:
            fh.write(yml.rstrip('\n') + '\n')
        with open(os.path.join(OUT_FTL, f + '.ftl'), 'w', encoding='utf-8') as fh:
            fh.write('\n'.join(sorted(ftl)))


if __name__ == '__main__':
    main()
