import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from inventory_missing_prefab_name_leads import inventory, key


class NameLeadTests(unittest.TestCase):
    def test_version_guid_and_lod_normalization(self):
        self.assertEqual(key('hivemind.pack.variant-abc123.prefab-sm-door-abc00000000007041811747187730999-lod1',
                             'hivemind.pack.variant-abc123'),
                         ('hivemind.pack', 'sm-door:lod1'))
        self.assertEqual(key('hivemind.pack.sm-door-abc00000000007041811747187730999',
                             'hivemind.pack'), ('hivemind.pack', 'sm-door:lod0'))

    def test_different_lods_and_names_remain_distinct(self):
        self.assertNotEqual(key('hivemind.p.sm-door-lod1', 'hivemind.p'),
                            key('hivemind.p.sm-door', 'hivemind.p'))
        self.assertNotEqual(key('hivemind.p.sm-door-a', 'hivemind.p'),
                            key('hivemind.p.sm-door-b', 'hivemind.p'))
        with self.assertRaises(ValueError):
            key('other.sm-door', 'hivemind.p')

    def test_inventory_finds_counterpart_and_rejects_changed_details(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            vault = root / 'vault'
            (vault / 'metadata').mkdir(parents=True)
            packs = {}
            for folder, pack, ident in [
                ('one', 'hivemind.p', 'hivemind.p.sm-door-abc00000000007041811747187730999'),
                ('two', 'hivemind.p.variant-abcdef', 'hivemind.p.variant-abcdef.prefab-sm-door-abc00000000007041811747187730998')]:
                (vault / folder).mkdir()
                data = json.dumps({'assets': [{'id': ident, 'model_sha256': 'a' * 64}]}).encode()
                (vault / folder / 'details.v1.json').write_bytes(data)
                packs[pack] = {'path': folder, 'files': {'details.v1.json': hashlib.sha256(data).hexdigest()}}
            registry = json.dumps({'packs': packs}).encode()
            (vault / 'metadata/asset-import-registry.v1.json').write_bytes(registry)
            source = root / 'missing.json'
            source.write_text(json.dumps({'state': 'inventory_only', 'registry_sha256': hashlib.sha256(registry).hexdigest(),
                                          'missing_cards': [{'id': 'hivemind.p.sm-door-abc00000000007041811747187730999'}]}))
            result = inventory(vault, source)
            self.assertEqual(result['target_count'], 1)
            self.assertEqual(len(result['groups']), 1)
            self.assertEqual(len(result['groups'][0]['cards']), 2)
            (vault / 'two/details.v1.json').write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'metadata changed'):
                inventory(vault, source)


if __name__ == '__main__':
    unittest.main()
