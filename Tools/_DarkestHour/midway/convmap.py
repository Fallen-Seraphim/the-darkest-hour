import re,json
S='/tmp/claude-0/-home-claude/b98f3588-2b1f-59b9-a305-05f4e1959a09/scratchpad/midway'
DH='/home/claude/the-darkest-hour'
g=json.load(open(S+'/gen.json')); kinds=g['kinds']
def newid(n): return n.replace('CMUMohawkMidway','DHMidway').replace('CMUMohawk','DHMidway')
txt=open(S+'/midway.yml').read()
head,body=txt.split('entities:\n',1)
head=head.replace("engineVersion: 264.0.0","engineVersion: 264.0.0")
head=re.sub(r'CMUMohawkTile([0-9a-f]+)',r'DHMidwayTile\1',head).replace('CMShuttleTileInvisible','DHMidwayTileInvisible')
blocks=re.split(r'(?m)^(?=- proto: )',body)
out=[]; count=0; maxuid=0
for b in blocks:
    if not b.strip(): continue
    m=re.match(r"- proto: (.*)\n",b); proto=m.group(1).strip()
    if b.rstrip().endswith('...'): b=b.rstrip()[:-3].rstrip()+'\n'
    for u in re.findall(r'uid: (\d+)',b): maxuid=max(maxuid,int(u))
    if proto=="''":
        # грид: убираем компоненты CMU
        lines=b.split('\n'); res=[]; skip=False
        for l in lines:
            mm=re.match(r'    - type: (\w+)',l)
            if mm: skip=mm.group(1) in ('Dropship','MultiDeckDropship','ShipFaction','MohawkMechanisms')
            if not skip: res.append(l)
        out.append('\n'.join(res)); count+=1; continue
    k=kinds[proto]
    if k=='drop': continue
    new = k[1] if isinstance(k,list) else newid(proto)
    b=b.replace(f'- proto: {proto}\n',f'- proto: {new}\n',1)
    count+=len(re.findall(r'(?m)^  - uid: ',b))
    out.append(b)
extra=[('DHCommsRelayPortableEmpireFolded','0.5,-3.5')]
for p,pos in extra:
    maxuid+=1; count+=1
    out.append(f"- proto: {p}\n  entities:\n  - uid: {maxuid}\n    components:\n    - type: Transform\n      parent: 1\n      pos: {pos}\n")
res=head+'entities:\n'+''.join(x if x.endswith('\n') else x+'\n' for x in out)+'...\n'
res=re.sub(r'entityCount: \d+',f'entityCount: {count}',res)
open(DH+'/Resources/Maps/_DarkestHour/Shuttles/midway.yml','w').write(res)
print(count)
