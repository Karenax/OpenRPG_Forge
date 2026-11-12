import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from compose_variant_test_baselines import compose_baselines


class BaselineCompositionTests(unittest.TestCase):
    def test_preserves_success_and_refusal_and_rejects_changed_log(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            old, rebased, combined = (root / name for name in ('old', 'rebased', 'combined'))
            for path in (old, rebased, combined):
                path.mkdir()
            aliases = [{'retained_id': 'a', 'retired_id': 'b', 'retired_sha256': 'b' * 64},
                       {'retained_id': 'c', 'retired_id': 'd', 'retired_sha256': 'd' * 64}]
            original_manifest = {'packs': {'p': {'revision': 'same'}},
                                 'aliases_requiring_project_migration': aliases}
            original_raw = json.dumps(original_manifest).encode()
            rebased_manifest = {**original_manifest, 'rebased_from_stage_sha256': hashlib.sha256(original_raw).hexdigest()}
            rebased_raw = json.dumps(rebased_manifest).encode()
            combined_manifest = {'aliases_requiring_project_migration': aliases,
                'composed_sources': [{'path': str(rebased.resolve()), 'manifest_sha256': hashlib.sha256(rebased_raw).hexdigest()}]}
            combined_raw = json.dumps(combined_manifest).encode()
            index = {'a': ('p', {'id': 'a'}, {}), 'c': ('p', {'id': 'c'}, {})}
            baseline = root / 'baseline.json'
            baseline.write_text(json.dumps({'schema': 'openrpg.variant-export-baseline.v1',
                'stage_manifest_sha256': hashlib.sha256(original_raw).hexdigest(), 'exports': {
                    'b': {'sha256': 'b' * 64, 'outcome': 'success'},
                    'd': {'sha256': 'd' * 64, 'outcome': 'error', 'error': 'missing license'}}}))
            log = root / 'native.log'
            log.write_text('Verified 2 real legacy references against staged snapshot; 1 exports succeeded, 1 reproduced existing failures\ntest result: ok. 2 passed; 0 failed')
            lookup = {old: (original_raw, original_manifest, index), rebased: (rebased_raw, rebased_manifest, index),
                      combined: (combined_raw, combined_manifest, index)}
            with patch('compose_variant_test_baselines.stage_data', side_effect=lambda p: lookup[p]):
                result = compose_baselines(combined, [(old, rebased, baseline, log)])
                self.assertEqual(result['exports']['b']['outcome'], 'success')
                self.assertEqual(result['exports']['d']['error'], 'missing license')
                log.write_text('test result: ok. 2 passed; 0 failed')
                with self.assertRaisesRegex(ValueError, 'native stage test'):
                    compose_baselines(combined, [(old, rebased, baseline, log)])


if __name__ == '__main__':
    unittest.main()
