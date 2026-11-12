import json
import tempfile
import unittest
from pathlib import Path

import vault_storage as storage
from stage_source_support_relinks import replace
from validate_source_support_relinks import validate


class ValidationTests(unittest.TestCase):
    def test_validates_real_payload_then_rejects_tampering(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            vault = root/'vault'
            stage = root/'stage'
            (vault/'metadata').mkdir(parents=True)
            stage.mkdir()
            (vault/'old').write_bytes(b'source')
            (vault/'keep').write_bytes(b'source')
            (vault/'meta').write_bytes(b'meta')
            (vault/'archive').write_bytes(b'archive')
            digest = storage.sha(b'source')
            cards = {'assets': [{'id': 'p.item', 'source_file': 'old', 'source_sha256': digest,
                                 'source_meta_file': 'meta', 'source_meta_sha256': storage.sha(b'meta'),
                                 'source_prefab_sha256': storage.sha(b'optional'),
                                 'source_archive': 'archive', 'source_archive_bytes': 7}]}
            source = json.dumps(cards).encode()
            (vault/'metadata/catalog.json').write_bytes(source)
            pointer = json.dumps({'path': 'metadata/catalog.json'}).encode()
            (vault/'metadata/asset-source-support-registry.v1.json').write_bytes(pointer)
            (vault/'metadata/asset-import-registry.v1.json').write_bytes(b'{}')
            item = {'old': 'old', 'retained': 'keep', 'sha256': digest, 'references': 1}
            result, _, ids = replace(cards, {'old': item})
            staged = json.dumps(result).encode()
            (stage/'source-support.v1.json').write_bytes(staged)
            plan = {'schema': 'openrpg.source-support-relinks-stage.v1', 'state': 'prepared_only',
                    'source_registry_sha256': storage.sha(pointer), 'asset_registry_sha256': storage.sha(b'{}'),
                    'prior_catalog_sha256': storage.sha(source), 'staged_catalog_sha256': storage.sha(staged),
                    'changed_ids': ids, 'paths': [item]}
            (stage/'plan.json').write_text(json.dumps(plan), encoding='utf8')
            result = validate(vault, stage)
            self.assertEqual(result['changed_cards'], 1)
            self.assertEqual(result['optional_prefab_hashes_without_paths'], 1)
            (vault/'keep').write_bytes(b'altered')
            with self.assertRaisesRegex(ValueError, 'payload SHA differs'):
                validate(vault, stage)


if __name__ == '__main__':
    unittest.main()
