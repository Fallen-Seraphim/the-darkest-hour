"""Renames items named after vanilla jobs to The Darkest Hour jobs (English text for now; Russian comes with the
localisation). Touches entity names and descriptions in prototypes and item-facing locale strings (stamps,
loadout groups, steal targets, accesses, beacons, holopads, spray painter styles). Vanilla job names, job
descriptions, tips and lore texts are left alone: those jobs are switched off, not deleted.

Run from the repository root:  python3 Tools/_DarkestHour/rename_jobs/rename.py
Safe to run again (and after merging upstream)."""
import glob
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))

# vanilla -> ours; longest first
NAMES = [
    ('head of personnel', 'adjutant'),
    ('head of security', 'security chief'),
    ('chief medical officer', 'chief physician'),
    ('chief engineer', 'chief technician'),
    ('cargo technician', 'loader'),
    ('cargo tech', 'loader'),
    ('salvage specialist', 'expeditor'),
    ('medical doctor', 'physician'),
    ('station engineer', 'technician'),
    ('senior engineer', 'lead technician'),
    ('senior physician', 'lead physician'),
    ('senior researcher', 'lead scientist'),
    ('senior officer', 'security instructor'),
]
ABBR = [('HoP', 'adjutant'), ('HoS', 'security chief'), ('CMO', 'chief physician'), ('CE', 'chief technician')]

LOCALE = [
    'Resources/Locale/en-US/paper/stamp-component.ftl',
    'Resources/Locale/en-US/preferences/loadout-groups.ftl',
    'Resources/Locale/en-US/objectives/conditions/steal-target-groups.ftl',
    'Resources/Locale/en-US/objectives/conditions/steal.ftl',
    'Resources/Locale/en-US/prototypes/access/accesses.ftl',
    'Resources/Locale/en-US/navmap-beacons/station-beacons.ftl',
    'Resources/Locale/en-US/holopad/holopad.ftl',
    'Resources/Locale/en-US/spray-painter/spray-painter.ftl',
]


def match_case(src, new):
    words = src.split()
    if src[0].isupper() and (len(words) == 1 and src.isupper() or words[-1][:1].isupper()):
        # Title Case ("Head of Security", "CMO", "HoS") -> Title Case
        return ' '.join(w[:1].upper() + w[1:] for w in new.split())
    if src[0].isupper():
        return new[:1].upper() + new[1:]
    return new


def rename_text(text):
    for old, new in NAMES:
        text = re.sub(re.escape(old), lambda m: match_case(m.group(0), new), text, flags=re.I)
    for old, new in ABBR:
        # abbreviations only as whole words, any case for HoP/HoS/CMO, exact case for CE
        flags = 0 if old == 'CE' else re.I
        text = re.sub(r'\b' + old + r'\b', lambda m: match_case(m.group(0), new), text, flags=flags)
    return text


def is_loc_key(value):
    v = value.strip().strip('"\'')
    return ' ' not in v and '-' in v


def rename_prototypes():
    changed = 0
    for path in glob.glob(os.path.join(ROOT, 'Resources/Prototypes/**/*.yml'), recursive=True):
        lines = open(path, encoding='utf-8').read().split('\n')
        out = []
        for ln in lines:
            m = re.match(r'^(\s+(?:- )?(?:name|description): )(.*)$', ln)
            if m and not is_loc_key(m.group(2)):
                # do not touch the comment part
                body, sep, comment = m.group(2).partition(' #')
                new = rename_text(body) + sep + comment
                if new != m.group(2):
                    ln = m.group(1) + new
                    changed += 1
            out.append(ln)
        new_text = '\n'.join(out)
        if new_text != '\n'.join(lines):
            open(path, 'w', encoding='utf-8').write(new_text)
    return changed


def rename_locale():
    changed = 0
    for rel in LOCALE:
        path = os.path.join(ROOT, rel)
        if not os.path.exists(path):
            continue
        lines = open(path, encoding='utf-8').read().split('\n')
        out = []
        for ln in lines:
            m = re.match(r'^([\w.-]+\s*=\s*)(.*)$', ln)
            if m:
                new = rename_text(m.group(2))
                if new != m.group(2):
                    ln = m.group(1) + new
                    changed += 1
            out.append(ln)
        open(path, 'w', encoding='utf-8').write('\n'.join(out))
    return changed


if __name__ == '__main__':
    print('prototype lines:', rename_prototypes())
    print('locale lines:', rename_locale())
