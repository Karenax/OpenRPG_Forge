import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from inventory_descriptor_candidates import inventory


def encode(value):
    return json.dumps(value).encode()


class DescriptorInventoryTests(unittest.TestCase):
    def test_checks_pins_and_classifies_candidates_without_deleting_models(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            metadata = root / 'metadata'
            metadata.mkdir()
            files = [root / 'a.glb', root / 'b.glb']
            for f in files:
                f.write_bytes(b'unchanged')
            assets = [{'id': key, 'model_path': str(path), 'model_sha256': pin * 64}
                      for key, path, pin in zip(('a', 'b'), files, ('a', 'b'))]
            raw = encode({'assets': assets})
            (metadata / 'details.v1.json').write_bytes(raw)
            registry = encode({'packs': {'p': {'path': 'metadata', 'files':
                {'details.v1.json': hashlib.sha256(raw).hexdigest()}}}})
            (metadata / 'asset-import-registry.v1.json').write_bytes(registry)
            pair = {'comparison_current': True, 'mesh_payloads_exact': 'True', 'images_exact': 'True',
                    'main_private_extensions': "['OPENRPG_source_graph']",
                    'variant_private_extensions': "['OPENRPG_source_graph']",
                    'main_id': 'a', 'variant_id': 'b', 'main_path': str(files[0]),
                    'variant_path': str(files[1]), 'main_sha': 'a' * 64, 'variant_sha': 'b' * 64}
            source = root / 'inventory.json'
            source.write_bytes(encode({'registry_sha256': hashlib.sha256(registry).hexdigest(), 'pairs': [pair]}))
            def descriptor(vault, path):
                return {'bytes': 9, 'sha256': ('a' if path == files[0] else 'b') * 64}
            with patch('vault_storage.confined', side_effect=lambda vault, path: path), \
                 patch('vault_storage.read_descriptor', side_effect=descriptor):
                result = inventory(root, source)
                self.assertEqual(result['count'], 1)
                self.assertTrue(result['candidates'][0]['variant']['declared_pin_matches'])
                pair.update(images_exact='False', transforms_exact='True',
                            main_private_extensions='[]', variant_private_extensions='[]')
                source.write_bytes(encode({'registry_sha256': hashlib.sha256(registry).hexdigest(), 'pairs': [pair]}))
                self.assertEqual(inventory(root, source, 'identical-mesh')['count'], 1)
                pair['transforms_exact'] = 'False'
                source.write_bytes(encode({'registry_sha256': hashlib.sha256(registry).hexdigest(), 'pairs': [pair]}))
                self.assertEqual(inventory(root, source, 'identical-mesh')['count'], 0)
                source.write_bytes(encode({'registry_sha256': '0' * 64, 'pairs': [pair]}))
                with self.assertRaisesRegex(ValueError, 'registry has changed'):
                    inventory(root, source)
            self.assertTrue(all(p.read_bytes() == b'unchanged' for p in files))


if __name__ == '__main__':
    unittest.main()
