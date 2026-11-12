import hashlib
import io
import unittest

from audit_unity_block_variants import blocks


class BlockAuditTests(unittest.TestCase):
    def test_tracks_original_sha_and_normalizes_only_line_endings(self):
        raw = b'%YAML 1.1\r\n--- !u!1 &123\r\nGameObject:\r\n  name: test\r\n'
        sha, actual = blocks(io.BytesIO(raw))
        _, normalized = blocks(io.BytesIO(raw.replace(b'\r\n', b'\n')))
        self.assertEqual(sha, hashlib.sha256(raw).hexdigest())
        self.assertEqual(actual, normalized)
        _, altered = blocks(io.BytesIO(raw.replace(b'name: test', b'name: changed')))
        self.assertNotEqual(actual, altered)

    def test_rejects_duplicate_id_and_binary(self):
        with self.assertRaisesRegex(ValueError, 'duplicate'):
            blocks(io.BytesIO(b'--- !u!1 &5\na: b\n--- !u!1 &5\na: c\n'))
        with self.assertRaisesRegex(ValueError, 'text comparison'):
            blocks(io.BytesIO(b'--- !u!1 &5\n\x00\n'))


if __name__ == '__main__':
    unittest.main()
