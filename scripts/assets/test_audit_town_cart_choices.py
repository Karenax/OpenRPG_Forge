import hashlib
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import audit_town_cart_choices as audit


class TownCartTests(unittest.TestCase):
    def test_refuses_changed_source_and_incomplete_preferred_card(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            prefab, fbx = root / 'cart.prefab', root / 'cart.fbx'
            prefab.write_bytes(b'prefab')
            fbx.write_bytes(b'fbx')
            pin = lambda data: hashlib.sha256(data).hexdigest()
            source = {'guid': audit.GUID, 'path': str(prefab), 'sha256': pin(b'prefab')}
            measurement = {'bounds': [[0], [1]], 'dimensions': [1, 2, 3], 'triangles': 2, 'vertices': 4, 'nodes': [1]}
            cards = [{'id': str(i), 'model_sha256': str(i), 'license': 'license',
                      'source_prefab': source, 'measurement': measurement,
                      'readiness': {'metadata_ready': bool(i)}} for i in range(2)]
            entries = [{'id': str(i), 'sha256': str(i), 'source_prefab': source,
                        'source_support_provenance': {'source_file': 'cart.fbx', 'source_sha256': pin(b'fbx')}}
                       for i in range(2)]
            entries[1]['visual_review'] = 'agent-reviewed-blender-cpu'
            with patch.object(audit, 'VAULT', root):
                self.assertEqual(audit.check(cards, entries)['keep_side'], 'variant')
                fbx.write_bytes(b'changed')
                with self.assertRaisesRegex(ValueError, 'FBX SHA'):
                    audit.check(cards, entries)
                fbx.write_bytes(b'fbx')
                cards[1]['readiness']['metadata_ready'] = False
                with self.assertRaisesRegex(ValueError, 'complete cards'):
                    audit.check(cards, entries)


if __name__ == '__main__':
    unittest.main()
