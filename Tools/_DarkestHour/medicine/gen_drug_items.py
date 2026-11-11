#!/usr/bin/env python3
"""Генератор упаковок лекарств DH (этап 3 переработки медицины): таблетки, баночки, шприцы, бутылочки,
кувшины, автоинъекторы + ru/en строки. Запуск из корня репозитория: python3 Tools/_DarkestHour/medicine/gen_drug_items.py"""

# id реагента (без DH), ru-имя, en-имя
DRUGS = {
    'Ketorolac': ('кеторолак', 'ketorolac'),
    'Tramadol': ('трамадол', 'tramadol'),
    'Morphine': ('морфин', 'morphine'),
    'Fentanyl': ('фентанил', 'fentanyl'),
    'Ketamine': ('кетамин', 'ketamine'),
    'Etamsylate': ('этамзилат', 'etamsylate'),
    'Ringer': ('раствор Рингера', "Ringer's solution"),
    'Colloid': ('коллоидный раствор', 'colloid solution'),
    'Albumin': ('альбумин', 'albumin'),
    'Norepinephrine': ('норадреналин', 'norepinephrine'),
    'Atropine': ('атропин', 'atropine'),
    'Propofol': ('пропофол', 'propofol'),
    'Midazolam': ('мидазолам', 'midazolam'),
    'Naloxone': ('налоксон', 'naloxone'),
    'Flumazenil': ('флумазенил', 'flumazenil'),
    'Dexamethasone': ('дексаметазон', 'dexamethasone'),
    'PrussianBlue': ('берлинская лазурь', 'Prussian blue'),
    'Omnireazin': ('омниреазин', 'omnireazine'),
}

# таблетки: реагент, ед., pillType (спрайт pillN = pillType+1)
PILLS = [('Ketorolac', 10, 0), ('Tramadol', 10, 4), ('Dexamethasone', 10, 6), ('PrussianBlue', 15, 9)]
CANISTER = 5
# шприцы: реагент, ед.
SYRINGES = [('Ketorolac', 15), ('Tramadol', 15), ('Morphine', 10), ('Ketamine', 15), ('Etamsylate', 15),
            ('Norepinephrine', 10), ('Atropine', 15), ('Midazolam', 10), ('Naloxone', 10), ('Flumazenil', 10),
            ('Dexamethasone', 15), ('PrussianBlue', 15)]
# бутылочки (30 ед., фентанил — 10)
BOTTLES = [(d, 10 if d == 'Fentanyl' else 30) for d in DRUGS if d != 'Omnireazin']
JUGS = ['Ringer', 'Colloid', 'Albumin']
# автоинъекторы: реагент, ед., состояние спрайта, цвет в руках, ru/en описание
MEDIPENS = [
    ('Omnireazin', 15, 'morphen', '#5bcaca',
     'Армейский автоинъектор омниреазина — то же, что в автоаптечке модульной брони. Сращивает лёгкие и средние раны и притупляет боль. Солдат всё ещё ранен — он просто может продолжать бой. Пока.',
     "An army omnireazine auto-injector, the same drug as in the modular armour's auto-medkit. Knits light and moderate wounds and dulls the pain. The soldier is still wounded; they can just keep fighting. For now."),
    ('Morphine', 10, 'stimpen', '#e8e4f0',
     'Автоинъектор морфина: сильное обезболивание. Не колите дважды подряд — угнетает дыхание.',
     'A morphine auto-injector: strong pain relief. Do not use two in a row, it depresses breathing.'),
    ('Etamsylate', 15, 'punctpen', '#c7a333',
     'Автоинъектор этамзилата: понемногу останавливает кровотечение и замедляет внутреннее.',
     'An etamsylate auto-injector: gradually stops bleeding and slows internal bleeding.'),
    ('Dexamethasone', 15, 'dermpen', '#d17c2c',
     'Автоинъектор дексаметазона: снимает отёк и воспаление, умеренно лечит ожоги.',
     'A dexamethasone auto-injector: reduces swelling and inflammation, moderately treats burns.'),
    ('Atropine', 15, 'penacid', '#573d9e',
     'Автоинъектор атропина: антидот при отравлениях, поддерживает дыхание.',
     'An atropine auto-injector: an antidote for poisoning that supports breathing.'),
    ('Norepinephrine', 10, 'dexpen', '#2a6bd1',
     'Автоинъектор норадреналина: держит давление при шоке и в критическом состоянии.',
     'A norepinephrine auto-injector: keeps blood pressure up in shock and critical condition.'),
    ('PrussianBlue', 15, 'arithpen', '#1f3a8a',
     'Автоинъектор берлинской лазури: выводит радиоактивные изотопы.',
     'A Prussian blue auto-injector: flushes out radioactive isotopes.'),
    ('Naloxone', 10, 'salpen', '#3cb4c8',
     'Автоинъектор налоксона: снимает передозировку опиоидов.',
     'A naloxone auto-injector: reverses an opioid overdose.'),
]


def reagent(d):
    return f'DH{d}'


def main():
    y = ['# DarkestHour: упаковки лекарств (сгенерировано Tools/_DarkestHour/medicine/gen_drug_items.py — правьте генератор).', '']
    ru = ['# Сгенерировано Tools/_DarkestHour/medicine/gen_drug_items.py', '']
    lab_ru = ['# Сгенерировано Tools/_DarkestHour/medicine/gen_drug_items.py — надписи на таблетках', '']
    lab_en = list(lab_ru)

    for d, units, ptype in PILLS:
        r, e = DRUGS[d]
        label = f'dh-pill-label-{d.lower()}'
        lab_ru.append(f'{label} = {r} {units} ед.')
        lab_en.append(f'{label} = {e} {units}u')
        y += [
            '- type: entity', '  name: pill', f'  suffix: {d} {units}u', '  parent: Pill', f'  id: DHPill{d}', '  components:',
            '  - type: Pill', f'    pillType: {ptype}', '  - type: Sprite', f'    state: pill{ptype + 1}',
            '  - type: Label', f'    localizedLabel: {label}', '  - type: Solution', '    solution:', '      reagents:',
            f'      - ReagentId: {reagent(d)}', f'        Quantity: {units}', '',
            '- type: entity', '  name: pill canister', '  parent: PillCanister', f'  id: DHPillCanister{d}',
            f'  suffix: {d} {units}u, {CANISTER}', '  components:', '  - type: Label', f'    localizedLabel: {label}',
            '  - type: EntityTableContainerFill', '    containers:', '      storagebase:', f'        id: DHPill{d}',
            f'        amount: {CANISTER}', '',
        ]
        ru += [f'ent-DHPill{d} = таблетка', '    .desc = Это не свеча.', f'    .suffix = {r[0].upper() + r[1:]} {units} ед.',
               f'ent-DHPillCanister{d} = баночка для таблеток', '    .desc = Вмещает до 10 таблеток.',
               f'    .suffix = {r[0].upper() + r[1:]} {units} ед., {CANISTER} шт.']

    for d, units in SYRINGES:
        r, e = DRUGS[d]
        y += ['- type: entity', f'  suffix: {d}', '  parent: PrefilledSyringe', f'  id: DHSyringe{d}', '  components:',
              '  - type: Label', f'    localizedLabel: reagent-name-dh-{d.lower()}', '  - type: Solution', '    solution:',
              '      reagents:', f'      - ReagentId: {reagent(d)}', f'        Quantity: {units}', '']
        ru += [f'ent-DHSyringe{d} = шприц', '    .desc = Используется для забора образцов крови у существ или для введения им реагентов.', f'    .suffix = {r[0].upper() + r[1:]}']

    for d, units in BOTTLES:
        r, e = DRUGS[d]
        y += ['- type: entity', f'  id: DHBottle{d}', f'  suffix: {d}', '  parent: BaseChemistryBottleFilled', '  components:',
              '  - type: Label', f'    localizedLabel: reagent-name-dh-{d.lower()}', '  - type: Solution', '    solution:',
              '      reagents:', f'      - ReagentId: {reagent(d)}', f'        Quantity: {units}', '']
        ru += [f'ent-DHBottle{d} = бутылочка', '    .desc = Маленькая бутылочка.', f'    .suffix = {r[0].upper() + r[1:]}']

    for d in JUGS:
        r, e = DRUGS[d]
        y += ['- type: entity', '  parent: Jug', f'  suffix: {d}', f'  id: DHJug{d}', '  components:', '  - type: Label',
              f'    localizedLabel: reagent-name-dh-{d.lower()}', '  - type: Solution', '    solution:', '      reagents:',
              f'      - ReagentId: {reagent(d)}', '        Quantity: 240', '']
        ru += [f'ent-DHJug{d} = кувшин', '    .desc = Используется для хранения очень большого количества химических веществ или растворов. Пить из него крайне не рекомендуется.', f'    .suffix = {r}']

    for d, units, state, color, desc_ru, desc_en in MEDIPENS:
        r, e = DRUGS[d]
        y += ['- type: entity', '  parent: ChemicalMedipen', f'  id: DHMedipen{d}', f'  name: {e} auto-injector',
              f'  description: "{desc_en}"', '  components:', '  - type: Item', '    inhandVisuals:', '      left:',
              '      - state: base-needle-inhand-left', '      - state: base-colorA-inhand-left', f'        color: "{color}"',
              '      - state: base-colorB-inhand-left', '      right:', '      - state: base-needle-inhand-right',
              '      - state: base-colorA-inhand-right', f'        color: "{color}"', '      - state: base-colorB-inhand-right',
              '  - type: Sprite', '    sprite: Objects/Specific/Medical/medipen.rsi', '    layers:', f'    - state: {state}',
              '      map: ["enum.SolutionContainerLayers.Fill"]', '  - type: SolutionContainerVisuals', '    maxFillLevels: 1',
              '    changeColor: false', f'    emptySpriteName: {state}_empty', '  - type: Solution', '    solution:',
              '      reagents:', f'      - ReagentId: {reagent(d)}', f'        Quantity: {units}', '']
        ru += [f'ent-DHMedipen{d} = автоинъектор ({r})', f'    .desc = {desc_ru}']

    root = '.'
    open(f'{root}/Resources/Prototypes/_DarkestHour/Entities/Objects/Specific/Medical/drugs.yml', 'w').write('\n'.join(y).rstrip() + '\n')
    open(f'{root}/Resources/Locale/ru-RU/ss14-ru/prototypes/_DarkestHour/Entities/Objects/Specific/Medical/drugs.ftl', 'w').write('\n'.join(ru) + '\n')
    open(f'{root}/Resources/Locale/ru-RU/_DarkestHour/drug_labels.ftl', 'w').write('\n'.join(lab_ru) + '\n')
    open(f'{root}/Resources/Locale/en-US/_DarkestHour/drug_labels.ftl', 'w').write('\n'.join(lab_en) + '\n')


if __name__ == '__main__':
    main()
