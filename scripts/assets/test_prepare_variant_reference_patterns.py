import unittest

from prepare_variant_reference_patterns import patterns, vault_uuid


class ReferencePatternTests(unittest.TestCase):
    def test_uuid_matches_rust_vault_identity_contract(self):
        self.assertEqual(vault_uuid('demo.arbre-eclaire'), '65c7370e-c994-55ff-b7fc-f9b64ce3a714')

    def test_includes_old_and_new_identity_and_logical_hash(self):
        entry = {'retained_id': 'pack.item', 'retired_id': 'pack.variant-a.item',
                 'retained_sha256': 'a' * 64, 'retired_sha256': 'b' * 64}
        found = patterns({'candidates': [entry]})
        self.assertEqual(len(found), 6)
        self.assertIn(vault_uuid(entry['retired_id']), found)
        self.assertIn(entry['retired_sha256'], found)
        with self.assertRaises(ValueError):
            patterns({'candidates': [{**entry, 'retired_sha256': 'bad'}]})


if __name__ == '__main__':
    unittest.main()
