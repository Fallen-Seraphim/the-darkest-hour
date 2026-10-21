"""Индекс прототипов: id -> тип, файл, строки, родители; все скалярные ссылки с путём."""
import os, sys, pickle, yaml
from yaml import CSafeLoader as L

REPO = os.environ.get('DH_REPO') or os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../..'))
ROOT = os.path.join(REPO, 'Resources/Prototypes')


def scal(n):
    return n.value if isinstance(n, yaml.ScalarNode) else None


def walk(node, path, out):
    if isinstance(node, yaml.ScalarNode):
        out.append((tuple(path), node.value, node.start_mark.line))
    elif isinstance(node, yaml.SequenceNode):
        for i, c in enumerate(node.value):
            walk(c, path + [('#', i)], out)
    elif isinstance(node, yaml.MappingNode):
        for k, v in node.value:
            kv = scal(k)
            # ключи словаря тоже ссылки (инвентари автоматов, materials и т.п.)
            out.append((tuple(path + [('key', kv)]), kv, k.start_mark.line))
            walk(v, path + [kv], out)


def build():
    protos = {}   # (type, id) -> dict
    byid = {}     # id -> list of (type,id)
    refs = []     # (file, type, id, path, value, line)
    errors = []
    for dp, dn, fn in os.walk(ROOT):
        for f in fn:
            if not f.endswith('.yml'):
                continue
            p = os.path.join(dp, f)
            rel = os.path.relpath(p, REPO)
            try:
                with open(p, encoding='utf-8-sig') as fh:
                    root = yaml.compose(fh, Loader=L)
            except Exception as e:
                errors.append((rel, str(e)[:200]))
                continue
            if not isinstance(root, yaml.SequenceNode):
                continue
            for item in root.value:
                if not isinstance(item, yaml.MappingNode):
                    continue
                d = {scal(k): v for k, v in item.value}
                t = scal(d.get('type')) if 'type' in d else None
                pid = scal(d.get('id')) if 'id' in d else None
                par = []
                if 'parent' in d:
                    pn = d['parent']
                    if isinstance(pn, yaml.ScalarNode):
                        par = [pn.value]
                    elif isinstance(pn, yaml.SequenceNode):
                        par = [scal(x) for x in pn.value]
                abstract = 'abstract' in d and scal(d['abstract']) == 'true'
                key = (t, pid)
                protos[key] = dict(type=t, id=pid, file=rel, start=item.start_mark.line,
                                   end=item.end_mark.line, parents=par, abstract=abstract)
                byid.setdefault(pid, []).append(key)
                out = []
                walk(item, [], out)
                for path, val, line in out:
                    if path and path[-1] in ('type', 'id') and len(path) == 1:
                        continue
                    refs.append((rel, t, pid, path, val, line))
    return protos, byid, refs, errors


if __name__ == '__main__':
    protos, byid, refs, errors = build()
    with open(sys.argv[1], 'wb') as fh:
        pickle.dump((protos, byid, refs), fh)
    print(len(protos), 'protos', len(refs), 'refs', len(errors), 'errors')
    for e in errors[:10]:
        print(e)
