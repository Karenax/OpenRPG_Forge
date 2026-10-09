import copy
import importlib.util
import json
from pathlib import Path
import unittest

path = Path(__file__).parents[1] / 'verify-animation-clock-pair.py'
spec = importlib.util.spec_from_file_location('clock_pair', path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class ClockPairTests(unittest.TestCase):
    def fixture(self):
        source = dict(source_commit=module.COMMIT, status='ready_reference_only',
                      animation_clocks=dict(global_time_s=2., ocean_time_s=2.25,
                                            cloud_clock=[124., 2.125, 1./60., 0.],
                                            wind_speed=7., wind_direction=[.35, .94]))
        native = dict(diagnostic=dict(comparison_hold=True, pending=False, virtual_time_s=2.,
                                      scene_wind=dict(time=2., speed=7., direction=[.35,.94]),
                                      wind_pose=dict(time=2., speed=7., direction=[.35,.94]),
                                      ocean_clock=dict(paused=True, quiescent=True, completed=dict(time=2.25)),
                                      cloud_clock=dict(time=2.125, dt=1./60., submitted_step=125, encoded_step=125)))
        return source, native

    def test_independent_source_precompile_times_are_preserved(self):
        result = module.compare(*self.fixture())
        self.assertEqual(result['status'], 'uniform_clocks_match')
        self.assertFalse(result['temporal_parity_qualified'])
        self.assertFalse(result['visual_parity'])

    def test_global_time_alone_cannot_validate_pairing(self):
        source, native = self.fixture()
        source['animation_clocks'] = dict(global_time_s=2.)
        self.assertEqual(module.compare(source, native)['status'], 'failed_closed')

    def test_ocean_and_cloud_drift_are_each_rejected(self):
        for group in ['ocean_clock','cloud_clock']:
            with self.subTest(group=group):
                source, native = self.fixture()
                if group == 'ocean_clock':native['diagnostic'][group]['completed']['time'] += .1
                else:native['diagnostic'][group]['time'] += .1
                self.assertEqual(module.compare(source, native)['status'], 'failed_closed')

    def test_missing_hold_or_unencoded_transaction_is_rejected(self):
        source, native = self.fixture()
        for change in [dict(comparison_hold=False), dict(cloud_clock=dict(time=2.125,dt=1./60.,submitted_step=126,encoded_step=125))]:
            altered = copy.deepcopy(native)
            altered['diagnostic'].update(change)
            self.assertEqual(module.compare(source, altered)['status'], 'failed_closed')

    def test_nonfinite_or_boolean_uniform_is_rejected(self):
        for value in [True, float('nan'), float('inf')]:
            source, native = self.fixture()
            native['diagnostic']['wind_pose']['time'] = value
            result = module.compare(source, native)
            self.assertEqual(result['status'], 'failed_closed')
            json.dumps(result, allow_nan=False)

    def test_fractional_or_boolean_transaction_counter_is_rejected(self):
        for value in [125.5, True]:
            source, native = self.fixture()
            native['diagnostic']['cloud_clock']['submitted_step'] = value
            self.assertEqual(module.compare(source, native)['status'], 'failed_closed')


if __name__ == '__main__':
    unittest.main()
