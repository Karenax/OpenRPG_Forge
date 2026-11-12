"""Publish qualified source-card aliases only after the shared Studio is updated."""
import argparse
import json
import os
from pathlib import Path
import uuid

import vault_storage as storage
from publish_source_support_relinks import importer_module
from publish_variant_transition import ensure_vault_directory
from stage_source_support_pair_consolidation import prepare
from stage_archived_effect_variants import prepare as prepare_archived_effects

MODEL_IMPORTER = Path(r'E:\CodexData\skills\openrpg-import-assets\scripts\asset_import.py')


def verify_alias_transition(importer, previous, document, model_ids, pairs):
    prior = importer.source_alias_ids(previous, model_ids)
    actual = importer.source_alias_ids(document, model_ids)
    new = {p['hdrp_id'] for p in pairs}
    if len(new) != len(pairs) or prior & new or actual != prior | new:
        raise ValueError('source alias set differs')
    def records(catalog):
        return {alias['retired_id']:alias for card in catalog['assets']
                for alias in card.get('consolidated_sources', [])}
    old_records, next_records = records(previous), records(document)
    if any(next_records.get(ident) != old_records[ident] for ident in prior):
        raise ValueError('existing source alias changed')
    return len(new)


def verify_existing_alias_transition(importer, previous, document, model_ids, pairs):
    prior = importer.source_alias_ids(previous, model_ids)
    actual = importer.source_alias_ids(document, model_ids)
    selected = {p['hdrp_id'] for p in pairs}
    if len(selected) != len(pairs) or not selected <= prior or actual != prior:
        raise ValueError('existing alias set differs')
    def records(catalog):
        return {a['retired_id']:a for card in catalog['assets'] for a in card.get('consolidated_sources', [])}
    old, new = records(previous), records(document)
    if any(new[k] != old[k] for k in prior-selected):
        raise ValueError('unselected existing alias changed')
    if any(new[k] == old[k] for k in selected):
        raise ValueError('selected existing alias did not change')
    return len(selected)


def publish(vault, stage, proof_path, studio, native_test, source_importer, source_importer_sha, model_importer_sha,
            archived_inventory=None, archived_proofs=()):
    vault, stage = vault.resolve(strict=True), stage.resolve(strict=True)
    proof_raw = proof_path.read_bytes()
    proof = json.loads(proof_raw)
    plan_raw, staged_raw = (stage/'plan.json').read_bytes(), (stage/'source-support.v1.json').read_bytes()
    plan = json.loads(plan_raw)
    if (proof.get('state') != 'passed' or proof.get('stage_plan_sha256') != storage.sha(plan_raw)
            or proof.get('stage_catalog_sha256') != storage.sha(staged_raw)
            or proof.get('pairs') != len(plan['pairs'])
            or proof.get('studio_binary_sha256') != storage.sha(studio.read_bytes())
            or proof.get('native_test_sha256') != storage.sha(native_test.read_bytes())
            or storage.sha(MODEL_IMPORTER.read_bytes()) != model_importer_sha):
        raise ValueError('reader, importer or staged proof changed; install qualified shared Studio first')
    if storage.sha(Path(source_importer.__file__).read_bytes()) != source_importer_sha:
        raise ValueError('source importer changed')
    with source_importer.shared_import_lock(vault):
        if plan.get('schema') == 'openrpg.archived-effect-variant-stage.v1':
            if archived_inventory is None or not archived_proofs:
                raise ValueError('archived effect inventory and proofs required')
            expected_raw, expected_plan = prepare_archived_effects(vault, archived_inventory, archived_proofs,
                                                                  plan.get('existing_alias_migration') is True)
        else:
            if archived_inventory is not None or archived_proofs:
                raise ValueError('unexpected archived effect proof')
            expected_raw, expected_plan = prepare(vault, distinct_previews=plan.get('distinct_previews') is True,
                                              preserve_distinct_text=plan.get('preserve_distinct_text') is True,
                                              preserve_preview_metadata=plan.get('preserve_preview_metadata') is True,
                                              preserve_taxonomy=plan.get('preserve_taxonomy') is True,
                                              preserve_same_preview_metadata=plan.get('preserve_same_preview_metadata') is True,
                                              preserve_material_bindings=plan.get('preserve_material_bindings') is True,
                                              preserve_prefab_components=plan.get('preserve_prefab_components') is True,
                                              preserve_geometry_details=plan.get('preserve_geometry_details') is True,
                                              preserve_lod_variants=plan.get('preserve_lod_variants') is True,
                                              preserve_source_reuse=plan.get('preserve_source_reuse') is True,
                                              preserve_provenance_details=plan.get('preserve_provenance_details') is True,
                                              preserve_light_prefab=plan.get('preserve_light_prefab') is True,
                                              preserve_measured_source=plan.get('preserve_measured_source') is True,
                                              preserve_lod_collider=plan.get('preserve_lod_collider') is True,
                                              preserve_pipeline_light=plan.get('preserve_pipeline_light') is True)
        if expected_raw != staged_raw or expected_plan != plan:
            raise ValueError('stage cannot be regenerated from current catalogs')
        state = source_importer.load_state(vault)
        if (storage.sha(state['support_registry_raw']) != plan['source_registry_sha256']
                or storage.sha(state['asset_registry_raw']) != plan['asset_registry_sha256']):
            raise ValueError('registry changed')
        document = json.loads(staged_raw)
        checker = verify_existing_alias_transition if plan.get('existing_alias_migration') is True else verify_alias_transition
        new_count = checker(source_importer,state['catalog'],document,state['model_ids'],plan['pairs'])
        if plan.get('schema') == 'openrpg.archived-effect-variant-stage.v1':
            # Rehash the selected source payloads and their parent archives
            # inside the publication lock. The archive proof was pinned above.
            source_importer.validate_catalog(document, vault, set(), state['model_ids'],
                                             {p['urp_id'] for p in plan['pairs']})
        if len(staged_raw) > source_importer.MAX_JSON:
            raise ValueError('source catalog exceeds reader budget')
        revision = plan['new_catalog_sha256']
        revisions = ensure_vault_directory(vault, ['metadata', 'asset-source-support-revisions'])
        target = revisions/revision
        if target.exists():
            if storage.sha(storage.confined(vault, target/'source-support.v1.json').read_bytes()) != revision:
                raise ValueError('existing source revision conflicts')
        else:
            partial = revisions/(revision+'.partial-'+uuid.uuid4().hex)
            partial.mkdir()
            with (partial/'source-support.v1.json').open('xb') as f:
                f.write(staged_raw)
                f.flush()
                os.fsync(f.fileno())
            if storage.sha((partial/'source-support.v1.json').read_bytes()) != revision:
                raise ValueError('source revision readback failed')
            os.replace(partial, target)
        final = storage.confined(vault, target/'source-support.v1.json')
        if (storage.sha(final.read_bytes()) != revision
                or state['support_registry_path'].read_bytes() != state['support_registry_raw']
                or state['asset_registry_path'].read_bytes() != state['asset_registry_raw']
                or storage.sha(studio.read_bytes()) != proof['studio_binary_sha256']
                or proof_path.read_bytes() != proof_raw):
            raise ValueError('publication inputs changed')
        pointer = {'schema_version':1, 'path':final.relative_to(vault).as_posix(),
                   'sha256':revision, 'revision':revision}
        pointer_raw = source_importer.canonical_json(pointer)
        transaction = uuid.uuid4().hex
        directory = ensure_vault_directory(vault, ['metadata','asset-source-support-transactions'])
        receipt_path = directory/(transaction+'.json')
        receipt = {'schema':'openrpg.source-support-alias-transaction.v1', 'state':'prepared',
                   'transaction':transaction, 'prior_pointer':state['support_registry'], 'new_pointer':pointer,
                   'stage_plan_sha256':storage.sha(plan_raw), 'proof_sha256':storage.sha(proof_raw),
                   'studio_sha256':proof['studio_binary_sha256'], 'source_importer_sha256':source_importer_sha,
                   'model_importer_sha256':model_importer_sha, 'consolidated_pairs':new_count, 'files_removed':0}
        source_importer.atomic_write(receipt_path, source_importer.canonical_json(receipt))
        source_importer.atomic_write(state['support_registry_path'], pointer_raw)
        if state['support_registry_path'].read_bytes() != pointer_raw:
            raise ValueError('publication readback failed; inspect transaction')
        receipt['state'] = 'committed'
        source_importer.atomic_write(receipt_path, source_importer.canonical_json(receipt))
        return receipt


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for name in ('vault','stage','proof','studio','native-test'):
        p.add_argument('--'+name, type=Path, required=True)
    p.add_argument('--source-importer-sha256', required=True)
    p.add_argument('--model-importer-sha256', required=True)
    p.add_argument('--archived-inventory', type=Path)
    p.add_argument('--archive-proof', type=Path, action='append', default=[])
    a = p.parse_args()
    importer = importer_module(a.source_importer_sha256)
    result = publish(a.vault, a.stage, a.proof, a.studio, a.native_test, importer,
                     a.source_importer_sha256, a.model_importer_sha256, a.archived_inventory, a.archive_proof)
    print(json.dumps({'state':result['state'], 'transaction':result['transaction'],
                      'pairs':result['consolidated_pairs'], 'files_removed':0}))
