#!/usr/bin/env python3
"""DarkestHour: лоадауты «Осады» (решение автора 2026-11-13) — личные вещи бойцов Республики и Империи.

Набор по образцу лоадаутов RMC-14/CMU: головной убор, очки, шарф/маска, курево, фляга, мелочи и нож. Всё кладётся
в рюкзак (снаряжение должности надевается раньше — roleLoadout.dhAfterStartingGear), поэтому шлем, гарнитура и визоры
должности не слетают. Одежда — спрайты RMC-14 (CC-BY-SA, cmss13), перенесены в Resources/Textures/_RMC14.

Запуск из корня репозитория: python3 Tools/_DarkestHour/loadouts/build_assault_loadouts.py
Пишет:
  Resources/Prototypes/_DarkestHour/Loadouts/assault_cosmetics.yml   — предметы;
  Resources/Prototypes/_DarkestHour/Loadouts/assault_loadouts.yml    — loadout, loadoutGroup, roleLoadout;
  Resources/Locale/ru-RU/ss14-ru/prototypes/_DarkestHour/Loadouts/assault_cosmetics.ftl — имена предметов.
"""
import glob
import os
import re

R = 'Resources'
H = '_RMC14/Objects/Clothing/Head'
E = '_RMC14/Objects/Clothing/Eyes/Glasses'
M = '_RMC14/Objects/Clothing/Mask'

# id: (родитель, rsi, en имя, en описание, ru имя, ru описание, доп. компоненты)
ITEMS = [
    # --- головные уборы
    ('DHClothingHeadBeretGreen', 'ClothingHeadBase', f'{H}/Hats/beret_green.rsi', 'green beret', 'A plain green service beret.', 'зелёный берет', 'Простой зелёный армейский берет.'),
    ('DHClothingHeadBeretTan', 'ClothingHeadBase', f'{H}/Hats/beret_tan.rsi', 'tan beret', 'A sand-coloured service beret.', 'песочный берет', 'Армейский берет песочного цвета.'),
    ('DHClothingHeadBeretCamo', 'ClothingHeadBase', f'{H}/Hats/beret/jungle.rsi', 'camouflage beret', 'A beret in woodland camouflage.', 'камуфляжный берет', 'Берет в лесном камуфляже.'),
    ('DHClothingHeadBoonie', 'ClothingHeadBase', f'{H}/Hats/boonie/jungle.rsi', 'boonie hat', 'A wide-brimmed field hat in woodland camouflage.', 'панама', 'Полевая панама с широкими полями в лесном камуфляже.'),
    ('DHClothingHeadBoonieTan', 'ClothingHeadBase', f'{H}/Hats/boonie_tan/jungle.rsi', 'tan boonie hat', 'A wide-brimmed sand-coloured field hat.', 'песочная панама', 'Полевая панама песочного цвета с широкими полями.'),
    ('DHClothingHeadFieldCap', 'ClothingHeadBase', f'{H}/Hats/cap/jungle.rsi', 'field cap', 'A camouflage field cap with a short visor.', 'полевое кепи', 'Камуфляжное полевое кепи с коротким козырьком.'),
    ('DHClothingHeadBandBrown', 'ClothingHeadBase', f'{H}/Hats/headband/brown.rsi', 'brown headband', 'A strip of brown cloth tied around the head.', 'коричневая повязка', 'Полоса коричневой ткани, повязанная на голову.'),
    ('DHClothingHeadBandGreen', 'ClothingHeadBase', f'{H}/Hats/headband/green.rsi', 'green headband', 'A strip of green cloth tied around the head.', 'зелёная повязка', 'Полоса зелёной ткани, повязанная на голову.'),
    ('DHClothingHeadBandGray', 'ClothingHeadBase', f'{H}/Hats/headband/gray.rsi', 'grey headband', 'A strip of grey cloth tied around the head.', 'серая повязка', 'Полоса серой ткани, повязанная на голову.'),
    ('DHClothingHeadBandRed', 'ClothingHeadBase', f'{H}/Hats/headband/red.rsi', 'red headband', 'A strip of red cloth tied around the head.', 'красная повязка', 'Полоса красной ткани, повязанная на голову.'),
    ('DHClothingHeadBandTan', 'ClothingHeadBase', f'{H}/Hats/headband/tan.rsi', 'tan headband', 'A strip of sand-coloured cloth tied around the head.', 'песочная повязка', 'Полоса ткани песочного цвета, повязанная на голову.'),
    ('DHClothingHeadBeanieBlack', 'ClothingHeadBase', f'{H}/Beanies/beanie.rsi', 'black watch cap', 'A warm knitted watch cap.', 'чёрная вязаная шапка', 'Тёплая вязаная шапка.'),
    ('DHClothingHeadBeanieGreen', 'ClothingHeadBase', f'{H}/Beanies/green.rsi', 'green watch cap', 'A warm knitted watch cap, olive green.', 'зелёная вязаная шапка', 'Тёплая вязаная шапка оливкового цвета.'),
    ('DHClothingHeadBeanieTan', 'ClothingHeadBase', f'{H}/Beanies/tan.rsi', 'tan watch cap', 'A warm knitted watch cap, sand-coloured.', 'песочная вязаная шапка', 'Тёплая вязаная шапка песочного цвета.'),
    ('DHClothingHeadBeanieGray', 'ClothingHeadBase', f'{H}/Beanies/gray.rsi', 'grey watch cap', 'A warm knitted watch cap, grey.', 'серая вязаная шапка', 'Тёплая вязаная шапка серого цвета.'),
    ('DHClothingHeadUshanka', 'ClothingHeadBase', f'{H}/Hats/ushanka.rsi', 'fur hat', 'A fur hat with ear flaps, for cold nights on the ground.', 'ушанка', 'Меховая шапка с ушами — для холодных ночей на земле.'),
    ('DHImperialClothingHeadBeretBlack', 'ClothingHeadBase', f'{H}/TSE/tse_beret.rsi', 'Imperial beret', 'A black beret with the Imperial badge.', 'имперский берет', 'Чёрный берет с имперской кокардой.'),
    ('DHImperialClothingHeadBeretGuard', 'ClothingHeadBase', f'{H}/TSE/tsepa_beret.rsi', 'Imperial guard beret', 'A beret of the Imperial guard regiments.', 'берет имперской гвардии', 'Берет гвардейских полков Империи.'),
    ('DHClothingHeadBeretBlack', 'ClothingHeadBase', f'{H}/Hats/black_beret.rsi', 'black beret', 'A plain black beret.', 'чёрный берет', 'Простой чёрный берет.'),
    ('DHImperialClothingHeadCapPeaked', 'ClothingHeadBase', f'{H}/TSE/tsepa_cap_peaked.rsi', 'Imperial officer peaked cap', 'A black peaked cap of an Imperial officer.', 'фуражка имперского офицера', 'Чёрная фуражка имперского офицера.'),
    # --- очки
    ('DHClothingEyesAviators', 'ClothingEyesBase', f'{E}/aviators.rsi', 'aviator glasses', 'Tinted glasses in a thin metal frame.', 'очки-авиаторы', 'Тонированные очки в тонкой металлической оправе.'),
    ('DHClothingEyesBallisticGoggles', 'ClothingEyesBase', f'{E}/ballistic_goggles.rsi', 'ballistic goggles', 'Shatterproof goggles that keep grit and splinters out of the eyes.', 'баллистические очки', 'Ударопрочные очки: защищают глаза от песка и мелких осколков.'),
    ('DHClothingEyesSunglasses', 'ClothingEyesBase', f'{E}/sunglasses.rsi', 'cheap sunglasses', 'Cheap sunglasses. They only keep the sun out.', 'дешёвые солнцезащитные очки', 'Дешёвые тёмные очки. Защищают только от солнца.'),
    ('DHClothingEyesEyepatch', 'ClothingEyesBase', f'{E}/eyepatch.rsi', 'eyepatch', 'A black patch over one eye.', 'повязка на глаз', 'Чёрная повязка на один глаз.'),
    # --- маски и шарфы
    ('DHClothingMaskBalaclava', 'ClothingMaskBase', f'{M}/balaclava.rsi', 'balaclava', 'A black knitted balaclava that hides the face.', 'балаклава', 'Чёрная вязаная балаклава, скрывает лицо.', ['IdentityBlocker']),
    ('DHClothingMaskBalaclavaGreen', 'ClothingMaskBase', f'{M}/swatclava.rsi', 'green balaclava', 'An olive knitted balaclava that hides the face.', 'зелёная балаклава', 'Оливковая вязаная балаклава, скрывает лицо.', ['IdentityBlocker']),
    ('DHClothingMaskCoif', 'ClothingMaskBase', f'{M}/coif.rsi', 'coif', 'A woodland camouflage coif covering the neck and lower face.', 'капюшон-маска', 'Камуфляжная маска-капюшон: закрывает шею и низ лица.', ['IdentityBlocker']),
    ('DHClothingMaskCoifGrey', 'ClothingMaskBase', f'{M}/grey_coif.rsi', 'grey coif', 'A grey coif covering the neck and lower face.', 'серый капюшон-маска', 'Серая маска-капюшон: закрывает шею и низ лица.', ['IdentityBlocker']),
    ('DHClothingMaskCoifTan', 'ClothingMaskBase', f'{M}/tan_coif.rsi', 'tan coif', 'A sand-coloured coif covering the neck and lower face.', 'песочный капюшон-маска', 'Маска-капюшон песочного цвета: закрывает шею и низ лица.', ['IdentityBlocker']),
    ('DHClothingMaskScarfBlack', 'ClothingMaskBase', f'{M}/Scarfs/black.rsi', 'black face scarf', 'A black scarf worn over the mouth and nose against dust.', 'чёрный шарф', 'Чёрный шарф: прикрывает рот и нос от пыли.'),
    ('DHClothingMaskScarfGray', 'ClothingMaskBase', f'{M}/Scarfs/gray.rsi', 'grey face scarf', 'A grey scarf worn over the mouth and nose against dust.', 'серый шарф', 'Серый шарф: прикрывает рот и нос от пыли.'),
    ('DHClothingMaskScarfGreen', 'ClothingMaskBase', f'{M}/Scarfs/green.rsi', 'green face scarf', 'A green scarf worn over the mouth and nose against dust.', 'зелёный шарф', 'Зелёный шарф: прикрывает рот и нос от пыли.'),
    ('DHClothingMaskScarfTan', 'ClothingMaskBase', f'{M}/Scarfs/tan.rsi', 'tan face scarf', 'A sand-coloured scarf worn over the mouth and nose against dust.', 'песочный шарф', 'Шарф песочного цвета: прикрывает рот и нос от пыли.'),
    ('DHClothingMaskNeckerchiefBlack', 'ClothingMaskBase', f'{M}/Neckerchief/black.rsi', 'black neckerchief', 'A black cloth tied around the neck.', 'чёрный шейный платок', 'Чёрный платок, повязанный на шею.'),
    ('DHClothingMaskNeckerchiefGreen', 'ClothingMaskBase', f'{M}/Neckerchief/green.rsi', 'green neckerchief', 'A green cloth tied around the neck.', 'зелёный шейный платок', 'Зелёный платок, повязанный на шею.'),
    ('DHClothingMaskNeckerchiefGray', 'ClothingMaskBase', f'{M}/Neckerchief/gray.rsi', 'grey neckerchief', 'A grey cloth tied around the neck.', 'серый шейный платок', 'Серый платок, повязанный на шею.'),
    ('DHClothingMaskNeckerchiefRed', 'ClothingMaskBase', f'{M}/Neckerchief/red.rsi', 'red neckerchief', 'A red cloth tied around the neck.', 'красный шейный платок', 'Красный платок, повязанный на шею.'),
    ('DHClothingMaskNeckerchiefTan', 'ClothingMaskBase', f'{M}/Neckerchief/tan.rsi', 'tan neckerchief', 'A sand-coloured cloth tied around the neck.', 'песочный шейный платок', 'Платок песочного цвета, повязанный на шею.'),
    ('DHClothingMaskTornScarfBlack', 'ClothingMaskBase', f'{M}/TornScarfs/black.rsi', 'torn black scarf', 'A ragged black scarf, worn thin by the field.', 'рваный чёрный шарф', 'Потрёпанный чёрный шарф, истёртый в поле.'),
    ('DHClothingMaskTornScarfGreen', 'ClothingMaskBase', f'{M}/TornScarfs/green.rsi', 'torn green scarf', 'A ragged green scarf, worn thin by the field.', 'рваный зелёный шарф', 'Потрёпанный зелёный шарф, истёртый в поле.'),
    ('DHClothingMaskTornScarfGrey', 'ClothingMaskBase', f'{M}/TornScarfs/grey.rsi', 'torn grey scarf', 'A ragged grey scarf, worn thin by the field.', 'рваный серый шарф', 'Потрёпанный серый шарф, истёртый в поле.'),
]

# Фляги (спрайты RMC-14 canteen.rsi): армейская — с водой, металлическая — пустая.
DRINKS = [
    ('DHDrinkCanteen', 'canteen', 'canteen', 'A military water canteen.', 'армейская фляга', 'Армейская фляга для воды.', 60),
    ('DHDrinkMetalFlask', 'flask_marine', 'metal hip flask', 'A battered metal hip flask with a unit stamp.', 'металлическая фляжка', 'Помятая металлическая фляжка с клеймом части.', 0),
]

# Группы лоадаутов: id → (en, ru, maxLimit, [предметы])
REP_HEAD = ['DHClothingHeadBeretGreen', 'DHClothingHeadBeretCamo', 'DHClothingHeadBeretTan', 'DHClothingHeadBoonie',
            'DHClothingHeadBoonieTan', 'DHClothingHeadFieldCap', 'DHClothingHeadBandGreen', 'DHClothingHeadBandBrown',
            'DHClothingHeadBandTan', 'DHClothingHeadBandGray', 'DHClothingHeadBeanieGreen', 'DHClothingHeadBeanieTan',
            'DHClothingHeadBeanieGray', 'DHClothingHeadUshanka']
EMP_HEAD = ['DHImperialClothingHeadBeretBlack', 'DHImperialClothingHeadBeret', 'DHImperialClothingHeadCap', 'DHImperialClothingHeadBeretGuard', 'DHClothingHeadBeretBlack',
            'DHClothingHeadBandRed', 'DHClothingHeadBandGray', 'DHClothingHeadBeanieBlack', 'DHClothingHeadBeanieGray',
            'DHClothingHeadUshanka']
EYES = ['DHClothingEyesAviators', 'DHClothingEyesBallisticGoggles', 'DHClothingEyesSunglasses', 'DHClothingEyesEyepatch']
REP_MASK = ['DHClothingMaskBalaclavaGreen', 'DHClothingMaskCoif', 'DHClothingMaskCoifTan', 'DHClothingMaskScarfGreen',
            'DHClothingMaskScarfTan', 'DHClothingMaskNeckerchiefGreen', 'DHClothingMaskNeckerchiefTan',
            'DHClothingMaskNeckerchiefGray', 'DHClothingMaskTornScarfGreen']
EMP_MASK = ['DHClothingMaskBalaclava', 'DHClothingMaskCoifGrey', 'DHClothingMaskScarfBlack', 'DHClothingMaskScarfGray',
            'DHClothingMaskNeckerchiefBlack', 'DHClothingMaskNeckerchiefRed', 'DHClothingMaskNeckerchiefGray',
            'DHClothingMaskTornScarfBlack', 'DHClothingMaskTornScarfGrey']
SMOKES = ['CigPackGreen', 'CigPackRed', 'CigPackBlue', 'CigPackBlack', 'Lighter', 'FlippoLighter', 'Matchbox']
FLASKS = ['DHDrinkCanteen', 'DHDrinkMetalFlask', 'DrinkFlaskBar']
PERSONAL = ['d6Dice', 'd20Dice', 'HarmonicaInstrument', 'FoodSnackChocolateBar', 'Pen', 'Paper']
KNIVES = ['CombatKnife', 'SurvivalKnife']
REP_CMD = ['CigarCase', 'DrinkFlask', 'DHRepublicClothingHeadCapOfficer']
EMP_CMD = ['CigarGoldCase', 'DrinkFlask', 'DHImperialClothingHeadCapPeaked']

GROUPS = [
    ('DHLoadoutRepublicHead', 'Headwear', 'Головной убор', 1, REP_HEAD),
    ('DHLoadoutImperialHead', 'Headwear', 'Головной убор', 1, EMP_HEAD),
    ('DHLoadoutEyewear', 'Eyewear', 'Очки', 1, EYES),
    ('DHLoadoutRepublicMask', 'Scarf or mask', 'Шарф или маска', 1, REP_MASK),
    ('DHLoadoutImperialMask', 'Scarf or mask', 'Шарф или маска', 1, EMP_MASK),
    ('DHLoadoutSmokables', 'Smokes', 'Курево', 2, SMOKES),
    ('DHLoadoutFlasks', 'Flask', 'Фляга', 1, FLASKS),
    ('DHLoadoutPersonal', 'Personal items', 'Личные вещи', 2, PERSONAL),
    ('DHLoadoutKnife', 'Knife', 'Нож', 1, KNIVES),
    ('DHLoadoutRepublicCommand', 'Command', 'Командирское', 2, REP_CMD),
    ('DHLoadoutImperialCommand', 'Command', 'Командирское', 2, EMP_CMD),
]

REPUBLIC = ['DHGarrisonCommander', 'DHGarrisonOfficer', 'DHGarrisonArtilleryOfficer', 'DHGarrisonRifleman',
            'DHGarrisonMachinegunner', 'DHGarrisonSniper', 'DHGarrisonFlamer', 'DHGarrisonAAGunner', 'DHGarrisonMedic',
            'DHGarrisonSapper', 'DHGarrisonMortarCommander', 'DHGarrisonMortarOperator', 'DHGarrisonFieldArtillerist',
            'DHGarrisonTankCommander', 'DHGarrisonTankCrew', 'DHGarrisonAPCCommander', 'DHGarrisonAPCCrew']
EMPIRE = ['DHCarrierOperationCommander', 'DHCarrierCaptain', 'DHMarineCommander', 'DHCarrierSupportOfficer',
          'DHCarrierPilot', 'DHCarrierAttackPilot', 'DHCarrierArtillerist', 'DHCarrierTechnician',
          'DHMarineWeaponsSpecialist', 'DHMarineFieldMedic', 'DHMarineMachinegunner', 'DHMarineRifleman',
          'DHMarineMortarCommander', 'DHMarineMortarOperator', 'DHMarineFieldArtillerist', 'DHMarineTankCommander',
          'DHMarineTankCrew', 'DHMarineAPCCommander', 'DHMarineAPCCrew']
COMMAND = {'DHGarrisonCommander', 'DHGarrisonOfficer', 'DHGarrisonArtilleryOfficer', 'DHCarrierOperationCommander',
           'DHCarrierCaptain', 'DHMarineCommander', 'DHCarrierSupportOfficer'}


def loadout_id(item):
    return 'DHLoadout' + (item[2:] if item.startswith('DH') else item)


def main():
    head = '# Сгенерировано Tools/_DarkestHour/loadouts/build_assault_loadouts.py — правьте генератор.\n'
    y = [head, '# Личные вещи «Осады»: одежда — спрайты RMC-14 (CC-BY-SA, cmss13).\n']
    ru = []
    for it in ITEMS:
        pid, parent, rsi, en, en_d, ru_n, ru_d = it[:7]
        extra = it[7] if len(it) > 7 else []
        y.append(f'\n- type: entity\n  parent: {parent}\n  id: {pid}\n  name: {en}\n  description: {en_d}\n'
                 f'  components:\n  - type: Sprite\n    sprite: {rsi}\n  - type: Clothing\n    sprite: {rsi}\n')
        for comp in extra:
            y.append(f'  - type: {comp}\n')
        ru.append(f'ent-{pid} = {ru_n}\n    .desc = {ru_d}\n')
    for pid, state, en, en_d, ru_n, ru_d, water in DRINKS:
        y.append(f'\n- type: entity\n  parent: [DrinkBaseMaterialStrongMetal, SolutionSmall, DrinkBase]\n  id: {pid}\n'
                 f'  name: {en}\n  description: {en_d}\n  components:\n  - type: Sprite\n'
                 f'    sprite: _RMC14/Objects/Consumable/Drinks/canteen.rsi\n    state: {state}\n'
                 f'  - type: Item\n    size: Small\n')
        if water:
            y.append(f'  - type: Solution\n    solution:\n      reagents:\n      - ReagentId: Water\n'
                     f'        Quantity: {water}\n')
        ru.append(f'ent-{pid} = {ru_n}\n    .desc = {ru_d}\n')
    open(f'{R}/Prototypes/_DarkestHour/Loadouts/assault_cosmetics.yml', 'w', encoding='utf-8').write(''.join(y))
    d = f'{R}/Locale/ru-RU/ss14-ru/prototypes/_DarkestHour/Loadouts'
    os.makedirs(d, exist_ok=True)
    open(f'{d}/assault_cosmetics.ftl', 'w', encoding='utf-8').write('\n'.join(ru))

    # лоадауты: по одному на предмет (всё — в рюкзак)
    L = [head, '# Лоадауты «Осады»: личные вещи в рюкзак. Должности — roleLoadout Job<id>.\n']
    items = []
    for _, _, _, _, lst in GROUPS:
        for it in lst:
            if it not in items:
                items.append(it)
    # каждый предмет — id сущности, а не лоадаута (иначе меню лоадаутов падает на спрайте)
    known = {it[0] for it in ITEMS} | {d[0] for d in DRINKS}
    for path in glob.glob(f'{R}/Prototypes/**/*.yml', recursive=True):
        text = open(path, encoding='utf-8').read()
        known |= set(re.findall(r'^- type: entity\n(?:  .*\n)*?  id: (\S+)', text, re.M))
    missing = [it for it in items if it not in known]
    if missing:
        raise SystemExit('нет таких сущностей: ' + ', '.join(missing))
    for it in items:
        L.append(f'\n- type: loadout\n  id: {loadout_id(it)}\n  storage:\n    back:\n    - {it}\n')
    en_ftl, ru_ftl = [], []
    for gid, en, ru_n, mx, lst in GROUPS:
        key = 'dh-loadout-group-' + gid[len('DHLoadout'):].lower()
        L.append(f'\n- type: loadoutGroup\n  id: {gid}\n  name: {key}\n  minLimit: 0\n  maxLimit: {mx}\n  loadouts:\n')
        L += [f'  - {loadout_id(it)}\n' for it in lst]
        en_ftl.append(f'{key} = {en}')
        ru_ftl.append(f'{key} = {ru_n}')
    for side, jobs in (('Republic', REPUBLIC), ('Imperial', EMPIRE)):
        for job in jobs:
            groups = [f'DHLoadout{side}Head', 'DHLoadoutEyewear', f'DHLoadout{side}Mask', 'DHLoadoutSmokables',
                      'DHLoadoutFlasks', 'DHLoadoutPersonal', 'DHLoadoutKnife']
            if job in COMMAND:
                groups.append(f'DHLoadout{side}Command')
            L.append(f'\n- type: roleLoadout\n  id: Job{job}\n  dhAfterStartingGear: true\n  groups:\n')
            L += [f'  - {g}\n' for g in groups]
    open(f'{R}/Prototypes/_DarkestHour/Loadouts/assault_loadouts.yml', 'w', encoding='utf-8').write(''.join(L))
    hdr = '# Группы лоадаутов «Осады» (Tools/_DarkestHour/loadouts/build_assault_loadouts.py)\n'
    open(f'{R}/Locale/en-US/_DarkestHour/assault_loadouts.ftl', 'w', encoding='utf-8').write(hdr + '\n'.join(en_ftl) + '\n')
    open(f'{R}/Locale/ru-RU/_DarkestHour/assault_loadouts.ftl', 'w', encoding='utf-8').write(hdr + '\n'.join(ru_ftl) + '\n')
    print('предметов', len(ITEMS) + len(DRINKS), 'лоадаутов', len(items), 'должностей', len(REPUBLIC) + len(EMPIRE))


if __name__ == '__main__':
    main()
