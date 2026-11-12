import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from rebase_variant_test_baseline import rebase_baseline


class RebaseBaselineTests(unittest.TestCase):
    def test_carries_only_identical_metadata_and_pins(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            old, new = root / 'old', root / 'new'
            alias = {'retained_id': 'a', 'retired_id': 'b', 'retired_sha256': 'b' * 64}
            first = {'packs': {'pack': {'revision': 'x'}}, 'aliases_requiring_project_migration': [alias]}
            old_raw = json.dumps(first).encode()
            second = {**first, 'rebased_from_stage_sha256': hashlib.sha256(old_raw).hexdigest()}
            new_raw = json.dumps(second).encode()
            baseline = root / 'baseline.json'
            baseline.write_text(json.dumps({'schema': 'openrpg.variant-export-baseline.v1',
                'stage_manifest_sha256': hashlib.sha256(old_raw).hexdigest(),
                'provenance': {'state': 'metadata_equivalence_only'},
                'exports': {'b': {'sha256': 'b' * 64, 'outcome': 'error', 'error': 'missing license'}}}))
            lookup = {old: (old_raw, first, {}), new: (new_raw, second, {})}
            with patch('rebase_variant_test_baseline.stage_data', side_effect=lambda path: lookup[path]):
                result = rebase_baseline(old, new, baseline)
                self.assertEqual(result['exports']['b']['error'], 'missing license')
                self.assertEqual(result['stage_manifest_sha256'], hashlib.sha256(new_raw).hexdigest())
                second['packs'] = {'pack': {'revision': 'changed'}}
                with self.assertRaisesRegex(ValueError, 'not metadata-identical'):
                    rebase_baseline(old, new, baseline)


if __name__ == '__main__':
    unittest.main()
