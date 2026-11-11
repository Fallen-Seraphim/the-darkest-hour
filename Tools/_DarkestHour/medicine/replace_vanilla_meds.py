#!/usr/bin/env python3
"""Этап 3 переработки медицины: убрать ванильные (выдуманные) лекарства из всех источников — аптечек, раздатчиков,
шкафов, поясов, сумок, спавнеров, снаряжения, таблиц лута — и заменить нашими. Определения ванильных предметов не
трогаем (они просто больше нигде не выдаются). Идемпотентен. Запуск из корня репозитория."""
import os
import re

MAP = {
    # таблетки и баночки
    'PillTricordrazine': 'DHPillKetorolac', 'PillCanisterTricordrazine': 'DHPillCanisterKetorolac',
    'PillBicaridine': 'DHPillTramadol', 'PillCanisterBicaridine': 'DHPillCanisterTramadol',
    'PillKelotane': 'DHPillDexamethasone', 'PillCanisterKelotane': 'DHPillCanisterDexamethasone',
    'PillDermaline': 'DHPillDexamethasone', 'PillCanisterDermaline': 'DHPillCanisterDexamethasone',
    'PillDylovene': 'PillCharcoal', 'PillCanisterDylovene': 'PillCanisterCharcoal',
    'PillDexalin': 'PillIron', 'PillCanisterDexalin': 'PillCanisterIron',
    'PillHyronalin': 'DHPillPrussianBlue', 'PillCanisterHyronalin': 'DHPillCanisterPrussianBlue',
    'PillAmbuzol': 'DHPillKetorolac', 'PillAmbuzolPlus': 'DHPillKetorolac', 'PillCanisterAmbuzol': 'DHPillCanisterKetorolac',
    # шприцы
    'SyringeBicaridine': 'DHSyringeTramadol', 'SyringeDermaline': 'DHSyringeDexamethasone',
    'SyringeEthylredoxrazine': 'DHSyringeNaloxone', 'SyringeHyronalin': 'DHSyringePrussianBlue',
    'SyringePhalanximine': 'DHSyringePrussianBlue', 'SyringeInaprovaline': 'DHSyringeNorepinephrine',
    'SyringeSigynate': 'DHSyringeDexamethasone', 'SyringeAmbuzol': 'DHSyringeKetorolac',
    # бутылочки
    'ChemistryBottleBicaridine': 'DHBottleTramadol', 'ChemistryBottleKelotane': 'DHBottleDexamethasone',
    'ChemistryBottleDermaline': 'DHBottleDexamethasone', 'ChemistryBottleDylovene': 'ChemistryBottleCharcoal',
    'ChemistryBottleDexalin': 'DHBottleNorepinephrine', 'ChemistryBottleDexalinPlus': 'DHBottleNorepinephrine',
    'ChemistryBottleInaprovaline': 'DHBottleNorepinephrine', 'ChemistryBottleEthylredoxrazine': 'DHBottleNaloxone',
    'ChemistryBottleHyronalin': 'DHBottlePrussianBlue', 'ChemistryBottleArithrazine': 'DHBottlePrussianBlue',
    'ChemistryBottlePhalanximine': 'DHBottlePrussianBlue', 'ChemistryBottleOmnizine': 'DHBottleAlbumin',
    'ChemistryBottleTricordrazine': 'DHBottleKetorolac', 'ChemistryBottleCognizine': 'DHBottleMidazolam',
    'ChemistryBottleCryoxadone': 'DHBottleAlbumin', 'ChemistryBottleDoxarubixadone': 'DHBottleAlbumin',
    'ChemistryBottleLeporazine': 'DHBottleDexamethasone', 'ChemistryBottleBruizine': 'DHBottleTramadol',
    'ChemistryBottleLacerinol': 'DHBottleEtamsylate', 'ChemistryBottlePuncturase': 'DHBottleEtamsylate',
    'ChemistryBottlePyrazine': 'DHBottleDexamethasone', 'ChemistryBottleSigynate': 'DHBottleDexamethasone',
    'ChemistryBottleSiderlac': 'DHBottleDexamethasone', 'ChemistryBottleUltravasculine': 'ChemistryBottleCharcoal',
    'ChemistryBottleStellibinin': 'DHBottleAtropine', 'ChemistryBottleSynaptizine': 'DHBottleMidazolam',
    'ChemistryBottleAmbuzol': 'DHBottleKetorolac', 'ChemistryBottleAmbuzolPlus': 'DHBottleKetorolac',
    'ChemistryBottleBarozine': 'DHBottleNorepinephrine', 'ChemistryBottlePolypyryliumOligomers': 'DHBottleEtamsylate',
    'ChemistryBottleInsuzine': 'DHBottleAlbumin', 'ChemistryBottleOpporozidone': 'DHBottleAlbumin',
    'ChemistryBottleAloxadone': 'DHBottleDexamethasone', 'ChemistryBottleOculine': 'DHBottleKetorolac',
    'ChemistryBottleLipozine': 'DHBottleKetorolac', 'ChemistryBottlePsicodine': 'DHBottleMidazolam',
    'ChemistryBottleEthyloxyephedrine': 'DHBottleMidazolam', 'ChemistryBottleDiphenylmethylamine': 'DHBottleMidazolam',
    'BeakerSmallArith': 'DHBottlePrussianBlue', 'BeakerSmallBruiz': 'DHBottleTramadol',
    'BeakerSmallLace': 'DHBottleEtamsylate', 'BeakerSmallPhal': 'DHBottlePrussianBlue', 'BeakerSmallSigy': 'DHBottleDexamethasone',
    'CryoxadoneBeakerSmall': 'DHBottleAlbumin',
    # кувшины
    'JugBicaridine': 'DHJugRinger', 'JugTrico': 'DHJugRinger', 'JugDexPlusSaline': 'DHJugAlbumin',
    'JugDexalinPlus': 'DHJugAlbumin', 'JugPunctTranex': 'DHJugColloid', 'JugPuncturase': 'DHJugColloid',
    'JugPyraDerma': 'DHJugAlbumin', 'JugDermaline': 'DHJugAlbumin', 'JugDylovene': 'JugSaline', 'JugHyronalin': 'DHJugRinger',
    # автоинъекторы
    'CombatMedipen': 'DHMedipenOmnireazin', 'BruteAutoInjector': 'DHMedipenOmnireazin',
    'BurnAutoInjector': 'DHMedipenDexamethasone', 'PyraAutoInjector': 'DHMedipenDexamethasone',
    'AntiPoisonMedipen': 'DHMedipenAtropine', 'RadAutoInjector': 'DHMedipenPrussianBlue',
    'AirlossAutoInjector': 'DHMedipenNorepinephrine', 'SpaceMedipen': 'DHMedipenNorepinephrine',
    'PunctAutoInjector': 'DHMedipenEtamsylate',
}

PAT = re.compile(r'\b(' + '|'.join(sorted(MAP, key=len, reverse=True)) + r')\b')
# Строки-определения и наследование не трогаем.
SKIP_LINE = re.compile(r'^(  id: |  parent: |- id: \w+\s*$(?!))')

ROOTS = ['Resources/Prototypes', 'Resources/Maps/_DarkestHour', 'Tools/_DarkestHour']
# В этих файлах лежат определения ванильных предметов: внутри них меняем только таблицы (entityTable)
# и случайную баночку (содержимое ванильных баночек и наборов оставляем — сами они больше нигде не выдаются).
DEFINITION_FILES = {
    'Resources/Prototypes/Entities/Objects/Specific/Medical/healing.yml',
    'Resources/Prototypes/Entities/Objects/Specific/Medical/hypospray.yml',
    'Resources/Prototypes/Entities/Objects/Specific/Chemistry/chemistry-bottles.yml',
    'Resources/Prototypes/Entities/Objects/Specific/Chemistry/chemical-containers.yml',
    'Resources/Prototypes/Entities/Objects/Specific/Chemistry/chemistry.yml',
}
SELF = os.path.relpath(__file__)


def replace_line(line):
    if line.startswith('  id: ') or line.startswith('  parent: '):
        return line
    return PAT.sub(lambda m: MAP[m.group(1)], line)


def process_definitions(text):
    blocks = re.split(r'(?m)^(?=- type: )', text)
    out = []
    for b in blocks:
        if b.startswith('- type: entityTable') or re.search(r'(?m)^  id: PillCanisterRandom\s*$', b):
            b = ''.join(replace_line(l) for l in b.splitlines(keepends=True))
        out.append(b)
    return ''.join(out)


def main():
    changed = []
    for root in ROOTS:
        for dp, _, fn in os.walk(root):
            for f in fn:
                if not f.endswith(('.yml', '.py')):
                    continue
                p = os.path.join(dp, f)
                if os.path.relpath(p) == SELF or '__pycache__' in p:
                    continue
                t = open(p, encoding='utf-8').read()
                if not PAT.search(t):
                    continue
                if p in DEFINITION_FILES:
                    n = process_definitions(t)
                else:
                    n = ''.join(replace_line(l) for l in t.splitlines(keepends=True))
                if n != t:
                    open(p, 'w', encoding='utf-8').write(n)
                    changed.append(p)
    print('\n'.join(changed))


if __name__ == '__main__':
    main()
