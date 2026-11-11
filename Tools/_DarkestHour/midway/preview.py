import yaml,base64,struct,math,json,os
from PIL import Image
R='/home/claude/the-darkest-hour/Resources/'
class L(yaml.SafeLoader): pass
L.add_multi_constructor('!', lambda l,s,n: None)
d=yaml.load(open(R+'Maps/_DarkestHour/Shuttles/midway.yml'),Loader=L)
protos={}
for f in ['base.yml','parts.yml']:
    for e in yaml.load(open(R+'Prototypes/_DarkestHour/Shuttles/Midway/'+f),Loader=L): protos[e['id']]=e
tilesyml={t['id']:t for t in yaml.load(open(R+'Prototypes/_DarkestHour/Shuttles/Midway/tiles.yml'),Loader=L)}
tm=d['tilemap']
grid=[e for p in d['entities'] for e in p['entities'] if e['uid']==1][0]
tiles={}
for c in grid['components']:
    if c['type']=='MapGrid':
        for k,ch in c['chunks'].items():
            cx,cy=map(int,ch['ind'].split(','))
            b=base64.b64decode(ch['tiles']); n=len(b)//256
            for i in range(256):
                t=struct.unpack_from('<i',b,i*n)[0]
                if t: tiles[(cx*16+i%16,cy*16+i//16)]=tm[t]
x0=min(k[0] for k in tiles)-2; x1=max(k[0] for k in tiles)+3; y0=min(k[1] for k in tiles)-2; y1=max(k[1] for k in tiles)+3
W=(x1-x0)*32; H=(y1-y0)*32
img=Image.new('RGBA',(W,H),(20,24,30,255))
def px(x,y): return int((x-x0)*32), int((y1-y)*32)
for (x,y),t in tiles.items():
    sp=tilesyml[t]['sprite'].lstrip('/')
    im=Image.open(R+sp).convert('RGBA').crop((0,0,32,32))
    X,Y=px(x,y+1); img.alpha_composite(im,(X,Y))
def comp(pid,typ):
    e=protos.get(pid); out={}
    chainl=[]
    while e:
        chainl.append(e); e=protos.get(e.get('parent'))
    for e in reversed(chainl):
        for c in e.get('components',[]) or []:
            if c['type']==typ: out.update(c)
    return out
order={'Walls':1,'WallMountedItems':3,'SmallObjects':2,'Overdoors':4}
items=[]
for p in d['entities']:
    sp=comp(p['proto'],'Sprite')
    if not sp: continue
    for e in p['entities']:
        for c in e['components']:
            if c['type']=='Transform' and c.get('parent')==1:
                x,y=map(float,c['pos'].split(',')); rot=float(str(c.get('rot','0 rad')).split()[0])
                items.append((order.get(sp.get('drawdepth','Objects'),2),x,y,rot,sp))
for _,x,y,rot,sp in sorted(items,key=lambda t:t[0]):
    base=sp.get('sprite'); ox,oy=map(float,str(sp.get('offset','0,0')).split(','))
    for l in (sp.get('layers') or [{'state':sp.get('state')}]):
        if l.get('visible') is False: continue
        s=l.get('sprite',base); st=l.get('state')
        if not s or not st: continue
        meta=json.load(open(R+'Textures/'+s+'/meta.json')); sw,sh=meta['size']['x'],meta['size']['y']
        dirs=next((q.get('directions',1) for q in meta['states'] if q['name']==st),1)
        im=Image.open(R+'Textures/'+s+'/'+st+'.png').convert('RGBA')
        # direction index: S=0,N=1,E=2,W=3; rot in rad, SS14 rotation CCW
        di=0
        if dirs>=4:
            a=round(rot/(math.pi/2))%4  # 0:S facing(down),1:E? approximate
            di={0:0,1:2,2:1,3:3}[a]
        cols=im.width//sw
        fx=(di%cols)*sw; fy=(di//cols)*sh
        fr=im.crop((fx,fy,fx+sw,fy+sh))
        X=int((x+ox-x0)*32 - sw/2); Y=int((y1-(y+oy))*32 - sh/2)
        img.alpha_composite(fr,(max(0,X),max(0,Y)) if X>=0 and Y>=0 else (0,0)) if X>=0 and Y>=0 else None
img.save('/tmp/claude-0/-home-claude/b98f3588-2b1f-59b9-a305-05f4e1959a09/scratchpad/midway_preview.png')
print(W,H)
