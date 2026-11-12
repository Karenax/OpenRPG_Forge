import tempfile
import sys
import unittest
from pathlib import Path

sys.path.insert(0, r'E:\CodexData\skills\openrpg-import-assets\scripts')
import asset_import
from publish_variant_transition import digest
from qualify_variant_retirement import qualify
from test_retire_consolidated_models import ready


class RetirementQualificationTests(unittest.TestCase):
    def test_qualifies_only_current_studio_and_proof(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, stage, plan, old, paths = ready(Path(temp))
            studio, proof = Path(old['studio']), Path(old['exclusive_read_proof'])
            receipt = qualify(vault, plan, stage, proof, studio, digest(studio), asset_import)
            self.assertEqual(receipt['state'], 'ready')
            self.assertTrue(all(path.exists() for path in paths))
            with self.assertRaisesRegex(ValueError, 'Studio changed'):
                qualify(vault, plan, stage, proof, studio, '0' * 64, asset_import)


if __name__ == '__main__':
    unittest.main()
