"""Каскадное вырезание прототипов. python3 cut.py <deleted.json> [--dry]
deleted.json: {"seed": [[type,id],...]} — начальный список; каскад дописывает сам."""
import json, os, sys, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dhidx
from yedit import Editor, navigate

REPO = dhidx.REPO

# одиночные поля, при удалении цели которых удаляется весь прототип
DELETE_PROTO_FIELDS = {
    ('latheRecipe', ('result',)),
    ('listing', ('productEntity',)),
    ('cargoProduct', ('product',)),
    ('construction', ('graph',)),
    ('construction', ('startNode',)),
    ('stack', ('spawn',)),
}
KEY_ITEM = {'id', 'proto', 'prototype', 'item', 'entity', 'Id', 'tableId'}


def ref_kind(t, path, value, deleted_types, kept_types):
    strs = [p for p in path if isinstance(p, str)]
    if 'tags' in strs or 'tag' in strs[-1:]:
        return 'Tag' if 'Tag' in deleted_types else None
    if t == 'latheRecipePack' and strs[:1] == ['recipes']:
        return 'latheRecipe'
    if t == 'technology' and strs[:1] == ['recipeUnlocks']:
        return 'latheRecipe'
    if t == 'technology' and strs[:1] == ['technologyPrerequisites']:
        return 'technology'
    if t == 'entity' and 'recipes' in strs:
        return 'latheRecipe'
    if 'staticPacks' in strs or 'dynamicPacks' in strs or 'packs' in strs:
        return 'latheRecipePack'
    if t == 'construction' and strs[:1] in (['graph'], ['startNode'], ['targetNode']):
        return 'constructionGraph' if strs[:1] == ['graph'] else None
    if 'entity' in deleted_types:
        return 'entity'
    # другой тип — только если такого id среди живых нет
    if kept_types:
        return None  # такой id есть у живого прототипа другого типа — не трогаем
    cand = list(deleted_types)
    return cand[0] if len(cand) == 1 else None


def main():
    cfg = json.load(open(sys.argv[1]))
    dry = '--dry' in sys.argv
    deleted = set(tuple(x) for x in cfg['seed'])
    manual_keep_paths = set()
    it = 0
    while True:
        it += 1
        protos, byid, refs, errors = dhidx.build()
        assert not errors, errors
        present = {k for k in protos if k in deleted}
        dtypes = collections.defaultdict(set)
        for t, i in deleted:
            dtypes[i].add(t)
        ktypes = collections.defaultdict(set)
        for (t, i) in protos:
            if (t, i) not in deleted:
                ktypes[i].add(t)

        edits = collections.defaultdict(list)  # file -> [(kind, top_start_line, path)]
        newdel = set()
        manual = []
        for f, t, pid, path, val, line in refs:
            if (t, pid) in deleted:
                continue
            if val not in dtypes:
                continue
            kind = ref_kind(t, path, val, dtypes[val], ktypes[val])
            if kind is None:
                continue
            if (kind, val) not in deleted:
                continue
            top = protos[(t, pid)]['start']
            strs = tuple(p for p in path if isinstance(p, str))
            # родитель удалён → удалить и ребёнка
            if path[:1] == ('parent',):
                newdel.add((t, pid))
                continue
            if t == 'entity' and strs[-1:] == ('tableId',):
                newdel.add((t, pid))
                continue
            if (t, strs) in DELETE_PROTO_FIELDS:
                newdel.add((t, pid))
                continue
            if t == 'constructionGraph' and strs[-1:] == ('entity',):
                newdel.add((t, pid))
                continue
            if t == 'entity' and len(path) >= 3 and path[0] == 'components' and path[-1] in ('prototype', 'stealGroup', 'pack') \
                    and len(path) == 3:
                newdel.add((t, pid))
                continue
            # контейнер с одним EntSelector: containers/<name>/id
            if t == 'entity' and len(strs) >= 3 and strs[-3] == 'containers' and strs[-1] == 'id' \
                    and not isinstance(path[-2], tuple):
                newdel.add((t, pid))
                continue
            if isinstance(path[-1], tuple) and path[-1][0] == '#':
                edits[f].append(('seq', top, path))
                continue
            if isinstance(path[-1], tuple) and path[-1][0] == 'key':
                edits[f].append(('pair', top, path[:-1] + (path[-1][1],)))
                continue
            if path[-1] in KEY_ITEM and len(path) >= 2 and isinstance(path[-2], tuple) and path[-2][0] == '#':
                edits[f].append(('seq', top, path[:-1]))
                continue
            if t == 'startingGear' and len(path) == 2 and path[0] == 'equipment':
                edits[f].append(('pair', top, path))
                continue
            manual.append((t, pid, path, val, f, line))

        for k in present:
            edits[protos[k]['file']].append(('top', protos[k]['start'], ()))

        newdel -= deleted
        print(f'iter {it}: protos to drop {len(present)}, ref edits {sum(len(v) for v in edits.values())}, '
              f'cascade +{len(newdel)}, manual {len(manual)}')
        for x in sorted(newdel):
            print('  cascade', x)
        deleted |= newdel
        if dry:
            for m in manual:
                print('  MANUAL', m)
            if not newdel:
                break
            continue
        if not edits and not newdel:
            for m in manual:
                print('  MANUAL', m)
            break
        problems = []
        for f, lst in edits.items():
            p = os.path.join(REPO, f)
            text = open(p, encoding='utf-8-sig').read()
            ed = Editor(text)
            idx_by_line = {n.start_mark.line: j for j, n in enumerate(ed.root.value)}
            for kind, top, path in lst:
                j = idx_by_line[top]
                full = (('#', j),) + tuple(path)
                try:
                    if kind == 'top':
                        ed.remove_top(j)
                    elif kind == 'seq':
                        ed.remove_seq_item(full)
                    else:
                        ed.remove_pair(full)
                except Exception as e:
                    problems.append((f, kind, path, repr(e)))
            problems += [(f,) + tuple(x) for x in ed.problems]
            out = ed.result()
            if out.strip() == '':
                os.remove(p)
            else:
                open(p, 'w', encoding='utf-8').write(out)
        for pr in problems:
            print('  PROBLEM', pr)
        if problems:
            break
        if not newdel and not any(e for e in edits.values()):
            break
    json.dump({'seed': sorted(deleted)}, open(sys.argv[1].replace('.json', '.out.json'), 'w'), indent=0)


if __name__ == '__main__':
    main()
