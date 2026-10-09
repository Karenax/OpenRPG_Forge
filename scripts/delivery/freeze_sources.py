"""Freeze current build inputs, including uncommitted work, without modifying them."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--repository', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root = args.repository.resolve()
    source = args.output / 'source'
    args.output.mkdir(parents=True, exist_ok=False)
    source.mkdir()
    scopes = ['crates', 'apps', 'tools', 'third_party', 'scripts', 'examples',
              'Cargo.toml', 'Cargo.lock', 'rust-toolchain.toml', '.cargo']
    def paths():
        result = subprocess.run(['git', '-C', str(root), 'ls-files', '-z', '--cached',
                                 '--others', '--exclude-standard', '--', *scopes],
                                capture_output=True, check=True)
        return sorted(set(result.stdout.decode('utf-8').rstrip('\0').split('\0')))
    selected = paths()
    pins = {}
    for relative in selected:
        path = root / relative
        if not path.exists():
            continue  # A tracked deletion is part of the current source state.
        if any(part.is_symlink() or getattr(part.lstat(), 'st_file_attributes', 0) & 0x400
               for part in [path, *path.parents] if part != root.parent):
            raise ValueError(f'Reparse input refused: {path}')
        pin = sha(path)
        destination = source / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, destination)
        if sha(destination) != pin or sha(path) != pin:
            raise ValueError(f'Source changed during snapshot: {relative}')
        pins[relative] = {'sha256': pin, 'bytes': path.stat().st_size}
    if paths() != selected or any(sha(root / name) != pin['sha256'] for name, pin in pins.items()):
        raise ValueError('Source changed during snapshot; retained snapshot is not accepted')
    head = subprocess.check_output(['git', '-C', str(root), 'rev-parse', 'HEAD'], text=True).strip()
    state = subprocess.check_output(['git', '-C', str(root), 'status', '--porcelain=v1'], text=True)
    (args.output / 'source-status.txt').write_text(state, encoding='utf-8')
    manifest = {'schema': 'forge.delivery.source-snapshot.v1', 'head': head,
                'contains_uncommitted_work': bool(state), 'files': pins}
    target = args.output / 'source-inputs.json'
    target.write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'source': str(source), 'files': len(pins),
                      'bytes': sum(pin['bytes'] for pin in pins.values()),
                      'manifest_sha256': sha(target), 'head': head}))


if __name__ == '__main__':
    main()
