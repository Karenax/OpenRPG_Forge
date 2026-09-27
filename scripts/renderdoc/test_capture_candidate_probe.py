"""Pure tests of the asynchronous API readiness guard; no capture."""
import unittest
import json
import tempfile
from pathlib import Path
from capture_candidate_probe import api_ready, bridge_inputs, digest


class ApiReadinessTests(unittest.TestCase):
    def test_integrated_candidate_keeps_water_and_clouds_and_rejects_missing_pins(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            inputs = {}
            for name in ('runtime', 'world_sketch', 'kit_build', 'preview', 'forest_catalog',
                         'water_medium', 'cloud_profile'):
                path = root / name
                path.write_bytes(name.encode())
                inputs[name] = {'path': str(path), 'sha256': digest(path)}
            document = {'schema': 'forge.bridge-video-evidence',
                        'status': 'video_checks_passed_review_pending', 'weather': 'rain',
                        'requested_quality': 'high', 'forest_mode': 'batches', 'inputs': inputs,
                        'forward_water_candidate_requested': True, 'cloud_layer_requested': True}
            manifest = root / 'manifest.json'
            manifest.write_text(json.dumps(document), encoding='utf-8')
            _, pins, arguments, _ = bridge_inputs(root / 'output', manifest)
            self.assertEqual(len(pins), 8)
            for flag in ('--water-depth-preview', '--water-reflection-preview', '--cloud-layer-preview'):
                self.assertIn(flag, arguments)
            self.assertEqual(arguments[arguments.index('--water-medium-input') + 1], str(root / 'water_medium'))
            for field in ('forward_water_candidate_requested', 'cloud_layer_requested'):
                for value in (False, 'true'):
                    wrong = dict(document, **{field: value})
                    manifest.write_text(json.dumps(wrong), encoding='utf-8')
                    with self.assertRaisesRegex(RuntimeError, 'Inconsistent optional'):
                        bridge_inputs(root / 'output', manifest)
            manifest.write_text(json.dumps(document), encoding='utf-8')
            incomplete = dict(document, inputs={key: value for key, value in inputs.items()
                                               if key != 'water_medium'})
            manifest.write_text(json.dumps(incomplete), encoding='utf-8')
            with self.assertRaisesRegex(RuntimeError, 'Inconsistent optional'):
                bridge_inputs(root / 'output', manifest)
            manifest.write_text(json.dumps(document), encoding='utf-8')
            (root / 'cloud_profile').write_bytes(b'changed')
            with self.assertRaisesRegex(RuntimeError, 'hash mismatch'):
                bridge_inputs(root / 'output', manifest)

    def test_bridge_inputs_pin_sources_and_ignore_embedded_commands(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            inputs = {}
            for name in ('runtime', 'world_sketch', 'kit_build', 'preview', 'forest_catalog'):
                path = root / name
                path.write_bytes(name.encode())
                inputs[name] = {'path': str(path), 'sha256': digest(path)}
            document = {'schema': 'forge.bridge-video-evidence',
                        'status': 'video_checks_passed_review_pending', 'weather': 'clear',
                        'requested_quality': 'high', 'forest_mode': 'batches', 'inputs': inputs,
                        'runtime_command': ['DO_NOT_EXECUTE']}
            manifest = root / 'manifest.json'
            manifest.write_text(json.dumps(document), encoding='utf-8')
            runtime, pins, arguments, trigger = bridge_inputs(root / 'output', manifest)
            self.assertEqual(runtime, (root / 'runtime').resolve())
            self.assertEqual(len(pins), 6)
            self.assertNotIn('DO_NOT_EXECUTE', arguments)
            self.assertEqual(trigger.name, 'frame-0162.png')
            self.assertNotIn('--capture', arguments)
            self.assertIn(str(root / 'output' / 'frames'), arguments)
            (root / 'runtime').write_bytes(b'changed')
            with self.assertRaisesRegex(RuntimeError, 'hash mismatch'):
                bridge_inputs(root / 'output', manifest)
            document['requested_quality'] = 'unbounded'
            manifest.write_text(json.dumps(document), encoding='utf-8')
            with self.assertRaisesRegex(RuntimeError, 'Unsupported'):
                bridge_inputs(root / 'output', manifest)

    def test_waits_for_presenting_api(self):
        self.assertFalse(api_ready('', False, False))
        self.assertFalse(api_ready('Vulkan', False, True))

    def test_accepts_supported_presenting_vulkan(self):
        self.assertTrue(api_ready('Vulkan', True, True))

    def test_refuses_wrong_or_unsupported_api(self):
        for name, supported in [('D3D12', True), ('Vulkan', False), ('', True)]:
            with self.subTest(name=name, supported=supported):
                with self.assertRaises(RuntimeError):
                    api_ready(name, True, supported)
