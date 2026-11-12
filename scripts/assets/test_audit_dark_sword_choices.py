import hashlib
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
import audit_dark_sword_choices as audit


class SwordTests(unittest.TestCase):
    def test_changed_source_and_measurement_are_rejected(self):
        with TemporaryDirectory() as tmp:
            paths = [Path(tmp) / x for x in ('a', 'b', 'mesh')]
            for path in paths:
                path.write_bytes(b'source')
            pin = hashlib.sha256(b'source').hexdigest()
            measurement = {'bounds': [[0], [1]], 'dimensions': [1, 2, 3], 'triangles': 2, 'nodes': [1]}
            cards = [{'id': str(i), 'license': 'license', 'model_sha256': str(i), 'measurement': dict(measurement),
                      'readiness': {'metadata_ready': True}} for i in range(2)]
            entries = [{'id': str(i), 'sha256': str(i), 'source_guid': audit.GUID,
                        'geometry_roundtrip_passed': True, 'source_sha256': pin} for i in range(2)]
            entries[0].update(source_fbx_sha256=pin, visual_review='prior_agent_review_preserved', model_bytes_equal_to_active_asset=True)
            self.assertEqual(audit.check(cards, entries, paths[:2], paths[2])['keep_side'], 'main')
            paths[2].write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'FBX SHA'):
                audit.check(cards, entries, paths[:2], paths[2])
            paths[2].write_bytes(b'source')
            cards[1]['measurement']['triangles'] = 3
            with self.assertRaisesRegex(ValueError, 'measurement differs'):
                audit.check(cards, entries, paths[:2], paths[2])


if __name__ == '__main__':
    unittest.main()
