"""Check a complete fixed optical collection and reject altered readiness evidence."""
import copy
import importlib.util
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('capture', Path(__file__).with_name('Capture-WorldSketch.py'))
capture = importlib.util.module_from_spec(spec)
spec.loader.exec_module(capture)
root = Path(sys.argv.pop(1)).resolve()


class FixedCaptures(unittest.TestCase):
    def test_complete_pinned_collection(self):
        manifest = json.loads((root / 'manifest.json').read_text())
        self.assertTrue(manifest['fixed_optical'])
        for source in manifest['inputs'].values():
            self.assertEqual(capture.digest(Path(source['path'])), source['sha256'])
        world = json.loads(Path(manifest['inputs']['world_sketch']['path']).read_text())
        capture.verify_complete_coverage(manifest, world)
        for run in manifest['runs']:
            folder = root / run['id']
            preview = json.loads((folder / 'preview.json').read_text())
            observed = capture.verify_fixed(folder, preview, manifest['quality'], manifest['forest_batches'])
            self.assertEqual(observed['sha256'], run['sha256'])
            self.assertEqual(run['command'][run['command'].index('--weather') + 1], run['weather'])
            self.assertIn('--water-reflection-preview', run['command'])
            self.assertIn('--cloud-layer-preview', run['command'])
            self.assertNotIn('--metrics', run['command'])

    def test_bad_readiness_rejected(self):
        for corruption in ('missing', 'unstable', 'camera', 'terrain', 'batches', 'error'):
            with self.subTest(corruption=corruption), tempfile.TemporaryDirectory(dir=root.parent) as temp:
                folder = Path(temp) / 'case'
                shutil.copytree(root / 'hero_reference_match-clear', folder)
                preview = json.loads((folder / 'preview.json').read_text())
                log = folder / 'runtime.jsonl'
                events = [json.loads(x) for x in log.read_text().splitlines() if x.startswith('{')]
                if corruption == 'missing':
                    events = [e for e in events if e.get('event_kind') != 'capture_requested']
                elif corruption == 'error':
                    events.append({'level': 'ERROR'})
                else:
                    for event in events:
                        if corruption == 'unstable' and event.get('event_kind') == 'capture_requested':
                            event['stable_observations'] = 0
                        if corruption == 'camera' and event.get('event_kind') == 'fixed_view_stability_observed':
                            event['camera_id'] = 'wrong'
                        if corruption == 'terrain' and event.get('event_kind') == 'terrain_lod_projected':
                            event['terrain_hash'] = 'wrong'
                        if corruption == 'batches' and event.get('event_kind') == 'governed_far_batches_projected':
                            event['batches'] = 0
                log.write_text('\n'.join(json.dumps(e) for e in events))
                with self.assertRaises(ValueError):
                    capture.verify_fixed(folder, preview, 'high', True)


if __name__ == '__main__':
    unittest.main()
