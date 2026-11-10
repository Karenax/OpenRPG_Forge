import json
import pathlib
import tempfile
import unittest

import audit_variant_candidates as audit
import audit_source_variant_candidates as source_audit
import audit_source_duplicate_bytes as source_bytes


class VariantAuditTest(unittest.TestCase):
    def test_shared_model_is_not_a_deletion(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            path = root / 'model.glb'
            path.write_bytes(b'a sample shared model')
            h = audit.sha(path.read_bytes())
            data = {'assets': [
                {'id': 'pack.object', 'model_path': str(path), 'model_sha256': h, 'names': {'fr': 'Un'}},
                {'id': 'pack.variant-0123456789abcdef.object', 'model_path': str(path), 'model_sha256': h, 'names': {'fr': 'Deux'}},
            ]}
            payload = json.dumps(data).encode()
            (root / 'revision').mkdir()
            (root / 'revision/details.v1.json').write_bytes(payload)
            (root / 'metadata').mkdir()
            registry = {'packs': {'pack': {'path': 'revision', 'files': {'details.v1.json': audit.sha(payload)}}}}
            (root / 'metadata/asset-import-registry.v1.json').write_text(json.dumps(registry))
            result = audit.audit(root, root / 'out', verify_shared=True)
            self.assertEqual(result['pairs'], 1)
            self.assertEqual(result['shared_hash_match'], 1)
            self.assertEqual(result['deletable_verified'], 0)
            self.assertEqual(path.read_bytes(), b'a sample shared model')

    def test_variant_pairing_is_not_a_pipeline_claim(self):
        self.assertEqual(audit.base_id('pack.variant-0123456789abcdef.foo'), 'pack.foo')
        self.assertEqual(audit.pipeline([{'original_source_path': 'Assets/X/URP/thing.fbx'}]), 'urp')
        self.assertEqual(audit.pipeline([{'path': 'a thing.fbx'}]), 'unknown')

    def test_ambiguous_sources_are_not_promoted_to_pairs(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            (root / 'metadata').mkdir()
            entries = []
            for ident, side in [('a', 'HDRP'), ('b', 'HDRP'), ('c', 'URP')]:
                entries.append({'id': ident, 'pack_id': 'pack', 'source_asset_path': f'Assets/{side}/model.prefab', 'source_sha256': 'abc'})
            payload = json.dumps({'assets': entries}).encode()
            (root / 'sources.json').write_bytes(payload)
            pointer = {'path': 'sources.json', 'sha256': audit.sha(payload)}
            (root / 'metadata/asset-source-support-registry.v1.json').write_text(json.dumps(pointer))
            result = source_audit.audit(root, root / 'out')
            self.assertEqual(result['paired'], 0)
            self.assertEqual(result['ambiguous'], 1)
            self.assertEqual(result['same_source_sha'], 0)

    def test_identical_source_bytes_stay_protected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            (root / 'metadata').mkdir()
            (root / 'a').write_bytes(b'exact')
            (root / 'b').write_bytes(b'exact')
            h = audit.sha(b'exact')
            entries = [{'id': side, 'pack_id': 'pack', 'source_asset_path': f'Assets/{side}/model.prefab',
                        'source_sha256': h, 'source_file': file} for side, file in [('HDRP','a'),('URP','b')]]
            payload = json.dumps({'assets': entries}).encode()
            (root / 'sources.json').write_bytes(payload)
            pointer = {'path': 'sources.json', 'sha256': audit.sha(payload)}
            (root / 'metadata/asset-source-support-registry.v1.json').write_text(json.dumps(pointer))
            source_audit.audit(root, root / 'out')
            result = source_bytes.audit(root, root / 'out')
            self.assertEqual(result['verified'], 1)
            self.assertEqual(result['gross_urp_bytes'], 5)
            self.assertEqual(result['proven_reclaimable_bytes'], 0)
            self.assertTrue((root / 'a').exists() and (root / 'b').exists())


if __name__ == '__main__':
    unittest.main()
