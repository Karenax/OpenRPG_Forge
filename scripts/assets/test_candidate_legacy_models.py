import json
import tempfile
import unittest
import zlib
from pathlib import Path

import vault_storage as storage
from candidate_legacy_models import chunks, load_recipes


class CandidateLegacyTests(unittest.TestCase):
    def fixture(self, root):
        vault = root / 'vault'
        vault.mkdir()
        payload = b'legacy-model-bytes' * 100
        old = vault / 'old.glb'
        old.write_bytes(payload)
        writer = storage.Packer(vault)
        try:
            item = writer.prepare(old)
            writer.close_pack()
            writer.publish(item)
        finally:
            writer.close()
        descriptor = storage.read_descriptor(vault, old)
        pin = storage.sha(payload)
        old.unlink()
        recipe = {'schema': 'openrpg.legacy-vault-descriptor.v1', 'logical_sha256': pin,
                  'logical_bytes': len(payload), 'descriptor': descriptor}
        alias = {'retired_id': 'old-id', 'retired_model_sha256': pin,
                 'original_detail': {'id': 'old-id', 'model_sha256': pin},
                 'original_profile': {'external_id': 'old-id'}, 'legacy_recipe': recipe}
        path = vault / 'metadata/packs/test'
        path.mkdir(parents=True)
        detail = json.dumps({'assets': [{'id': 'new-id', 'consolidated_variants': [alias]}]}).encode()
        (path / 'details.v1.json').write_bytes(detail)
        (vault / 'metadata/asset-import-registry.v1.json').write_text(json.dumps(
            {'packs': {'test': {'path': 'metadata/packs/test', 'files': {'details.v1.json': storage.sha(detail)}}}}))
        return vault, pin, payload, recipe

    def test_recovers_pinned_historical_model_without_old_file(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, pin, payload, _ = self.fixture(Path(temp).resolve())
            recipes, registry_sha = load_recipes(vault, {'old-id': pin})
            self.assertEqual(b''.join(chunks(vault, recipes['old-id'], pin)), payload)
            self.assertTrue(storage.valid_hex(registry_sha, 64))
            self.assertFalse((vault / 'old.glb').exists())

    def test_refuses_wrong_identity_and_corrupted_pack(self):
        with tempfile.TemporaryDirectory() as temp:
            vault, pin, _, recipe = self.fixture(Path(temp).resolve())
            with self.assertRaisesRegex(ValueError, 'identity or recipe'):
                load_recipes(vault, {'old-id': '0' * 64})
            pack = vault / '.forge-storage/packs' / (recipe['descriptor']['segments'][0]['pack'] + '.pack')
            data = bytearray(pack.read_bytes())
            data[recipe['descriptor']['segments'][0]['offset']] ^= 1
            pack.write_bytes(data)
            with self.assertRaises((ValueError, zlib.error)):
                b''.join(chunks(vault, recipe, pin))


if __name__ == '__main__':
    unittest.main()
