"""Find exact candidate-path references in verified active Vault catalogues."""
import argparse
import collections
import csv
import hashlib
import json
from pathlib import Path


def digest(data):
    return hashlib.sha256(data).hexdigest()


def norm(value):
    value = value.replace('\\', '/').casefold()
    if value.startswith('f:/openrpg_assetvault/'):
        value = value[len('f:/openrpg_assetvault/'):]
    return value


def strings(value, path=''):
    if isinstance(value, str):
        yield path, value
    elif isinstance(value, dict):
        for k, v in value.items():
            yield from strings(v, path + '/' + str(k))
    elif isinstance(value, list):
        for i, v in enumerate(value):
            yield from strings(v, path + '/' + str(i))


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--vault', type=Path, required=True)
    p.add_argument('--candidates', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    vault = a.vault.resolve(strict=True)
    with a.candidates.open(encoding='utf-8-sig', newline='') as f:
        candidates = list(csv.DictReader(f))
    keys = {norm(x['candidate']): x for x in candidates}
    if len(keys) != len(candidates):
        raise ValueError('duplicate candidate path')
    import_path = vault / 'metadata/asset-import-registry.v1.json'
    source_path = vault / 'metadata/asset-source-support-registry.v1.json'
    imp_raw, src_raw = import_path.read_bytes(), source_path.read_bytes()
    imp, src = json.loads(imp_raw), json.loads(src_raw)
    source_data = (vault / src['path']).read_bytes()
    if digest(source_data) != src['sha256']:
        raise ValueError('source catalogue changed')
    found = collections.defaultdict(list)
    def inspect(document, scope):
        for key, value in strings(document):
            candidate = norm(value)
            if candidate in keys:
                found[candidate].append(scope + key)
    inspect(json.loads(source_data), 'source_catalogue')
    for pack, entry in sorted(imp['packs'].items()):
        for filename in ('details.v1.json', 'profiles.v1.json'):
            raw = (vault / entry['path'] / filename).read_bytes()
            if digest(raw) != entry['files'][filename]:
                raise ValueError('catalogue SHA mismatch: ' + pack + '/' + filename)
            inspect(json.loads(raw), 'import_catalogue/' + pack + '/' + filename)
    if import_path.read_bytes() != imp_raw or source_path.read_bytes() != src_raw:
        raise ValueError('registry changed during audit')
    with a.output.open('x', encoding='utf-8-sig', newline='') as f:
        fields = ['candidate', 'logical_bytes', 'sha256', 'active_reference_count', 'active_references', 'deletable_verified']
        writer = csv.DictWriter(f, fieldnames=fields); writer.writeheader()
        for key, candidate in keys.items():
            writer.writerow({'candidate': candidate['candidate'], 'logical_bytes': candidate['logical_bytes'],
                             'sha256': candidate['sha256'], 'active_reference_count': len(found[key]),
                             'active_references': json.dumps(found[key], ensure_ascii=False), 'deletable_verified': False})
    print(json.dumps({'candidates': len(keys), 'referenced_paths':sum(bool(found[x]) for x in keys),
                      'total_exact_matches': sum(len(found[x]) for x in keys),
                      'import_registry_sha256': digest(imp_raw), 'source_registry_sha256': digest(src_raw)}))


if __name__ == '__main__':
    main()
