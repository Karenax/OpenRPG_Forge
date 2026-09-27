"""Read-only, reproducible text inventory; not a copyright or provenance verdict."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import unicodedata


def strings(value, pointer=''):
    if isinstance(value, str):
        yield {'pointer': pointer, 'text': value}
    elif isinstance(value, dict):
        for key, child in value.items():
            escaped = key.replace('~', '~0').replace('/', '~1')
            yield from strings(child, pointer + '/' + escaped)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from strings(child, pointer + '/' + str(index))


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('duplicate JSON key: ' + key)
        result[key] = value
    return result


def reference_mentions(text):
    normalized = unicodedata.normalize('NFKC', text).casefold()
    return sorted(set(re.findall(r'\b(?:cadle|humpalumps)\b', normalized)))


def inventory(root):
    root = root.resolve(strict=True)
    entries = []
    for path in sorted(root.rglob('*')):
        if path.is_symlink():
            raise ValueError('symlink is outside inventory contract')
        if not path.is_file() or path.suffix.lower() not in {'.json', '.md'}:
            continue
        if not path.resolve().is_relative_to(root) or path.stat().st_size > 5 * 1024 * 1024:
            raise ValueError('unbounded text input')
        data = path.read_bytes()
        text = data.decode('utf-8-sig')
        values = list(strings(json.loads(text, object_pairs_hook=unique_object))) \
            if path.suffix.lower() == '.json' else [{'pointer': '', 'text': text}]
        entries.append({'path': path.relative_to(root).as_posix(),
                        'sha256': hashlib.sha256(data).hexdigest(),
                        'reference_tokens_in_raw_text': reference_mentions(text),
                        'strings': values})
    if not entries:
        raise ValueError('empty text inventory')
    for entry in entries:
        if hashlib.sha256((root / entry['path']).read_bytes()).hexdigest() != entry['sha256']:
            raise ValueError('source changed during inventory')
    return {'schema': 'forge.text-source-inventory', 'schema_version': 1,
            'source_root': str(root), 'status': 'inventory_only_requires_review',
            'copyright_clearance': False, 'derivation_excluded': False,
            'binary_assets_assessed': False, 'files': entries}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    output = args.output.resolve()
    if output.exists() or output.is_relative_to(args.source.resolve(strict=True)):
        raise ValueError('fresh output outside sources required')
    result = inventory(args.source)
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open('x', encoding='utf-8') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
    print(json.dumps({'status': result['status'], 'files': len(result['files']),
                      'strings': sum(len(f['strings']) for f in result['files'])}))


if __name__ == '__main__':
    main()
