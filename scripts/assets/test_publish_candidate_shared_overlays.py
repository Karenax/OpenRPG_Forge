import contextlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import vault_storage as storage
from candidate_shared_reader import CandidateSharedReader
from prepare_candidate_shared_overlays import prepare
from publish_candidate_shared_overlays import publish


class FakeImporter:
    def shared_import_lock(self, _vault):
        return contextlib.nullcontext()

    def canonical_json(self, value):
        return (json.dumps(value, sort_keys=True) + '\n').encode()

    def atomic_write(self, path, data):
        path.write_bytes(data)


class CandidateOverlayPublicationTests(unittest.TestCase):
    def fixture(self, root):
        vault = root / 'vault'
        (vault / 'metadata').mkdir(parents=True)
        (vault / 'metadata/asset-source-support-registry.v1.json').write_bytes(b'{}')
        (vault / 'metadata/asset-import-registry.v1.json').write_bytes(b'{}')
        registry = {'schema': 'openrpg.unity-shared-overlay-registry.v1', 'overlays': []}
        (vault / 'metadata/unity-shared-overlay-registry.v1.json').write_text(json.dumps(registry))
        for name in ('one', 'two'):
            path = vault / f'candidates/hivemind/{name}/dependencies'
            path.mkdir(parents=True)
            (path / 'x.bin').write_bytes(b'content')
        row = {'old': 'candidates/hivemind/one/dependencies/x.bin',
               'retained': 'candidates/hivemind/two/dependencies/x.bin',
               'sha256': storage.sha(b'content'), 'bytes': 7}
        plan = root / 'plan.json'
        plan.write_text(json.dumps({'schema': 'openrpg.candidate-dependency-sharing-plan.v1',
                                    'state': 'prepared_only', 'rows': [row]}))
        stage = root / 'stage'
        prepare(vault, plan, stage)
        return vault, plan, stage, row

    @patch('publish_candidate_shared_overlays.exclusive_storage_lock', return_value=contextlib.nullcontext())
    def test_publishes_resolver_without_deleting_original(self, _lock):
        with tempfile.TemporaryDirectory() as temp:
            vault, plan, stage, row = self.fixture(Path(temp).resolve())
            importer = FakeImporter()
            self.assertEqual(publish(vault, stage, plan, importer)['state'], 'dry_run_passed')
            self.assertEqual(publish(vault, stage, plan, importer, True)['state'], 'published')
            reader = CandidateSharedReader.from_package(vault, vault / 'candidates/hivemind/one')
            with reader.open('dependencies/x.bin') as stream:
                self.assertEqual(stream.read(), b'content')
            self.assertEqual((vault / row['old']).read_bytes(), b'content')

    @patch('publish_candidate_shared_overlays.exclusive_storage_lock', return_value=contextlib.nullcontext())
    def test_refuses_changed_candidate_before_publication(self, _lock):
        with tempfile.TemporaryDirectory() as temp:
            vault, plan, stage, row = self.fixture(Path(temp).resolve())
            (vault / row['old']).write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'candidate payload changed'):
                publish(vault, stage, plan, FakeImporter(), True)
            registry = json.loads((vault / 'metadata/unity-shared-overlay-registry.v1.json').read_bytes())
            self.assertEqual(registry['overlays'], [])


if __name__ == '__main__':
    unittest.main()
