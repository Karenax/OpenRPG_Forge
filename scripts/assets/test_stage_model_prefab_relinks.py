import copy
import unittest
from pathlib import Path

from stage_model_prefab_relinks import replace_prefabs


class PrefabRelinkTests(unittest.TestCase):
    def test_relinks_prefab_and_meta_preserving_other_fields(self):
        mapping = {
            'old/a.prefab': {'retained':'new/a.prefab', 'absolute':Path('F:/OpenRPG_AssetVault/new/a.prefab'), 'sha256':'abc'},
            'old/a.prefab.meta': {'retained':'new/a.prefab.meta', 'absolute':Path('F:/OpenRPG_AssetVault/new/a.prefab.meta'), 'sha256':'def'},
        }
        document = {'source_prefab':{'path':r'F:\OpenRPG_AssetVault\old\a.prefab', 'guid':'123', 'sha256':'abc'},
                    'source_prefab_meta_file':'old/a.prefab.meta', 'source_prefab_meta_sha256':'def',
                    'source_prefab_asset_path':'old/a.prefab', 'names':{'fr':'maison'}}
        hits = {}
        replace_prefabs(document, mapping, hits)
        self.assertEqual(document['source_prefab']['path'], str(mapping['old/a.prefab']['absolute']))
        self.assertEqual(document['source_prefab_meta_file'], 'new/a.prefab.meta')
        self.assertEqual(document['source_prefab_asset_path'], 'old/a.prefab')
        self.assertEqual(document['names'], {'fr':'maison'})
        self.assertEqual(len(hits), 2)

    def test_declared_hash_mismatch_rejected(self):
        row = {'retained':'new/a.prefab', 'absolute':Path('new/a.prefab'), 'sha256':'abc'}
        document = {'source_prefab_file':'old/a.prefab', 'source_prefab_sha256':'wrong'}
        before = copy.deepcopy(document)
        with self.assertRaisesRegex(ValueError, 'SHA mismatch'):
            replace_prefabs(document, {'old/a.prefab':row}, {})
        self.assertEqual(document, before)


if __name__ == '__main__':
    unittest.main()
