import hashlib
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

import audit_military_container_choices as audit


class MilitaryContainerChoiceTests(unittest.TestCase):
    def test_source_hash_and_review_are_required(self):
        with TemporaryDirectory() as tmp:
            paths = [Path(tmp) / 'a', Path(tmp) / 'b']
            for i, path in enumerate(paths):
                path.write_bytes(bytes([i]))
            measurement = {'bounds': [[0], [1]], 'dimensions': [1, 2, 3],
                           'triangles': 2, 'vertices': 4, 'nodes': [{'node': 0}]}
            cards = [{'id': str(i), 'license': 'license', 'model_sha256': str(i),
                      'source_prefab': str(i), 'measurement': measurement,
                      'readiness': {'metadata_ready': True}} for i in range(2)]
            entries = [{'id': str(i), 'sha256': str(i), 'source_guid': audit.GUID,
                        'source_prefab': str(i), 'geometry_roundtrip_passed': True,
                        'source_prefab_sha256': hashlib.sha256(bytes([i])).hexdigest(),
                        'components': [{'mesh_sha256': 'a' * 64}]} for i in range(2)]
            entries[0].update(material_review_pending=False, visual_review='agent_review')
            self.assertEqual(audit.check(cards, entries, paths)['keep_side'], 'main')
            paths[1].write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'source prefab SHA'):
                audit.check(cards, entries, paths)
            paths[1].write_bytes(b'\1')
            entries[0]['material_review_pending'] = True
            with self.assertRaisesRegex(ValueError, 'material review'):
                audit.check(cards, entries, paths)


if __name__ == '__main__':
    unittest.main()
