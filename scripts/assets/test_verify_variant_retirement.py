import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, r'E:\CodexData\skills\openrpg-import-assets\scripts')
import asset_import
from retire_consolidated_models import retire
from test_retire_consolidated_models import ready
from verify_variant_retirement import verify


class RetirementVerificationTests(unittest.TestCase):
    def test_detects_recreated_retired_file(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            vault, stage, plan, receipt, paths = ready(root)
            result = retire(vault, plan, stage, receipt, asset_import, root / 'retire.jsonl')
            verified = verify(vault, plan, receipt, result)
            self.assertEqual(verified['retired_absent'], 1)
            self.assertEqual(verified['retained_models_verified'], 1)
            paths[1].write_bytes(b'recreated')
            with self.assertRaisesRegex(ValueError, 'still exists'):
                verify(vault, plan, receipt, result)


if __name__ == '__main__':
    unittest.main()
