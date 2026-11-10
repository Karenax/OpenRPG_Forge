"""Read-only catalogue audit. Output is triage, never a deletion authorization."""
import argparse
import collections
import csv
import hashlib
import json
import os
import pathlib
import re
import sys

import vault_storage


def sha(data):
    return hashlib.sha256(data).hexdigest()


def base_id(ident):
    return re.sub(r'\.variant-[a-f0-9]+(?=\.)', '', ident, count=1)


def hashes(value):
    if isinstance(value, list):
        return sorted(h for item in value for h in hashes(item))
    if isinstance(value, dict):
        return sorted([v for k, v in value.items() if k == 'sha256' and isinstance(v, str) and len(v) == 64] +
                      [h for k, v in value.items() if k != 'sha256' for h in hashes(v)])
    return []


def same_present(a, b):
    return bool(a) and a == b


def pipeline(value):
    text = json.dumps(value, ensure_ascii=False).replace('\\\\', '/')
    found = {x.lower() for x in re.findall(r'(?i)(?:^|/)(hdrp|urp)(?:\(default\))?(?=/)', text)}
    return next(iter(found)) if len(found) == 1 else 'unknown'


def audit(vault, output, verify_shared):
    vault = pathlib.Path(vault).resolve()
    registry_path = vault / 'metadata/asset-import-registry.v1.json'
    original = registry_path.read_bytes()
    registry = json.loads(original)
    grouped = collections.defaultdict(list)
    refs = collections.Counter()
    for pack, entry in sorted(registry['packs'].items()):
        data = (vault / entry['path'] / 'details.v1.json').read_bytes()
        if sha(data) != entry['files']['details.v1.json']:
            raise ValueError('Changed or corrupt details: ' + pack)
        for asset in json.loads(data)['assets']:
            grouped[base_id(asset['id'])].append(asset)
            if asset.get('model_path'):
                refs[os.path.normcase(os.path.normpath(asset['model_path']))] += 1
    rows = []
    fields = ['names', 'descriptions', 'taxonomy', 'tags', 'anchors', 'relationships',
              'source_fbx', 'source_prefab', 'license', 'private_only']
    for key, group in sorted(grouped.items()):
        main = [a for a in group if '.variant-' not in a['id']]
        alt = [a for a in group if '.variant-' in a['id']]
        if len(main) != 1 or len(alt) != 1:
            continue
        a, b = main[0], alt[0]
        ma, mb = a.get('measurement') or {}, b.get('measurement') or {}
        pa, pb = a.get('model_path', ''), b.get('model_path', '')
        same_path = bool(pa) and os.path.normcase(os.path.normpath(pa)) == os.path.normcase(os.path.normpath(pb))
        same_sha = bool(a.get('model_sha256')) and a.get('model_sha256') == b.get('model_sha256')
        row = dict(id=key, main_id=a['id'], variant_id=b['id'], main_path=pa, variant_path=pb,
                   main_sha=a.get('model_sha256', ''), variant_sha=b.get('model_sha256', ''),
                   main_pipeline=pipeline([a.get('source_fbx'), a.get('source_prefab')]),
                   variant_pipeline=pipeline([b.get('source_fbx'), b.get('source_prefab')]),
                   same_path=same_path, same_declared_sha=same_sha,
                   same_nonempty_image_hashes=same_present(hashes(ma.get('images')), hashes(mb.get('images'))),
                   same_nonempty_material_metadata=same_present(ma.get('materials'), mb.get('materials')),
                   same_measured_counts_bounds=all(ma.get(k) is not None and ma.get(k) == mb.get(k) for k in ('vertices','triangles','bounds')),
                   same_source_fbx_hashes=same_present(hashes(a.get('source_fbx')), hashes(b.get('source_fbx'))),
                   same_source_prefab_hashes=same_present(hashes(a.get('source_prefab')), hashes(b.get('source_prefab'))),
                   different_metadata=';'.join(f for f in fields if a.get(f) != b.get(f)),
                   main_active_model_refs=refs[os.path.normcase(os.path.normpath(pa))] if pa else 0,
                   variant_active_model_refs=refs[os.path.normcase(os.path.normpath(pb))] if pb else 0,
                   same_path_logical_hash_verified='not_requested',
                   decision='shared_file_preserve_ids_and_metadata' if same_path and same_sha else 'review_required',
                   deletable_verified=False)
        rows.append(row)
    if verify_shared:
        selected = [r for r in rows if r['same_path'] and r['same_declared_sha']]
        for n, row in enumerate(selected, 1):
            try:
                p = pathlib.Path(row['main_path']).resolve(strict=True)
                if not p.is_relative_to(vault):
                    raise ValueError('model outside Vault')
                actual = vault_storage.digest(vault, p)
                row['same_path_logical_hash_verified'] = 'match' if actual == row['main_sha'] else 'mismatch'
            except Exception as error:
                row['same_path_logical_hash_verified'] = 'error: ' + str(error)
            if n % 50 == 0 or n == len(selected):
                print(f'shared model verification {n}/{len(selected)}', flush=True)
    if registry_path.read_bytes() != original:
        raise ValueError('Registry changed during audit; results not written')
    output = pathlib.Path(output)
    output.mkdir(parents=True, exist_ok=True)
    csv_path = output / 'variantes-paires.csv'
    summary_path = output / 'resume.json'
    if csv_path.exists() or summary_path.exists():
        raise ValueError('Output exists; choose a new output directory')
    with csv_path.open('x', encoding='utf-8-sig', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=list(rows[0]) if rows else ['id'])
        writer.writeheader(); writer.writerows(rows)
    summary = dict(registry_sha256=sha(original), registry_packs=len(registry['packs']), pairs=len(rows),
                   same_path=sum(r['same_path'] for r in rows),
                   same_declared_sha=sum(r['same_declared_sha'] for r in rows),
                   shared_hash_match=sum(r['same_path_logical_hash_verified']=='match' for r in rows),
                   shared_hash_not_matched=sum(r['same_path_logical_hash_verified'] not in ('match','not_requested') for r in rows),
                   deletable_verified=0, immediate_reclaimable_bytes_proven=0,
                   limitations=['ID pairing does not prove pipeline identity', 'No project/save or historical reference audit',
                                'No individual material graph or geometry byte comparison',
                                'No packed-block reachability or physical compaction calculation',
                                'Same logical file cannot be deleted while retained by another card'])
    with summary_path.open('x', encoding='utf-8') as f:
        json.dump(summary, f, ensure_ascii=False, indent=2); f.write('\n')
    return summary


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=pathlib.Path, required=True)
    p.add_argument('--output', type=pathlib.Path, required=True)
    p.add_argument('--verify-shared', action='store_true')
    args = p.parse_args()
    print(json.dumps(audit(args.vault, args.output, args.verify_shared), ensure_ascii=False), flush=True)
