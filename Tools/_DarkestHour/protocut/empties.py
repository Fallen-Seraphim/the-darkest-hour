"""Опустевшие после вырезания коллекции: удалить прототип / ключ. Возвращает список удалённых прототипов в json."""
import json, os, subprocess, sys, yaml
OUT = os.path.abspath(sys.argv[1])
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from yedit import Editor

REPO = os.environ.get('DH_REPO') or os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../..'))
os.chdir(REPO)
files = subprocess.run(['git', 'diff', '--name-only', os.environ.get('DH_BASE', 'origin/master')], capture_output=True, text=True).stdout.split()

REMOVE_PAIR_KEYS = {'borg_module', 'inhand', 'special', 'equipment', 'recipeUnlocks', 'storage'}
deleted = []
report = []
for f in files:
    if not f.endswith('.yml') or not os.path.exists(f):
        continue
    text = open(f, encoding='utf-8-sig').read()
    ed = Editor(text)
    root = ed.root
    if root is None or not isinstance(root, yaml.SequenceNode):
        continue
    changed = False
    for ti, top in enumerate(root.value):
        d = {k.value: v for k, v in top.value}
        t = d['type'].value if 'type' in d else None
        pid = d['id'].value if 'id' in d else None
        found = []

        def walk(n, path):
            if isinstance(n, yaml.MappingNode):
                for k, v in n.value:
                    if isinstance(v, yaml.ScalarNode) and v.value == '' and v.tag == 'tag:yaml.org,2002:null':
                        found.append(path + [k.value])
                    walk(v, path + [k.value])
            elif isinstance(n, yaml.SequenceNode):
                for i, c in enumerate(n.value):
                    walk(c, path + [('#', i)])
        walk(top, [])
        action = None
        for p in found:
            strs = [x for x in p if isinstance(x, str)]
            last = strs[-1]
            if t == 'borgType' and last == 'defaultModules':
                ln = None
                for k, v in top.value:
                    if k.value == 'defaultModules':
                        ln = k.start_mark.line
                line = ed.lines[ln]
                ed.spans.append((ed.offs[ln], ed.offs[ln + 1], line.rstrip('\n').rstrip() + ' []\n'))
                changed = True
                continue
            # прототип целиком — только когда опустело его содержимое по смыслу
            whole = (
                (t == 'entity' and 'containers' in strs and strs[-1] in ('children', 'ItemCabinet') and strs.index('containers') == len(strs) - 3 + (strs[-1] == 'ItemCabinet'))
                or (t == 'entityTable' and strs == ['table', 'children'])
                or (t == 'vendingMachineInventory' and last == 'startingInventory')
                or (t == 'latheRecipePack' and last == 'recipes')
                or (t == 'entity' and last in ('prototypes',) and 'components' in strs)
                or (t == 'entity' and last == 'hands')
                or (t == 'entity' and last == 'spawn' and 'thresholds' in strs)
                or (t == 'entity' and strs[-2:] == ['table', 'children'] and 'components' in strs and len(strs) == 3)
            )
            if not whole and last == 'children' and len(p) >= 3 and isinstance(p[-2], tuple):
                ed.remove_seq_item([('#', ti)] + p[:-1])
                changed = True
                continue
            if not whole:
                if last == 'rarePrototypes':
                    ed.remove_pair([('#', ti)] + p)
                    changed = True
                    continue
                ed.remove_pair([('#', ti)] + p)
                report.append(('PAIR', f, t, pid, '/'.join(map(str, p))))
                changed = True
                continue
            action = 'delete'
            report.append((f, t, pid, '/'.join(map(str, p))))
        if action == 'delete':
            ed.remove_top(ti)
            deleted.append([t, pid])
            changed = True
    if ed.problems:
        print('PROBLEMS', f, ed.problems)
    if changed:
        open(f, 'w', encoding='utf-8').write(ed.result())
for r in report:
    print('DEL', r)
json.dump(deleted, open(OUT, 'w'))
