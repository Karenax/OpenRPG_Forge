import json
import tempfile
import unittest
from pathlib import Path

import vault_storage as storage
from prepare_candidate_shared_overlays import prepare
from unity_shared_overlay import UnitySourceResolver


class CandidateSharedOverlayPreparationTests(unittest.TestCase):
    def fixture(self, root):
        vault = root / 'vault'
        (vault / 'metadata').mkdir(parents=True)
        (vault / 'metadata/asset-source-support-registry.v1.json').write_bytes(b'{}')
        (vault / 'metadata/asset-import-registry.v1.json').write_bytes(b'{}')
        for package in ('first', 'second'):
            (vault / f'candidates/hivemind/{package}/dependencies').mkdir(parents=True)
            (vault / f'candidates/hivemind/{package}/dependencies/a.mips').write_bytes(b'identical')
        old = 'candidates/hivemind/first/dependencies/a.mips'
        keep = 'candidates/hivemind/second/dependencies/a.mips'
        plan = {'schema': 'openrpg.candidate-dependency-sharing-plan.v1', 'state': 'prepared_only',
                'rows': [{'old': old, 'retained': keep, 'sha256': storage.sha(b'identical'), 'bytes': 9}]}
        path = root / 'plan.json'
        path.write_text(json.dumps(plan), encoding='utf8')
        return vault, path, plan

    def test_complete_package_resolves_shared_file_without_mutation(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            vault, plan_path, plan = self.fixture(root)
            out = root / 'prepared'
            result = prepare(vault, plan_path, out)
            self.assertTrue(result['full_plan'])
            record = result['packages'][0]
            resolver = UnitySourceResolver(vault, out / record['manifest'], record['sha256'])
            self.assertEqual(resolver.resolve('dependencies/a.mips'), vault / plan['rows'][0]['retained'])
            self.assertEqual((vault / plan['rows'][0]['old']).read_bytes(), b'identical')

    def test_refuses_changed_source_and_alias_cycles(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            vault, plan_path, plan = self.fixture(root)
            (vault / plan['rows'][0]['retained']).write_bytes(b'different')
            with self.assertRaisesRegex(ValueError, 'retained file changed'):
                prepare(vault, plan_path, root / 'bad')
            plan['rows'].append({'old': plan['rows'][0]['retained'], 'retained': plan['rows'][0]['old'],
                                 'sha256': storage.sha(b'identical'), 'bytes': 9})
            plan_path.write_text(json.dumps(plan), encoding='utf8')
            with self.assertRaisesRegex(ValueError, 'cyclic'):
                prepare(vault, plan_path, root / 'cyclic')


if __name__ == '__main__':
    unittest.main()
