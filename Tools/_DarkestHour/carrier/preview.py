"""Превью авианосца в PNG (плитка, стены, двери, крупные машины, «Мидуэй» на стоянке). Только для проверки схемы."""
import base64, json, math, struct, sys, yaml, pickle, glob, os
from PIL import Image, ImageDraw
R = 'Resources/'
OUT = sys.argv[1] if len(sys.argv) > 1 else 'carrier_preview.png'
class L(yaml.CSafeLoader): pass
L.add_multi_constructor('!', lambda l, s, n: None)
d = yaml.load(open(R + 'Maps/_DarkestHour/Shuttles/imperial_carrier.yml'), Loader=L)
tiledefs = {}
for f in glob.glob(R + 'Prototypes/**/*.yml', recursive=True):
    try:
        for x in yaml.load(open(f, encoding='utf-8-sig'), Loader=L) or []:
            if isinstance(x, dict) and x.get('type') == 'tile':
                tiledefs[x['id']] = x
    except Exception:
        pass
def tsprite(t):
    while t in tiledefs:
        if tiledefs[t].get('sprite'): return tiledefs[t]['sprite']
        t = tiledefs[t].get('parent')
tm = d['tilemap']
grid = [e for p in d['entities'] for e in p['entities'] if e['uid'] == 1][0]
tiles = {}
for c in grid['components']:
    if c['type'] == 'MapGrid':
        for k, ch in c['chunks'].items():
            cx, cy = map(int, ch['ind'].split(','))
            b = base64.b64decode(ch['tiles']); n = len(b) // 256
            for i in range(256):
                t = struct.unpack_from('<i', b, i * n)[0]
                if t: tiles[(cx * 16 + i % 16, cy * 16 + i // 16)] = tm[t]
S = 16
x0 = min(k[0] for k in tiles) - 2; x1 = max(k[0] for k in tiles) + 3
y0 = min(k[1] for k in tiles) - 2; y1 = max(k[1] for k in tiles) + 3
img = Image.new('RGBA', ((x1 - x0) * S, (y1 - y0) * S), (12, 14, 20, 255))
cache = {}
def px(x, y): return (x - x0) * S, (y1 - y - 1) * S
for (x, y), t in tiles.items():
    sp = tsprite(t)
    if sp not in cache:
        try: cache[sp] = Image.open(R + sp.lstrip('/')).convert('RGBA').crop((0, 0, 32, 32)).resize((S, S))
        except Exception: cache[sp] = Image.new('RGBA', (S, S), (90, 90, 90, 255))
    img.alpha_composite(cache[sp], px(x, y))
dr = ImageDraw.Draw(img)
COL = {'DHCarrierHull': (60, 64, 72), 'DHCarrierWall': (95, 100, 110), 'DHCarrierWallWhite': (200, 205, 210),
       'DHCarrierWindow': (110, 170, 220)}
BIG = {'DHFusionReactorShip': 3, 'DHSupplyElevator': 5}
mark = {'Airlock': (60, 200, 90), 'SpawnPoint': None, 'Cable': None, 'Poweredlight': (255, 240, 150),
        'APCBasic': (255, 120, 40), 'DHHangarAirSeal': (80, 160, 255)}
for p in d['entities']:
    pr = p['proto']
    for e in p['entities']:
        tr = [c for c in e.get('components', []) if c['type'] == 'Transform']
        if not tr or 'pos' not in tr[0] or tr[0].get('parent') != 1: continue
        x, y = map(float, tr[0]['pos'].split(',')); X, Y = math.floor(x), math.floor(y)
        if pr in COL:
            dr.rectangle([*px(X, Y), px(X, Y)[0] + S - 1, px(X, Y)[1] + S - 1], fill=COL[pr]); continue
        col = next((v for k, v in mark.items() if k in pr), 'x')
        if col is None: continue
        if col == 'x':
            n = BIG.get(pr, 1)
            a = px(X - n // 2, Y + n // 2)
            dr.rectangle([a[0] + 2, a[1] + 2, a[0] + n * S - 3, a[1] + n * S - 3], outline=(240, 80, 80))
        else:
            a = px(X, Y)
            dr.rectangle([a[0] + 4, a[1] + 4, a[0] + S - 5, a[1] + S - 5], fill=col)
mid = '/tmp/claude-0/-home-claude/b98f3588-2b1f-59b9-a305-05f4e1959a09/scratchpad/midway_preview.png'
if os.path.exists(mid):
    m = Image.open(mid).convert('RGBA'); m = m.resize((m.width // 2, m.height // 2))
    # превью «Мидуэя»: от x = -10 до 10, от y = -14 до 12 его клеток (по 32 px), центр на (-14, 34)
    img.alpha_composite(m, (px(-14 - 10, 34 + 12)[0], px(-14 - 10, 34 + 12)[1] + S))
img.save(OUT)
print(OUT, img.size)
