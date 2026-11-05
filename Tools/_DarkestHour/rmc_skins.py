"""RMC-like sprites for vanilla tools and items.

The vanilla RSIs are overwritten state by state with sprites from an RMC-14 checkout (CC-BY-SA-3.0), so every
prototype that uses them (and every variant) gets the new look without touching the YAML. A state that is not in
the table keeps its vanilla image; a state mapped to None becomes empty (an overlay that would not fit the new
shape, such as the multitool lights).

Run from the repository root after an upstream merge that brought the vanilla sprites back:
    python3 Tools/_DarkestHour/rmc_skins.py /path/to/RMC-14
"""
import json
import os
import shutil
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
TEX = os.path.join(ROOT, 'Resources/Textures')

V = 'Objects/'
R = '_RMC14/Objects/'

# vanilla rsi -> {vanilla state: (rmc rsi, rmc state) | None}
SKINS = {
    V + 'Tools/wrench.rsi': {
        'icon': (R + 'Tools/wrench.rsi', 'icon'),
        'inhand-left': (R + 'Tools/wrench.rsi', 'inhand-left'),
        'inhand-right': (R + 'Tools/wrench.rsi', 'inhand-right'),
        'equipped-BELT': (R + 'Tools/wrench.rsi', 'equipped-BELT'),
    },
    V + 'Tools/screwdriver.rsi': {
        s: (R + 'Tools/screwdriver.rsi', s)
        for s in ('screwdriver', 'screwdriver-map', 'screwdriver-screwybits', 'inhand-left', 'inhand-right',
                  'equipped-BELT')
    },
    V + 'Tools/wirecutters.rsi': {
        s: (R + 'Tools/wirecutters.rsi', s)
        for s in ('cutters', 'cutters-cutty-thingy', 'cutters-map', 'inhand-left', 'inhand-right',
                  'cutters-inhand-left', 'cutters-inhand-right', 'handle-inhand-left', 'handle-inhand-right',
                  'cutters-equipped-BELT', 'handle-equipped-BELT')
    },
    V + 'Tools/multitool.rsi': {
        'icon': (R + 'Tools/multitool.rsi', 'multitool'),
        'inhand-left': (R + 'Tools/multitool.rsi', 'inhand-left'),
        'inhand-right': (R + 'Tools/multitool.rsi', 'inhand-right'),
        'green-unlit': None, 'yellow-unlit': None, 'red-unlit': None,
        'green-inhand-left': None, 'green-inhand-right': None,
        'yellow-inhand-left': None, 'yellow-inhand-right': None,
        'red-inhand-left': None, 'red-inhand-right': None,
    },
    V + 'Tools/welder.rsi': {
        'icon': (R + 'Tools/welder.rsi', 'icon'),
        'welder_flame': (R + 'Tools/welder.rsi', 'welder_on'),
        'inhand-left': (R + 'Tools/welder.rsi', 'inhand-left'),
        'inhand-right': (R + 'Tools/welder.rsi', 'inhand-right'),
        'inhand-left-flame': (R + 'Tools/welder.rsi', 'inhand-left-on'),
        'inhand-right-flame': (R + 'Tools/welder.rsi', 'inhand-right-on'),
        'equipped-BELT': (R + 'Tools/welder.rsi', 'equipped-BELT'),
    },
    V + 'Tools/welder_industrial.rsi': {
        'icon': (R + 'Tools/welder_industrial.rsi', 'icon'),
        'welder_flame': (R + 'Tools/welder_industrial.rsi', 'welder_on'),
        'inhand-left': (R + 'Tools/welder_industrial.rsi', 'inhand-left'),
        'inhand-right': (R + 'Tools/welder_industrial.rsi', 'inhand-right'),
        'inhand-left-flame': (R + 'Tools/welder_industrial.rsi', 'inhand-left-on'),
        'inhand-right-flame': (R + 'Tools/welder_industrial.rsi', 'inhand-right-on'),
        'equipped-BELT': (R + 'Tools/welder_industrial.rsi', 'equipped-BELT'),
    },
    V + 'Tools/welder_mini.rsi': {
        'icon': (R + 'Tools/welder_hand.rsi', 'icon'),
        'welder_flame': (R + 'Tools/welder_hand.rsi', 'welder_on'),
        'inhand-left': (R + 'Tools/welder_hand.rsi', 'inhand-left'),
        'inhand-right': (R + 'Tools/welder_hand.rsi', 'inhand-right'),
        'inhand-left-flame': (R + 'Tools/welder_hand.rsi', 'inhand-left-on'),
        'inhand-right-flame': (R + 'Tools/welder_hand.rsi', 'inhand-right-on'),
        'equipped-BELT': (R + 'Tools/welder_hand.rsi', 'equipped-BELT'),
    },
    V + 'Tools/flashlight.rsi': {
        'flashlight': (R + 'Tools/Light/flashlight.rsi', 'flashlight'),
        'flashlight-on': (R + 'Tools/Light/flashlight.rsi', 'flashlight-on'),
        'flashlight-overlay': (R + 'Tools/Light/flashlight.rsi', 'flashlight-on'),
        'inhand-left': (R + 'Tools/Light/flashlight.rsi', 'off-inhand-left'),
        'inhand-right': (R + 'Tools/Light/flashlight.rsi', 'off-inhand-right'),
        'inhand-left-light': (R + 'Tools/Light/flashlight.rsi', 'on-inhand-left'),
        'inhand-right-light': (R + 'Tools/Light/flashlight.rsi', 'on-inhand-right'),
    },
    V + 'Tools/lantern.rsi': {
        s: (R + 'Tools/Light/lantern.rsi', s)
        for s in ('lantern', 'lantern-on', 'off-inhand-left', 'off-inhand-right', 'on-inhand-left',
                  'on-inhand-right')
    },
    V + 'Tools/shovel.rsi': {
        s: (R + 'Tools/shovel.rsi', s) for s in ('icon', 'inhand-left', 'inhand-right')
    },
    V + 'Tools/t-ray.rsi': {
        'tray-on': (R + 'Tools/t-ray.rsi', 'tray-on'),
        'tray-off': (R + 'Tools/t-ray.rsi', 'tray-off'),
    },
    V + 'Tools/handdrill.rsi': {
        'handdrill': (R + 'Tools/mining_prop_tools.rsi', 'handdrill'),
    },
    V + 'Tools/geiger.rsi': {
        s: (R + 'Tools/geiger_counter.rsi', s)
        for s in ('geiger_base', 'geiger_on_idle', 'geiger_on_low', 'geiger_on_med', 'geiger_on_high',
                  'geiger_on_ext', 'inhand-left', 'inhand-right')
    },
    V + 'Tools/hand_labeler.rsi': {
        'hand_labeler': (R + 'Tools/hand_labeler.rsi', 'hand_labeler'),
    },
    V + 'Tools/rolling_pin.rsi': {
        s: (R + 'Tools/Kitchen/rolling_pin.rsi', s) for s in ('icon', 'inhand-left', 'inhand-right')
    },
    V + 'Tools/Hydroponics/hatchet.rsi': {
        s: (R + 'Tools/Hydroponics/hatchet.rsi', s) for s in ('icon', 'inhand-left', 'inhand-right')
    },
    V + 'Tools/Hydroponics/hoe.rsi': {
        s: (R + 'Tools/Hydroponics/mini_hoe.rsi', s) for s in ('icon', 'inhand-left', 'inhand-right')
    },
    V + 'Tools/Hydroponics/spade.rsi': {
        s: (R + 'Tools/Hydroponics/spade.rsi', s) for s in ('icon', 'inhand-left', 'inhand-right')
    },
    **{
        V + 'Tools/Toolboxes/toolbox_%s.rsi' % van: {
            s: (R + 'Tools/Toolboxes/toolbox_%s.rsi' % rmc, s)
            for s in ('icon', 'icon-open', 'inhand-left', 'inhand-right')
        }
        for van, rmc in (('blue', 'blue'), ('green', 'green'), ('red', 'red'), ('yellow', 'yellow'),
                         ('syn', 'syndi'))
    },
    V + 'Devices/communication.rsi': {
        'walkietalkie': (R + 'Devices/radio.rsi', 'walkietalkie'),
        'walkietalkie-inhand-left': (R + 'Devices/radio.rsi', 'inhand-left'),
        'walkietalkie-inhand-right': (R + 'Devices/radio.rsi', 'inhand-right'),
    },
    V + 'Specific/Medical/healthanalyzer.rsi': {
        'icon': (R + 'Medical/health_analyzer.rsi', 'health'),
        'analyzer': (R + 'Medical/health_analyzer.rsi', 'health'),
        'analyzer-inhand-left': (R + 'Medical/health_analyzer.rsi', 'inhand-left'),
        'analyzer-inhand-right': (R + 'Medical/health_analyzer.rsi', 'inhand-right'),
    },
    V + 'Specific/Medical/firstaidkits.rsi': {
        k: v
        for van, rmc, hand in (('firstaid', 'firstaid', 'firstaid'), ('brutekit', 'advfirstaid', 'advfirstaid'),
                               ('burnkit', 'firefirstaid', 'firefirstaid'), ('toxinkit', 'antitoxin', 'antitoxin'),
                               ('o2kit', 'o2', 'o2'), ('radkit', 'radfirstaid', 'radfirstaid'),
                               ('advkit', 'purplefirstaid', 'purplefirstaid'), ('blackkit', 'bezerk', 'beserk'))
        for k, v in ((van, (R + 'Medical/first_aid_kits.rsi', rmc)),
                     (van + '-inhand-left', (R + 'Medical/first_aid_kits.rsi', hand + '-inhand-left')),
                     (van + '-inhand-right', (R + 'Medical/first_aid_kits.rsi', hand + '-inhand-right')))
    },
    V + 'Specific/Medical/Surgery/scalpel.rsi': {
        s: (R + 'Medical/Surgery/scalpel.rsi', s) for s in ('scalpel', 'inhand-left', 'inhand-right')
    },
    V + 'Specific/Medical/Surgery/saw.rsi': {
        s: (R + 'Medical/Surgery/saw.rsi', s) for s in ('saw', 'inhand-left', 'inhand-right')
    },
    V + 'Specific/Medical/Surgery/drill.rsi': {
        s: (R + 'Medical/Surgery/drill.rsi', s) for s in ('drill', 'inhand-left', 'inhand-right')
    },
    V + 'Specific/Medical/Surgery/cautery.rsi': {
        s: (R + 'Medical/Surgery/cautery.rsi', s) for s in ('cautery', 'inhand-left', 'inhand-right')
    },
    V + 'Specific/Medical/Surgery/hemostat.rsi': {
        s: (R + 'Medical/Surgery/hemostat.rsi', s) for s in ('hemostat', 'inhand-left', 'inhand-right')
    },
    V + 'Specific/Medical/Surgery/retractor.rsi': {
        s: (R + 'Medical/Surgery/retractor.rsi', s) for s in ('retractor', 'inhand-left', 'inhand-right')
    },
    V + 'Misc/handcuffs.rsi': {
        s: (R + 'Misc/handcuffs.rsi', s) for s in ('handcuff', 'inhand-left', 'inhand-right', 'equipped-BELT')
    },
}


# RMC RSIs used as they are by the DarkestHour faction clothing (copied to the same path under _RMC14)
C = R + 'Clothing/'
COPY = [
    # Republic (USCM)
    C + 'Uniforms/Marine/standard/jungle.rsi', C + 'Uniforms/Marine/medic/jungle.rsi',
    C + 'Uniforms/Marine/engineer/jungle.rsi', C + 'Uniforms/Marine/tanker/jungle.rsi',
    C + 'Head/Hats/cap/jungle.rsi',
    C + 'OuterClothing/Armor/m3/light/padded/jungle.rsi', C + 'OuterClothing/Armor/m3/standard/padded/jungle.rsi',
    C + 'Hands/Gloves/brown.rsi',
    C + 'Back/Backpacks/Marines/standard/jungle-desert-classic.rsi',
    C + 'Back/Backpacks/Marines/medic/jungle-desert-classic.rsi',
    C + 'Belt/marine/jungle-classic.rsi', C + 'Belt/medicbag/jungle-classic.rsi',
    # Empire (TWE Royal Marines)
    C + 'Uniforms/TSE/RCM/standard.rsi', C + 'Uniforms/TSE/RCM/team_lead.rsi',
    C + 'Head/Royal/cap.rsi', C + 'Head/Royal/beret.rsi',
    C + 'OuterClothing/Royal/light.rsi', C + 'OuterClothing/Royal/padded.rsi',
    C + 'Hands/Gloves/royal_marine.rsi',
    C + 'Back/Backpacks/RCM/medium.rsi', C + 'Back/Backpacks/RCM/medic.rsi',
    C + 'Belt/rcm_general.rsi', C + 'Belt/rcm_medical.rsi',
]

# new RSIs assembled from RMC states: path -> {state: (rmc rsi, rmc state)}
D = '_DarkestHour/Clothing/RMC/'


def magboots(boots):
    return {
        'icon': (boots, 'icon'), 'icon-on': (boots, 'icon'),
        'equipped-FEET': (boots, 'equipped-FEET'), 'on-equipped-FEET': (boots, 'equipped-FEET'),
        'equipped-FEET-vox': (boots, 'equipped-FEET'), 'on-equipped-FEET-vox': (boots, 'equipped-FEET'),
    }


COMPOSITES = {
    D + 'magboots_republic.rsi': magboots(C + 'Shoes/Boots/jungle.rsi'),
    D + 'magboots_imperial.rsi': magboots(C + 'Shoes/Boots/black.rsi'),
}


def copy_rsis(rmc_tex):
    for rsi in COPY:
        dst = os.path.join(TEX, rsi)
        if os.path.isdir(dst):
            shutil.rmtree(dst)
        shutil.copytree(os.path.join(rmc_tex, rsi), dst)


def build_composites(rmc_tex):
    for target, mapping in COMPOSITES.items():
        tpath = os.path.join(TEX, target)
        os.makedirs(tpath, exist_ok=True)
        states = []
        sources = set()
        for state, (rsi, rstate) in mapping.items():
            rpath = os.path.join(rmc_tex, rsi)
            rmeta = load_meta(rpath)
            entry = dict(next(s for s in rmeta['states'] if s['name'] == rstate))
            entry['name'] = state
            states.append(entry)
            shutil.copyfile(os.path.join(rpath, rstate + '.png'), os.path.join(tpath, state + '.png'))
            sources.add(rmeta.get('copyright', rsi))
        meta = {'version': 1, 'license': 'CC-BY-SA-3.0',
                'copyright': '; '.join(sorted(sources)) + ' | Assembled for The Darkest Hour (Tools/_DarkestHour/rmc_skins.py)',
                'size': {'x': 32, 'y': 32}, 'states': states}
        with open(os.path.join(tpath, 'meta.json'), 'w', encoding='utf-8') as f:
            json.dump(meta, f, ensure_ascii=False, indent=2)
            f.write('\n')


def load_meta(path):
    with open(os.path.join(path, 'meta.json'), encoding='utf-8-sig') as f:
        return json.load(f)


def main(rmc):
    rmc_tex = os.path.join(rmc, 'Resources/Textures')
    done = 0
    for target, mapping in SKINS.items():
        tpath = os.path.join(TEX, target)
        if not os.path.isdir(tpath):
            print('skip (no vanilla rsi):', target)
            continue

        meta = load_meta(tpath)
        size = (meta['size']['x'], meta['size']['y'])
        states = {s['name']: s for s in meta['states']}
        sources = set()
        for state, src in mapping.items():
            if state not in states:
                print('skip (no vanilla state):', target, state)
                continue

            if src is None:
                dirs = states[state].get('directions', 1)
                cols = min(dirs, 4)
                Image.new('RGBA', (size[0] * cols, size[1] * ((dirs + 3) // 4)), (0, 0, 0, 0)).save(
                    os.path.join(tpath, state + '.png'))
                states[state] = {'name': state, 'directions': dirs} if dirs > 1 else {'name': state}
                continue

            rsi, rstate = src
            rpath = os.path.join(rmc_tex, rsi)
            rmeta = load_meta(rpath)
            if (rmeta['size']['x'], rmeta['size']['y']) != size:
                print('skip (size):', target, state)
                continue

            rstates = {s['name']: s for s in rmeta['states']}
            if rstate not in rstates:
                print('skip (no rmc state):', rsi, rstate)
                continue

            shutil.copyfile(os.path.join(rpath, rstate + '.png'), os.path.join(tpath, state + '.png'))
            entry = dict(rstates[rstate])
            entry['name'] = state
            states[state] = entry
            sources.add(rmeta.get('copyright', rsi))
            done += 1

        meta['states'] = [states[s['name']] for s in meta['states']]
        note = 'DarkestHour: RMC-like sprites from RMC-14 (' + '; '.join(sorted(sources)) + ')'
        if sources and 'RMC-14' not in meta.get('copyright', ''):
            meta['copyright'] = (meta.get('copyright', '') + '. ' + note).strip('. ')
        meta['license'] = 'CC-BY-SA-3.0'
        with open(os.path.join(tpath, 'meta.json'), 'w', encoding='utf-8') as f:
            json.dump(meta, f, ensure_ascii=False, indent=2)
            f.write('\n')

    print('states replaced:', done)
    copy_rsis(rmc_tex)
    build_composites(rmc_tex)


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else '/home/claude/rmc')
