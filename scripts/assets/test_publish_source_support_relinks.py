import json
import tempfile
import unittest
from pathlib import Path

import vault_storage as storage
from publish_source_support_relinks import IMPORTER, importer_module, publish
from stage_source_support_relinks import replace


class PublishTests(unittest.TestCase):
    def test_atomic_relink_and_refusal_when_registry_changed(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            vault, stage = root/'vault', root/'stage'
            (vault/'metadata').mkdir(parents=True)
            stage.mkdir()
            for path, data in [('old', b'source'), ('keep', b'source'), ('meta', b'meta'), ('archive', b'archive')]:
                (vault/path).write_bytes(data)
            sha = storage.sha(b'source')
            original = {'schema_version': 1, 'assets': [{'id': 'p.item', 'source_file': 'old',
                        'source_sha256': sha, 'source_meta_file': 'meta', 'source_meta_sha256': storage.sha(b'meta'),
                        'source_archive': 'archive', 'source_archive_bytes': 7}]}
            raw = json.dumps(original).encode()
            (vault/'metadata/catalog.json').write_bytes(raw)
            pointer = {'schema_version': 1, 'path': 'metadata/catalog.json', 'sha256': storage.sha(raw), 'revision': storage.sha(raw)}
            pointer_raw = json.dumps(pointer).encode()
            (vault/'metadata/asset-source-support-registry.v1.json').write_bytes(pointer_raw)
            asset_raw = json.dumps({'packs': {}}).encode()
            (vault/'metadata/asset-import-registry.v1.json').write_bytes(asset_raw)
            item = {'old': 'old', 'retained': 'keep', 'sha256': sha, 'references': 1}
            updated, _, ids = replace(original, {'old': item})
            update_raw = json.dumps(updated).encode()
            (stage/'source-support.v1.json').write_bytes(update_raw)
            plan = {'schema': 'openrpg.source-support-relinks-stage.v1', 'state': 'prepared_only',
                    'source_registry_sha256': storage.sha(pointer_raw), 'asset_registry_sha256': storage.sha(asset_raw),
                    'prior_catalog_sha256': storage.sha(raw), 'staged_catalog_sha256': storage.sha(update_raw),
                    'changed_ids': ids, 'paths': [item]}
            (stage/'plan.json').write_text(json.dumps(plan), encoding='utf8')
            importer_sha = storage.sha(IMPORTER.read_bytes())
            importer = importer_module(importer_sha)
            receipt = publish(vault, stage, importer, importer_sha)
            self.assertEqual(receipt['state'], 'committed')
            self.assertEqual(json.loads((vault/'metadata/asset-source-support-registry.v1.json').read_bytes()), receipt['new_pointer'])
            self.assertEqual((vault/'old').read_bytes(), b'source')
            with self.assertRaisesRegex(ValueError, 'registry changed'):
                publish(vault, stage, importer, importer_sha)
            self.assertFalse((vault/'metadata/asset-import.lock').exists())


if __name__ == '__main__':
    unittest.main()
