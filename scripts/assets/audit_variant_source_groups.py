"""Audit source-support groups as files, retaining every card and dependency."""
import argparse
import collections
import csv
import hashlib
import json
import pathlib
import re


def sha_bytes(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--vault', type=pathlib.Path, required=True)
    parser.add_argument('--output', type=pathlib.Path, required=True)
    args = parser.parse_args()
    vault = args.vault.resolve(strict=True)
    pointer_file = vault / 'metadata/asset-source-support-registry.v1.json'
    pointer_raw = pointer_file.read_bytes()
    pointer = json.loads(pointer_raw)
    raw = (vault / pointer['path']).read_bytes()
    if sha_bytes(raw) != pointer['sha256']:
        raise ValueError('source catalogue SHA mismatch')
    groups = collections.defaultdict(list)
    for asset in json.loads(raw)['assets']:
        path = (asset.get('source_asset_path') or '').replace('\\', '/')
        m = re.findall(r'(?i)(?:^|/)(hdrp(?:\(default\))?|urp(?:\(default\))?)(?=/)', path)
        if len(m) != 1:
            continue
        pipeline = 'hdrp' if m[0].lower().startswith('hdrp') else 'urp'
        norm = re.sub(r'(?i)(?<=/)(?:hdrp(?:\(default\))?|urp(?:\(default\))?)(?=/)', '<pipeline>', path).lower()
        pack = re.sub(r'\.variant-[a-f0-9]+$', '', asset.get('pack_id', ''))
        groups[(pack, norm)].append((pipeline, asset))
    cache = {}
    def verify(path, expected):
        key = (path, expected)
        if key in cache:
            return cache[key]
        try:
            p = (vault / path).resolve(strict=True)
            if not path or not p.is_relative_to(vault) or not expected:
                raise ValueError('missing or outside-vault path/hash')
            h = hashlib.sha256()
            size = 0
            with p.open('rb') as f:
                for chunk in iter(lambda: f.read(1024 * 1024), b''):
                    h.update(chunk); size += len(chunk)
            result = {'path': path, 'expected_sha256': expected, 'actual_sha256': h.hexdigest(),
                      'bytes': size, 'match': h.hexdigest() == expected}
        except (OSError, ValueError) as error:
            result = {'path': path, 'expected_sha256': expected, 'match': False, 'error': str(error)}
        cache[key] = result
        return result
    fields = ['pack', 'normalized_path', 'hdrp_card_count', 'urp_card_count', 'hdrp_files', 'urp_files',
              'hdrp_meta', 'urp_meta', 'all_source_hashes_verified', 'all_meta_hashes_verified',
              'all_source_contents_identical', 'all_meta_contents_identical', 'deletable_verified']
    rows = []
    selected = [(k, v) for k, v in sorted(groups.items()) if {s for s, _ in v} == {'hdrp', 'urp'}]
    for n, ((pack, path), entries) in enumerate(selected, 1):
        sides = {side: [a for s, a in entries if s == side] for side in ('hdrp','urp')}
        def checked(side, file_key, hash_key):
            identities = sorted({(a.get(file_key, ''), a.get(hash_key, '')) for a in sides[side]})
            return [verify(path, h) for path, h in identities]
        hf, uf = (checked(s, 'source_file', 'source_sha256') for s in ('hdrp','urp'))
        hm, um = (checked(s, 'source_meta_file', 'source_meta_sha256') for s in ('hdrp','urp'))
        def valid(rs):
            return bool(rs) and all(x['match'] for x in rs)
        def identical(rs):
            return valid(rs) and len({x['actual_sha256'] for x in rs}) == 1
        rows.append({'pack': pack, 'normalized_path': path,
                     'hdrp_card_count': len(sides['hdrp']), 'urp_card_count': len(sides['urp']),
                     'hdrp_files': json.dumps(hf, ensure_ascii=False), 'urp_files': json.dumps(uf, ensure_ascii=False),
                     'hdrp_meta': json.dumps(hm, ensure_ascii=False), 'urp_meta': json.dumps(um, ensure_ascii=False),
                     'all_source_hashes_verified': valid(hf + uf), 'all_meta_hashes_verified': valid(hm + um),
                     'all_source_contents_identical': identical(hf + uf), 'all_meta_contents_identical': identical(hm + um),
                     'deletable_verified': False})
        if n % 100 == 0 or n == len(selected):
            print(f'{n}/{len(selected)} groupes; {len(cache)} fichiers et empreintes', flush=True)
    if pointer_file.read_bytes() != pointer_raw:
        raise ValueError('source registry changed during audit')
    with args.output.open('x', encoding='utf-8-sig', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=fields); writer.writeheader(); writer.writerows(rows)
    print(json.dumps({'groups':len(rows),'unique_file_hash_pairs':len(cache),
                      'source_verified':sum(x['all_source_hashes_verified'] for x in rows),
                      'meta_verified':sum(x['all_meta_hashes_verified'] for x in rows),
                      'source_identical':sum(x['all_source_contents_identical'] for x in rows),
                      'meta_identical':sum(x['all_meta_contents_identical'] for x in rows),
                      'source_registry_sha256':sha_bytes(pointer_raw), 'catalogue_sha256':pointer['sha256']}), flush=True)


if __name__ == '__main__':
    main()
