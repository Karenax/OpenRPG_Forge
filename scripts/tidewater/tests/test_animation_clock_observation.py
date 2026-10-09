import copy
import importlib.util
import unittest
from pathlib import Path

path = Path(__file__).resolve().parents[1] / 'analyze-animation-clock-observation.py'
spec = importlib.util.spec_from_file_location('clock_observation', path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def observation():
    diagnostic = dict(virtual_time_s=2, scene_wind=dict(time=2),
                      wind_pose=dict(time=1.4, frame_index=14),
                      ocean_clock=dict(completed=dict(time=1.2), paused=True, quiescent=True),
                      cloud_clock=dict(time=1.8, dt=0, frame_index=18, submitted_step=19, encoded_step=19),
                      comparison_hold=True, pending=False)
    return dict(schema='forge.tidewater.animation-clock-observation', version=1,
                status='observed', failure=None, listener_closed=True,
                before=dict(diagnostic=copy.deepcopy(diagnostic)),
                held=dict(diagnostic=copy.deepcopy(diagnostic)),
                later=dict(diagnostic=copy.deepcopy(diagnostic)))


class ClockObservationTests(unittest.TestCase):
    def test_frozen_clocks_do_not_imply_aligned_clocks_or_parity(self):
        result = module.analyze(observation())
        self.assertTrue(result['clocks_frozen'])
        self.assertAlmostEqual(result['offsets_from_virtual_time']['held']['ocean_time'], -.8)
        self.assertFalse(result['temporal_parity_qualified'])
        self.assertFalse(result['gpu_completion_verified'])

    def test_stationary_time_with_advancing_cloud_history_is_reported(self):
        ledger = observation()
        for field in ('frame_index', 'submitted_step', 'encoded_step'):
            ledger['later']['diagnostic']['cloud_clock'][field] += 2
        result = module.analyze(ledger)
        self.assertTrue(result['clocks_frozen'])
        self.assertFalse(result['cloud_transactions_unchanged'])
        self.assertEqual(result['changes_while_held']['cloud_submitted'], 2)

    def test_time_drift_is_not_reported_as_frozen(self):
        ledger = observation()
        ledger['later']['diagnostic']['wind_pose']['time'] += .25
        result = module.analyze(ledger)
        self.assertFalse(result['clocks_frozen'])
        self.assertEqual(result['changes_while_held']['plant_time'], .25)

    def test_missing_invalid_or_unheld_samples_fail_closed(self):
        for value in (None, float('nan'), True):
            ledger = observation()
            ledger['held']['diagnostic']['cloud_clock']['time'] = value
            result = module.analyze(ledger)
            self.assertEqual(result['status'], 'failed_closed')
            self.assertFalse(result['clocks_frozen'])
        ledger = observation()
        ledger['held']['diagnostic']['comparison_hold'] = False
        self.assertEqual(module.analyze(ledger)['status'], 'failed_closed')


if __name__ == '__main__':
    unittest.main()
