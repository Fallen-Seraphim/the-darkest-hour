import pickle,copy
S='/tmp/claude-0/-home-claude/b98f3588-2b1f-59b9-a305-05f4e1959a09/scratchpad/midway'
defs=pickle.load(open(S+'/defs.pkl','rb'))
def parents(x):
    p=x.get('parent')
    if p is None: return []
    return p if isinstance(p,list) else [p]
def chain(i,seen=None):
    out=[i]
    x=defs.get(i,({},None))[0]
    for p in parents(x): out+=chain(p)
    return out
def mergemap(a,b):
    r=copy.deepcopy(a)
    for k,v in b.items():
        if isinstance(v,dict) and isinstance(r.get(k),dict): r[k]=mergemap(r[k],v)
        else: r[k]=copy.deepcopy(v)
    return r
def resolve(i):
    x=defs.get(i,({},None))[0]
    comps={}
    for p in parents(x):
        for t,c in resolve(p).items():
            comps[t]=mergemap(comps[t],c) if t in comps else copy.deepcopy(c)
    for c in x.get('components',[]) or []:
        t=c['type']; c2={k:v for k,v in c.items() if k!='type'}
        comps[t]=mergemap(comps[t],c2) if t in comps else c2
    # RemoveComponents
    return comps
