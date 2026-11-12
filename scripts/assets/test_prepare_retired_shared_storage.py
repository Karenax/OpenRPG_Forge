import hashlib
import json
import struct
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

import prepare_retired_shared_storage as subject
import vault_storage as storage


def glb(payload):
    data = payload + b'\0' * (-len(payload) % 4)
    doc = b'{}  '
    return (b'glTF' + struct.pack('<II', 2, 20 + len(doc) + 8 + len(data))
            + struct.pack('<II', len(doc), 0x4e4f534a) + doc
            + struct.pack('<II', len(data), 0x004e4942) + data)


class PrepareTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.vault = self.root / 'vault'
        (self.vault / 'metadata').mkdir(parents=True)
        models = self.vault / 'normalized/hivemind/demo'
        models.mkdir(parents=True)
        self.old = models / 'old.glb'
        self.keep = models / 'keep.glb'
        self.old_raw, self.keep_raw = glb(b'old mesh'), glb(b'new mesh')
        self.old.write_bytes(self.old_raw)
        self.keep.write_bytes(self.keep_raw)
        self.old_sha = hashlib.sha256(self.old_raw).hexdigest()
        keep_sha = hashlib.sha256(self.keep_raw).hexdigest()
        item = {'retired_id': 'old', 'retained_id': 'keep',
                'retired_path': str(self.old), 'retained_path': str(self.keep),
                'retired_sha256': self.old_sha, 'retained_sha256': keep_sha}
        detail = json.dumps({'assets': [{'id': 'old', 'model_path': str(self.old), 'model_sha256': self.old_sha},
                                        {'id': 'keep', 'model_path': str(self.keep), 'model_sha256': keep_sha}]}).encode()
        (self.vault / 'metadata/details.v1.json').write_bytes(detail)
        registry = json.dumps({'packs': {'p': {'path': 'metadata', 'files': {'details.v1.json': storage.sha(detail)}}}}).encode()
        (self.vault / 'metadata/asset-import-registry.v1.json').write_bytes(registry)
        self.plan = self.root / 'plan.json'
        self.plan.write_text(json.dumps({'schema': 'openrpg.variant-consolidation-plan.v1', 'state': 'planning',
                                         'registry_sha256': storage.sha(registry), 'candidate_count': 1,
                                         'candidates': [item]}))
        self.ids = self.root / 'ids.json'
        self.ids.write_text(json.dumps({'retired_ids': ['old']}))
        self.receipt = self.root / 'receipt.json'
        self.receipt.write_text('{}')
        self.report = self.root / 'report.json'

    def test_rejects_changed_model_and_does_not_write(self):
        self.old.write_bytes(glb(b'changed'))
        with self.assertRaisesRegex(ValueError, 'SHA changed'):
            subject.validate(self.vault, self.plan, {'old'})
        self.assertFalse((self.vault / '.forge-storage').exists())

    def test_rejects_changed_catalog(self):
        (self.vault / 'metadata/details.v1.json').write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError, 'catalog detail SHA changed'):
            subject.validate(self.vault, self.plan, {'old'})

    def test_shared_model_preserves_exact_bytes_and_rejects_replay(self):
        def native(argv, **kwargs):
            root, path = Path(argv[2]), Path(argv[3])
            return SimpleNamespace(stdout=json.dumps({'state': 'verified', 'sha256': storage.digest(root, path),
                                                      'bytes': len(self.old_raw)}))
        with patch.object(storage, 'verify_deployed_readers'), patch.object(storage.subprocess, 'run', side_effect=native):
            result = subject.apply(self.vault, self.plan, self.ids, self.receipt, Path('unused'), self.report)
        self.assertEqual(result['state'], 'complete')
        self.assertEqual(len(result['completed']), 1)
        self.assertEqual(storage.read_bytes(self.vault, self.old), self.old_raw)
        self.assertEqual(self.keep.read_bytes(), self.keep_raw)
        with self.assertRaisesRegex(ValueError, 'existing report'):
            subject.apply(self.vault, self.plan, self.ids, self.receipt, Path('unused'), self.report)

    def test_prepublication_failure_preserves_original(self):
        with patch.object(storage, 'verify_deployed_readers'), patch.object(storage.Packer, 'prepare', side_effect=ValueError('test failure')):
            with self.assertRaisesRegex(ValueError, 'test failure'):
                subject.apply(self.vault, self.plan, self.ids, self.receipt, Path('unused'), self.report)
        self.assertEqual(self.old.read_bytes(), self.old_raw)
        self.assertEqual(json.loads(self.report.read_text())['state'], 'interrupted')
        self.assertFalse((self.vault / '.forge-storage/WRITER.lock').exists())


if __name__ == '__main__':
    unittest.main()
