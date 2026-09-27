"""Regression checks against a supplied evidence snapshot; never launches graphics."""
import copy
import importlib.util
from pathlib import Path
import sys
import unittest

spec = importlib.util.spec_from_file_location('parity', Path(__file__).with_name('Compare-NativeStudioProfiles.py'))
parity = importlib.util.module_from_spec(spec)
spec.loader.exec_module(parity)
root = Path(sys.argv.pop(1))
native, studio = parity.load_inputs(root / 'native/runtime.jsonl', [root / f'studio/stage-{i}.json' for i in range(5)])
sessions = ['first', 'first', 'second', 'second', 'second']


class ParityTests(unittest.TestCase):
    def test_real_evidence(self):
        self.assertEqual(parity.compare(native, studio, sessions)['status'], 'SEMANTIC_PROFILE_PARITY_PASS')

    def test_rejects_mutated_evidence(self):
        mutations = [
            lambda x: x['application'].update(gpu_ready=False),
            lambda x: x['gpu_submission']['identity'].update(sequence=999),
            lambda x: x['gpu_submission'].update(pipelines_pending=1),
            lambda x: x['gpu_submission']['effects'].update(missing=['water']),
            lambda x: x['effective']['plan']['source_bindings'].update(water='changed'),
            lambda x: x['effective']['plan']['effective']['water'].update(reflection_steps=999),
            lambda x: x['effective']['plan'].update(substitutions=[]),
            lambda x: x.update(requested_tier='low'),
        ]
        for mutate in mutations:
            with self.subTest(mutation=mutate):
                changed = copy.deepcopy(studio)
                mutate(changed[2])
                with self.assertRaises(ValueError):
                    parity.compare(native, changed, sessions)

    def test_does_not_hide_restart(self):
        with self.assertRaisesRegex(ValueError, 'stale Studio'):
            parity.compare(native, studio, ['one'] * 5)

    def test_missing_stage(self):
        with self.assertRaises(ValueError):
            parity.compare(native, studio[:-1], sessions)


if __name__ == '__main__':
    unittest.main()
