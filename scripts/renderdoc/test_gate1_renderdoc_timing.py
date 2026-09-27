"""Counter fixtures are not GPU measurements."""
import unittest
from types import SimpleNamespace as NS
from unittest.mock import Mock
from test_gate1_renderdoc_replay import replay_fixture, action, Flags
from gate1_renderdoc_replay import analyze_capture


def fixture():
    api, capture, controller = replay_fixture()
    # Timing also handles dispatch actions, absent from the original accounting stub.
    api.ActionFlags = NS(Drawcall=Flags.Drawcall, Present=Flags.Present, Dispatch=32)
    api.GPUCounter = NS(EventGPUDuration=1)
    api.CompType = NS(Float='Float')
    api.CounterUnit = NS(Seconds='Seconds')
    controller.EnumerateCounters = lambda: [1]
    controller.DescribeCounter = lambda _: NS(resultType='Float', resultByteWidth=8,
                                             unit='Seconds', name='GPU Duration')
    controller.FetchCounters = Mock(return_value=[NS(counter=1, eventId=1, value=NS(d=.002))])
    return api, capture, controller


class TimingTests(unittest.TestCase):
    def test_opt_in_and_milliseconds(self):
        api, capture, controller = fixture()
        _, result = analyze_capture(api, 'fixture.rdc', gpu_durations=True)
        self.assertEqual(result['gpu_durations']['leaf_duration_sum_ms_not_frame_time'], 2.)
        controller.FetchCounters.assert_called_once_with([1])
        controller.Shutdown.assert_called_once()
        capture.Shutdown.assert_called_once()

    def test_default_does_not_fetch(self):
        api, _, controller = fixture()
        _, result = analyze_capture(api, 'fixture.rdc')
        self.assertNotIn('gpu_durations', result)
        controller.FetchCounters.assert_not_called()

    def test_unavailable_closes_replay(self):
        api, capture, controller = fixture()
        controller.EnumerateCounters = lambda: []
        with self.assertRaisesRegex(ValueError, 'unavailable'):
            analyze_capture(api, 'fixture.rdc', gpu_durations=True)
        controller.Shutdown.assert_called_once()
        capture.Shutdown.assert_called_once()

    def test_invalid_missing_and_duplicate_results_fail(self):
        for values in [[], [-1.], [float('nan')], [float('inf')], [.001, .002]]:
            api, capture, controller = fixture()
            controller.FetchCounters.return_value = [NS(counter=1, eventId=1, value=NS(d=v)) for v in values]
            with self.assertRaises(ValueError):
                analyze_capture(api, 'fixture.rdc', gpu_durations=True)
            controller.Shutdown.assert_called_once()
            capture.Shutdown.assert_called_once()

    def test_container_duration_not_added_to_child(self):
        from gate1_renderdoc_timing import fetch_durations
        api, _, controller = fixture()
        roots = [action(1, Flags.MultiAction, children=[action(2, Flags.Drawcall)])]
        controller.FetchCounters.return_value = [NS(counter=1, eventId=i, value=NS(d=.002)) for i in (1, 2)]
        result = fetch_durations(api, controller, roots, None)
        self.assertEqual(result['leaf_duration_sum_ms_not_frame_time'], 2.)
        self.assertEqual(len(result['container_rows_excluded_from_sum']), 1)

    def test_wrong_units_and_unknown_events_are_rejected(self):
        for error in ['unit', 'event', 'counter']:
            api, capture, controller = fixture()
            if error == 'unit':
                controller.DescribeCounter = lambda _: NS(resultType='Float', resultByteWidth=8,
                                                         unit='Bytes', name='wrong')
            elif error == 'event':
                controller.FetchCounters.return_value[0].eventId = 999
            else:
                controller.FetchCounters.return_value[0].counter = 999
            with self.assertRaises(ValueError):
                analyze_capture(api, 'fixture.rdc', gpu_durations=True)
            controller.Shutdown.assert_called_once()
            capture.Shutdown.assert_called_once()


if __name__ == '__main__':
    unittest.main()
