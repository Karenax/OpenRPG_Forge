"""Summarize bounded source differences for still-active named variant pairs."""
import argparse
import collections
import difflib
import json
from pathlib import Path

import vault_storage as storage
from stage_source_support_relinks import norm


def audit(vault, inventory):
    vault = vault.resolve(strict=True)
    raw_inv = inventory.read_bytes()
    inv = json.loads(raw_inv)
    pointer_path = storage.confined(vault, vault / 'metadata/asset-source-support-registry.v1.json')
    pointer_raw = pointer_path.read_bytes()
    if storage.sha(pointer_raw) != inv['source_registry_sha256']:
        raise ValueError('source inventory is stale')
    pointer = json.loads(pointer_raw)
    catalogue_raw = storage.confined(vault, vault / pointer['path']).read_bytes()
    if storage.sha(catalogue_raw) != pointer['sha256']:
        raise ValueError('catalogue SHA mismatch')
    cards = {row['id']: row for row in json.loads(catalogue_raw)['assets']}
    grouped = {}
    for pair in inv['pairs']:
        a, b = (cards[pair[key]] for key in ('hdrp_id', 'urp_id'))
        key = (a['source_file'], a['source_sha256'], b['source_file'], b['source_sha256'])
        grouped.setdefault(key, []).append([pair['hdrp_id'], pair['urp_id']])
    cache = {}

    def read(rel, pin):
        norm(rel)
        path = storage.confined(vault, vault / rel)
        if (rel, pin) not in cache:
            if path.stat().st_size > 2 * 1024 * 1024:
                raise ValueError('source above 2 MiB comparison limit')
            with storage.locked_source(path, share_delete=False) as stream:
                raw = stream.read(2 * 1024 * 1024 + 1)
            if len(raw) > 2 * 1024 * 1024 or storage.sha(raw) != pin:
                raise ValueError('source changed or SHA mismatch')
            lines = raw.decode('utf-8-sig').splitlines()
            if len(lines) > 10000:
                raise ValueError('source above 10000 line comparison limit')
            cache[(rel, pin)] = lines
        return cache[(rel, pin)]

    results = []
    for (ap, ah, bp, bh), ids in grouped.items():
        row = {'hdrp_source': ap, 'urp_source': bp, 'card_pairs': ids}
        try:
            a, b = read(ap, ah), read(bp, bh)
            diff = difflib.SequenceMatcher(None, a, b, autojunk=True)
            hunks = []
            for op, i, j, k, l in diff.get_opcodes():
                if op != 'equal':
                    hunks.append({'operation': op, 'hdrp_lines': j-i, 'urp_lines': l-k,
                                  'hdrp_sample': a[i:min(j, i+4)], 'urp_sample': b[k:min(l, k+4)]})
            row.update(state='compared', hunk_count=len(hunks), hunks=hunks[:30],
                       hunks_truncated=len(hunks)>30)
        except (OSError, UnicodeError, ValueError, KeyError) as exc:
            row.update(state='review_required', reason=str(exc))
        results.append(row)
    if pointer_path.read_bytes() != pointer_raw or inventory.read_bytes() != raw_inv:
        raise ValueError('inputs changed during audit')
    return {'state': 'audit_only', 'source_registry_sha256': storage.sha(pointer_raw),
            'inventory_sha256': storage.sha(raw_inv), 'results': results,
            'limitations': 'Text diff samples only; no material, dependency or visual equivalence established.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('vault', 'inventory', 'output'):
        parser.add_argument('--' + name, required=True, type=Path)
    args = parser.parse_args()
    result = audit(args.vault, args.inventory)
    with args.output.open('x', encoding='utf8') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({'source_pairs': len(result['results']),
                      'states': dict(collections.Counter(x['state'] for x in result['results']))}))
