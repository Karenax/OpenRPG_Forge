"""Prove the installer embedded the canonical presentation, not a Hub redesign."""
import argparse
import json
from pathlib import Path
from build_delivery import digest, write

def main():
    p = argparse.ArgumentParser()
    p.add_argument('--original',type=Path,required=True)
    p.add_argument('--snapshot',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True)
    args = p.parse_args()
    if args.output.exists():
        raise ValueError('Fresh output required')
    canonical, frontend = args.snapshot/'canonical-ui',args.snapshot/'dist'
    entries = []
    for copied in sorted(canonical.rglob('*')):
        if not copied.is_file(): continue
        relative = copied.relative_to(canonical)
        if digest(args.original/relative) != digest(copied):
            raise ValueError(f'Canonical presentation changed: {relative}')
        entries.append(dict(path=relative.as_posix(),sha256=digest(copied)))
    for name in ('styles.css','app.js','codex-core.js'):
        if digest(canonical/name) != digest(frontend/name):
            raise ValueError(f'Packaged presentation differs: {name}')
    original_index = (canonical/'index.html').read_text(encoding='utf-8')
    expected = original_index.replace('</head>','  <script defer src="desktop.js"></script>\n</head>')
    if (frontend/'index.html').read_text(encoding='utf-8') != expected:
        raise ValueError('HTML replaced rather than bridged')
    for copied in (canonical/'assets').rglob('*'):
        if copied.is_file() and digest(copied) != digest(frontend/copied.relative_to(canonical)):
            raise ValueError('Illustration changed')
    write(args.output,dict(status='PASS_CANONICAL_AUBE_PRESENTATION_PARITY',files=entries,
          canonical_files_identical=True,styles_app_codex_identical=True,
          all_illustrations_identical=True,html_only_adds_original_desktop_bridge=True,
          human_visual_acceptance=False))
    print(json.dumps(dict(status='PASS_CANONICAL_AUBE_PRESENTATION_PARITY',files=len(entries))))

if __name__ == '__main__': main()
