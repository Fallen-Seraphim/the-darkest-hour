"""Удаление элементов из YAML-файлов по путям, с сохранением форматирования остального текста."""
import yaml
from yaml import CSafeLoader as L


def compose(text):
    return yaml.compose(text, Loader=L)


def last_line(node):
    """Последняя строка с содержимым узла."""
    best = -1
    stack = [node]
    while stack:
        n = stack.pop()
        if isinstance(n, yaml.ScalarNode):
            e = n.end_mark
            ln = e.line if e.column > 0 else e.line - 1
            ln = max(ln, n.start_mark.line)
            best = max(best, ln)
        elif isinstance(n, yaml.SequenceNode):
            if n.flow_style:
                best = max(best, n.end_mark.line)
            stack.extend(n.value)
            if not n.value:
                best = max(best, n.start_mark.line)
        elif isinstance(n, yaml.MappingNode):
            if n.flow_style:
                best = max(best, n.end_mark.line)
            for k, v in n.value:
                stack.append(k)
                stack.append(v)
            if not n.value:
                best = max(best, n.start_mark.line)
    return best


def navigate(root, path):
    """Возвращает (parent, last_step, node)."""
    node = root
    parent = None
    step = None
    for s in path:
        parent = node
        step = s
        if isinstance(s, tuple) and s[0] == '#':
            node = node.value[s[1]]
        elif isinstance(s, tuple) and s[0] == 'key':
            for k, v in node.value:
                if isinstance(k, yaml.ScalarNode) and k.value == s[1]:
                    node = k
                    break
            else:
                raise KeyError(s)
        else:
            for k, v in node.value:
                if isinstance(k, yaml.ScalarNode) and k.value == s:
                    node = v
                    break
            else:
                raise KeyError(s)
    return parent, step, node


class Editor:
    def __init__(self, text):
        self.text = text
        self.lines = text.splitlines(keepends=True)
        self.offs = [0]
        for l in self.lines:
            self.offs.append(self.offs[-1] + len(l))
        self.root = compose(text)
        self.spans = []  # (start, end, repl)
        self.problems = []

    def off(self, line, col):
        return self.offs[line] + col

    def line_span(self, a, b):
        return (self.offs[a], self.offs[min(b + 1, len(self.lines))], '')

    def remove_seq_item(self, path):
        """path указывает на элемент последовательности (последний шаг ('#', i))."""
        seq_path = path[:-1]
        _, _, seq = navigate(self.root, seq_path) if seq_path else (None, None, self.root)
        i = path[-1][1]
        item = seq.value[i]
        if seq.flow_style:
            if i + 1 < len(seq.value):
                nxt = seq.value[i + 1]
                s = self.off(item.start_mark.line, item.start_mark.column)
                e = self.off(nxt.start_mark.line, nxt.start_mark.column)
            elif i > 0:
                prv = seq.value[i - 1]
                s = self.off(prv.end_mark.line, prv.end_mark.column)
                e = self.off(item.end_mark.line, item.end_mark.column)
            else:
                s = self.off(item.start_mark.line, item.start_mark.column)
                e = self.off(item.end_mark.line, item.end_mark.column)
            self.spans.append((s, e, ''))
            return
        L0 = item.start_mark.line
        prefix = self.lines[L0][:item.start_mark.column]
        if prefix.strip() not in ('-',) and not prefix.strip().startswith('-'):
            # '-' на предыдущей строке
            if L0 > 0 and self.lines[L0 - 1].strip() == '-':
                L0 -= 1
            else:
                self.problems.append(('seq item prefix', path, prefix))
                return
        if prefix.strip().replace('-', '').strip():
            self.problems.append(('seq item compact', path, prefix))
            return
        self.spans.append(self.line_span(L0, last_line(item)))

    def remove_pair(self, path):
        """path указывает на значение ключа в отображении (последний шаг — строка)."""
        map_path = path[:-1]
        _, _, mp = navigate(self.root, map_path)
        key = path[-1]
        idx = None
        for j, (k, v) in enumerate(mp.value):
            if isinstance(k, yaml.ScalarNode) and k.value == key:
                idx = j
                break
        if idx is None:
            self.problems.append(('no key', path))
            return
        k, v = mp.value[idx]
        if mp.flow_style:
            if idx + 1 < len(mp.value):
                s = self.off(k.start_mark.line, k.start_mark.column)
                nk = mp.value[idx + 1][0]
                e = self.off(nk.start_mark.line, nk.start_mark.column)
            elif idx > 0:
                pv = mp.value[idx - 1][1]
                s = self.off(pv.end_mark.line, pv.end_mark.column)
                e = self.off(v.end_mark.line, v.end_mark.column)
            else:
                s = self.off(k.start_mark.line, k.start_mark.column)
                e = self.off(v.end_mark.line, v.end_mark.column)
            self.spans.append((s, e, ''))
            return
        L0 = k.start_mark.line
        prefix = self.lines[L0][:k.start_mark.column]
        end = last_line(v) if not (isinstance(v, yaml.ScalarNode) and v.value == '' and v.start_mark.line == L0) else L0
        end = max(end, L0)
        if '-' in prefix:
            if idx + 1 < len(mp.value):
                nk = mp.value[idx + 1][0]
                dash = prefix.index('-')
                nl = nk.start_mark.line
                line = self.lines[nl]
                if line[:dash + 2].strip():
                    self.problems.append(('redash', path))
                    return
                self.spans.append(self.line_span(L0, nl - 1))
                self.spans.append((self.offs[nl] + dash, self.offs[nl] + dash + 2, '- '))
            else:
                self.problems.append(('only pair in seq item', path))
            return
        self.spans.append(self.line_span(L0, end))

    def remove_top(self, index):
        item = self.root.value[index]
        L0 = item.start_mark.line
        end = last_line(item)
        # пустая строка после — тоже
        if end + 1 < len(self.lines) and not self.lines[end + 1].strip():
            end += 1
        self.spans.append(self.line_span(L0, end))

    def result(self):
        spans = sorted(set(self.spans), key=lambda s: (s[0], -s[1]))
        # убрать вложенные
        merged = []
        for s in spans:
            if merged and s[0] < merged[-1][1] and s[2] == '' and merged[-1][2] == '':
                if s[1] > merged[-1][1]:
                    merged[-1] = (merged[-1][0], s[1], '')
                continue
            if merged and s[0] < merged[-1][1]:
                continue  # замена внутри удаляемого куска
            merged.append(s)
        t = self.text
        for s, e, r in reversed(merged):
            t = t[:s] + r + t[e:]
        return fix_anchors(self.text, t)


def fix_anchors(old, new):
    """Якорь (&name) попал в удалённый кусок, а ссылки (*name) остались — перенести определение к первой ссылке."""
    import re
    ol = old.splitlines(keepends=True)
    for m in re.finditer(r'&([A-Za-z0-9_]+)', old):
        name = m.group(1)
        if ('&' + name) in new or ('*' + name) not in new:
            continue
        # блок якоря в старом тексте
        li = old[:m.start()].count('\n')
        line = ol[li]
        ind = len(line) - len(line.lstrip())
        block = []
        for l in ol[li + 1:]:
            if l.strip() and len(l) - len(l.lstrip()) <= ind:
                break
            block.append(l)
        nl = new.splitlines(keepends=True)
        for j, l in enumerate(nl):
            if ('*' + name) in l:
                nind = len(l) - len(l.lstrip())
                shift = nind - ind
                rb = [(' ' * shift + b) if shift >= 0 else b[-shift:] for b in block]
                nl[j] = l.replace('*' + name, '&' + name) 
                nl[j + 1:j + 1] = rb
                break
        new = ''.join(nl)
    return new
