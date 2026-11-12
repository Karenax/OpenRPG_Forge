import csv
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


class CoverageAuditTests(unittest.TestCase):
    def test_multiple_variants_all_pairs_and_registry_guard(self):
        script = Path(__file__).with_name('audit_variant_pair_coverage.py')
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / 'metadata').mkdir()
            (root / 'pack').mkdir()
            ids = ['item.model', 'item.variant-abc.model', 'item.variant-def.model',
                   'orphan.variant-fed.model']
            details = json.dumps({'assets': [
                {'id': ident, 'model_path': str(root / (ident + '.glb')),
                 'model_sha256': str(i) * 64} for i, ident in enumerate(ids, 1)
            ]}).encode()
            (root / 'pack/details.v1.json').write_bytes(details)
            registry = json.dumps({'packs': {'pack': {'path': 'pack', 'files': {
                'details.v1.json': hashlib.sha256(details).hexdigest()}}}}).encode()
            (root / 'metadata/asset-import-registry.v1.json').write_bytes(registry)
            output, pairs = root / 'groups.csv', root / 'pairs.csv'
            command = [sys.executable, '-B', str(script), '--vault', str(root),
                       '--expected-registry-sha', hashlib.sha256(registry).hexdigest(),
                       '--output', str(output), '--pairs-output', str(pairs)]
            result = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            summary = json.loads(result.stdout)
            self.assertEqual(summary['additional_pairs'], 3)
            self.assertEqual(summary['counts']['multiple_or_missing_main'], 1)
            with pairs.open(encoding='utf-8-sig', newline='') as stream:
                values = list(csv.DictReader(stream))
            self.assertEqual(len({r['id'] for r in values}), 3)
            self.assertTrue(all(r['same_path'] == 'False' for r in values))
            (root / 'pack/details.v1.json').write_bytes(details + b' ')
            output.unlink()
            pairs.unlink()
            result = subprocess.run(command, capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertFalse(output.exists())


if __name__ == '__main__':
    unittest.main()
