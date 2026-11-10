import unittest
from audit_variant_exact_candidates import diffs


class JsonDifferenceTests(unittest.TestCase):
    def test_reports_asset_identity_and_missing_fields(self):
        a = {'nodes': [{'extras': {'asset_id': 'hdrp'}}]}
        b = {'nodes': [{'extras': {'asset_id': 'urp'}}]}
        self.assertEqual(list(diffs(a, b)), ['$/nodes/0/extras/asset_id'])
        self.assertEqual(list(diffs(a, {'nodes': []})), ['$/nodes (length)'])
        self.assertEqual(list(diffs(a, {})), ['$/nodes (missing)'])


if __name__ == '__main__':
    unittest.main()
