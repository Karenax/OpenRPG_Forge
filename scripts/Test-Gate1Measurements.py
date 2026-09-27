"""Synthetic contract tests, independent of the GPU or retained proof files."""
import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('measurements', Path(__file__).with_name('Verify-Gate1Measurements.py'))
measurements = importlib.util.module_from_spec(spec)
spec.loader.exec_module(measurements)


class MeasurementsTests(unittest.TestCase):
    def setUp(self):
        self.metrics = {
            'gpu': {'backend': 'Vulkan', 'memory': {'status': 'available',
                    'scope': 'backend_allocator_not_resident_vram_or_process_total',
                    'allocator': {'allocated_bytes': 100, 'reserved_bytes': 200}}},
            'render_diagnostics': [{'path': 'render/main_opaque_pass_3d/elapsed_' + suffix,
                                    'value': 0.5, 'suffix': 'ms'} for suffix in ['cpu', 'gpu']],
            'render_work': {'gpu_draw_calls': None},
        }
        self.replay = {'status': 'PASS', 'capture': {'pipeline_type': 'Vulkan',
                        'degraded': False, 'present_count': 1},
                       'metrics': {'draws': 10, 'instances': 20, 'triangles': 30}}

    def test_success_does_not_close_gate_or_warmup(self):
        result = measurements.validate(self.metrics, self.replay)
        self.assertFalse(result['gate1_pass'])
        self.assertFalse(result['warmup_qualified'])
        self.assertEqual(result['studio_gpu'], 'WAIVED_ADR_0012')

    def test_missing_nonfinite_and_negative_timings_rejected(self):
        for value in [None, float('nan'), float('inf'), -1, True]:
            with self.subTest(value=value):
                data = copy.deepcopy(self.metrics)
                data['render_diagnostics'][1]['value'] = value
                with self.assertRaises(ValueError):
                    measurements.validate(data, self.replay)

    def test_invented_draws_invalid_memory_and_degraded_replay_rejected(self):
        changes = [lambda m, r: m['render_work'].update(gpu_draw_calls=10),
                   lambda m, r: m['gpu']['memory']['allocator'].update(reserved_bytes=99),
                   lambda m, r: m['gpu']['memory'].update(status='unavailable'),
                   lambda m, r: r['capture'].update(degraded=True),
                   lambda m, r: r['metrics'].update(draws=0)]
        for change in changes:
            m, r = copy.deepcopy(self.metrics), copy.deepcopy(self.replay)
            change(m, r)
            with self.assertRaises(ValueError):
                measurements.validate(m, r)


if __name__ == '__main__':
    unittest.main()
