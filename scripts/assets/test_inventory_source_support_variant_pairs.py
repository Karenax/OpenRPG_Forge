import unittest
from inventory_source_support_variant_pairs import classify


class PairTests(unittest.TestCase):
    def test_matches_same_id_and_source_path_but_not_lod(self):
        h = {'id': 'pack.prefab-x', 'source_asset_path': 'Assets/A/HDRP(Default)/x.prefab',
             'source_sha256': 'a', 'source_meta_sha256': 'b'}
        u = {'id': 'pack.variant-abcd.prefab-x', 'source_asset_path': 'Assets/A/URP/x.prefab',
             'source_sha256': 'a', 'source_meta_sha256': 'b'}
        other = dict(u, id=u['id'] + '-lod1')
        pairs = classify([h, u, other])
        self.assertEqual(len(pairs), 1)
        self.assertTrue(pairs[0]['declared_source_sha_equal'])
        self.assertTrue(pairs[0]['declared_meta_sha_equal'])

    def test_missing_hash_and_ambiguous_identity_never_assert_equality(self):
        h = {'id': 'pack.prefab-x', 'source_asset_path': 'Assets/A/HDRP/x.prefab'}
        u = {'id': 'pack.variant-abcd.prefab-x', 'source_asset_path': 'Assets/A/URP/x.prefab'}
        self.assertFalse(classify([h, u])[0]['declared_source_sha_equal'])
        self.assertEqual(classify([h, u, dict(u)]), [])


if __name__ == '__main__':
    unittest.main()
