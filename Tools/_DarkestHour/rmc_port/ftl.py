import json,os,collections
from ru import TR, suffix
D='/home/claude/the-darkest-hour/Resources'
data=json.load(open('names.json'))
by=collections.defaultdict(list)
miss=set()
def tr(s):
    s=s.strip()
    if s not in TR: miss.add(s); return s
    return TR[s]
for e,f,n,de,s in data:
    lines=[f'ent-{e} = {tr(n)}']
    if de: lines.append(f'    .desc = {tr(de)}')
    if s:
        s=s.strip()
        try:
            sf='ставить на восток или запад' if s.startswith('Use East, West') else suffix(s)
            lines.append(f'    .suffix = {sf}')
        except KeyError as k:
            print('skip suffix',e,s)
    by[f].append('\n'.join(lines))
for f,entries in by.items():
    rel=os.path.relpath(f,D+'/Prototypes')[:-4]+'.ftl'
    out=D+'/Locale/ru-RU/ss14-ru/prototypes/'+rel
    os.makedirs(os.path.dirname(out),exist_ok=True)
    open(out,'w').write('\n\n'.join(entries)+'\n')
    print(rel,len(entries))
print('missing',miss)
