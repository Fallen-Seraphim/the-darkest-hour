import glob,json,os,yaml
exec(open('port.py').read().split('# все сущности RMC')[0])
D='/home/claude/the-darkest-hour/Resources'
ents={};efile={}
for f in sorted(glob.glob(D+'/Prototypes/_RMC14/Entities/Structures/**/*.yml',recursive=True)):
    if 'memorial' in f: continue
    for d in load(f):
        if isinstance(d,dict) and d.get('type')=='entity': ents[d['id']]=d; efile[d['id']]=f
dhnames={}
def field(e,k):
    d=ents.get(e)
    if d is None: return None
    if k in d: return d[k]
    if k=='suffix': return None
    p=d.get('parent'); ps=[] if p is None else ([p] if isinstance(p,str) else p)
    for q in ps:
        v=field(q,k)
        if v is not None: return v
    return None
out=[]
for e,d in ents.items():
    if d.get('abstract') and 'name' not in d: continue
    out.append((e,efile[e],field(e,'name'),field(e,'description'),d.get('suffix')))
json.dump(out,open('names.json','w'),ensure_ascii=False,indent=0)
u=set()
for e,f,n,de,s in out:
    for x in (n,de,s):
        if x: u.add(x.strip())
json.dump(sorted(u),open('strings.json','w'),ensure_ascii=False,indent=0)
print(len(out),len(u), sum(1 for o in out if o[2] is None))
