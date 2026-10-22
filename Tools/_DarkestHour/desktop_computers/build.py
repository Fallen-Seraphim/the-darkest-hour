"""Настольные компьютеры: собирает RSI из консолей RMC-14 (CM-SS13, CC-BY-SA-3.0).

python3 build.py <RMC-14 repo> <наш repo>

Корпус всех «серых» настольных компьютеров CM одинаковый (atmos0), поэтому:
- frame — выключенный компьютер (atmos0), 4 направления;
- экраны — только пиксели, отличающиеся от корпуса (рисуются без затенения при питании), с анимацией;
- имена экранов и клавиш — как у ванильных компьютеров (Structures/Machines/computers.rsi), чтобы дочерние
  прототипы не править: их stateScreen/stateKeys ссылаются на те же имена;
- клавиши и клавиатура — пустые (клавиатура уже в корпусе), broken — разбитый экран (atmosb), panel — открытая панель.
"""
import json, os, sys
from PIL import Image, ImageChops, ImageDraw

RMC, REPO = sys.argv[1], sys.argv[2]
SRC = os.path.join(RMC, 'Resources/Textures/_RMC14/Structures/Machines/computer.rsi')
OUT = os.path.join(REPO, 'Resources/Textures/_DarkestHour/Structures/Machines/desktop_computers.rsi')

SCREENS = {
    'generic': 'terminal', 'alert-0': 'alert_0', 'alert-1': 'alert_1', 'alert-2': 'alert_2', 'tank': 'tank',
    'shuttle': 'shuttle', 'syndishuttle': 'shuttle', 'helm': 'demo_sim', 'power_monitor': 'engineering_terminal',
    'medcomp': 'medlaptop', 'explosive': 'security', 'crew': 'cmonitor', 'teleporters_station': 'atmos',
    'teleporters_syndicate': 'security', 'rdcomp': 'research', 'artifact': 'research', 'id': 'id', 'comm': 'comm',
    'comm_syndie': 'comm', 'comm_wizard': 'comm', 'comm_logs': 'comm', 'solar_screen': 'atmos', 'request': 'demo_sim',
    'request-eng': 'demo_sim', 'request-med': 'demo_sim', 'request-sci': 'demo_sim', 'request-sec': 'demo_sim',
    'request-srv': 'demo_sim', 'request-syndie': 'demo_sim', 'bounty': 'demo_sim', 'allocate': 'id', 'dna': 'operating',
    'salvjob': 'demo_sim', 'mining': 'engineering_terminal', 'cameras': 'cameras', 'transfer': 'demo_sim',
    'service': 'medlaptop', 'sensors': 'atmos', 'robot': 'research', 'xenorobot': 'research', 'aiupload': 'research',
    'ai-fixer-empty': 'research', 'ai-fixer-full': 'research', 'ai-fixer-404': 'security',
    **{f'ai-fixer-progress-{i}': 'research' for i in range(4)}, **{f'ai-fixer-purge-{i}': 'security' for i in range(4)},
}
BLANK = ['blank', 'generic_keyboard', 'generic_keys', 'atmos_key', 'syndie_key', 'power_key', 'med_key', 'security_key',
         'telesci_key', 'rd_key', 'tech_key', 'id_key', 'service_keys', 'syndie_generic_keys', 'wizard_key']

meta = json.load(open(os.path.join(SRC, 'meta.json')))
st = {s['name']: s for s in meta['states']}


def frames(name):
    im = Image.open(os.path.join(SRC, name + '.png')).convert('RGBA')
    s = st[name]
    nd = s.get('directions', 1)
    delays = s.get('delays', [[1.0]] * nd)
    nf = len(delays[0])
    w = im.width // 32
    out = []
    for d in range(nd):
        row = []
        for f in range(nf):
            i = d * nf + f
            row.append(im.crop((i % w * 32, i // w * 32, i % w * 32 + 32, i // w * 32 + 32)))
        out.append(row)
    return out, delays


def save(name, fr, delays=None):
    nf = len(fr[0])
    n = 4 * nf
    w = min(n, 8)
    h = (n + w - 1) // w
    sheet = Image.new('RGBA', (w * 32, h * 32), (0, 0, 0, 0))
    for d in range(4):
        for f in range(nf):
            i = d * nf + f
            sheet.paste(fr[d][f], (i % w * 32, i // w * 32))
    sheet.save(os.path.join(OUT, name + '.png'))
    s = {'name': name, 'directions': 4}
    if nf > 1:
        s['delays'] = delays
    states.append(s)


os.makedirs(OUT, exist_ok=True)
states = []
base, _ = frames('atmos0')
save('frame', base)

for name, src in SCREENS.items():
    fr, delays = frames(src)
    over = []
    for d in range(4):
        row = []
        for f in fr[d]:
            diff = ImageChops.difference(f, base[d][0]).convert('RGB').point(lambda v: 255 if v else 0).convert('L')
            o = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
            o.paste(f, (0, 0), diff)
            row.append(o)
        over.append(row)
    save(name, over, delays)

broken, _ = frames('atmosb')
save('broken', [[broken[d][0]] for d in range(4)])

empty = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
for name in BLANK:
    save(name, [[empty] for _ in range(4)])

# открытая панель: тёмный проём с проводами на боку корпуса (на юг — справа от экрана, на север — на задней крышке)
panel = []
for box in [(20, 15, 23, 18), (12, 9, 19, 13), (16, 12, 19, 16), (12, 12, 15, 16)]:  # S, N, E, W
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    dr = ImageDraw.Draw(im)
    dr.rectangle(box, fill=(28, 28, 32, 255))
    dr.point([(box[0] + 1, box[1] + 1), (box[0] + 2, box[3] - 1)], fill=(220, 60, 50, 255))
    dr.point([(box[2] - 1, box[1] + 1)], fill=(230, 200, 60, 255))
    panel.append([im])
save('panel', panel)

json.dump({'version': 1, 'license': 'CC-BY-SA-3.0',
           'copyright': 'Desktop computers from RMC-14 (_RMC14/Structures/Machines/computer.rsi), taken from cmss13 '
                        'at https://github.com/cmss13-devs/cmss13 ; split into frame and screen overlays, panel drawn '
                        'by The Darkest Hour',
           'size': {'x': 32, 'y': 32}, 'states': states},
          open(os.path.join(OUT, 'meta.json'), 'w'), indent=1)
print(len(states), 'states')
