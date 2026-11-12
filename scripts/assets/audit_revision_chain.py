"""Classify registered asset snapshots against recorded transaction ancestry.

Read-only audit: ancestry is not a proof that a revision is disposable. Journals,
external consumers and shared packs are not changed by this tool.
"""
import argparse
import collections
import json
from pathlib import Path

import vault_storage as storage


def trace(pack, head, read_event):
    seen = set()
    revisions = set()
    node = head
    issues = []
    while node is not None:
        tx = node.get('transaction')
        if not isinstance(tx, str) or len(tx) != 32 or any(c not in '0123456789abcdef' for c in tx):
            issues.append('invalid transaction identity')
            break
        if tx in seen:
            issues.append('transaction cycle')
            break
        seen.add(tx)
        revisions.add((node.get('path'), node.get('revision')))
        try:
            event = read_event(tx)
        except (OSError, ValueError) as exc:
            issues.append('unreadable transaction: ' + str(exc))
            break
        if event.get('operation') == 'apply-atomic-pack-pair':
            if event.get('transaction') != tx:
                issues.append('group transaction identity mismatch')
                break
            next_node = event.get('next_registry', {}).get('packs', {}).get(pack)
            previous = event.get('previous_registry', {}).get('packs', {}).get(pack)
            same_pack = next_node is not None
        else:
            next_node = event.get('next')
            previous = event.get('previous')
            same_pack = event.get('pack_id') == pack
        if not same_pack or next_node != node or event.get('state') != 'committed':
            issues.append('transaction pointer or state mismatch')
            break
        if event.get('operation') not in ('apply', 'rollback', 'apply-atomic-pack-pair'):
            issues.append('unknown transaction operation')
            break
        node = previous
    return revisions, seen, issues


def audit(vault):
    vault = vault.resolve(strict=True)
    index = storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    data = index.read_bytes()
    registry = json.loads(data)
    totals = collections.Counter()
    rows = []
    for pack, head in sorted(registry['packs'].items()):
        def read_event(tx):
            path = storage.confined(vault, vault / 'metadata/asset-import-history' / (tx + '.json'))
            return json.loads(path.read_bytes())
        reachable, transactions, issues = trace(pack, head, read_event)
        history = {(item['path'], item['revision']) for item in head.get('history', [])}
        on_chain = history & reachable
        outside = history - reachable
        totals.update({'packs': 1, 'transactions': len(transactions), 'history': len(history),
                       'history_on_chain': len(on_chain), 'history_outside_chain': len(outside),
                       'packs_with_issues': bool(issues)})
        rows.append({'pack': pack, 'transactions': len(transactions), 'history': len(history),
                     'history_on_chain': len(on_chain),
                     'history_outside_chain': [{'path': p, 'revision': r} for p, r in sorted(outside)],
                     'issues': issues})
    if index.read_bytes() != data:
        raise ValueError('registry changed during audit')
    return {'registry_sha256': storage.sha(data), 'totals': dict(totals), 'packs': rows,
            'limitations': 'Ancestry only; does not establish use, restorability, or safe removal.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    result = audit(args.vault)
    with args.output.open('x', encoding='utf-8', newline='\n') as out:
        json.dump(result, out, ensure_ascii=False, indent=2)
        out.write('\n')
    print(json.dumps(result['totals']))
