import unittest

from plan_variant_consolidation import only_node_asset_ids


class ConsolidationSelectionTests(unittest.TestCase):
    def test_only_node_identity_differences_are_candidates(self):
        record = {'status': 'compared', 'binary_chunk_exact': True,
                  'difference_paths_truncated': False,
                  'json_difference_paths': ['$/nodes/0/extras/asset_id', '$/nodes/12/extras/asset_id']}
        self.assertTrue(only_node_asset_ids(record))
        self.assertFalse(only_node_asset_ids({**record, 'binary_chunk_exact': False}))
        self.assertFalse(only_node_asset_ids({**record, 'difference_paths_truncated': True}))
        self.assertFalse(only_node_asset_ids({**record, 'json_difference_paths': ['$/materials/0/alphaMode']}))
        self.assertFalse(only_node_asset_ids({**record, 'json_difference_paths': []}))


if __name__ == '__main__':
    unittest.main()
