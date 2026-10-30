"""Порт декора RMC-14 в The Darkest Hour: прототипы (с вычищенными RMC-компонентами) + текстуры."""
import os, re, sys, json, shutil, glob, yaml

RMC = '/home/claude/rmc/Resources'
DH = '/home/claude/the-darkest-hour/Resources'
SP = os.path.dirname(os.path.abspath(__file__))
COMPS = set(open(os.path.join(SP, '..', 'dhcomps.txt')).read().split())

FILES = open(os.path.join(SP, '..', 'rmcfiles.txt')).read().split()
PROTO = 'Prototypes/_RMC14/'


class L(yaml.SafeLoader):
    pass


def _tag(loader, suffix, node):
    if isinstance(node, yaml.MappingNode):
        return loader.construct_mapping(node, deep=True)
    if isinstance(node, yaml.SequenceNode):
        return loader.construct_sequence(node, deep=True)
    return loader.construct_scalar(node)


L.add_multi_constructor('!', _tag)


def load(path):
    try:
        return yaml.load(open(path, encoding='utf-8-sig'), Loader=L) or []
    except Exception as e:
        print('YAML', path, e)
        return []


# все сущности RMC (для родителей) и DH
rmc_ents, rmc_file = {}, {}
for f in glob.glob(RMC + '/Prototypes/_RMC14/**/*.yml', recursive=True):
    for d in load(f):
        if isinstance(d, dict) and d.get('type') == 'entity':
            rmc_ents[d['id']] = d
            rmc_file[d['id']] = os.path.relpath(f, RMC + '/' + PROTO)

dh_ids = set()
for f in glob.glob(DH + '/Prototypes/**/*.yml', recursive=True):
    for line in open(f, encoding='utf-8-sig', errors='ignore'):
        m = re.match(r'^  id: (\S+)', line)
        if m:
            dh_ids.add(m.group(1))

DH_MODSETS = set()
for f in glob.glob(DH + '/Prototypes/Damage/**/*.yml', recursive=True):
    for d in load(f):
        if isinstance(d, dict) and d.get('type') == 'damageModifierSet':
            DH_MODSETS.add(d['id'])


def parents(d):
    p = d.get('parent')
    return [] if p is None else ([p] if isinstance(p, str) else list(p))


# родители из других файлов RMC: переносим только нужные сущности в отдельный файл
REMAP = {'CMCatwalk': 'Catwalk', 'CMBaseStructureCorrodible': 'BaseStructure'}
files = list(FILES)
ids = {d['id'] for f in files for d in load(RMC + '/' + PROTO + f) if isinstance(d, dict) and d.get('type') == 'entity'}
extra = []
queue = [d for f in files for d in load(RMC + '/' + PROTO + f) if isinstance(d, dict) and d.get('type') == 'entity']
while queue:
    d = queue.pop()
    for p in parents(d):
        if p in ids or p in dh_ids or p in REMAP or p in extra:
            continue
        if p in rmc_ents:
            extra.append(p)
            queue.append(rmc_ents[p])
        else:
            print('UNKNOWN PARENT', p, 'in', d['id'])
print('extra parents', extra)

removed = {}
sprites = set()
COMP_RE = re.compile(r'^  - type: (\w+)\s*$')


def fix_block(lines):
    """Строки одной сущности: убрать неизвестные компоненты, damageContainer → Injurable."""
    out, i = [], 0
    in_comps = False
    while i < len(lines):
        line = lines[i]
        if re.match(r'^  components:', line):
            in_comps = True
            out.append(line)
            i += 1
            continue
        m = COMP_RE.match(line) if in_comps else None
        if not m:
            if re.match(r'^  \w', line):
                in_comps = False
            out.append(line)
            i += 1
            continue
        j = i + 1
        while j < len(lines) and not COMP_RE.match(lines[j]) and not re.match(r'^  \w', lines[j]) and not re.match(r'^\S', lines[j]):
            j += 1
        # хвостовые пустые строки/комментарии отдаём наружу
        k = j
        while k > i + 1 and (lines[k - 1].strip() == '' or lines[k - 1].lstrip().startswith('#') and not lines[k - 1].startswith('    ')):
            k -= 1
        comp, body, tail = m.group(1), lines[i + 1:k], lines[k:j]
        if comp not in COMPS:
            removed[comp] = removed.get(comp, 0) + 1
        elif comp == 'Damageable':
            container = [b for b in body if re.match(r'^    damageContainer:', b)]
            rest = [b for b in body if not re.match(r'^    damageContainer:', b)]
            ms = [b for b in rest if re.match(r'^    damageModifierSet:', b)]
            for b in ms:
                v = b.split(':', 1)[1].strip()
                if v != 'null' and v not in DH_MODSETS:
                    print('modset', v, '-> убран')
                    rest.remove(b)
            out.append(line)
            out.extend(rest)
            if container:
                out.append('  - type: Injurable\n')
                out.extend(container)
        else:
            out.append(line)
            out.extend(body)
        out.extend(tail)
        i = j
    return out


def entity_block(pid):
    text = open(RMC + '/' + PROTO + rmc_file[pid], encoding='utf-8-sig').read().replace('\r\n', '\n')
    for b in re.split(r'(?m)^(?=- )', text):
        if re.search(r'(?m)^  id: ' + re.escape(pid) + r'\s*$', b):
            return b.rstrip('\n') + '\n\n'
    raise KeyError(pid)


def port(rel, text=None):
    src = RMC + '/' + PROTO + rel
    if text is None:
        text = open(src, encoding='utf-8-sig').read().replace('\r\n', '\n')
    for a, b in REMAP.items():
        text = re.sub(r'(?m)^(  parent: )' + a + r'\s*$', r'\g<1>' + b, text)
    lines = text.splitlines(keepends=True)
    blocks, cur = [], []
    for line in lines:
        if re.match(r'^- ', line) and cur:
            blocks.append(cur)
            cur = []
        cur.append(line)
    if cur:
        blocks.append(cur)
    out = []
    for b in blocks:
        out.extend(fix_block(b))
    res = ''.join(out)
    for m in re.finditer(r'(?:sprite|rsi): *(_RMC14/[^\s]+\.rsi)', res):
        sprites.add(m.group(1))
    for m in re.finditer(r'(?:sprite|rsi): *([A-Z][^\s]+\.rsi)', res):
        if not os.path.isdir(DH + '/Textures/' + m.group(1)):
            print('MISSING vanilla rsi', m.group(1), rel)
    dst = DH + '/' + PROTO + rel
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    open(dst, 'w', encoding='utf-8').write(res)


for f in files:
    port(f)
if extra:
    port('Entities/Structures/rmc_ported_bases.yml',
         '# Базовые прототипы RMC-14, от которых наследуется портированный декор.\n\n' + ''.join(entity_block(p) for p in reversed(extra)))

missing_tex = []
for s in sorted(sprites):
    a, b = RMC + '/Textures/' + s, DH + '/Textures/' + s
    if not os.path.isdir(a):
        missing_tex.append(s)
        continue
    if not os.path.isdir(b):
        shutil.copytree(a, b)
print('files', len(files))
print('removed comps', removed)
print('sprites', len(sprites), 'missing in rmc clone', missing_tex)
json.dump(files, open(os.path.join(SP, 'files.json'), 'w'))
