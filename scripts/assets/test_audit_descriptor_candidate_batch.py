import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from audit_descriptor_candidate_batch import audit


class DescriptorBatchTests(unittest.TestCase):
    def test_success_and_partial_failure_never_delete_models(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'metadata').mkdir()
            registry = root / 'metadata/asset-import-registry.v1.json'
            registry.write_bytes(b'active registry')
            paths = [root / ('model%d.glb' % i) for i in range(4)]
            for p in paths:
                p.write_bytes(b'model')
            rows = [{'main_id': 'hivemind.demo.' + str(i), 'variant_id': 'alt.' + str(i),
                     'main_path': str(paths[2*i]), 'variant_path': str(paths[2*i+1]),
                     'main_sha': 'a' * 64, 'variant_sha': 'b' * 64} for i in range(2)]
            source = root / 'source.json'
            source.write_text(json.dumps({'registry_sha256': hashlib.sha256(registry.read_bytes()).hexdigest(),
                                          'pairs': rows}), encoding='utf8')
            detail = {'storage': 'descriptor', 'declared_pin_matches': True, 'logical_bytes': 5}
            candidates = root / 'candidates.json'
            candidates.write_text(json.dumps({'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                 'candidates': [{'variant_id': row['variant_id'], 'main': detail, 'variant': detail,
                                 'main_has_aliases': False, 'variant_has_aliases': False} for row in rows]}), encoding='utf8')
            with patch('vault_storage.confined', side_effect=lambda vault, path: path), \
                 patch('vault_storage.digest', return_value='a' * 64), \
                 patch('audit_descriptor_candidate_batch.build_descriptor_recipe', return_value={'recipe': 1}):
                out = root / 'success.jsonl'
                self.assertEqual(audit(root, source, candidates, out, 'demo', 1, 1), 1)
                records = [json.loads(line) for line in out.read_text().splitlines()]
                self.assertEqual(records[1]['variant_id'], 'alt.1')
                self.assertEqual(records[-1]['state'], 'complete')
            choices = root / 'choices.json'
            choices.write_text(json.dumps({'schema': 'openrpg.material-choice-audit.v1', 'state': 'audit_only',
                 'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                 'candidate_inventory_sha256': hashlib.sha256(candidates.read_bytes()).hexdigest(),
                 'registry_sha256': hashlib.sha256(registry.read_bytes()).hexdigest(), 'pack': 'demo',
                 'results': [{'state': 'candidate_only', 'main_id': rows[0]['main_id'],
                              'variant_id': rows[0]['variant_id'], 'keep_side': 'variant',
                              'preferred_controls': ['double_sided']}]}), encoding='utf8')
            with patch('vault_storage.confined', side_effect=lambda vault, path: path), \
                 patch('vault_storage.digest', return_value='b' * 64), \
                 patch('audit_descriptor_candidate_batch.build_descriptor_recipe', return_value={'logical_sha256': 'a' * 64}):
                out = root / 'reversed.jsonl'
                self.assertEqual(audit(root, source, candidates, out, 'demo', 2, choices=choices), 1)
                record = json.loads(out.read_text().splitlines()[1])
                self.assertEqual(record['retained_side'], 'variant')
                self.assertEqual(record['recipe']['logical_sha256'], 'a' * 64)
            with patch('vault_storage.confined', side_effect=lambda vault, path: path), \
                 patch('vault_storage.digest', return_value='wrong'):
                out = root / 'partial.jsonl'
                with self.assertRaisesRegex(ValueError, 'SHA changed'):
                    audit(root, source, candidates, out, 'demo', 2)
                self.assertEqual(len(out.read_text().splitlines()), 1)
            self.assertTrue(all(p.read_bytes() == b'model' for p in paths))


if __name__ == '__main__':
    unittest.main()
