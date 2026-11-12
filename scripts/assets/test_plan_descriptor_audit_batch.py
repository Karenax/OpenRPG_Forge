import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from plan_descriptor_audit_batch import plan_batch


class DescriptorPlanTests(unittest.TestCase):
    def test_requires_complete_pinned_batch_and_keeps_recipe(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / 'source.json'
            row = {'main_id': 'hivemind.demo.one', 'variant_id': 'variant.one',
                   'main_path': 'main.glb', 'variant_path': 'old.glb',
                   'main_sha': 'a' * 64, 'variant_sha': 'b' * 64}
            source.write_text(json.dumps({'registry_sha256': 'c' * 64, 'pairs': [row]}))
            header = {'schema': 'openrpg.descriptor-candidate-batch.v1', 'state': 'started',
                      'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                      'registry_sha256': 'c' * 64, 'planned': 1, 'pack': 'demo'}
            recipe = {'schema': 'openrpg.legacy-vault-descriptor.v1', 'logical_sha256': 'b' * 64}
            proof = {'state': 'verified_model_bytes', 'main_id': row['main_id'],
                     'variant_id': row['variant_id'], 'main_sha256': row['main_sha'],
                     'variant_sha256': row['variant_sha'], 'recipe': recipe}
            end = {'state': 'complete', 'verified': 1, 'models_removed': 0}
            batch = root / 'batch.jsonl'
            batch.write_text('\n'.join(json.dumps(x) for x in (header, proof, end)))
            with patch('plan_descriptor_audit_batch.load_packs', return_value={}), \
                 patch('plan_descriptor_audit_batch.prepare_transition'):
                plan, recipes = plan_batch(root, source, batch)
                self.assertEqual(plan['candidate_count'], 1)
                self.assertEqual(recipes['recipes']['variant.one'], recipe)
                with self.assertRaisesRegex(ValueError, 'does not match'):
                    plan_batch(root, source, [batch, batch])
                choices = root / 'choices.json'
                choices.write_text(json.dumps({'schema': 'openrpg.material-choice-audit.v1', 'state': 'audit_only',
                    'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(), 'registry_sha256': 'c' * 64,
                    'results': [{'state': 'candidate_only', 'main_id': row['main_id'], 'variant_id': row['variant_id'],
                                 'keep_side': 'variant', 'preferred_controls': ['double_sided']}]}))
                header['material_choices_sha256'] = hashlib.sha256(choices.read_bytes()).hexdigest()
                proof['retained_side'] = 'variant'
                proof['preferred_controls'] = ['double_sided']
                proof['recipe'] = {'schema': 'openrpg.legacy-vault-descriptor.v1', 'logical_sha256': 'a' * 64}
                batch.write_text('\n'.join(json.dumps(x) for x in (header, proof, end)))
                reversed_plan, reversed_recipes = plan_batch(root, source, batch, choices)
                self.assertEqual(reversed_plan['candidates'][0]['retained_id'], 'variant.one')
                self.assertEqual(reversed_plan['candidates'][0]['retired_id'], 'hivemind.demo.one')
                self.assertIn('hivemind.demo.one', reversed_recipes['recipes'])
                batch.write_text('\n'.join(json.dumps(x) for x in (header, proof)))
                with self.assertRaises(ValueError):
                    plan_batch(root, source, batch)


if __name__ == '__main__':
    unittest.main()
