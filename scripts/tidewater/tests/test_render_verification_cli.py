"""Run the existing pixel/depth CLIs and check their additive diagnostics."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

import numpy as np
from PIL import Image


SCRIPTS = Path(__file__).resolve().parents[1]


class ComparatorCliTests(unittest.TestCase):
    def run_script(self, name, *args):
        result = subprocess.run([sys.executable, '-B', str(SCRIPTS / name), *map(str, args)],
                                capture_output=True, text=True, timeout=60)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_coverage_cli_preserves_old_fields_and_localizes_loss(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            a = np.zeros((1080, 1920, 3), dtype=np.uint8)
            a[100:120, 100:120] = 255
            b = a.copy()
            b[100:105, 100:120] = 0
            b[300:310, 300:310] = 255
            source, target, report = root / 'source.png', root / 'target.png', root / 'report.json'
            Image.fromarray(a).save(source)
            Image.fromarray(b).save(target)
            self.run_script('compare-coverage.py', source, target, report)
            data = json.loads(report.read_text())
            self.assertEqual(data['disagreement_pixels'], 200)
            self.assertEqual(data['quality_diagnostics']['missing']['pixels'], 100)
            self.assertEqual(data['quality_diagnostics']['additional']['pixels'], 100)
            self.assertEqual(data['quality_diagnostics']['additional']['bbox_xyxy_inclusive'],
                             [300, 300, 309, 309])
            self.assertFalse(data['quality_diagnostics']['render_quality_qualified'])

    def test_depth_cli_exposes_empty_layer_and_separates_shared_depth(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            native = np.ones((3, 2048, 2048), dtype='<f4')
            files = {}
            for layer in range(3):
                source = np.ones((2048, 2048), dtype='<f4')
                if layer == 0:
                    source[10:20, 10:20] = .25
                    native[layer, 10:20, 10:20] = .5
                    native[layer, 10, 10] = 1
                    native[layer, 30, 30] = .5
                raw = source.tobytes()
                path = root / f'source-{layer}.bin'
                path.write_bytes(raw)
                files[str(layer)] = dict(file=str(path), sha256=hashlib.sha256(raw).hexdigest())
            oracle, packed, report = root / 'oracle.json', root / 'native.bin', root / 'report.json'
            oracle.write_text(json.dumps(dict(schema='forge.tidewater.object-shadow-oracle', version=1,
                source_commit='1438b1abfcaee3267092b75573014f4d9b4a983c', map_size=2048,
                files=files, casters=[])), encoding='utf-8')
            packed.write_bytes(native.tobytes())
            self.run_script('compare-object-depth.py', oracle, packed, report)
            data = json.loads(report.read_text())
            layer = data['layers'][0]
            self.assertEqual(layer['coverage_disagreement'], 2)
            diagnostic = layer['quality_diagnostics']
            self.assertEqual(diagnostic['missing']['pixels'], 1)
            self.assertEqual(diagnostic['additional']['pixels'], 1)
            self.assertEqual(diagnostic['shared_depth']['pixels'], 99)
            self.assertEqual(diagnostic['shared_depth']['max_absolute_error'], .25)
            self.assertEqual(diagnostic['shared_depth']['candidate_farther_pixels'], 99)
            self.assertIsNone(data['layers'][1]['intersection_over_union'])
            self.assertIn('empty_reference', data['layers'][1]['quality_diagnostics']['status'])


if __name__ == '__main__':
    unittest.main()
