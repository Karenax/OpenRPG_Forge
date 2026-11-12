"""Stage historical models in shared storage before a catalog consolidation.

This is a transitional operation, not retirement: paths, IDs, logical bytes and
hashes remain unchanged. Only a frozen set of small raw GLBs in a current
consolidation plan is accepted. Run the normal publication and retirement
qualification separately; the final space change must be measured there.
"""
import argparse
import json
import shutil
from pathlib import Path

import vault_storage as storage
from variant_legacy_recipe import MAX_INLINE_MODEL_BYTES, split_glb


def validate(vault, plan, wanted):
    vault = vault.resolve(strict=True)
    plan_raw = plan.read_bytes()
    doc = json.loads(plan_raw)
    if doc.get('schema') != 'openrpg.variant-consolidation-plan.v1' or doc.get('state') != 'planning':
        raise ValueError('unexpected consolidation plan')
    index = storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    registry = index.read_bytes()
    if storage.sha(registry) != doc['registry_sha256']:
        raise ValueError('registry changed')
    candidates = doc['candidates']
    if len(candidates) != doc['candidate_count']:
        raise ValueError('plan candidate count mismatch')
    selected = [x for x in candidates if x['retired_id'] in wanted]
    if len(selected) != len(wanted) or len({x['retired_id'] for x in selected}) != len(wanted):
        raise ValueError('IDs absent or repeated')
    if len({x['retired_path'] for x in selected}) != len(selected):
        raise ValueError('duplicate retired model path')
    relevant = {x[side + '_id'] for x in selected for side in ('retired', 'retained')}
    active = {}
    for entry in json.loads(registry)['packs'].values():
        detail = storage.confined(vault, vault / entry['path'] / 'details.v1.json').read_bytes()
        if storage.sha(detail) != entry['files']['details.v1.json']:
            raise ValueError('catalog detail SHA changed')
        for card in json.loads(detail)['assets']:
            if card['id'] in relevant:
                if card['id'] in active:
                    raise ValueError('duplicate active identity')
                active[card['id']] = card
    if set(active) != relevant:
        raise ValueError('planned asset is not active')
    rows = []
    for item in selected:
        for side in ('retired', 'retained'):
            card = active[item[side + '_id']]
            if card.get('model_path') != item[side + '_path'] or card.get('model_sha256') != item[side + '_sha256']:
                raise ValueError('catalog path or model SHA changed')
        path = storage.confined(vault, Path(item['retired_path']))
        if path.suffix.lower() != '.glb' or path.relative_to(vault).parts[:2] != ('normalized', 'hivemind'):
            raise ValueError('model outside normalized Hivemind scope')
        if storage.read_descriptor(vault, path) is not None:
            raise ValueError('model is already shared; inspect prior operation')
        info = path.stat()
        if storage.windows_readonly(info) or not 0 < info.st_size <= MAX_INLINE_MODEL_BYTES:
            raise ValueError('model read-only or outside small-model budget')
        raw = path.read_bytes()
        if storage.sha(raw) != item['retired_sha256']:
            raise ValueError('retired model SHA changed')
        split_glb(raw)
        retained = storage.confined(vault, Path(item['retained_path']))
        if storage.digest(vault, retained) != item['retained_sha256']:
            raise ValueError('retained model SHA changed')
        rows.append({'id': item['retired_id'], 'path': str(path), 'bytes': info.st_size,
                     'mtime_ns': info.st_mtime_ns, 'sha256': item['retired_sha256']})
    if index.read_bytes() != registry or plan.read_bytes() != plan_raw:
        raise ValueError('registry or plan changed')
    return vault, index, registry, plan_raw, rows


def apply(vault, plan, ids_path, receipt_path, verifier, report_path):
    if report_path.exists():
        raise ValueError('existing report requires inspection, not automatic replay')
    ids_doc = json.loads(ids_path.read_bytes())
    wanted = set(ids_doc['retired_ids'])
    if not wanted or len(wanted) != len(ids_doc['retired_ids']):
        raise ValueError('empty or duplicate selection')
    vault, index, registry, plan_raw, rows = validate(vault, plan, wanted)
    receipt = json.loads(receipt_path.read_bytes())
    storage.verify_deployed_readers(receipt, vault, verifier)
    transactions = vault / '.forge-storage/transactions'
    if transactions.exists() and any(storage.confined(vault, transactions).iterdir()):
        raise ValueError('unfinished vault transaction')
    # Leave room for original bytes plus the temporary packs while replacing.
    if shutil.disk_usage(vault).free < 2 * sum(row['bytes'] for row in rows):
        raise ValueError('insufficient free space for temporary shared staging')
    report = {'state': 'preparing', 'vault': str(vault), 'registry_sha256': storage.sha(registry),
              'plan_sha256': storage.sha(plan_raw), 'ids_sha256': storage.sha(ids_path.read_bytes()),
              'reader_receipt_sha256': storage.sha(receipt_path.read_bytes()), 'planned': rows,
              'completed': [], 'free_before': shutil.disk_usage(vault).free,
              'purpose': 'temporary staging for qualified consolidation; no catalog retirement'}
    storage.atomic_json(report_path, report)
    writer = None
    published = False
    try:
        writer = storage.Packer(vault)
        prepared = []
        for row in rows:
            item = writer.prepare(Path(row['path']), row['sha256'])
            if item is None or (item['bytes'], item['mtime_ns']) != (row['bytes'], row['mtime_ns']):
                raise ValueError('model changed after validation')
            prepared.append(item)
        writer.close_pack()
        if index.read_bytes() != registry or plan.read_bytes() != plan_raw:
            raise ValueError('registry or plan changed before publication')
        report['state'] = 'publishing'
        report['pack_bytes'] = writer.new_pack_bytes
        storage.atomic_json(report_path, report)
        for item in prepared:
            if index.read_bytes() != registry or plan.read_bytes() != plan_raw:
                raise ValueError('registry or plan changed during publication')
            report['current'] = item['path']
            storage.atomic_json(report_path, report)
            # Once publication starts, preserve all packs even if a transaction
            # raises: the stock recovery journal identifies its durable state.
            published = True
            outcome = writer.publish(item, verifier)
            report['completed'].append(outcome)
            storage.atomic_json(report_path, report)
        report.pop('current', None)
        report['state'] = 'complete'
    except BaseException as error:
        if writer is not None and not published:
            writer.discard_unpublished_batch(0)
        report['state'] = 'interrupted'
        report['error'] = str(error)
        raise
    finally:
        if writer is not None:
            writer.close()
        report['free_after'] = shutil.disk_usage(vault).free
        storage.atomic_json(report_path, report)
    return report


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for name in ('vault', 'plan', 'ids', 'reader-receipt', 'verify-cli', 'report'):
        p.add_argument('--' + name, type=Path, required=True)
    a = p.parse_args()
    result = apply(a.vault, a.plan, a.ids, a.reader_receipt, a.verify_cli, a.report)
    print(json.dumps({'state': result['state'], 'models': len(result['completed']),
                      'pack_bytes': result.get('pack_bytes')}))
