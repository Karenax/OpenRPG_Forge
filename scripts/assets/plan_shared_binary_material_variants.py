"""Plan lossless storage for active material variants with identical binary chunks.

This does not consolidate catalogue identities or establish visual equivalence.
"""
import csv
import hashlib
import json
import stat
from pathlib import Path

import vault_storage

ROOT = Path(r'F:\OpenRPG_AssetVault')
BASE = Path(r'E:\OpenRPG_Forge\.build\asset-conversion')
REPORTS = Path(r'E:\OpenRPG_Forge\Preuves-et-analyses\rapports\audit-variantes-hdrp-urp-20261110')
AUDITS = ('townsmith-double-sided-audit-146-20261111.json',
          'audit-blocs-villa-forge-provenance-v2-20261111.json',
          'audit-blocs-modular-cave-provenance-v2-20261111.json',
          'audit-blocs-witch-house-20261111.json')


def main():
    index = ROOT / 'metadata/asset-import-registry.v1.json'
    before = index.read_bytes()
    registry = json.loads(before)
    with (REPORTS / 'variantes-paires.csv').open(encoding='utf-8-sig', newline='') as f:
        pairs = {r['id']: r for r in csv.DictReader(f)}
    selected = {}
    for name in AUDITS:
        data = json.loads((REPORTS / name).read_bytes())
        for row in data['results']:
            if (row.get('binary_chunk_exact') is True and row.get('truncated') is False
                    and any('/doubleSided' in x for x in row.get('differences', []))):
                selected[row['id']] = row
    active = {}
    for entry in registry['packs'].values():
        path = vault_storage.confined(ROOT, ROOT / entry['path'] / 'details.v1.json')
        raw = path.read_bytes()
        if hashlib.sha256(raw).hexdigest() != entry['files']['details.v1.json']:
            raise ValueError('active details SHA mismatch')
        for item in json.loads(raw)['assets']:
            active[item['id']] = item
    files, skips, seen = [], {}, {}
    for number, identity in enumerate(sorted(selected), 1):
        pair = pairs[identity]
        for side in ('main', 'variant'):
            asset = active.get(pair[side + '_id'])
            if not asset or asset.get('model_path') != pair[side + '_path'] or asset.get('model_sha256') != pair[side + '_sha']:
                skips['inactive_or_changed'] = skips.get('inactive_or_changed', 0) + 1
                continue
            path = vault_storage.confined(ROOT, Path(pair[side + '_path']))
            if path in seen:
                if seen[path] != pair[side + '_sha']:
                    raise ValueError('conflicting SHA for shared path')
                continue
            seen[path] = pair[side + '_sha']
            before_stat = path.stat()
            if getattr(before_stat, 'st_file_attributes', 0) & getattr(stat, 'FILE_ATTRIBUTE_READONLY', 1):
                skips['read_only'] = skips.get('read_only', 0) + 1
                continue
            with path.open('rb') as f:
                magic = f.read(8)
                if magic == b'ORPVLT1\n':
                    skips['already_shared'] = skips.get('already_shared', 0) + 1
                    continue
                if magic[:4] != b'glTF':
                    raise ValueError('unexpected model envelope')
                f.seek(0)
                digest = hashlib.file_digest(f, 'sha256').hexdigest()
            after = path.stat()
            if digest != pair[side + '_sha'] or (before_stat.st_size, before_stat.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
                raise ValueError('model changed since audit: ' + str(path))
            files.append({'path': str(path), 'bytes': after.st_size, 'mtime_ns': after.st_mtime_ns, 'sha256': digest})
        if number % 25 == 0:
            print(f'checked {number}/{len(selected)} pairs; {len(files)} raw files eligible', flush=True)
    if index.read_bytes() != before:
        raise ValueError('registry changed during planning')
    plan = {'schema': 1, 'root': str(ROOT), 'files': files,
            'logical_bytes': sum(x['bytes'] for x in files),
            'policy': {'scope': 'active exact-binary material variants; retain all identities and logical SHA',
                       'registry_sha256': hashlib.sha256(before).hexdigest(), 'skips': skips}}
    with (BASE / 'plan-mutualisation-materiaux-distincts-20261111.json').open('x', encoding='utf8') as f:
        json.dump(plan, f, ensure_ascii=False, indent=2); f.write('\n')
    print(json.dumps({'pairs': len(selected), 'files': len(files), 'bytes': plan['logical_bytes'], 'skips': skips}))


if __name__ == '__main__':
    main()
