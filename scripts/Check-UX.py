#!/usr/bin/env python3
"""Advisory source checks; no edits, no application dependencies, no WCAG verdict."""
import argparse
from html.parser import HTMLParser
from pathlib import Path
import re

SKIP = {'node_modules', 'target', 'dist', 'gen', 'generated', 'upstream', 'tests',
        'test', 'fixtures', 'snapshots', 'vendor', '.git', '.build', 'diagnostics', 'admin'}
EXT = {'.rs', '.html', '.js', '.mjs', '.jsx', '.tsx', '.ts', '.css'}
TECH = re.compile(r'\b(?:HTTP\s*[45]?\d\d|SYNC_[A-Z_]+|backend|stack\s*trace|debug|panic|command_id|request_id|localhost:\d+|[A-Z_]+_FAILED)\b', re.I)


def view_ranges(text):
    """Locate Leptos view! bodies, ignoring quoted strings and comments."""
    ranges = []
    token = re.compile(r'r(#+)?".*?"\1|"(?:\\.|[^"\\])*"|//[^\n]*|/\*.*?\*/|[{}]', re.S)
    for m in re.finditer(r'\bview!\s*\{', text):
        depth = 1
        for t in token.finditer(text, m.end()):
            if t.group() == '{':
                depth += 1
            elif t.group() == '}':
                depth -= 1
                if depth == 0:
                    ranges.append((m.end(), t.start()))
                    break
    return ranges


def visible_strings(text, suffix):
    if suffix == '.rs':
        for start, end in view_ranges(text):
            # Literal children and user-facing attributes, not class/id/API args.
            part = text[start:end]
            pattern = re.compile(r'(?:>\s*|\b(?:title|placeholder|aria-label)\s*=\s*)("(?:\\.|[^"\\])*")')
            for m in pattern.finditer(part):
                yield start + m.start(1), m.group(1)[1:-1]
    elif suffix in {'.js', '.mjs', '.ts', '.jsx', '.tsx'}:
        pattern = re.compile(r'\b(?:textContent|innerText|placeholder|title)\s*=\s*(["\x27])((?:\\.|(?!\1).)*)\1')
        for m in pattern.finditer(text):
            yield m.start(2), m.group(2)
        if suffix in {'.jsx', '.tsx'}:
            for m in re.finditer(r'>\s*([^<>{}\n]+)\s*<', text):
                yield m.start(1), m.group(1)


class Markup(HTMLParser):
    def __init__(self, warn, check_text):
        super().__init__()
        self.warn, self.check_text = warn, check_text
        self.ids, self.labels, self.fields = set(), set(), []
        self.stack = []

    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        line = self.getpos()[0]
        if tag not in {'area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input', 'link', 'meta', 'source', 'track', 'wbr'}:
            self.stack.append(tag)
        if a.get('id'):
            if a['id'] in self.ids:
                self.warn(line, 'duplicate-id', f"Identifiant HTML répété : {a['id']}")
            self.ids.add(a['id'])
        if tag == 'label' and a.get('for'):
            self.labels.add(a['for'])
        if tag == 'img' and 'alt' not in a:
            self.warn(line, 'image-alt', 'Image sans attribut alt ; vérifier son rôle.')
        if tag in {'input', 'select', 'textarea'} and a.get('type') not in {'hidden', 'submit', 'button', 'reset', 'image'}:
            if not any(a.get(k) for k in ('aria-label', 'aria-labelledby', 'title')) and 'label' not in self.stack:
                self.fields.append((line, a.get('id')))
        for key in ('title', 'placeholder', 'aria-label', 'alt'):
            if a.get(key):
                self.check_text(line, a[key])

    def handle_endtag(self, tag):
        if tag in self.stack:
            self.stack = self.stack[:len(self.stack) - 1 - self.stack[::-1].index(tag)]

    def handle_data(self, data):
        if not any(t in self.stack for t in ('script', 'style', 'code', 'pre')):
            self.check_text(self.getpos()[0], data)

    def finish(self):
        for line, field_id in self.fields:
            if not field_id or field_id not in self.labels:
                self.warn(line, 'field-name', 'Champ sans label associé détectable ; vérifier le nom accessible.')


def inspect(path, root, long_limit):
    text = path.read_text(encoding='utf-8-sig')
    lines = text.splitlines()
    findings = []
    def warn(line, code, message):
        # Explicit suppression is local, reviewable, and requires a reason.
        context = '\n'.join(lines[max(0, line-2):line])
        if re.search(r'ux-ignore:\s*' + re.escape(code) + r'\s+\S', context):
            return
        findings.append((str(path.relative_to(root)), line, code, message))
    def check_text(line, value):
        value = re.sub(r'\s+', ' ', value).strip()
        if TECH.search(value):
            warn(line, 'technical-copy', f'Texte possiblement technique : {value[:110]}')
        if len(value) > long_limit:
            warn(line, 'long-copy', f'Texte de {len(value)} caractères : vérifier son utilité, sans retirer une aide métier nécessaire.')
    if path.suffix == '.html':
        parser = Markup(warn, check_text)
        parser.feed(text)
        parser.finish()
    else:
        for offset, value in visible_strings(text, path.suffix):
            check_text(text.count('\n', 0, offset) + 1, value)
    if path.suffix == '.css':
        for m in re.finditer(r'outline\s*:\s*(?:none|0)\b', text):
            warn(text.count('\n', 0, m.start()) + 1, 'focus-style', 'Contour supprimé : vérifier un focus alternatif visible.')
    return findings


def source_files(root, inputs):
    def visit(p):
        if p.is_symlink() or (hasattr(p, 'is_junction') and p.is_junction()):
            return
        if p.is_dir():
            if p.name.lower() not in SKIP:
                for child in sorted(p.iterdir()):
                    yield from visit(child)
        elif p.suffix in EXT and not re.search(r'(?:[._-](?:test|spec|generated)|diagnostic|(?:^|[_-])admin)', p.name, re.I):
            p.relative_to(root)  # Refuse a file outside the requested root.
            yield p
    found = set()
    for value in inputs:
        p = root / value
        if not p.exists():
            raise FileNotFoundError(p)
        for item in visit(p):
            if item not in found:
                found.add(item)
                yield item


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('paths', nargs='*', help='Fichiers/dossiers relatifs au dépôt ; défaut : frontend apps.')
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--long-limit', type=int, default=180)
    parser.add_argument('--max-warnings', type=int, default=80)
    args = parser.parse_args()
    root = args.root.resolve()
    paths = args.paths or ['apps/forge_studio/src', 'apps/forge_studio/index.html',
                           'apps/forge_studio/styles.css', 'apps/forge_launcher/canonical-ui']
    findings, count = [], 0
    for path in source_files(root, paths):
        count += 1
        findings.extend(inspect(path, root, args.long_limit))
    for file, line, code, message in findings[:args.max_warnings]:
        print(f'{file}:{line}: warning UX [{code}] {message}')
    print(f'UX : {count} fichiers ; {len(findings)} avertissements ; {min(len(findings), args.max_warnings)} affichés. Heuristique non bloquante, aucune édition.')
    print('Limites : chaînes dynamiques, traductions, Leptos hors view!, ARIA calculé, contrastes, cohérence et duplication de composants exigent une revue ciblée et/ou le navigateur.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
