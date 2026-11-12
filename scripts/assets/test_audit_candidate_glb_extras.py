import csv
import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from audit_candidate_glb_extras import audit


class CandidateGlbAuditTests(unittest.TestCase):
    def test_rejects_stale_inventory_and_classifies_only_qualified_extras(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / 'metadata').mkdir()
            registry = root / 'metadata/asset-import-registry.v1.json'
            registry.write_bytes(b'{}')
            volumes = root / 'volumes.json'
            volumes.write_text(json.dumps({'registry_sha256': hashlib.sha256(b'{}').hexdigest(),
                'rows': [{'id': 'x', 'pack': 'pack', 'state': 'current_candidate'}]}))
            comparison = root / 'comparison.csv'
            with comparison.open('w', newline='') as stream:
                writer = csv.DictWriter(stream, fieldnames=['id', 'main_path', 'main_sha', 'variant_path', 'variant_sha'])
                writer.writeheader()
                writer.writerow({'id': 'x', 'main_path': 'a', 'main_sha': 'a', 'variant_path': 'b', 'variant_sha': 'b'})
            first = {'nodes': [{'extras': {'asset_id': 'a'}}]}
            second = {'nodes': [{'extras': {'asset_id': 'b'}}]}
            with patch('audit_candidate_glb_extras.load', side_effect=[(first, 'tail'), (second, 'tail')]):
                result = audit(root, volumes, comparison, 'pack')
            self.assertTrue(result['results'][0]['metadata_only'])
            registry.write_bytes(b'{"changed":true}')
            with self.assertRaises(ValueError):
                audit(root, volumes, comparison, 'pack')


if __name__ == '__main__':
    unittest.main()
