"""Stream Unity YAML blocks and classify differences, without changing sources."""
import argparse
import collections
import hashlib
import json
import re
from pathlib import Path

import vault_storage as storage
from stage_source_support_relinks import norm

HEADER = re.compile(rb'^--- !u!(\d+) &(-?\d+)(?: stripped)?\s*$')
MAX_LINE = 4 * 1024 * 1024
MAX_SOURCE = 512 * 1024 * 1024


def blocks(stream):
    original = hashlib.sha256()
    records = {}
    current = None
    digest = hashlib.sha256()
    total = 0
    while True:
        line = stream.readline(MAX_LINE + 1)
        if not line:
            break
        total += len(line)
        if total > MAX_SOURCE or len(line) > MAX_LINE or b'\x00' in line:
            raise ValueError('source exceeds text comparison limits')
        original.update(line)
        match = HEADER.match(line.rstrip(b'\r\n'))
        if match:
            if current is not None:
                records[current] = digest.hexdigest()
            current = (int(match.group(1)), int(match.group(2)))
            if current in records:
                raise ValueError('duplicate Unity block id')
            digest = hashlib.sha256()
        if current is not None:
            digest.update(line.rstrip(b'\r\n') + b'\n')
    if current is None:
        raise ValueError('no Unity YAML block found')
    records[current] = digest.hexdigest()
    return original.hexdigest(), records


def audit(vault, inventory):
    vault = vault.resolve(strict=True)
    raw = inventory.read_bytes()
    inv = json.loads(raw)
    pointer = storage.confined(vault, vault/'metadata/asset-source-support-registry.v1.json')
    before = pointer.read_bytes()
    if storage.sha(before) != inv['source_registry_sha256']:
        raise ValueError('stale inventory')
    record = json.loads(before)
    catalogue = storage.confined(vault, vault/record['path']).read_bytes()
    if storage.sha(catalogue) != record['sha256']:
        raise ValueError('catalogue SHA mismatch')
    active_cards = json.loads(catalogue)['assets']
    cards = {x['id']: x for x in active_cards}
    for owner in active_cards:
        for alias in owner.get('consolidated_sources', []):
            original = alias['original_detail']
            ident = alias['retired_id']
            if ident != original.get('id') or ident in cards:
                raise ValueError('duplicate or invalid source alias')
            cards[ident] = original
    pairs = {}
    for item in inv['pairs']:
        a, b = (cards[item[key]] for key in ('hdrp_id', 'urp_id'))
        key = (a['source_file'], a['source_sha256'], b['source_file'], b['source_sha256'])
        pairs.setdefault(key, []).append([item['hdrp_id'], item['urp_id']])
    cache = {}

    def read(rel, pin):
        norm(rel)
        if (rel, pin) not in cache:
            path = storage.confined(vault, vault/rel)
            with storage.locked_source(path, share_delete=False) as stream:
                actual, result = blocks(stream)
            if actual != pin:
                raise ValueError('source SHA mismatch')
            cache[(rel, pin)] = result
        return cache[(rel, pin)]

    result = []
    for (a, ah, b, bh), ids in pairs.items():
        row = {'hdrp_source': a, 'urp_source': b, 'card_pairs': ids}
        try:
            left, right = read(a, ah), read(b, bh)
            keys = left.keys() | right.keys()
            changes = collections.Counter()
            for key in keys:
                state = ('only_hdrp' if key not in right else 'only_urp' if key not in left
                         else 'modified' if left[key] != right[key] else 'equal')
                changes[(str(key[0]), state)] += 1
            row.update(state='compared', blocks_by_class={
                cls: {state: count for (c, state), count in changes.items() if c == cls}
                for cls in sorted({c for c, _ in changes}, key=int)})
        except (OSError, ValueError, KeyError) as exc:
            row.update(state='review_required', reason=str(exc))
        result.append(row)
    if pointer.read_bytes() != before or inventory.read_bytes() != raw:
        raise ValueError('inputs changed')
    return {'state': 'audit_only', 'source_registry_sha256': storage.sha(before),
            'inventory_sha256': storage.sha(raw), 'results': result,
            'limitations': 'Block hashes do not establish geometry, dependency, render or deletion equivalence.'}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for name in ('vault', 'inventory', 'output'):
        p.add_argument('--'+name, required=True, type=Path)
    args = p.parse_args()
    result = audit(args.vault, args.inventory)
    with args.output.open('x', encoding='utf8') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({'pairs': len(result['results']),
                      'states': dict(collections.Counter(x['state'] for x in result['results']))}))
