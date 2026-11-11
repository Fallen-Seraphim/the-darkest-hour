import sys,re,os,copy,collections,json
sys.path.insert(0,'/tmp/claude-0/-home-claude/b98f3588-2b1f-59b9-a305-05f4e1959a09/scratchpad/midway')
from merge import *
import yaml
S='/tmp/claude-0/-home-claude/b98f3588-2b1f-59b9-a305-05f4e1959a09/scratchpad/midway'
DH='/home/claude/the-darkest-hour'
need=[l.strip() for l in open(S+'/protos.txt') if l.strip() and l.strip()!="''"]

def newid(n): return n.replace('CMUMohawkMidway','DHMidway').replace('CMUMohawk','DHMidway')
def fixpath(p):
    if p.startswith('CMU14/'): return '_'+p
    return p

textures=set()
def sprite_yaml(sp, ind='    ', keepmap=False):
    """Emit a Sprite component body (without '- type: Sprite')."""
    out=[]
    path=sp.get('sprite')
    if path: out.append(f"{ind}sprite: {fixpath(path)}"); textures.add(fixpath(path))
    for k in ['drawdepth','offset','noRot','snapCardinals','overrideDir','enableOverrideDir']:
        if k in sp:
            v=sp[k]
            if isinstance(v,bool): v='true' if v else 'false'
            elif k=='offset': v=f'"{v}"'
            out.append(f"{ind}{k}: {v}")
    if 'layers' in sp:
        out.append(f"{ind}layers:")
        for L in sp['layers']:
            first=True
            L={kk:vv for kk,vv in L.items() if keepmap or kk!='map'}
            for k,v in L.items():
                pre=f"{ind}- " if first else f"{ind}  "; first=False
                if k=='sprite': v=fixpath(v); textures.add(v)
                if isinstance(v,bool): v='true' if v else 'false'
                elif isinstance(v,list): v='[ '+', '.join(f'"{x}"' for x in v)+' ]'
                elif k=='state': v=f'"{v}"'
                out.append(f"{pre}{k}: {v}")
    elif 'state' in sp:
        out.append(f'{ind}state: "{sp["state"]}"')
    return out

def own(n,t):
    for c in defs[n][0].get('components',[]) or []:
        if c['type']==t: return c
    return None

kinds={}
for n in need:
    ch=chain(n)
    if n in ('CMULifeSupportVent','CMUMohawkRampMarker') or 'CMUMohawkInternalModule' in ch: kinds[n]='drop'
    elif n=='RMCTinyFan': kinds[n]=('replace','AtmosDeviceFanTiny')
    elif n=='WarpPoint': kinds[n]=('replace','WarpPoint')
    elif n=='AU14VehicleRadioSet': kinds[n]=('replace','DHFieldPhoneEmpireDropship')
    elif 'CMComputerDropshipNavigation' in ch: kinds[n]='nav'
    elif 'CMBaseWallInvincible' in ch: kinds[n]='wall'
    elif 'CMUMohawkControl' in ch: kinds[n]='control'
    elif 'CMUMohawkDecoration' in ch: kinds[n]='deco'
    elif 'CMSeatPilot' in ch: kinds[n]='pilot'
    elif 'CMSeatPassenger' in ch: kinds[n]='seat'
    elif 'CMUMohawkLight' in ch: kinds[n]='light'
    elif 'CMUMohawkDoor' in ch: kinds[n]='door'
    elif 'CMUMohawkRampRailing' in ch: kinds[n]='railing'
    elif any(x in ch for x in ['CMUMohawkCamera','CMUMohawkIntercom','CMUMohawkMedicalCabinet']): kinds[n]='wallmount'
    elif any(x in ch for x in ['CMComputerDropship','RMCOverwatchConsole','CMUMohawkMapTable']): kinds[n]='machine'
    else: raise Exception(n)
print(collections.Counter(k if isinstance(k,str) else 'replace' for k in kinds.values()))

def name_of(n):
    for c in chain(n):
        x=defs.get(c,({},None))[0]
        if 'name' in x: return x['name']
BASE={'wall':'DHMidwayBulkheadBase','deco':'DHMidwayHullBase','control':'DHMidwayControlBase','seat':'DHMidwaySeatBase',
      'pilot':'DHMidwaySeatBase','light':'DHMidwayLightBase','door':'DHMidwayDoorBase','railing':'DHMidwayRailingBase',
      'wallmount':'DHMidwayWallmountBase','machine':'DHMidwayMachineBase','nav':'DHMidwayFlightConsoleBase'}
out=[]; locnames={}
for n in need:
    k=kinds[n]
    if not isinstance(k,str) or k=='drop': continue
    nid=newid(n)
    out.append("- type: entity")
    out.append(f"  parent: {BASE[k]}")
    out.append(f"  id: {nid}")
    nm=name_of(n)
    if k in ('wall','deco','door','control','machine','wallmount','seat','pilot'):
        out.append(f"  name: {nm}")
    locnames[nid]=nm
    out.append("  components:")
    sp=resolve(n).get('Sprite') if k not in ('wall','deco') else (own(n,'Sprite') or resolve(n).get('Sprite'))
    if k in ('seat','pilot'):
        sp=dict(sp); sp['layers']=[{'state':'passenger_chair','map':['seat']}]; sp.pop('state',None)
    if k=='light':
        sp=dict(sp); sp['layers']=[{'state':'bptube1','map':['enum.PoweredLightLayers.Base']}]; sp.pop('state',None)
    out.append("  - type: Sprite")
    out+=sprite_yaml(sp, keepmap=k in ('light','door','seat','pilot'))
    if k=='wall':
        ic=own(n,'Icon'); oc=own(n,'Occluder')
        if ic:
            out.append("  - type: Icon"); out.append(f"    sprite: {fixpath(ic['sprite'])}"); out.append(f'    state: "{ic["state"]}"')
        if oc is not None and oc.get('enabled') is False:
            out.append("  - type: Occluder"); out.append("    enabled: false")
    if k=='light':
        pl=resolve(n)['PointLight']
        out.append("  - type: PointLight"); out.append(f"    offset: \"{pl['offset']}\"")
    if k=='machine':
        fx=resolve(n).get('Fixtures')
        if fx and 'maptable' in str(sp):
            out.append("  - type: Fixtures"); out.append("    fixtures:"); out.append("      fix1:")
            out.append("        shape:"); out.append("          !type:PhysShapeAabb"); out.append(f"          bounds: \"{fx['fixtures']['fix1']['shape']['bounds']}\"")
            out.append("        density: 60"); out.append("        mask: [ MachineMask ]"); out.append("        layer: [ MidImpassable, LowImpassable ]")
    out.append("")
open(S+'/gen_entities.yml','w').write("\n".join(out))
json.dump({'kinds':{k:v for k,v in kinds.items()},'loc':locnames,'textures':sorted(textures)},open(S+'/gen.json','w'),indent=1)
print(len(out), sorted(textures))
