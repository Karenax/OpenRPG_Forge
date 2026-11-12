"""Restore retired source paths from pinned identical copies when needed.

This utility never changes a catalog or overwrites an existing file. It is a
recovery tool, not a routine copy or authorization to delete originals.
"""
import argparse
import os
from pathlib import Path

import vault_storage as storage
from stage_source_support_relinks import digest_file, norm


def restore_one(vault, item):
    vault = vault.resolve(strict=True)
    old, retained, pin = item['old'], item['retained'], item['sha256']
    norm(old)
    norm(retained)
    if not storage.valid_hex(pin, 64) or norm(old) == norm(retained):
        raise ValueError('invalid recovery mapping')
    source = storage.confined(vault, vault/retained)
    target = vault/old
    parent = storage.confined(vault, target.parent)
    if digest_file(source) != pin:
        raise ValueError('retained source SHA differs')
    if target.exists():
        target = storage.confined(vault, target)
        if digest_file(target) != pin:
            raise ValueError('existing recovery target differs')
        return 'already_present'
    # Exclusive creation cannot overwrite a concurrently created target. An
    # interrupted partial is deliberately left for inspection and never reused.
    with source.open('rb') as src, target.open('xb') as dst:
        for block in iter(lambda: src.read(4 * 1024 * 1024), b''):
            dst.write(block)
        dst.flush()
        os.fsync(dst.fileno())
    storage.confined(vault, parent)
    target = storage.confined(vault, target)
    if digest_file(target) != pin or digest_file(source) != pin:
        raise ValueError('recovery readback mismatch; preserve both files for inspection')
    return 'restored'


if __name__ == '__main__':
    import json
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', required=True, type=Path)
    p.add_argument('--plan', required=True, type=Path)
    p.add_argument('--old-path', required=True, help='exact original Vault-relative source path')
    args = p.parse_args()
    plan = json.loads(args.plan.read_bytes())
    if plan.get('schema') not in ('openrpg.source-support-relinks-stage.v1', 'openrpg.model-preview-recovery.v1'):
        p.error('invalid source relink plan')
    matches = [x for x in plan['paths'] if x['old'] == args.old_path]
    if len(matches) != 1:
        p.error('old path must match exactly one recorded mapping')
    print(restore_one(args.vault, matches[0]))
