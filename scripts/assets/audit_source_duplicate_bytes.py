"""Verify source-card byte duplicates; report references, never delete."""
import argparse
import collections
import csv
import hashlib
import json
import pathlib


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def audit(vault, directory):
    vault, directory = pathlib.Path(vault).resolve(), pathlib.Path(directory)
    pointer_path = vault / 'metadata/asset-source-support-registry.v1.json'
    pointer_bytes = pointer_path.read_bytes()
    pointer = json.loads(pointer_bytes)
    catalog_bytes = (vault / pointer['path']).read_bytes()
    if hashlib.sha256(catalog_bytes).hexdigest() != pointer['sha256']:
        raise ValueError('source support hash mismatch')
    cards = json.loads(catalog_bytes)['assets']
    refs = collections.Counter(str(c.get('source_file', '')).lower().replace('\\', '/') for c in cards)
    with (directory / 'variantes-sources.csv').open(encoding='utf-8-sig', newline='') as f:
        candidates = [r for r in csv.DictReader(f) if r['same_source_sha'] == 'True'
                      and r['hdrp_source'].lower().replace('\\', '/') != r['urp_source'].lower().replace('\\', '/')]
    results = []
    for row in candidates:
        out = {k: row[k] for k in ('pack','normalized_path','hdrp_source','urp_source','hdrp_sha','urp_sha','same_meta_sha')}
        try:
            left = (vault / row['hdrp_source']).resolve(strict=True)
            right = (vault / row['urp_source']).resolve(strict=True)
            if not left.is_relative_to(vault) or not right.is_relative_to(vault):
                raise ValueError('source outside vault')
            left_sha, right_sha = digest(left), digest(right)
            out.update(hdrp_actual_sha=left_sha, urp_actual_sha=right_sha,
                       hdrp_bytes=left.stat().st_size, urp_bytes=right.stat().st_size,
                       hdrp_active_source_cards=refs[row['hdrp_source'].lower().replace('\\', '/')],
                       urp_active_source_cards=refs[row['urp_source'].lower().replace('\\', '/')],
                       bytes_verified=left_sha == right_sha == row['hdrp_sha'] == row['urp_sha'], error='')
        except (ValueError, OSError) as error:
            out.update(hdrp_actual_sha='', urp_actual_sha='', hdrp_bytes='', urp_bytes='',
                       hdrp_active_source_cards='', urp_active_source_cards='', bytes_verified=False, error=str(error))
        out.update(deletable_verified=False, status='active_source_requires_relink_and_dependency_audit')
        results.append(out)
    if pointer_path.read_bytes() != pointer_bytes:
        raise ValueError('source registry changed')
    result_path = directory / 'sources-identiques-chemins-distincts.csv'
    if result_path.exists():
        raise ValueError('output already exists')
    with result_path.open('x', encoding='utf-8-sig', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=list(results[0]) if results else ['pack'])
        writer.writeheader(); writer.writerows(results)
    verified = [r for r in results if r['bytes_verified']]
    potential = {r['urp_source'].lower().replace('\\', '/'): r['urp_bytes'] for r in verified}
    return dict(candidates=len(results), verified=len(verified), failed=len(results)-len(verified),
                distinct_urp_paths=len(potential), gross_urp_bytes=sum(potential.values()),
                proven_reclaimable_bytes=0, note='active source references and dependencies not rewritten')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=pathlib.Path, required=True)
    p.add_argument('--output', type=pathlib.Path, required=True)
    args = p.parse_args()
    print(json.dumps(audit(args.vault, args.output), ensure_ascii=False))
