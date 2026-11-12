import hashlib
import json
import os
import stat
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, os.environ.get('OPENRPG_IMPORTER_SCRIPTS', r'E:\CodexData\skills\openrpg-import-assets\scripts'))
import asset_import
import vault_storage
import publish_variant_transition
import variant_legacy_recipe
import validate_no_legacy_reads
import retire_consolidated_models
import audit_candidate_local_references
from publish_variant_transition import digest, publish
from retire_consolidated_models import retire
from test_publish_variant_transition import setup_fixture


def ready(root):
    vault, stage, plan, recipes, receipt, paths = setup_fixture(root)
    publish(vault, stage, plan, recipes, receipt, asset_import)
    support = vault / 'metadata/source-support.json'
    support.write_text('{"assets":[]}', encoding='utf8')
    pointer = vault / 'metadata/asset-source-support-registry.v1.json'
    pointer.write_text(json.dumps({'path': 'metadata/source-support.json', 'sha256': digest(support)}), encoding='utf8')
    proof = root / 'proof.json'
    test_binary = root / 'test-binary.exe'
    test_binary.write_bytes(b'test')
    proof.write_text(json.dumps({'state': 'passed', 'exclusive_legacy_paths': 1,
                     'registry_sha256': digest(vault / 'metadata/asset-import-registry.v1.json'),
                     'stage_manifest_sha256': digest(stage / 'transition.json'),
                     'plan_fingerprint': validate_no_legacy_reads.plan_fingerprint(plan),
                     'models_removed': 0, 'child_command': [str(test_binary)],
                     'test_binary_sha256': digest(test_binary)}), encoding='utf8')
    qualification = {'schema': 'openrpg.variant-retirement-qualification.v1', 'state': 'ready',
                     'vault': str(vault), 'stage_manifest_sha256': digest(stage / 'transition.json'),
                     'studio': receipt['studio'], 'studio_sha256': receipt['studio_sha256'],
                     'exclusive_read_proof': str(proof), 'exclusive_read_proof_sha256': digest(proof),
                     'retirement_tool_sha256': digest(retire_consolidated_models.__file__),
                     'storage_reader_sha256': digest(vault_storage.__file__),
                     'publisher_tool_sha256': digest(publish_variant_transition.__file__),
                     'recipe_tool_sha256': digest(variant_legacy_recipe.__file__),
                     'proof_tool_sha256': digest(validate_no_legacy_reads.__file__),
                     'candidate_reference_audit_sha256': digest(audit_candidate_local_references.__file__),
                     'canonical_importer_sha256': digest(asset_import.__file__),
                     'registry_sha256': digest(vault / 'metadata/asset-import-registry.v1.json'),
                     'source_registry_sha256': digest(pointer)}
    return vault, stage, plan, qualification, paths


class RetirementTests(unittest.TestCase):
    def test_promotion_reference_blocks_before_removal(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, stage, plan, receipt, paths = ready(Path(temp))
            with patch.object(audit_candidate_local_references, 'find_references',
                              return_value=[{'references':[{'document':'catalog.promote.v1.json'}]}]):
                with self.assertRaisesRegex(ValueError, 'promotion metadata still references'):
                    retire_consolidated_models.preflight(vault, plan, stage, receipt)
            self.assertTrue(Path(plan['candidates'][0]['retired_path']).exists())

    def test_corrected_witch_delivery_allowed_only_as_retained(self):
        with tempfile.TemporaryDirectory() as temp:
            vault=Path(temp);root=vault/'normalized';root.mkdir()
            delivered=vault/'witch-house-urp-20261108-r2'/'models';delivered.mkdir(parents=True)
            keep=delivered/'00.glb';old=root/'old.glb';keep.write_bytes(b'x');old.write_bytes(b'y')
            row={'retained_path':str(keep),'retained_sha256':'a'*64,'retired_path':str(old),'retired_sha256':'b'*64}
            self.assertEqual(len(retire_consolidated_models.protected_model_paths(vault,[row])),2)
            row={'retained_path':str(old),'retained_sha256':'b'*64,'retired_path':str(keep),'retired_sha256':'a'*64}
            with self.assertRaisesRegex(ValueError,'outside allowed'):retire_consolidated_models.protected_model_paths(vault,[row])

    def test_repeated_retained_path_allowed_but_retired_path_never_reused(self):
        with tempfile.TemporaryDirectory() as temp:
            vault = Path(temp)
            root = vault / 'normalized'
            root.mkdir()
            keep, first, second = (root / name for name in ('keep.glb', 'first.glb', 'second.glb'))
            for path in (keep, first, second):
                path.write_bytes(b'model')
            row = {'retained_path': str(keep), 'retained_sha256': 'a' * 64,
                   'retired_path': str(first), 'retired_sha256': 'b' * 64}
            other = {**row, 'retired_path': str(second), 'retired_sha256': 'c' * 64}
            self.assertEqual(len(retire_consolidated_models.protected_model_paths(vault, [row, other])), 3)
            with self.assertRaisesRegex(ValueError, 'path reused'):
                retire_consolidated_models.protected_model_paths(vault, [row, row])
            with self.assertRaisesRegex(ValueError, 'path reused'):
                retire_consolidated_models.protected_model_paths(vault, [row, {**other, 'retained_sha256': 'd' * 64}])
            with self.assertRaisesRegex(ValueError, 'path reused'):
                retire_consolidated_models.protected_model_paths(vault, [row, {**other, 'retired_path': str(keep)}])

    def test_changed_export_baseline_blocks_retirement(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            vault, stage, plan, receipt, paths = ready(root)
            baseline = root / 'baseline.json'
            baseline.write_text('{"original":true}', encoding='utf8')
            proof = Path(receipt['exclusive_read_proof'])
            evidence = json.loads(proof.read_bytes())
            evidence['export_baseline'] = str(baseline)
            evidence['export_baseline_sha256'] = digest(baseline)
            proof.write_text(json.dumps(evidence), encoding='utf8')
            receipt['exclusive_read_proof_sha256'] = digest(proof)
            baseline.write_text('{"original":false}', encoding='utf8')
            with self.assertRaisesRegex(ValueError, 'export baseline changed'):
                retire(vault, plan, stage, receipt, asset_import, root / 'retirement.jsonl')
            self.assertTrue(all(path.exists() for path in paths))
            self.assertFalse((root / 'retirement.jsonl').exists())

    @unittest.skipUnless(os.name == 'nt', 'Windows read-only attribute required')
    def test_readonly_attribute_is_restored_when_deletion_fails(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            vault, stage, plan, receipt, paths = ready(root)
            os.chmod(paths[1], stat.S_IREAD)
            real_unlink = Path.unlink
            def fail_target(path, *args, **kwargs):
                if path == paths[1]:
                    raise OSError('simulated failure')
                return real_unlink(path, *args, **kwargs)
            try:
                with patch.object(Path, 'unlink', fail_target):
                    with self.assertRaisesRegex(OSError, 'simulated failure'):
                        retire(vault, plan, stage, receipt, asset_import, root / 'retirement.jsonl', allow_readonly=True)
                self.assertTrue(paths[1].exists())
                self.assertTrue(vault_storage.windows_readonly(paths[1].stat()))
                self.assertIn('readonly_restored', (root / 'retirement.jsonl').read_text())
            finally:
                if paths[1].exists():
                    os.chmod(paths[1], stat.S_IWRITE | stat.S_IREAD)

    @unittest.skipUnless(os.name == 'nt', 'Windows read-only attribute required')
    def test_readonly_retirement_requires_explicit_option(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            vault, stage, plan, receipt, paths = ready(root)
            os.chmod(paths[1], stat.S_IREAD)
            try:
                with self.assertRaisesRegex(ValueError, 'read-only'):
                    retire(vault, plan, stage, receipt, asset_import, root / 'refused.jsonl')
                self.assertTrue(paths[1].exists())
                result = retire(vault, plan, stage, receipt, asset_import, root / 'retirement.jsonl', allow_readonly=True)
                self.assertEqual(len(result['removed']), 1)
                self.assertFalse(paths[1].exists())
                self.assertTrue(paths[0].exists())
                self.assertIn('readonly_clearing', (root / 'retirement.jsonl').read_text())
            finally:
                if paths[1].exists():
                    os.chmod(paths[1], stat.S_IWRITE | stat.S_IREAD)

    def test_removes_only_retired_file_after_published_alias(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            vault, stage, plan, receipt, paths = ready(root)
            result = retire(vault, plan, stage, receipt, asset_import, root / 'retirement.jsonl')
            self.assertEqual(result['state'], 'removed_pending_verification')
            self.assertTrue(paths[0].exists())
            self.assertFalse(paths[1].exists())
            self.assertEqual(len(result['removed']), 1)
            self.assertEqual(len(asset_import.load_pack(vault, 'main')[2]['assets']), 1)

    def test_changed_retired_file_aborts_before_deletion(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            vault, stage, plan, receipt, paths = ready(root)
            paths[1].write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'retired logical SHA changed'):
                retire(vault, plan, stage, receipt, asset_import, root / 'retirement.jsonl')
            self.assertEqual(paths[1].read_bytes(), b'changed')
            self.assertTrue(paths[0].exists())
            self.assertFalse((root / 'retirement.jsonl').exists())

    def test_active_reference_to_retired_path_blocks_removal(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            vault, stage, plan, receipt, paths = ready(root)
            support = vault / 'metadata/source-support.json'
            support.write_text(json.dumps({'assets': [{'consolidated_variants': [{'model_path': str(paths[1])}]}]}), encoding='utf8')
            pointer = vault / 'metadata/asset-source-support-registry.v1.json'
            pointer.write_text(json.dumps({'path': 'metadata/source-support.json', 'sha256': digest(support)}), encoding='utf8')
            receipt['source_registry_sha256'] = digest(pointer)
            with self.assertRaisesRegex(ValueError, 'active references'):
                retire(vault, plan, stage, receipt, asset_import, root / 'retirement.jsonl')
            self.assertTrue(all(p.exists() for p in paths))


if __name__ == '__main__':
    unittest.main()
