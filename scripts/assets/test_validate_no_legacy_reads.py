import copy
import hashlib
import json
import unittest
from validate_no_legacy_reads import validate_scope, validate_export_baseline


def row(ident, kept_path='kept.glb', retired_path='old.glb'):
    return {'retained_id': 'kept-' + ident, 'retired_id': ident,
            'retained_uuid': 'u-' + ident, 'retired_uuid': 'v-' + ident,
            'retained_sha256': 'a' * 64, 'retired_sha256': 'b' * 64,
            'retained_path': kept_path, 'retired_path': retired_path}


class ScopeTests(unittest.TestCase):
    def test_export_baseline_requires_exact_stage_and_outcomes(self):
        manifest = json.dumps({'aliases_requiring_project_migration': [row('a'), row('b')]}).encode()
        baseline = {'schema': 'openrpg.variant-export-baseline.v1',
                    'stage_manifest_sha256': hashlib.sha256(manifest).hexdigest(),
                    'exports': {'a': {'sha256': 'b' * 64, 'outcome': 'success'},
                                'b': {'sha256': 'b' * 64, 'outcome': 'error', 'error': 'missing license'}}}
        raw = json.dumps(baseline).encode()
        self.assertEqual(validate_export_baseline(raw, manifest), hashlib.sha256(raw).hexdigest())
        for mutate in (lambda b: b['exports'].pop('a'),
                       lambda b: b['exports']['a'].update(sha256='c' * 64),
                       lambda b: b['exports']['b'].pop('error'),
                       lambda b: b.update(stage_manifest_sha256='0' * 64)):
            bad = copy.deepcopy(baseline)
            mutate(bad)
            with self.assertRaises(ValueError):
                validate_export_baseline(json.dumps(bad).encode(), manifest)

    def test_accepts_a_subset_but_requires_a_full_stage_count(self):
        a, b = row('a'), row('b', 'other.glb', 'another.glb')
        self.assertEqual(validate_scope({'candidates': [a]},
            {'aliases_requiring_project_migration': [a, b]}), 2)

    def test_rejects_shared_paths_and_mismatched_pins(self):
        a = row('a')
        with self.assertRaises(ValueError):
            validate_scope({'candidates': [row('a', 'same.glb', 'SAME.GLB')]},
                           {'aliases_requiring_project_migration': [a]})
        altered = dict(a, retired_sha256='c' * 64)
        with self.assertRaises(ValueError):
            validate_scope({'candidates': [altered]}, {'aliases_requiring_project_migration': [a]})


if __name__ == '__main__':
    unittest.main()
