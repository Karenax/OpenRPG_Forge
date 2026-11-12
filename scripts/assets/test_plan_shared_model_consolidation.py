import csv
import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import plan_shared_model_consolidation as planner
from test_variant_legacy_recipe import glb


class SharedModelPlanTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        (self.root / 'metadata').mkdir()
        (self.root / 'metadata/asset-import-registry.v1.json').write_text('{}', encoding='utf8')
        self.model = self.root / 'model.glb'
        self.raw = glb(b'{"id":"same"}')
        self.model.write_bytes(self.raw)
        self.sha = hashlib.sha256(self.raw).hexdigest()
        self.csv = self.root / 'pairs.csv'
        self.row = dict(same_path='True', main_id='pack.main', variant_id='pack.variant',
                        main_path=str(self.model), variant_path=str(self.model),
                        main_sha=self.sha, variant_sha=self.sha,
                        same_path_logical_hash_verified='match', different_metadata='tags')

    def tearDown(self):
        self.tmp.cleanup()

    def write_rows(self, rows):
        with self.csv.open('w', encoding='utf8', newline='') as stream:
            writer = csv.DictWriter(stream, fieldnames=list(self.row))
            writer.writeheader()
            writer.writerows(rows)

    def run_plan(self):
        with patch.object(planner, 'load_packs', return_value={}), \
             patch.object(planner, 'prepare_transition'), \
             patch.object(planner.vault_storage, 'confined', side_effect=lambda root, path: path), \
             patch.object(planner.vault_storage, 'read_bytes', return_value=self.raw):
            return planner.make_plan(self.root, self.csv)

    def test_preserves_shared_path_and_produces_exact_recipe(self):
        self.write_rows([self.row])
        plan, document = self.run_plan()
        candidate = plan['candidates'][0]
        self.assertEqual(candidate['retained_path'], candidate['retired_path'])
        self.assertFalse(candidate['ready_for_removal'])
        self.assertEqual(document['recipes']['pack.variant']['logical_sha256'], self.sha)
        self.assertTrue(self.model.exists())

    def test_rejects_different_sha_or_reused_identity(self):
        different = dict(self.row, variant_sha='0' * 64)
        self.write_rows([different])
        with self.assertRaises(ValueError):
            self.run_plan()
        self.write_rows([self.row, self.row])
        with self.assertRaises(ValueError):
            self.run_plan()

    def test_rejects_registry_change_while_reading_models(self):
        self.write_rows([self.row])
        def change_registry(*_):
            (self.root / 'metadata/asset-import-registry.v1.json').write_text('{"changed":true}')
            return self.raw
        with patch.object(planner, 'load_packs', return_value={}), \
             patch.object(planner, 'prepare_transition'), \
             patch.object(planner.vault_storage, 'confined', side_effect=lambda root, path: path), \
             patch.object(planner.vault_storage, 'read_bytes', side_effect=change_registry):
            with self.assertRaises(ValueError):
                planner.make_plan(self.root, self.csv)


if __name__ == '__main__':
    unittest.main()
