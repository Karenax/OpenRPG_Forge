"""Synthetic API-boundary tests: these never open a real capture or GPU."""
import unittest
from enum import IntFlag
from types import SimpleNamespace as NS
from unittest.mock import Mock

from gate1_renderdoc_model import ContractError
from gate1_renderdoc_replay import AdapterError, analyze_capture


class Flags(IntFlag):
    Drawcall = 1
    Present = 2
    Instanced = 4
    Indirect = 8
    MultiAction = 16


def action(event, flags, indices=3, instances=0, children=None):
    return NS(eventId=event, flags=flags, numIndices=indices,
              numInstances=instances, children=children or [],
              GetName=lambda _: 'Synthetic action')


def replay_fixture():
    pipeline = NS(GetPrimitiveTopology=lambda: 'TriangleList',
                  GetShaderReflection=lambda stage: object())
    controller = NS(
        GetAPIProperties=lambda: NS(pipelineType='Vulkan', degraded=False),
        GetRootActions=lambda: [action(1, Flags.Drawcall), action(2, Flags.Present, 0)],
        GetStructuredFile=lambda: None, SetFrameEvent=Mock(),
        GetPipelineState=lambda: pipeline, Shutdown=Mock())
    capture = NS(OpenFile=Mock(return_value=0), LocalReplaySupport=lambda: 'Supported',
                 OpenCapture=Mock(return_value=(0, controller)), Shutdown=Mock())
    api = NS(ActionFlags=Flags, ResultCode=NS(Succeeded=0),
             ReplaySupport=NS(Supported='Supported'), GraphicsAPI=NS(Vulkan='Vulkan'),
             Topology=NS(TriangleList='TriangleList', LineList='LineList', PointList='PointList'),
             ShaderStage=NS(Vertex='Vertex', Pixel='Pixel'), ReplayOptions=lambda: None,
             OpenCaptureFile=lambda: capture)
    return api, capture, controller


class ReplayTests(unittest.TestCase):
    def test_extracts_actual_leaf_event_before_reading_pipeline(self):
        api, capture, controller = replay_fixture()
        document, result = analyze_capture(api, 'synthetic.rdc')
        controller.SetFrameEvent.assert_called_once_with(1, True)
        self.assertEqual(document['capture']['present_count'], 1)
        self.assertEqual(result['metrics']['triangles'], 1)
        self.assertEqual(result['metrics']['instances'], 1)
        controller.Shutdown.assert_called_once()
        capture.Shutdown.assert_called_once()

    def test_expanded_multidraw_counts_children_not_parent(self):
        api, capture, controller = replay_fixture()
        children = [action(2, Flags.Drawcall | Flags.Instanced, 6, 2),
                    action(3, Flags.Drawcall | Flags.Instanced, 3, 0)]
        controller.GetRootActions = lambda: [
            action(1, Flags.Drawcall | Flags.MultiAction, 999, 999, children),
            action(4, Flags.Present, 0)]
        _, result = analyze_capture(api, 'synthetic.rdc')
        self.assertEqual(result['metrics']['draws'], 2)
        self.assertEqual(result['metrics']['instances'], 2)
        self.assertEqual(result['metrics']['triangles'], 4)
        self.assertEqual(controller.SetFrameEvent.call_count, 2)
        capture.Shutdown.assert_called_once()

    def test_unsupported_or_degraded_replay_is_rejected_and_closed(self):
        for properties in [NS(pipelineType='D3D12', degraded=False),
                           NS(pipelineType='Vulkan', degraded=True)]:
            with self.subTest(properties=properties):
                api, capture, controller = replay_fixture()
                controller.GetAPIProperties = lambda: properties
                with self.assertRaises(AdapterError):
                    analyze_capture(api, 'synthetic.rdc')
                controller.Shutdown.assert_called_once()
                capture.Shutdown.assert_called_once()

    def test_open_failure_does_not_attempt_replay(self):
        api, capture, controller = replay_fixture()
        capture.OpenFile.return_value = 1
        with self.assertRaises(AdapterError):
            analyze_capture(api, 'synthetic.rdc')
        capture.OpenCapture.assert_not_called()
        controller.Shutdown.assert_not_called()
        capture.Shutdown.assert_called_once()

    def test_replay_failure_closes_allocated_controller(self):
        api, capture, controller = replay_fixture()
        capture.OpenCapture.return_value = (1, controller)
        with self.assertRaises(AdapterError):
            analyze_capture(api, 'synthetic.rdc')
        controller.Shutdown.assert_called_once()
        capture.Shutdown.assert_called_once()

    def test_unexpanded_multidraw_is_rejected_and_closed(self):
        api, capture, controller = replay_fixture()
        controller.GetRootActions = lambda: [action(1, Flags.Drawcall | Flags.MultiAction)]
        with self.assertRaises(AdapterError):
            analyze_capture(api, 'synthetic.rdc')
        controller.Shutdown.assert_called_once()
        capture.Shutdown.assert_called_once()

    def test_ambiguous_indirect_zero_never_becomes_a_measured_draw(self):
        api, capture, controller = replay_fixture()
        controller.GetRootActions = lambda: [
            action(1, Flags.Drawcall | Flags.Indirect | Flags.Instanced, 0, 0),
            action(2, Flags.Present, 0)]
        with self.assertRaises(ContractError) as error:
            analyze_capture(api, 'synthetic.rdc')
        self.assertEqual(error.exception.code, 'unresolved_indirect')
        controller.Shutdown.assert_called_once()
        capture.Shutdown.assert_called_once()


if __name__ == '__main__':
    unittest.main()
