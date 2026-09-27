"""Synthetic SDK-boundary tests; no GPU or visual qualification."""
import unittest
from types import SimpleNamespace as NS
from gate1_renderdoc_replay import AdapterError, inspect_draw_events


class DrawInspectionTests(unittest.TestCase):
    def test_bounds_and_unknown_draw_are_refused(self):
        rd = NS(ActionFlags=NS(Drawcall=1))
        for events in ([], [0], [1, 1], list(range(1, 18)), [3]):
            with self.assertRaises(AdapterError):
                inspect_draw_events(rd, object(), [], events)

    def test_depth_layer_shader_bytes_and_entry_are_preserved(self):
        rd = NS(ActionFlags=NS(Drawcall=1), ShaderStage=NS(Vertex=0, Pixel=1))
        action = NS(eventId=7, children=[], flags=1, numIndices=42, numInstances=3)
        descriptor = NS(resource='depth', firstSlice=2, firstMip=0)
        reflection = NS(encoding='SPIRV', rawBytes=b'shader')
        pipeline = NS(GetDepthTarget=lambda: descriptor, GetOutputTargets=lambda: [],
                      GetShaderReflection=lambda stage: reflection if stage == 0 else None,
                      GetShader=lambda stage: 'vertex-shader', GetShaderEntryPoint=lambda stage: 'vertex',
                      GetGraphicsPipelineObject=lambda: 'pipeline')
        selected = []
        controller = NS(GetTextures=lambda: [NS(resourceId='depth', width=4096, height=4096,
                                               arraysize=4, format=NS(Name=lambda: 'D32'))],
                        SetFrameEvent=lambda event, force: selected.append((event, force)),
                        GetPipelineState=lambda: pipeline,
                        DisassembleShader=lambda *args: 'OpEntryPoint Vertex')
        result = inspect_draw_events(rd, controller, [action], [7])
        self.assertEqual(selected, [(7, True)])
        self.assertEqual(result['draws'][0]['depth_target']['first_slice'], 2)
        self.assertEqual(result['draws'][0]['depth_target']['texture']['width'], 4096)
        self.assertEqual(result['draws'][0]['shaders'], {'Vertex': 'vertex-shader:vertex'})
        self.assertEqual(len(result['shaders']['vertex-shader:vertex']['raw_sha256']), 64)


if __name__ == '__main__':
    unittest.main()
