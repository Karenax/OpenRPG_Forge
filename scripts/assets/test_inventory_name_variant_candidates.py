import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from inventory_name_variant_candidates import inventory, prefab_paths


class NameInventoryTests(unittest.TestCase):
    def test_list_prefers_original_name_over_hashed_local_filename(self):
        entries = [{'path':'sources/files/123__Door.prefab','original_source_path':'Assets/HDRP/Door.prefab'},
                   {'path':'sources/files/456__Door.prefab','original_source_path':'Assets/HDRP/Door.prefab'}]
        self.assertEqual(prefab_paths(entries), ['Assets/HDRP/Door.prefab'])
        self.assertEqual(prefab_paths([{'path':'Assets/URP/Window.prefab'},None]), ['Assets/URP/Window.prefab'])

    def test_cross_pack_names_group_by_lod_without_claiming_duplicates(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); (root / 'metadata').mkdir(); packs = {}
            for pack, cards in {
                'hivemind.demo': [
                    {'id': 'hivemind.demo.door-abc', 'source_prefab': 'Assets/HDRP/Door.prefab'},
                    {'id': 'hivemind.demo.door-ghi', 'source_prefab': 'Assets/HDRP/Other/Door.prefab'},
                    {'id': 'hivemind.demo.door-abc-lod1', 'source_prefab': {'path': 'Assets/HDRP/Door.prefab'}},
                    {'id': 'hivemind.demo.unknown', 'names': {'en': 'Hidden prop'}},
                ],
                'hivemind.demo.variant-1234': [
                    {'id': 'hivemind.demo.variant-1234.door-def', 'source_prefab': 'Assets/URP/DOOR.prefab'},
                    {'id': 'hivemind.demo.variant-1234.unknown', 'names': {'en': 'Hidden prop'}},
                ],
            }.items():
                folder = root / pack; folder.mkdir()
                data = json.dumps({'assets': cards}).encode(); (folder / 'details.v1.json').write_bytes(data)
                packs[pack] = {'path': pack, 'files': {'details.v1.json': hashlib.sha256(data).hexdigest()}}
            data = json.dumps({'packs': packs}).encode(); (root/'metadata/asset-import-registry.v1.json').write_bytes(data)
            result = inventory(root, hashlib.sha256(data).hexdigest())
            self.assertEqual(len(result['groups']), 1)
            self.assertEqual(len(result['same_pack_name_groups']), 1)
            self.assertEqual(result['groups'][0]['lod'], 0)
            self.assertEqual(result['cards_without_source_prefab'], 2)
            self.assertEqual(len(result['display_name_leads_with_missing_prefab']), 1)
            self.assertEqual(result['state'], 'inventory_only')
            with self.assertRaisesRegex(ValueError, 'registry changed'):
                inventory(root, '0' * 64)


if __name__ == '__main__':
    unittest.main()
