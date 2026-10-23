"""Points the station hardsuits at the recoloured Monolith sprites (built by build.py) and gives them Monolith's
armour, speed, explosion and pressure values. Edits the vanilla prototypes in place; safe to run again.

Run from the repository root with the path to a Monolith checkout:
    python3 Tools/_DarkestHour/hardsuits/apply.py /path/to/monolith"""
import glob
import os
import re
import sys

import yaml

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
MARK = '# DarkestHour: скафандр Monolith в цветах Metallistick (Tools/_DarkestHour/hardsuits)'

# outer id -> (outer rsi, helmet id, helmet rsi)
SUITS = {
    'ClothingOuterHardsuitCap': ('capspace', 'ClothingHeadHelmetHardsuitCap', 'capspace'),
    'ClothingOuterHardsuitSecurity': ('security', 'ClothingHeadHelmetHardsuitSecurity', 'security'),
    'ClothingOuterHardsuitSecurityRed': ('security-red', 'ClothingHeadHelmetHardsuitSecurityRed', 'security-red'),
    'ClothingOuterHardsuitWarden': ('security-warden', 'ClothingHeadHelmetHardsuitWarden', 'security-warden'),
    'ClothingOuterHardsuitBrigmedic': ('brigmedic', 'ClothingHeadHelmetHardsuitBrigmedic', 'brigmedic'),
    'ClothingOuterHardsuitMedical': ('medical', 'ClothingHeadHelmetHardsuitMedical', 'medical'),
    'ClothingOuterHardsuitVoidParamed': ('paramed', 'ClothingHeadHelmetVoidParamed', 'paramedhelm'),
    'ClothingOuterHardsuitRd': ('rd', 'ClothingHeadHelmetHardsuitRd', 'rd'),
    'ClothingOuterHardsuitEngineeringWhite': ('engineering-white', 'ClothingHeadHelmetHardsuitEngineeringWhite',
                                              'engineering-white'),
    'ClothingOuterHardsuitEngineering': ('engineering', 'ClothingHeadHelmetHardsuitEngineering', 'engineering'),
    'ClothingOuterHardsuitAtmos': ('atmospherics', 'ClothingHeadHelmetHardsuitAtmos', 'atmospherics'),
    'ClothingOuterHardsuitSalvage': ('salvage', 'ClothingHeadHelmetHardsuitSalvage', 'salvage'),
    'ClothingOuterHardsuitSpatio': ('spatio', 'ClothingHeadHelmetHardsuitSpatio', 'spatiohelm'),
}
STATS = ['Armor', 'ClothingSpeedModifier', 'ExplosionResistance', 'PressureProtection']


class Loader(yaml.SafeLoader):
    pass


def _any(loader, suffix, node):
    if isinstance(node, yaml.MappingNode):
        return loader.construct_mapping(node, deep=True)
    if isinstance(node, yaml.SequenceNode):
        return loader.construct_sequence(node, deep=True)
    return loader.construct_scalar(node)


Loader.add_multi_constructor('!', _any)


def load_entities(root):
    ents = {}
    for f in glob.glob(os.path.join(root, 'Resources/Prototypes/Entities/Clothing/**/*.yml'), recursive=True):
        try:
            docs = yaml.load(open(f, encoding='utf-8'), Loader=Loader)
        except yaml.YAMLError:
            continue
        for d in docs or []:
            if isinstance(d, dict) and d.get('type') == 'entity':
                ents[d['id']] = d
    return ents


def resolved(ents, eid):
    out = {}

    def walk(i):
        e = ents.get(i)
        if not e:
            return
        p = e.get('parent')
        for pp in ([p] if isinstance(p, str) else (p or [])):
            walk(pp)
        for c in e.get('components') or []:
            out[c['type']] = c
    walk(eid)
    return out


def dump_component(comp):
    body = {k: v for k, v in comp.items() if k != 'type'}
    text = yaml.safe_dump(body, sort_keys=False, default_flow_style=False).rstrip('\n')
    lines = ['  - type: ' + comp['type']] + ['    ' + ln for ln in text.split('\n')]
    return lines


def entity_span(lines, eid):
    """Start (the '- type: entity' line) and end (exclusive) of the entity block with this id."""
    for i, ln in enumerate(lines):
        if re.match(r'^  id: ' + re.escape(eid) + r'\s*(#.*)?$', ln):
            start = i
            while not lines[start].startswith('- type:'):
                start -= 1
            end = i + 1
            while end < len(lines) and not lines[end].startswith('- type:') and not lines[end].startswith('#'):
                end += 1
            while end > start and lines[end - 1].strip() == '':
                end -= 1
            return start, end
    return None


def patch(path, eid, sprite_old, sprite_new, stats):
    lines = open(path, encoding='utf-8').read().split('\n')
    span = entity_span(lines, eid)
    if span is None:
        return False
    start, end = span
    block = lines[start:end]
    # sprite paths
    pat = re.compile(r'(?<!_DarkestHour/)' + re.escape(sprite_old))
    block = [pat.sub(sprite_new, ln) + (' ' + MARK if pat.search(ln) and MARK not in ln else '') for ln in block]
    # drop our own components that we override, then append Monolith's values
    out, skip = [], False
    for ln in block:
        m = re.match(r'^  - type: (\w+)', ln)
        if m:
            skip = m.group(1) in stats
        elif not ln.startswith('    '):
            skip = False
        if not skip:
            out.append(ln)
    if 'components:' not in '\n'.join(out):
        out.append('  components:')
    if stats:
        out.append('  # DarkestHour: защита, скорость и давление — как у Monolith (Tools/_DarkestHour/hardsuits/apply.py)')
    for name, comp in stats.items():
        out += dump_component(comp)
    lines[start:end] = out
    open(path, 'w', encoding='utf-8').write('\n'.join(lines))
    return True


def find_file(eid):
    for f in glob.glob(os.path.join(ROOT, 'Resources/Prototypes/Entities/Clothing/**/*.yml'), recursive=True):
        if re.search(r'^  id: ' + re.escape(eid) + r'\s*$', open(f, encoding='utf-8').read(), re.M):
            return f
    raise SystemExit('not found: ' + eid)


def main(mono):
    mono_ents = load_entities(mono)
    ours = load_entities(ROOT)
    for outer, (orsi, helmet, hrsi) in SUITS.items():
        for eid, folder, rsi in ((outer, 'OuterClothing', orsi), (helmet, 'Head', hrsi)):
            ours_c = resolved(ours, eid)
            mono_c = resolved(mono_ents, eid)
            stats = {k: mono_c[k] for k in STATS if k in mono_c and mono_c[k] != ours_c.get(k)}
            sprite_old = 'Clothing/%s/Hardsuits/%s.rsi' % (folder, rsi)
            sprite_new = '_DarkestHour/' + sprite_old
            f = find_file(eid)
            if not patch(f, eid, sprite_old, sprite_new, stats):
                raise SystemExit('could not patch ' + eid)
            print(eid, os.path.relpath(f, ROOT), ', '.join(stats) or '-')


if __name__ == '__main__':
    main(sys.argv[1])
