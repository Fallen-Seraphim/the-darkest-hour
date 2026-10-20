"""Point prototypes at the recoloured clothing.

Every `sprite: <vanilla path>.rsi` that names a recoloured RSI (see targets.json) is rewritten to the
_DarkestHour copy and marked with a DarkestHour comment. The edit is idempotent, so it can be re-run after
an upstream merge brings new items that use the same sprites.

Run from the repository root:  python3 Tools/_DarkestHour/recolor/apply.py"""
import glob
import json
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
MARK = '# DarkestHour: форма Metallistick (Tools/_DarkestHour/recolor)'


def main():
    with open(os.path.join(os.path.dirname(__file__), 'targets.json'), encoding='utf-8') as f:
        targets = json.load(f)
    alt = '|'.join(re.escape(t) for t in sorted(targets, key=len, reverse=True))
    rx = re.compile(r'^(?P<pre>\s*(?:-\s+)?sprite:\s*(?:&\S+\s+)?)(?P<abs>/Textures/)?(?P<path>' + alt + r')(?P<post>\s*(?:#.*)?)$')
    files = changed = 0
    for fn in glob.glob(os.path.join(ROOT, 'Resources/Prototypes/**/*.yml'), recursive=True):
        if os.sep + '_DarkestHour' + os.sep in fn:
            continue
        with open(fn, encoding='utf-8') as f:
            lines = f.read().split('\n')
        dirty = False
        for i, line in enumerate(lines):
            m = rx.match(line)
            if not m:
                continue
            new = m.group('pre') + (m.group('abs') or '') + '_DarkestHour/' + m.group('path')
            post = m.group('post').strip()
            new += ' ' + (post if post and 'DarkestHour' in post else MARK)
            if new != line:
                lines[i] = new
                dirty = True
                changed += 1
        if dirty:
            files += 1
            with open(fn, 'w', encoding='utf-8') as f:
                f.write('\n'.join(lines))
    print(f'{changed} sprite paths in {files} files')


if __name__ == '__main__':
    main()
