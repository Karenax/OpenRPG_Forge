"""Compare existing catalogue thumbnails for a prepared consolidation.

The thumbnails are historical evidence, not proof of a current Studio render.
No file in the Vault is modified.
"""
import argparse
import hashlib
import json
from pathlib import Path

import vault_storage


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def preview(vault, detail, ident, catalog_cache):
    catalog_path = vault_storage.confined(vault, Path(detail['source_catalog']))
    if catalog_path not in catalog_cache:
        raw = catalog_path.read_bytes()
        document = json.loads(raw)
        index = {}
        for entry in document['assets']:
            if entry['id'] in index:
                raise ValueError('duplicate source catalogue identity')
            index[entry['id']] = entry
        catalog_cache[catalog_path] = (sha(raw), index)
    entry = catalog_cache[catalog_path][1].get(ident)
    if entry is None or entry.get('sha256') != detail.get('model_sha256'):
        raise ValueError('catalogue identity or model SHA mismatch: ' + ident)
    path, expected = entry.get('preview'), entry.get('preview_sha256')
    if not path or not expected:
        return {'state': 'missing'}
    relative = Path(path)
    if relative.is_absolute() or '..' in relative.parts:
        raise ValueError('preview path escapes catalogue')
    try:
        preview_path = vault_storage.confined(vault, catalog_path.parent / relative)
        actual = sha(preview_path.read_bytes())
    except FileNotFoundError:
        return {'state': 'missing_declared', 'declared_preview': path, 'expected_sha256': expected}
    if actual != expected:
        raise ValueError('preview SHA mismatch: ' + ident)
    return {'state': 'verified', 'path': str(preview_path), 'sha256': actual}


def audit(vault, stage):
    vault, stage = vault.resolve(strict=True), stage.resolve(strict=True)
    registry = vault / 'metadata/asset-import-registry.v1.json'
    before = registry.read_bytes()
    manifest = json.loads((stage / 'transition.json').read_bytes())
    if sha(before) != manifest['registry_sha256']:
        raise ValueError('registry changed')
    wanted = {x['retired_id'] for x in manifest['aliases_requiring_project_migration']}
    results, catalogs = [], {}
    for pack, pins in manifest['packs'].items():
        raw = (stage / pack / 'details.v1.json').read_bytes()
        if sha(raw) != pins['details.v1.json']:
            raise ValueError('staged details changed')
        for item in json.loads(raw)['assets']:
            for old in item.get('consolidated_variants', []):
                old_id = old['retired_id']
                if old_id not in wanted:
                    continue
                a = preview(vault, item, item['id'], catalogs)
                b = preview(vault, old['original_detail'], old_id, catalogs)
                results.append({'retained_id': item['id'], 'retired_id': old_id,
                                'retained_preview': a, 'retired_preview': b,
                                'same_bytes': a['state'] == b['state'] == 'verified' and a['sha256'] == b['sha256']})
    if {r['retired_id'] for r in results} != wanted or len(results) != len(wanted):
        raise ValueError('missing or repeated aliases')
    if registry.read_bytes() != before or any(sha(path.read_bytes()) != data[0] for path, data in catalogs.items()):
        raise ValueError('registry or source catalogue changed during audit')
    return {'state': 'finished', 'registry_sha256': sha(before),
            'stage_manifest_sha256': sha((stage / 'transition.json').read_bytes()),
            'limitations': 'Existing thumbnails are not a current rendering or placement qualification.',
            'results': results}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--stage', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = audit(args.vault, args.stage)
    with args.output.open('x', encoding='utf8', newline='\n') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({'pairs': len(result['results']), 'same_bytes': sum(x['same_bytes'] for x in result['results']),
                      'missing': sum(x['retained_preview']['state'] != 'verified' or x['retired_preview']['state'] != 'verified' for x in result['results'])}))
