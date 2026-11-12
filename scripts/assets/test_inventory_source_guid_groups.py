import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from inventory_source_guid_groups import inventory


class SourceGuidInventoryTests(unittest.TestCase):
    def test_groups_by_pack_guid_and_lod_and_checks_registry(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            metadata = root / 'metadata'
            metadata.mkdir()
            guid = 'a' * 32
            packs = {}
            for pack, ids in [('hivemind.test', [f'hivemind.test.prefab-{guid}',
                                                    f'hivemind.test.prefab-{guid}-lod1']),
                              ('hivemind.test.variant-ab', [f'hivemind.test.variant-ab.other-{guid}'])]:
                folder = metadata / pack
                folder.mkdir()
                data = json.dumps({'assets': [{'id': i} for i in ids]}).encode()
                (folder / 'details.v1.json').write_bytes(data)
                packs[pack] = {'path': folder.relative_to(root).as_posix(),
                               'files': {'details.v1.json': hashlib.sha256(data).hexdigest()}}
            raw = json.dumps({'packs': packs}).encode()
            (metadata / 'asset-import-registry.v1.json').write_bytes(raw)
            pin = hashlib.sha256(raw).hexdigest()
            result = inventory(root, pin)
            self.assertEqual(len(result['groups']), 1)
            self.assertEqual(result['groups'][0]['lod'], 0)
            self.assertEqual(len(result['groups'][0]['cards']), 2)
            self.assertEqual(result['cards_without_guid'], 0)
            with self.assertRaises(ValueError):
                inventory(root, '0' * 64)


if __name__ == '__main__':
    unittest.main()
