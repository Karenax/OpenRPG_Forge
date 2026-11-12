import hashlib
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, os.environ.get('OPENRPG_IMPORTER_SCRIPTS', r'E:\CodexData\skills\openrpg-import-assets\scripts'))
import asset_import
import vault_storage
import prepare_variant_metadata_transition
import variant_legacy_recipe
import publish_variant_transition
import validate_no_legacy_reads
from prepare_variant_metadata_transition import encoded, prepare_transition
from prepare_variant_reference_patterns import vault_uuid
from publish_variant_transition import digest, publish, recover
from test_variant_legacy_recipe import glb
from variant_legacy_recipe import build_recipe


def h(data):
    return hashlib.sha256(data).hexdigest()


def setup_fixture(root):
    vault = root / 'vault'
    (vault / 'metadata').mkdir(parents=True)
    (vault / '.forge-storage').mkdir()
    (vault / 'normalized/models').mkdir(parents=True)
    keep = glb(b'{"id":"keep"}')
    old = glb(b'{"id":"old"}')
    paths = [vault / 'normalized/models/keep.glb', vault / 'normalized/models/old.glb']
    paths[0].write_bytes(keep)
    paths[1].write_bytes(old)
    ids = ['main.asset', 'alt.asset']
    packs = {}
    registry = {'schema_version': 1, 'packs': {}}
    for i, (name, ident, raw, path) in enumerate(zip(('main', 'alt'), ids, (keep, old), paths), 1):
        detail = {'id': ident, 'model_sha256': h(raw), 'model_path': str(path), 'license': 'private'}
        profile = {'external_id': ident, 'model_content_sha256': h(raw)}
        docs = {'details': {'schema_version': 1, 'pack_id': name, 'assets': [detail]},
                'profiles': {'schema_version': 1, 'profiles': [profile]}}
        packs[name] = docs
        revision = str(i) * 64
        target = vault / 'metadata/asset-import-revisions' / name / revision
        target.mkdir(parents=True)
        files = {}
        for key, filename in (('details', 'details.v1.json'), ('profiles', 'profiles.v1.json')):
            data = encoded(docs[key])
            (target / filename).write_bytes(data)
            files[filename] = h(data)
        registry['packs'][name] = {'revision': revision, 'path': target.relative_to(vault).as_posix(), 'files': files}
    registry_path = vault / 'metadata/asset-import-registry.v1.json'
    registry_path.write_bytes(encoded(registry))
    candidate = {'retained_id': ids[0], 'retired_id': ids[1], 'retained_path': str(paths[0]),
                 'retired_path': str(paths[1]), 'retained_sha256': h(keep), 'retired_sha256': h(old),
                 'retained_uuid': vault_uuid(ids[0]), 'retired_uuid': vault_uuid(ids[1])}
    plan = {'registry_sha256': digest(registry_path), 'candidates': [candidate]}
    recipes = {ids[1]: build_recipe(keep, old, h(keep), h(old))}
    proposal = prepare_transition(packs, [candidate], recipes)
    stage = root / 'stage'
    stage.mkdir()
    manifest = {'state': 'prepared_only', 'registry_sha256': plan['registry_sha256'],
                'includes_legacy_recipes': True, 'aliases_requiring_project_migration': proposal['aliases_requiring_project_migration'], 'packs': {}}
    for name, docs in proposal['packs'].items():
        target = stage / name
        target.mkdir()
        info = {}
        for key, filename in (('details', 'details.v1.json'), ('profiles', 'profiles.v1.json')):
            data = encoded(docs[key])
            (target / filename).write_bytes(data)
            info[filename] = h(data)
        info['revision'] = h(encoded({'details': docs['details'], 'profiles': docs['profiles']}))
        manifest['packs'][name] = info
    (stage / 'transition.json').write_bytes(encoded(manifest))
    studio = root / 'studio.exe'
    studio.write_bytes(b'test-compatible-studio')
    receipt = {'schema': 'openrpg.variant-reader-qualification.v1', 'state': 'ready',
               'vault': str(vault), 'registry_sha256': plan['registry_sha256'],
               'stage_manifest_sha256': digest(stage / 'transition.json'),
               'studio': str(studio), 'studio_sha256': digest(studio),
               'publisher_sha256': digest(publish_variant_transition.__file__),
               'storage_reader_sha256': digest(vault_storage.__file__),
               'transition_tool_sha256': digest(prepare_variant_metadata_transition.__file__),
               'recipe_tool_sha256': digest(variant_legacy_recipe.__file__),
               'canonical_importer_sha256': digest(asset_import.__file__),
               'checks': {k: 'passed' for k in ('native_legacy_reference','native_legacy_export',
                          'real_recipes','studio_library','stage_snapshot_integrity','recovery')}}
    return vault, stage, plan, recipes, receipt, paths


class PublisherTests(unittest.TestCase):
    def test_existing_export_refusal_requires_pinned_matching_baseline(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            vault, stage, plan, recipes, receipt, paths = setup_fixture(root)
            baseline_path = root / 'baseline.json'
            baseline_path.write_bytes(encoded({
                'schema': 'openrpg.variant-export-baseline.v1',
                'stage_manifest_sha256': digest(stage / 'transition.json'),
                'exports': {'alt.asset': {'sha256': plan['candidates'][0]['retired_sha256'],
                                          'outcome': 'error', 'error': 'missing license'}}}))
            receipt['checks']['native_legacy_export'] = 'baseline_matched'
            receipt['export_baseline'] = str(baseline_path)
            receipt['export_baseline_sha256'] = digest(baseline_path)
            receipt['baseline_validator_sha256'] = digest(validate_no_legacy_reads.__file__)
            original = baseline_path.read_bytes()
            baseline_path.write_bytes(original + b' ')
            with self.assertRaisesRegex(ValueError, 'baseline changed'):
                publish(vault, stage, plan, recipes, receipt, asset_import)
            self.assertTrue(all(path.exists() for path in paths))
            baseline_path.write_bytes(original)
            self.assertEqual(publish(vault, stage, plan, recipes, receipt, asset_import)['state'], 'committed')

    def test_publishes_verified_embedded_descriptor_recipe(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, stage, plan, recipes, receipt, paths = setup_fixture(Path(temp))
            writer = vault_storage.Packer(vault)
            try:
                item = writer.prepare(paths[1]); writer.close_pack(); writer.publish(item)
            finally:
                writer.close()
            candidate = plan['candidates'][0]
            recipes[candidate['retired_id']] = variant_legacy_recipe.build_descriptor_recipe(
                vault, paths[1], candidate['retired_sha256'])
            packs = prepare_variant_metadata_transition.load_packs(vault, plan)
            proposal = prepare_transition(packs, plan['candidates'], recipes)
            manifest = json.loads((stage / 'transition.json').read_bytes())
            for name, docs in proposal['packs'].items():
                info = {}
                for key, filename in (('details', 'details.v1.json'), ('profiles', 'profiles.v1.json')):
                    raw = encoded(docs[key]); (stage / name / filename).write_bytes(raw); info[filename] = h(raw)
                info['revision'] = h(encoded({'details': docs['details'], 'profiles': docs['profiles']}))
                manifest['packs'][name] = info
            (stage / 'transition.json').write_bytes(encoded(manifest))
            receipt['stage_manifest_sha256'] = digest(stage / 'transition.json')
            original_digest = vault_storage.digest
            def corrupt_legacy(root, path, *args):
                if Path(path) == paths[1]:
                    return '0' * 64
                return original_digest(root, path, *args)
            before = (vault / 'metadata/asset-import-registry.v1.json').read_bytes()
            with patch('vault_storage.digest', side_effect=corrupt_legacy):
                with self.assertRaisesRegex(ValueError, 'reconstruction SHA mismatch'):
                    publish(vault, stage, plan, recipes, receipt, asset_import)
            self.assertEqual((vault / 'metadata/asset-import-registry.v1.json').read_bytes(), before)
            self.assertTrue(all(path.exists() for path in paths))
            result = publish(vault, stage, plan, recipes, receipt, asset_import)
            self.assertEqual(result['state'], 'committed')
            retained = asset_import.load_pack(vault, 'main')[2]['assets'][0]
            saved = retained['consolidated_variants'][0]['legacy_recipe']
            self.assertEqual(saved['schema'], 'openrpg.legacy-vault-descriptor.v1')
            self.assertEqual(saved['descriptor']['sha256'], candidate['retired_sha256'])

    def test_atomic_group_publication_keeps_both_physical_models(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, stage, plan, recipes, receipt, paths = setup_fixture(Path(temp))
            result = publish(vault, stage, plan, recipes, receipt, asset_import)
            self.assertEqual(result['state'], 'committed')
            self.assertEqual(result['packs'], 2)
            self.assertTrue(all(path.exists() for path in paths))
            registry = asset_import.registry(vault)
            main = asset_import.load_pack(vault, 'main')[2]
            alt = asset_import.load_pack(vault, 'alt')[2]
            self.assertEqual(len(main['assets']), 1)
            self.assertEqual(len(alt['assets']), 0)
            self.assertEqual(main['assets'][0]['consolidated_variants'][0]['retired_id'], 'alt.asset')
            self.assertEqual(len(registry['packs']['main']['history']), 1)
            self.assertFalse((vault / '.forge-storage/WRITER.lock').exists())

    def test_rejects_stale_receipt_and_keeps_registry_intact(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, stage, plan, recipes, receipt, _ = setup_fixture(Path(temp))
            before = (vault / 'metadata/asset-import-registry.v1.json').read_bytes()
            with self.assertRaisesRegex(ValueError, 'qualification incomplete'):
                publish(vault, stage, plan, recipes, {**receipt, 'checks': {}}, asset_import)
            self.assertEqual((vault / 'metadata/asset-import-registry.v1.json').read_bytes(), before)

    def test_foreign_writer_lock_and_changed_model_are_preserved(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, stage, plan, recipes, receipt, paths = setup_fixture(Path(temp))
            registry_path = vault / 'metadata/asset-import-registry.v1.json'
            before = registry_path.read_bytes()
            lock = vault / '.forge-storage/WRITER.lock'
            lock.write_text('{"owner":"other"}', encoding='utf8')
            with self.assertRaises(FileExistsError):
                publish(vault, stage, plan, recipes, receipt, asset_import)
            self.assertEqual(json.loads(lock.read_text(encoding='utf8'))['owner'], 'other')
            lock.unlink()
            paths[1].write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'retired model changed'):
                publish(vault, stage, plan, recipes, receipt, asset_import)
            self.assertEqual(registry_path.read_bytes(), before)
            self.assertEqual(paths[1].read_bytes(), b'changed')

    def test_registry_changed_before_swap_is_not_overwritten(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, stage, plan, recipes, receipt, _ = setup_fixture(Path(temp))
            registry_path = vault / 'metadata/asset-import-registry.v1.json'
            original_atomic = asset_import.atomic
            def concurrent_change(path, value):
                result = original_atomic(path, value)
                if Path(path).parent.name == 'asset-import-history':
                    registry_path.write_text('{"schema_version":1,"packs":{},"external":true}', encoding='utf8')
                return result
            with patch.object(asset_import, 'atomic', side_effect=concurrent_change):
                with self.assertRaisesRegex(ValueError, 'changed before atomic swap'):
                    publish(vault, stage, plan, recipes, receipt, asset_import)
            self.assertTrue(json.loads(registry_path.read_text(encoding='utf8'))['external'])

    def test_interruption_before_registry_swap_keeps_old_registry(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, stage, plan, recipes, receipt, _ = setup_fixture(Path(temp))
            registry_path = vault / 'metadata/asset-import-registry.v1.json'
            before = registry_path.read_bytes()
            original_atomic = asset_import.atomic
            def fail_swap(path, value):
                if Path(path) == registry_path:
                    raise OSError('simulated interruption')
                return original_atomic(path, value)
            with patch.object(asset_import, 'atomic', side_effect=fail_swap):
                with self.assertRaisesRegex(OSError, 'simulated interruption'):
                    publish(vault, stage, plan, recipes, receipt, asset_import)
            self.assertEqual(registry_path.read_bytes(), before)
            self.assertFalse((vault / '.forge-storage/WRITER.lock').exists())
            journals = list((vault / 'metadata/asset-consolidation-history').glob('*.json'))
            self.assertEqual(len(journals), 1)
            self.assertEqual(json.loads(journals[0].read_text(encoding='utf8'))['state'], 'prepared')
            self.assertEqual(recover(vault, journals[0], asset_import)['state'], 'aborted')
            self.assertEqual(registry_path.read_bytes(), before)

    def test_recovery_finishes_journals_after_registry_swap(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, stage, plan, recipes, receipt, paths = setup_fixture(Path(temp))
            original_atomic = asset_import.atomic
            def fail_group_commit(path, value):
                if Path(path).parent.name == 'asset-consolidation-history' and value.get('state') == 'committed':
                    raise OSError('interrupted after registry swap')
                return original_atomic(path, value)
            with patch.object(asset_import, 'atomic', side_effect=fail_group_commit):
                with self.assertRaisesRegex(OSError, 'after registry swap'):
                    publish(vault, stage, plan, recipes, receipt, asset_import)
            journal = next((vault / 'metadata/asset-consolidation-history').glob('*.json'))
            result = recover(vault, journal, asset_import)
            self.assertEqual(result['state'], 'committed')
            self.assertEqual(len(asset_import.load_pack(vault, 'alt')[2]['assets']), 0)
            self.assertTrue(all(path.exists() for path in paths))


if __name__ == '__main__':
    unittest.main()
