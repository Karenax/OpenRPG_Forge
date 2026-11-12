"""Publish a checked source-support relink stage; never remove source files."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import uuid

import vault_storage as storage
from publish_variant_transition import ensure_vault_directory
from validate_source_support_relinks import validate

IMPORTER = Path(r'E:\CodexData\skills\openrpg-import-assets\scripts\source_support_import.py')


def importer_module(expected):
    if storage.sha(IMPORTER.read_bytes()) != expected:
        raise ValueError('canonical source importer changed')
    spec = importlib.util.spec_from_file_location('canonical_source_support_import', IMPORTER)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def publish(vault, stage, importer, importer_sha):
    vault = vault.resolve(strict=True)
    stage = stage.resolve(strict=True)
    with importer.shared_import_lock(vault):
        result = validate(vault, stage)
        plan_raw = (stage/'plan.json').read_bytes()
        plan = json.loads(plan_raw)
        catalog = (stage/'source-support.v1.json').read_bytes()
        if storage.sha(catalog) != plan['staged_catalog_sha256'] or len(catalog) > importer.MAX_JSON:
            raise ValueError('staged catalog changed or exceeds reader limit')
        state = importer.load_state(vault)
        if (storage.sha(state['support_registry_raw']) != plan['source_registry_sha256']
                or storage.sha(state['asset_registry_raw']) != plan['asset_registry_sha256']
                or storage.sha(state['catalog_raw']) != plan['prior_catalog_sha256']):
            raise ValueError('active catalogs changed')
        if storage.sha(Path(importer.__file__).read_bytes()) != importer_sha:
            raise ValueError('canonical source importer changed')
        revision = plan['staged_catalog_sha256']
        revisions = ensure_vault_directory(vault, ['metadata', 'asset-source-support-revisions'])
        directory = revisions/revision
        if directory.exists():
            file = storage.confined(vault, directory/'source-support.v1.json')
            if storage.sha(file.read_bytes()) != revision:
                raise ValueError('conflicting existing source revision')
        else:
            partial = revisions/(revision+'.partial-'+uuid.uuid4().hex)
            partial.mkdir()
            file = partial/'source-support.v1.json'
            with file.open('xb') as stream:
                stream.write(catalog)
                stream.flush()
                os.fsync(stream.fileno())
            if storage.sha(file.read_bytes()) != revision:
                raise ValueError('new revision readback failed')
            os.replace(partial, directory)
        file = storage.confined(vault, directory/'source-support.v1.json')
        if storage.sha(file.read_bytes()) != revision:
            raise ValueError('published revision readback failed')
        if (state['support_registry_path'].read_bytes() != state['support_registry_raw']
                or state['asset_registry_path'].read_bytes() != state['asset_registry_raw']):
            raise ValueError('registry changed before publication')
        pointer = {'path': file.relative_to(vault).as_posix(), 'revision': revision,
                   'schema_version': 1, 'sha256': revision}
        pointer_bytes = importer.canonical_json(pointer)
        transaction = uuid.uuid4().hex
        txdir = ensure_vault_directory(vault, ['metadata', 'asset-source-support-transactions'])
        receipt_path = txdir/(transaction+'.json')
        receipt = {'schema': 'openrpg.source-support-relinks-transaction.v1', 'state': 'prepared',
                   'transaction': transaction, 'prior_pointer': state['support_registry'],
                   'prior_registry_sha256': plan['source_registry_sha256'],
                   'new_registry_sha256': storage.sha(pointer_bytes), 'new_pointer': pointer,
                   'asset_registry_sha256': plan['asset_registry_sha256'],
                   'stage_plan_sha256': storage.sha(plan_raw), 'stage': str(stage),
                   'importer_sha256': importer_sha, 'validation': result,
                   'source_files_removed': 0}
        importer.atomic_write(receipt_path, importer.canonical_json(receipt))
        importer.atomic_write(state['support_registry_path'], pointer_bytes)
        if state['support_registry_path'].read_bytes() != pointer_bytes:
            raise ValueError('registry publication readback failed; inspect prepared transaction')
        receipt['state'] = 'committed'
        importer.atomic_write(receipt_path, importer.canonical_json(receipt))
        return receipt


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=Path, required=True)
    p.add_argument('--stage', type=Path, required=True)
    p.add_argument('--importer-sha256', required=True)
    args = p.parse_args()
    if not storage.valid_hex(args.importer_sha256, 64):
        p.error('canonical importer SHA-256 required')
    importer = importer_module(args.importer_sha256)
    result = publish(args.vault, args.stage, importer, args.importer_sha256)
    print(json.dumps({'state': result['state'], 'transaction': result['transaction'],
                      'revision': result['new_pointer']['revision'], 'source_files_removed': 0}))
