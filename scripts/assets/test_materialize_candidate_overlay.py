import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import vault_storage as storage
from materialize_candidate_overlay import catalog_models, export, cleanup_export


class CandidateOverlayExportTests(unittest.TestCase):
    def test_catalog_formats_and_missing_model_preflight(self):
        pin = storage.sha(b'model')
        self.assertEqual(catalog_models({'assets': [{'model': 'a.glb', 'sha256': pin}]}), {'a.glb': pin})
        self.assertEqual(catalog_models({'assets': [{'file': 'a.glb', 'sha256': pin}]}), {'a.glb': pin})
        self.assertEqual(catalog_models({'files': [{'path': 'a.glb', 'sha256': pin}]}), {'a.glb': pin})
        self.assertEqual(catalog_models({'materials': [], 'programs': []}), {})
        with tempfile.TemporaryDirectory() as temp:
            vault, work, manifest = self.fixture(Path(temp).resolve(), pin)
            catalog = json.dumps({'assets': [{'model': 'models/missing.glb', 'sha256': pin}]}).encode()
            (vault / 'candidate/catalog.promote.v1.json').write_bytes(catalog)
            doc = json.loads(manifest.read_bytes())
            doc['files'][0].update(sha256=storage.sha(catalog), bytes=len(catalog))
            manifest.write_text(json.dumps(doc), encoding='utf8')
            with self.assertRaisesRegex(ValueError, 'ambiguous historical'):
                export(vault, manifest, work / 'out', work)
            self.assertFalse((work / 'out').exists())

    def fixture(self, root, model_pin):
        vault = root / 'vault'
        work = root / 'work'
        (vault / 'candidate/models').mkdir(parents=True)
        (vault / 'shared').mkdir()
        work.mkdir()
        catalog = json.dumps({'assets': [{'model': 'models/a.glb', 'sha256': model_pin}]}).encode()
        (vault / 'candidate/catalog.promote.v1.json').write_bytes(catalog)
        (vault / 'candidate/models/a.glb').write_bytes(b'physical-envelope')
        (vault / 'shared/sample.mips').write_bytes(b'sample')
        files = [
            ('catalog.promote.v1.json', 'candidate/catalog.promote.v1.json', catalog),
            ('models/a.glb', 'candidate/models/a.glb', b'physical-envelope'),
            ('dependencies/sample.mips', 'shared/sample.mips', b'sample'),
        ]
        doc = {'schema': 'openrpg.unity-shared-overlay.v1', 'source_root': 'candidate', 'directories': ['models', 'dependencies'],
               'files': [{'logical': a, 'vault_path': b, 'sha256': storage.sha(c), 'bytes': len(c)}
                         for a, b, c in files], 'shared_files': 1,
               'logical_bytes': sum(len(c) for _, _, c in files)}
        manifest = work / 'manifest.json'
        manifest.write_text(json.dumps(doc), encoding='utf8')
        return vault, work, manifest

    def test_exports_shared_dependency_and_logical_model(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, work, manifest = self.fixture(Path(temp).resolve(), storage.sha(b'logical-glb'))
            with patch.object(storage, 'chunks', return_value=iter([b'logical-', b'glb'])):
                result = export(vault, manifest, work / 'out', work)
            self.assertEqual(result['state'], 'candidate_materialized')
            self.assertEqual((work / 'out/models/a.glb').read_bytes(), b'logical-glb')
            self.assertEqual((work / 'out/dependencies/sample.mips').read_bytes(), b'sample')
            self.assertEqual(result['models'][0]['sha256'], storage.sha(b'logical-glb'))
            receipt = work / 'receipt.json'
            receipt.write_text(json.dumps(result), encoding='utf8')
            (work / 'out/dependencies/sample.mips').write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'candidate export changed'):
                cleanup_export(manifest, receipt, work / 'out', work)
            (work / 'out/dependencies/sample.mips').write_bytes(b'sample')
            self.assertEqual(cleanup_export(manifest, receipt, work / 'out', work)['files'], 3)
            self.assertFalse((work / 'out').exists())

    def test_rejects_wrong_logical_model_without_changing_vault(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, work, manifest = self.fixture(Path(temp).resolve(), storage.sha(b'expected'))
            with patch.object(storage, 'chunks', return_value=iter([b'wrong'])):
                with self.assertRaisesRegex(ValueError, 'logical candidate model SHA'):
                    export(vault, manifest, work / 'out', work)
            self.assertEqual((vault / 'candidate/models/a.glb').read_bytes(), b'physical-envelope')
            self.assertEqual((vault / 'shared/sample.mips').read_bytes(), b'sample')

    def test_cli_exports_and_cleans_a_raw_model(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, work, manifest = self.fixture(Path(temp).resolve(), storage.sha(b'physical-envelope'))
            destination = work / 'cli-out'
            receipt = work / 'cli-receipt.json'
            command = [sys.executable, '-X', 'utf8', '-B', str(Path(__file__).with_name('materialize_candidate_overlay.py')),
                       '--vault', str(vault), '--manifest', str(manifest), '--work-root', str(work),
                       '--destination', str(destination), '--receipt', str(receipt)]
            subprocess.run(command, check=True, capture_output=True, text=True)
            self.assertTrue(destination.is_dir())
            subprocess.run([*command, '--cleanup'], check=True, capture_output=True, text=True)
            self.assertFalse(destination.exists())

    def test_restores_missing_historical_model_and_cleans_it(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, work, manifest = self.fixture(Path(temp).resolve(), storage.sha(b'current-model'))
            (vault / 'metadata').mkdir()
            registry = vault / 'metadata/asset-import-registry.v1.json'
            registry.write_bytes(b'{}')
            catalog = json.dumps({'assets': [
                {'id': 'current', 'model': 'models/a.glb', 'sha256': storage.sha(b'current-model')},
                {'id': 'old-id', 'model': 'historic/Old.glb', 'sha256': storage.sha(b'old-model')}]}).encode()
            (vault / 'candidate/catalog.promote.v1.json').write_bytes(catalog)
            doc = json.loads(manifest.read_bytes())
            doc['files'][0].update(sha256=storage.sha(catalog), bytes=len(catalog))
            doc['logical_bytes'] = sum(x['bytes'] for x in doc['files'])
            manifest.write_text(json.dumps(doc), encoding='utf8')
            with patch.object(storage, 'chunks', return_value=iter([b'current-model'])), \
                 patch('candidate_legacy_models.load_recipes', return_value=({'old-id': {}}, storage.sha(b'{}'))), \
                 patch('candidate_legacy_models.chunks', return_value=iter([b'old-model'])):
                result = export(vault, manifest, work / 'out', work)
            self.assertEqual((work / 'out/historic/Old.glb').read_bytes(), b'old-model')
            self.assertEqual(sum(x['source'] == 'historical_recipe' for x in result['models']), 1)
            receipt = work / 'receipt.json'
            receipt.write_text(json.dumps(result), encoding='utf8')
            self.assertEqual(cleanup_export(manifest, receipt, work / 'out', work)['files'], 4)


if __name__ == '__main__':
    unittest.main()
