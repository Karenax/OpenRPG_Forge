import hashlib
import struct
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import vault_storage
from variant_legacy_recipe import (MAX_CONSOLIDATION_MODEL_BYTES, build_descriptor_recipe, build_inline_recipe,
                                   build_recipe, split_glb, verify_recipe)


def glb(metadata, data=b'data'):
    metadata += b' ' * ((-len(metadata)) % 4)
    assert len(data) % 4 == 0
    size = 12 + 8 + len(metadata) + 8 + len(data)
    return (b'glTF' + struct.pack('<II', 2, size) + struct.pack('<II', len(metadata), 0x4e4f534a)
            + metadata + struct.pack('<II', len(data), 0x004e4942) + data)


class LegacyRecipeTests(unittest.TestCase):
    def test_inline_recipe_preserves_exact_file_and_rejects_changes(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            kept, old = glb(b'{"id":"kept"}'), glb(b'{"id":"old"}', b'aaaa')
            old_sha, kept_sha = hashlib.sha256(old).hexdigest(), hashlib.sha256(kept).hexdigest()
            path = root / 'old.glb'
            path.write_bytes(old)
            recipe = build_inline_recipe(old, old_sha)
            verify_recipe(kept, recipe, kept_sha, old_sha, vault=root, retired_path=path)
            path.write_bytes(glb(b'{"id":"changed"}', b'aaaa'))
            with self.assertRaisesRegex(ValueError, 'source SHA'):
                verify_recipe(kept, recipe, kept_sha, old_sha, vault=root, retired_path=path)
            with self.assertRaises(ValueError):
                build_inline_recipe(old + b'0' * (128 * 1024), old_sha)

    def test_large_descriptor_is_validated_with_one_gib_limit(self):
        descriptor = {'sha256': 'a' * 64, 'bytes': 700 * 1024 * 1024}
        with patch('vault_storage.read_descriptor', return_value=descriptor) as read, \
             patch('vault_storage.digest', return_value='a' * 64) as digest:
            result = build_descriptor_recipe(Path('vault'), Path('model'), 'a' * 64)
        self.assertEqual(result['logical_bytes'], descriptor['bytes'])
        self.assertEqual(MAX_CONSOLIDATION_MODEL_BYTES, 1024 * 1024 * 1024)
        self.assertEqual(read.call_args.args[2], MAX_CONSOLIDATION_MODEL_BYTES)
        self.assertEqual(digest.call_args.args[2], MAX_CONSOLIDATION_MODEL_BYTES)

    def test_descriptor_recipe_requires_verified_legacy_storage(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            kept = glb(b'{"id":"keep"}', b'aaaa')
            old = glb(b'{"id":"old"}', b'bbbb')
            path = root / 'old.glb'
            path.write_bytes(old)
            writer = vault_storage.Packer(root)
            try:
                item = writer.prepare(path)
                writer.close_pack()
                writer.publish(item)
            finally:
                writer.close()
            old_sha = hashlib.sha256(old).hexdigest()
            kept_sha = hashlib.sha256(kept).hexdigest()
            recipe = build_descriptor_recipe(root, path, old_sha)
            verify_recipe(kept, recipe, kept_sha, old_sha, vault=root, retired_path=path)
            altered = dict(recipe, logical_sha256='0' * 64)
            with self.assertRaises(ValueError):
                verify_recipe(kept, altered, kept_sha, old_sha, vault=root, retired_path=path)
            with self.assertRaises(ValueError):
                verify_recipe(kept, recipe, kept_sha, old_sha)

    def test_exact_reconstruction_and_corruption_refusal(self):
        kept, old = glb(b'{"id":"keep"}'), glb(b'{"id":"retired"}')
        digest = lambda raw: hashlib.sha256(raw).hexdigest()
        recipe = build_recipe(kept, old, digest(kept), digest(old))
        rebuilt = bytes.fromhex(recipe['prefix_hex']) + split_glb(kept)[1].tobytes()
        self.assertEqual(rebuilt, old)
        self.assertEqual(recipe['logical_sha256'], digest(rebuilt))
        verify_recipe(kept, recipe, digest(kept), digest(old))
        with self.assertRaises(ValueError):
            verify_recipe(kept, {**recipe, 'prefix_hex': '00' + recipe['prefix_hex'][2:]}, digest(kept), digest(old))
        changed = glb(b'{"id":"retired"}', b'bad!')
        with self.assertRaisesRegex(ValueError, 'payloads differ'):
            build_recipe(kept, changed, digest(kept), digest(changed))
        with self.assertRaisesRegex(ValueError, 'SHA differs'):
            build_recipe(kept, old, digest(kept), '0' * 64)
        with self.assertRaises(ValueError):
            split_glb(old[:-1])


if __name__ == '__main__':
    unittest.main()
