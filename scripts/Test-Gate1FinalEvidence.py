"""Negative contract tests, not graphical or human qualification."""
import copy
import importlib.util
from pathlib import Path
import unittest
import tempfile
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('final_evidence', Path(__file__).with_name('Verify-Gate1FinalEvidence.py'))
evidence = importlib.util.module_from_spec(spec)
spec.loader.exec_module(evidence)


class SupplementTests(unittest.TestCase):
    def setUp(self):
        self.documents = {
            'inputs.json': {'pins': [{'Path': f'input-{i}', 'Hash': 'hash'} for i in range(8)]},
            'analysis.json': {'status': 'PASS', 'capture': {'degraded': False, 'pipeline_type': 'Vulkan'},
                              'gpu_durations': {'status': 'PASS_REPLAY_ONLY'}, 'metrics': {}},
            'inspection.json': {'human_retouch_performed': False, 'publication_approved': False,
                                'assets': [{'id': str(i), 'round_trip_equal': True,
                                            'source': f'source-{i}', 'source_sha256': 'hash',
                                            'blend': f'blend-{i}', 'blend_sha256': 'hash'} for i in range(6)]},
            'host-result.json': {'status': 'PASS', 'process_exited': True},
        }

    def run_supplement(self, documents):
        with patch.object(evidence, 'read', side_effect=lambda p: documents[p.name]), \
             patch.object(evidence, 'verify_pin'), patch.object(evidence.capture, 'digest', return_value='hash'):
            return evidence.closure_supplement(Path('fixture'), {})

    def test_success_preserves_qualification_limits(self):
        result = self.run_supplement(self.documents)
        self.assertFalse(result['human_retouch_verified'])
        self.assertFalse(result['studio_gpu_qualified'])
        self.assertEqual(result['native_gpu']['timings_scope'], 'replay_only_not_live_frame_time')

    def test_invalid_supplements_are_rejected(self):
        changes = [
            ('analysis.json', lambda d: d['capture'].update(degraded=True)),
            ('analysis.json', lambda d: d['gpu_durations'].update(status='LIVE')),
            ('inputs.json', lambda d: d['pins'].pop()),
            ('inspection.json', lambda d: d.update(human_retouch_performed=True)),
            ('inspection.json', lambda d: d['assets'].pop()),
            ('inspection.json', lambda d: d['assets'][0].update(round_trip_equal=False)),
            ('host-result.json', lambda d: d.update(process_exited=False)),
        ]
        for name, change in changes:
            with self.subTest(document=name, change=change):
                documents = copy.deepcopy(self.documents)
                change(documents[name])
                with self.assertRaises(ValueError):
                    self.run_supplement(documents)

    def test_changed_hash_never_enters_index(self):
        files = {}
        with patch.object(evidence.capture, 'digest', return_value='changed'):
            with self.assertRaises(ValueError):
                evidence.verify_pin(Path('fixture'), 'expected', files)
        self.assertEqual(files, {})


class ReferenceTests(unittest.TestCase):
    def test_current_views_reject_wrong_binary_or_incomplete_protocol(self):
        baseline = {'inputs': {'runtime': {'sha256':
                    'f7b39f7536dbcfcc8180daf6ac614e96b9804633f4cd2f5524fc5258ad27bd5d'}},
                    'status': 'capture_checks_passed_review_pending',
                    'fixed_optical': True, 'quality': 'high'}
        for change in [lambda m: m['inputs']['runtime'].update(sha256='historical'),
                       lambda m: m.update(status='incomplete'),
                       lambda m: m.update(fixed_optical=False),
                       lambda m: m.update(quality='ultra')]:
            manifest = copy.deepcopy(baseline)
            change(manifest)
            with self.subTest(manifest=manifest), patch.object(evidence, 'verify_pin'), \
                 patch.object(evidence, 'read', return_value=manifest), self.assertRaises(ValueError):
                evidence.current_native_views(Path('fixture'), {})

    def setUp(self):
        self.report = {'size': [1920, 1080], 'gpu': 'synthetic renderer',
                       'quality': 'high', 'stats': [], 'errors': []}

    def test_empty_statistics_are_explicit_not_a_benchmark(self):
        result = evidence.reference_observation(self.report)
        self.assertEqual(result['statistics_records'], 0)
        self.assertFalse(result['matched_benchmark_verified'])
        self.assertEqual(result['resolution'], [1920, 1080])

    def test_statistics_or_matching_label_do_not_prove_equivalence(self):
        self.report['stats'] = [{'fps': 60}]
        self.report['errors'] = ['synthetic failure']
        result = evidence.reference_observation(self.report)
        self.assertEqual(result['reported_errors'], 1)
        self.assertEqual(result['statistics_records'], 1)
        self.assertFalse(result['matched_benchmark_verified'])

    def test_missing_or_malformed_conditions_are_rejected(self):
        for field, value in [('size', [True, 1080]), ('size', [0, 1080]),
                             ('stats', None), ('errors', {}), ('gpu', ''), ('quality', None)]:
            with self.subTest(field=field, value=value):
                report = copy.deepcopy(self.report)
                report[field] = value
                with self.assertRaises(ValueError):
                    evidence.reference_observation(report)


class StudioMotionTests(unittest.TestCase):
    def test_overlap_has_its_own_pins_and_source_count(self):
        directory, pins, count = evidence.studio_motion.CANDIDATES['preparation-overlap']
        self.assertEqual(directory, 'r1-mesh-preparation-overlap-20260911')
        self.assertEqual(count, 284)
        self.assertEqual(len(pins), 7)
        self.assertNotEqual(pins['source-inputs.json'], evidence.studio_motion.PINS['source-inputs.json'])
        self.assertFalse(evidence.studio_motion.source_drift_allowed(
            'preparation-overlap', 'crates/forge_engine/README.md',
            '4a84ca12d6d6c0213d2d1cac480f850ab6ca6d93542464fad8080773f7183c03',
            '0773afe7706259184afeaeeb79336c98a457248243733cf73f8f914dd45d0961'))

    def test_unknown_candidate_is_rejected_before_read(self):
        with self.assertRaisesRegex(ValueError, 'unknown Studio candidate'):
            evidence.studio_motion.collect(Path('fixture'), Path('repo'), {}, None, None, 'latest')

    def test_incomplete_or_duplicate_overlap_sources_rejected(self):
        for paths in [list(map(str, range(283))), ['duplicate'] * 284]:
            pins = {'branch': 'Rust', 'clean_revision': False,
                    'files': [{'path': p} for p in paths]}
            with self.subTest(count=len(paths)), self.assertRaisesRegex(ValueError, 'incomplete source pins'):
                evidence.studio_motion.collect(Path('fixture'), Path('repo'), {},
                    lambda p: pins, lambda *args: None, 'preparation-overlap')

    def test_retained_payload_cannot_escape_candidate_directory(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            candidate = root / 'candidate'
            candidate.mkdir()
            self.assertEqual(evidence.studio_motion.contained(candidate, '.'), candidate.resolve())
            with self.assertRaisesRegex(ValueError, 'outside candidate'):
                evidence.studio_motion.contained(candidate, '..')

    def test_incomplete_retained_report_or_gate_claim_rejected(self):
        for report in [{'sha256': {}, 'gate1_pass': False},
                       {'sha256': {str(i): 'hash' for i in range(8)}, 'gate1_pass': True}]:
            with self.subTest(report=report), self.assertRaisesRegex(ValueError, 'invalid retained parity coverage'):
                evidence.studio_motion.collect_overlap_parity(Path('fixture'), Path('studio'), {}, {},
                    lambda p: report, lambda *args: None)

    def test_documentary_drift_is_exact_and_candidate_scoped(self):
        old = '4a84ca12d6d6c0213d2d1cac480f850ab6ca6d93542464fad8080773f7183c03'
        new = '0773afe7706259184afeaeeb79336c98a457248243733cf73f8f914dd45d0961'
        allowed = evidence.studio_motion.source_drift_allowed
        self.assertTrue(allowed('bank-reuse', 'crates/forge_engine/README.md', old, new))
        for candidate, path, before, after in [
            ('preparation-budget', 'crates/forge_engine/README.md', old, new),
            ('bank-reuse', 'crates/forge_engine/src/terrain_quality.rs', old, new),
            ('bank-reuse', 'crates/forge_engine/README.md', 'other', new),
            ('bank-reuse', 'crates/forge_engine/README.md', old, 'other')]:
            self.assertFalse(allowed(candidate, path, before, after))

    def setUp(self):
        sources = {name: 'synthetic-hash' for name in
                   ['terrain', 'vegetation', 'environment', 'water', 'weather']}
        records = []
        for index, tier in enumerate(['high', 'low', 'high']):
            identity = {'adapter_generation': index + 1, 'sequence': index + 1,
                        'plan_hash': 'synthetic-plan'}
            records.append({'tier': tier, 'fixed_tick': index + 1,
                            'velocity': [1, 0, 0], 'effective_translation': [0.01, 0, 0],
                            'quality': {'requested_tier': tier,
                                'application': {'status': 'plan_application_ready', 'gpu_ready': True,
                                                'error': None, 'identity': identity.copy()},
                                'effective': {'status': 'plan_application_ready', 'gpu_ready': True,
                                              'error': None, 'plan_hash': 'synthetic-plan',
                                              'plan': {'source_bindings': sources.copy(),
                                                       'requested_tier': tier,
                                                       'adapter_generation': index + 1}},
                                'gpu_submission': {'identity': identity.copy(),
                                                   'queue_work_completed': True,
                                                   'matching_view_ready': True,
                                                   'pipelines_failed': 0, 'pipelines_pending': 0,
                                                   'assets': {'inventory_ready': True},
                                                   'effects': {'public_resources_ready': True,
                                                               'missing': []}}}})
        self.motion = {'schema': 'forge.studio-quality-motion-proof', 'schema_version': 1,
                       'protocol': 'prewarmed_bridge_cycle',
                       'status': 'PASS: GPU High→Low→High en déplacement',
                       'profile_preparation': {'phase': 'ready', 'error': None,
                           'completed': [{'tier': t} for t in ['low', 'medium', 'high', 'ultra', 'high']]},
                       'records': records, 'diagnostic_trace': [{}, {}, {}]}

    def assess(self, motion):
        return evidence.studio_motion.assess({'observations': {'quality_motion': motion}})

    def test_cycle_does_not_promote_fluidity_or_historical_captures(self):
        result = self.assess(self.motion)
        self.assertEqual(result['recorded_ticks'], [1, 2, 3])
        for field in ['fluidity_qualified', 'visual_review_by_collector',
                      'studio_gpu_measurements_qualified', 'same_candidate_as_historical_capture_series']:
            self.assertIs(result[field], False)

    def test_success_label_does_not_replace_actual_evidence(self):
        changes = [
            lambda m: m.update(protocol='cold_bridge_cycle'),
            lambda m: m['records'].pop(),
            lambda m: m['profile_preparation']['completed'].pop(),
            lambda m: m['records'][2].update(velocity=[0, 1, 0]),
            lambda m: m['records'][2].update(effective_translation=[0, 0, 0]),
            lambda m: m['records'][2].update(velocity=[float('nan'), 0, 0]),
            lambda m: m['records'][2].update(fixed_tick=1),
            lambda m: m['records'][2]['quality']['gpu_submission']['identity'].update(sequence=999),
            lambda m: m['records'][2]['quality']['effective']['plan']['source_bindings'].update(terrain='stale'),
            lambda m: m['records'][2]['quality']['gpu_submission'].update(queue_work_completed=False),
            lambda m: m['records'][2]['quality']['gpu_submission'].update(pipelines_pending=1),
            lambda m: m['records'][2]['quality']['gpu_submission']['assets'].update(inventory_ready=False),
            lambda m: m['records'][2]['quality']['gpu_submission']['effects'].update(missing=['water']),
            lambda m: m['diagnostic_trace'].pop(),
        ]
        for index, change in enumerate(changes):
            with self.subTest(corruption=index):
                motion = copy.deepcopy(self.motion)
                change(motion)
                with self.assertRaises(ValueError):
                    self.assess(motion)


if __name__ == '__main__':
    unittest.main()
