"""Compare pinned source-card identities; never recommends deleting a source."""
import argparse
import collections
import csv
import hashlib
import json
import pathlib
import re


def audit(vault, output):
    vault = pathlib.Path(vault)
    pointer_path = vault / 'metadata/asset-source-support-registry.v1.json'
    original = pointer_path.read_bytes()
    pointer = json.loads(original)
    raw = (vault / pointer['path']).read_bytes()
    if hashlib.sha256(raw).hexdigest() != pointer['sha256']:
        raise ValueError('Source support catalog hash mismatch')
    data = json.loads(raw)
    groups = collections.defaultdict(list)
    unidentified = 0
    for item in data['assets']:
        path = (item.get('source_asset_path') or '').replace('\\', '/')
        matches = re.findall(r'(?i)(?:^|/)(hdrp(?:\(default\))?|urp(?:\(default\))?)(?=/)', path)
        if len(matches) != 1:
            unidentified += 1
            continue
        side = 'hdrp' if matches[0].lower().startswith('hdrp') else 'urp'
        normalized = re.sub(r'(?i)(?<=/)(?:hdrp(?:\(default\))?|urp(?:\(default\))?)(?=/)', '<pipeline>', path).lower()
        pack = re.sub(r'\.variant-[a-f0-9]+$', '', item.get('pack_id', ''))
        groups[(pack, normalized)].append((side, item))
    rows = []
    for (pack, path), items in sorted(groups.items()):
        h = [a for side, a in items if side == 'hdrp']
        u = [a for side, a in items if side == 'urp']
        if not h or not u:
            continue
        pair = len(h) == len(u) == 1
        a, b = (h[0], u[0]) if pair else ({}, {})
        rows.append(dict(pack=pack, normalized_path=path, hdrp_count=len(h), urp_count=len(u),
                         hdrp_ids=';'.join(x['id'] for x in h), urp_ids=';'.join(x['id'] for x in u),
                         hdrp_source=a.get('source_file', ''), urp_source=b.get('source_file', ''),
                         hdrp_sha=a.get('source_sha256', ''), urp_sha=b.get('source_sha256', ''),
                         same_source_sha=pair and bool(a.get('source_sha256')) and a.get('source_sha256') == b.get('source_sha256'),
                         same_meta_sha=pair and bool(a.get('source_meta_sha256')) and a.get('source_meta_sha256') == b.get('source_meta_sha256'),
                         decision='source_and_dependencies_preserved', deletable_verified=False))
    if pointer_path.read_bytes() != original:
        raise ValueError('Source registry changed during audit')
    output = pathlib.Path(output)
    output.mkdir(parents=True, exist_ok=True)
    path = output / 'variantes-sources.csv'
    if path.exists():
        raise ValueError('Output already exists')
    with path.open('x', encoding='utf-8-sig', newline='') as file:
        writer = csv.DictWriter(file, fieldnames=list(rows[0]) if rows else ['pack'])
        writer.writeheader(); writer.writerows(rows)
    return dict(source_cards=len(data['assets']), unidentified_pipeline=unidentified,
                paired=sum(r['hdrp_count'] == r['urp_count'] == 1 for r in rows),
                ambiguous=sum(r['hdrp_count'] != 1 or r['urp_count'] != 1 for r in rows),
                same_source_sha=sum(r['same_source_sha'] for r in rows),
                source_catalog_sha256=pointer['sha256'])


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=pathlib.Path, required=True)
    p.add_argument('--output', type=pathlib.Path, required=True)
    a = p.parse_args()
    print(json.dumps(audit(a.vault, a.output), ensure_ascii=False))
