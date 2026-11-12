"""Prepare exact legacy ID, UUID and SHA patterns for project reference audits."""
import argparse
import json
import re
import uuid
from pathlib import Path


NAMESPACE = uuid.UUID('9e65f3d9-f8e2-5f10-8d1e-3e0b6e4d7c21')


def vault_uuid(asset_id):
    return str(uuid.uuid5(NAMESPACE, 'openrpg-forge:asset:asset-vault/v1:' + asset_id))


def patterns(plan):
    result = set()
    for entry in plan['candidates']:
        for prefix in ('retained', 'retired'):
            ident = entry[prefix + '_id']
            digest = entry[prefix + '_sha256']
            if not isinstance(ident, str) or not ident or '\n' in ident or '\r' in ident:
                raise ValueError('invalid identity')
            if not re.fullmatch('[0-9a-f]{64}', digest):
                raise ValueError('invalid SHA-256')
            result.update((ident, vault_uuid(ident), digest))
    return sorted(result)


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--plan', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    rows = patterns(json.loads(args.plan.read_text(encoding='utf-8')))
    with args.output.open('x', encoding='utf-8', newline='\n') as stream:
        stream.write('\n'.join(rows) + '\n')
    print(json.dumps({'patterns': len(rows), 'format': 'exact identity, UUID or logical SHA-256'}))
