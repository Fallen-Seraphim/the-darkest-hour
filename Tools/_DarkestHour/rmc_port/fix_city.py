"""Доводка порта городского декора RMC (port.py с files.txt городского набора): ссылки на прототипы RMC → наши,
графы строительства RMC убраны (у нас их нет), звуки RMC — скопированы.
Запуск после port.py: python3 Tools/_DarkestHour/rmc_port/fix_city.py <files.json>"""
import json
import os
import re
import shutil
import sys

DH = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'Resources')
RMC = '/home/claude/rmc/Resources'

SWAP = {
    'CMGirder': 'Girder', 'CMRodMetal': 'PartRodMetal1', 'CMShardGlass': 'ShardGlass', 'CMSheetMetal1': 'SheetSteel1',
    'RMCPlankWood1': 'MaterialWoodPlank1',
    'RMCLightBulbBlue': 'LightBulbCrystalBlue', 'RMCLightBulbRed': 'LightBulbCrystalRed', 'RMCLightBulb': 'LightBulb',
    'RMCLightTubeBlue': 'LightTubeCrystalBlue', 'RMCLightTubeRed': 'LightTubeCrystalRed',
    'RMCLightTubeWarm': 'LightTube', 'RMCLightTube': 'LightTube',
    'CMDrinkCanAle': 'DrinkBeerCan', 'CMDrinkCanBeerLite': 'DrinkBeerCan', 'CMDrinkCanTonic': 'DrinkTonicWaterCan',
    'RMCDrinkAlcoholRum': 'DrinkRumBottleFull', 'RMCDrinkAlcoholVodka': 'DrinkVodkaBottleFull',
    'RMCDrinkAlcoholWhiskey': 'DrinkWhiskeyBottleFull', 'RMCPaperCup': 'DrinkWaterCup',
    'RMCAlcohol': 'DrinkBottle', 'RMCCannedDrink': 'DrinkCan',
}

graphs = set(open(sys.argv[2]).read().split()) if len(sys.argv) > 2 else set()
files = ['Prototypes/_RMC14/' + f for f in json.load(open(sys.argv[1]))]
files.append('Prototypes/_RMC14/Entities/Structures/rmc_ported_bases_city.yml')
audio = set()
for rel in files:
    path = os.path.join(DH, rel)
    text = open(path, encoding='utf-8').read()
    for a, b in SWAP.items():
        text = re.sub(r'\b%s\b' % a, b, text)
    # Construction с графом RMC — прочь (вместе с полями компонента).
    lines = text.split('\n')
    out, i = [], 0
    while i < len(lines):
        if lines[i] == '  - type: Construction':
            j = i + 1
            while j < len(lines) and lines[j].startswith('    '):
                j += 1
            block = lines[i:j]
            graph = next((re.match(r'    graph: (\S+)', b).group(1) for b in block if re.match(r'    graph: ', b)), None)
            if graph is not None and graph not in graphs:
                i = j
                continue
        out.append(lines[i])
        i += 1
    text = '\n'.join(out)
    audio.update(re.findall(r'(/Audio/_RMC14/[^\s"]+)', text))
    open(path, 'w', encoding='utf-8').write(text)

for a in sorted(audio):
    src, dst = RMC + a, DH + a
    if os.path.exists(dst):
        continue
    if not os.path.exists(src):
        print('MISSING AUDIO', a)
        continue
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy(src, dst)
    for lic in ('attributions.yml',):
        p = os.path.join(os.path.dirname(src), lic)
        q = os.path.join(os.path.dirname(dst), lic)
        if os.path.exists(p) and not os.path.exists(q):
            shutil.copy(p, q)
    print('audio', a)
