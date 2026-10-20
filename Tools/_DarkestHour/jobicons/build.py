"""Job icons for The Darkest Hour (8x8, HUD and crew manifest).

Every icon is a badge in the department colour with a 6x6 glyph. Rank is shown by the frame:
heads of department get a brass frame, leads a light frame, trainees a dashed frame, everyone else a dark one.

Run from the repository root:  python3 Tools/_DarkestHour/jobicons/build.py
Writes Resources/Textures/_DarkestHour/Interface/Misc/job_icons.rsi and preview.png next to this script
(the preview is not committed)."""
import json
import os
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
OUT = os.path.join(ROOT, 'Resources/Textures/_DarkestHour/Interface/Misc/job_icons.rsi')


def hexc(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


# department: (fill, glyph)
DEPT = {
    # Metallistick command: graphite with brass
    'command': (hexc('34373c'), hexc('e0bd6a')),
    # corporate security: khaki with beige, grey frame
    'security': (hexc('6b6443'), hexc('e8dcb5')),
    'medical': (hexc('2f6f8c'), hexc('f2f6f8')),
    'supply': (hexc('7a5530'), hexc('f3e3c8')),
    'science': (hexc('5e4078'), hexc('f1e6f8')),
    'engineering': (hexc('a35a1f'), hexc('fff1d8')),
    'silicon': (hexc('275e57'), hexc('b8f5e6')),
}

FRAME = {
    'head': hexc('9a7630'),     # dark brass
    'lead': hexc('7c7a73'),     # steel grey
    'staff': None,              # darker shade of the department colour
    'trainee': None,            # same, dashed
}
SECURITY_FRAME = hexc('6a6c6e')  # grey frame for corporate security staff

G = {
    'star': ['..##..', '..##..', '######', '.####.', '.#..#.', '#....#'],
    'clipboard': ['..##..', '######', '#....#', '#.##.#', '#....#', '######'],
    'metall': ['#....#', '##..##', '#.##.#', '#....#', '#....#', '#....#'],
    'shield_band': ['######', '######', '#....#', '#.##.#', '.#..#.', '..##..'],
    'bars': ['#.#.#.', '#.#.#.', '######', '#.#.#.', '#.#.#.', '#.#.#.'],
    'magnifier': ['.###..', '#...#.', '#...#.', '.###..', '....#.', '.....#'],
    'document': ['####..', '#..#..', '#..###', '#....#', '#....#', '######'],
    'shield': ['######', '#....#', '#.##.#', '#....#', '.#..#.', '..##..'],
    'plane': ['..##..', '..##..', '######', '..##..', '..##..', '.####.'],
    'chevron2': ['#....#', '.#..#.', '..##..', '#....#', '.#..#.', '..##..'],
    'chevron': ['......', '......', '#....#', '.#..#.', '..##..', '......'],
    'cross': ['..##..', '..##..', '######', '######', '..##..', '..##..'],
    'flask': ['.####.', '..##..', '..##..', '.####.', '######', '######'],
    'heart': ['##..##', '#.##.#', '#....#', '.#..#.', '..##..', '......'],
    'cross_small': ['......', '..##..', '.####.', '.####.', '..##..', '......'],
    'crate_brace': ['######', '#...##', '#..#.#', '#.#..#', '##...#', '######'],
    'rocket': ['..##..', '.####.', '.####.', '.####.', '######', '#.##.#'],
    'crate': ['######', '#....#', '######', '#....#', '#....#', '######'],
    'atom': ['.####.', '#....#', '#.##.#', '#.##.#', '#....#', '.####.'],
    'crystal': ['..##..', '.#..#.', '#....#', '.#..#.', '..##..', '......'],
    'virus': ['..##..', '#.##.#', '.#..#.', '.#..#.', '#.##.#', '..##..'],
    'tube': ['.####.', '.#..#.', '.#..#.', '.#..#.', '.####.', '..##..'],
    'wrench': ['.#..#.', '.####.', '..##..', '..##..', '..##..', '..##..'],
    'tank': ['..##..', '.####.', '.#..#.', '.#..#.', '.#..#.', '.####.'],
    'hammer': ['######', '######', '..##..', '..##..', '..##..', '..##..'],
    'screwdriver': ['......', '....#.', '...#..', '..##..', '.##...', '.#....'],
    'lens': ['......', '..##..', '.#..#.', '#.##.#', '.#..#.', '..##..'],
}

# icon state, department, rank, glyph
ICONS = [
    ('Captain', 'command', 'head', 'star'),
    ('Adjutant', 'command', 'lead', 'clipboard'),
    ('MetallArmedSpecialist', 'command', 'staff', 'metall'),
    ('SecurityChief', 'security', 'head', 'shield_band'),
    ('Warden', 'security', 'lead', 'bars'),
    ('Detective', 'security', 'lead', 'magnifier'),
    ('Investigator', 'security', 'staff', 'document'),
    ('SecurityOfficer', 'security', 'staff', 'shield'),
    ('SecurityPilot', 'security', 'staff', 'plane'),
    ('SecurityInstructor', 'security', 'lead', 'chevron2'),
    ('SecurityCadet', 'security', 'trainee', 'chevron'),
    ('ChiefPhysician', 'medical', 'head', 'cross'),
    ('LeadPhysician', 'medical', 'lead', 'cross'),
    ('Physician', 'medical', 'staff', 'cross'),
    ('Chemist', 'medical', 'staff', 'flask'),
    ('Paramedic', 'medical', 'staff', 'heart'),
    ('MedicalIntern', 'medical', 'trainee', 'cross_small'),
    ('Quartermaster', 'supply', 'head', 'crate_brace'),
    ('Expeditor', 'supply', 'staff', 'rocket'),
    ('Loader', 'supply', 'staff', 'crate'),
    ('ResearchDirector', 'science', 'head', 'atom'),
    ('LeadScientist', 'science', 'lead', 'atom'),
    ('MaterialsScientist', 'science', 'staff', 'crystal'),
    ('Virologist', 'science', 'staff', 'virus'),
    ('LabAssistant', 'science', 'trainee', 'tube'),
    ('ChiefTechnician', 'engineering', 'head', 'wrench'),
    ('LeadTechnician', 'engineering', 'lead', 'wrench'),
    ('AtmosTechnician', 'engineering', 'staff', 'tank'),
    ('Technician', 'engineering', 'staff', 'hammer'),
    ('JuniorTechnician', 'engineering', 'trainee', 'screwdriver'),
    ('StationAi', 'silicon', 'staff', 'lens'),
]


def shade(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c)


def icon(dept, rank, glyph):
    fill, ink = DEPT[dept]
    frame = FRAME[rank] or shade(fill, 0.62)
    if dept == 'security' and rank in ('staff', 'trainee'):
        frame = SECURITY_FRAME
    img = Image.new('RGBA', (8, 8), (0, 0, 0, 0))
    px = img.load()
    for y in range(8):
        for x in range(8):
            edge = x in (0, 7) or y in (0, 7)
            corner = x in (0, 7) and y in (0, 7)
            if corner:
                continue
            if edge:
                if rank == 'trainee' and (x + y) % 2:
                    continue  # dashed frame
                px[x, y] = frame + (255,)
            else:
                # lit top-left, darker bottom-right inside the badge
                k = 1.08 if (x + y) <= 5 else (0.9 if (x + y) >= 10 else 1.0)
                px[x, y] = shade(fill, k) + (255,)
    for j, row in enumerate(G[glyph]):
        for i, ch in enumerate(row):
            if ch == '#':
                px[1 + i, 1 + j] = ink + (255,)
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    states = []
    for name, dept, rank, glyph in ICONS:
        icon(dept, rank, glyph).save(os.path.join(OUT, name + '.png'))
        states.append({'name': name})
    meta = {
        'version': 1,
        'license': 'CC-BY-SA-3.0',
        'copyright': 'Made for The Darkest Hour (Tools/_DarkestHour/jobicons/build.py), layout after the job icons of /tg/station',
        'size': {'x': 8, 'y': 8},
        'states': states,
    }
    with open(os.path.join(OUT, 'meta.json'), 'w', encoding='utf-8') as f:
        json.dump(meta, f, indent=4, ensure_ascii=False)
        f.write('\n')

    # preview: icons at 10x on a dark background, rows by department
    rows = []
    for dept in DEPT:
        rows.append([n for n, d, _, _ in ICONS if d == dept])
    cell = 8 * 10 + 8
    W = max(len(r) for r in rows) * cell + 8
    prev = Image.new('RGBA', (W, len(rows) * cell + 8), (46, 47, 50, 255))
    for j, r in enumerate(rows):
        for i, n in enumerate(r):
            im = Image.open(os.path.join(OUT, n + '.png')).resize((80, 80), Image.NEAREST)
            prev.alpha_composite(im, (8 + i * cell, 8 + j * cell))
    prev.save(os.path.join(os.path.dirname(__file__), 'preview.png'))


if __name__ == '__main__':
    main()
