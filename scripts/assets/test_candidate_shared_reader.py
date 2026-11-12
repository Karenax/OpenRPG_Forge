import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import vault_storage as storage
from candidate_shared_reader import CandidateSharedReader, MAX_RANGE


class CandidateSharedReaderTests(unittest.TestCase):
    def fixture(self, root):
        vault = root / 'vault'
        (vault / 'candidate').mkdir(parents=True)
        (vault / 'shared').mkdir()
        payload = b'prefix-resource-suffix'
        (vault / 'shared/resources.bin').write_bytes(payload)
        catalog = json.dumps({'files': [{'path': 'dependencies/resources.bin',
                                        'bytes': len(payload), 'sha256': storage.sha(payload)}]}).encode()
        (vault / 'candidate/catalog.v1.json').write_bytes(catalog)
        manifest = {'schema': 'openrpg.unity-shared-overlay.v1', 'source_root': 'candidate', 'files': [
            {'logical': 'catalog.v1.json', 'vault_path': 'candidate/catalog.v1.json',
             'bytes': len(catalog), 'sha256': storage.sha(catalog)},
            {'logical': 'dependencies/resources.bin', 'vault_path': 'shared/resources.bin',
             'bytes': len(payload), 'sha256': storage.sha(payload)}]}
        path = root / 'manifest.json'
        path.write_text(json.dumps(manifest), encoding='utf8')
        return vault, path, payload

    def test_catalog_and_range_use_shared_source_without_original(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, path, payload = self.fixture(Path(temp).resolve())
            reader = CandidateSharedReader(vault, path, storage.sha(path.read_bytes()))
            self.assertEqual(reader.verify_catalog_files()['files'], 1)
            self.assertEqual(reader.read_range('dependencies/resources.bin', 7, 8,
                                               storage.sha(b'resource')), b'resource')
            self.assertFalse((vault / 'candidate/dependencies/resources.bin').exists())

    def test_refuses_bad_range_hash_and_changed_shared_file(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, path, payload = self.fixture(Path(temp).resolve())
            reader = CandidateSharedReader(vault, path, storage.sha(path.read_bytes()))
            with self.assertRaisesRegex(ValueError, 'outside file'):
                reader.read_range('dependencies/resources.bin', len(payload), 1, storage.sha(b'x'))
            with self.assertRaisesRegex(ValueError, 'invalid candidate resource range'):
                reader.read_range('dependencies/resources.bin', 0, MAX_RANGE + 1, storage.sha(b'x'))
            with self.assertRaisesRegex(ValueError, 'range SHA'):
                reader.read_range('dependencies/resources.bin', 7, 8, storage.sha(b'wrong'))
            (vault / 'shared/resources.bin').write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'payload changed'):
                reader.verify_catalog_files()

    def test_catalog_checks_logical_glb_not_storage_envelope(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, path, _ = self.fixture(Path(temp).resolve())
            manifest = json.loads(path.read_bytes())
            envelope = b'envelope'
            model = b'logical-glb'
            (vault / 'candidate/model.glb').write_bytes(envelope)
            catalog = json.dumps({'files': [{'path': 'model.glb', 'bytes': len(model),
                                            'sha256': storage.sha(model)}]}).encode()
            (vault / 'candidate/catalog.v1.json').write_bytes(catalog)
            manifest['files'][0].update(bytes=len(catalog), sha256=storage.sha(catalog))
            manifest['files'].append({'logical': 'model.glb', 'vault_path': 'candidate/model.glb',
                                      'bytes': len(envelope), 'sha256': storage.sha(envelope)})
            path.write_text(json.dumps(manifest), encoding='utf8')
            reader = CandidateSharedReader(vault, path, storage.sha(path.read_bytes()))
            with patch.object(storage, 'chunks', return_value=iter([model])):
                self.assertEqual(reader.verify_catalog_files()['files'], 1)

    def test_package_reader_requires_published_pinned_manifest(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, path, _ = self.fixture(Path(temp).resolve())
            with self.assertRaisesRegex(ValueError, 'not published'):
                CandidateSharedReader.from_package(vault, vault / 'candidate')
            (vault / 'metadata').mkdir()
            published = vault / 'metadata/overlay.json'
            published.write_bytes(path.read_bytes())
            entry = {'source_root': 'candidate', 'manifest': 'metadata/overlay.json',
                     'sha256': storage.sha(published.read_bytes())}
            (vault / 'metadata/unity-shared-overlay-registry.v1.json').write_text(
                json.dumps({'schema': 'openrpg.unity-shared-overlay-registry.v1', 'overlays': [entry]}),
                encoding='utf8')
            reader = CandidateSharedReader.from_package(vault, vault / 'candidate')
            self.assertEqual(reader.verify_catalog_files()['files'], 1)
            published.write_text('{}', encoding='utf8')
            with self.assertRaisesRegex(ValueError, 'manifest SHA'):
                CandidateSharedReader.from_package(vault, vault / 'candidate')

    def test_viking_catalog_checks_model_and_shared_source(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, path, payload = self.fixture(Path(temp).resolve())
            manifest = json.loads(path.read_bytes())
            model = b'logical-model'
            (vault / 'candidate/model.glb').write_bytes(model)
            catalog = json.dumps({'schema_version': 1, 'assets': [
                {'file': 'model.glb', 'sha256': storage.sha(model), 'bytes': len(model),
                 'source_files': [{'file': 'dependencies/resources.bin', 'sha256': storage.sha(payload)}]}]}).encode()
            (vault / 'candidate/catalog.v1.json').write_bytes(catalog)
            manifest['files'][0].update(bytes=len(catalog), sha256=storage.sha(catalog))
            manifest['files'].append({'logical': 'model.glb', 'vault_path': 'candidate/model.glb',
                                      'bytes': len(model), 'sha256': storage.sha(model)})
            path.write_text(json.dumps(manifest), encoding='utf8')
            reader = CandidateSharedReader(vault, path, storage.sha(path.read_bytes()))
            with patch.object(storage, 'chunks', return_value=iter([model])):
                result = reader.verify_viking_catalog()
            self.assertEqual((result['assets'], result['files']), (1, 2))


if __name__ == '__main__':
    unittest.main()
