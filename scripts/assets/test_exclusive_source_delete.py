import hashlib
import tempfile
import unittest
from pathlib import Path

from exclusive_source_delete import ExclusiveSource


class ExclusiveDeleteTests(unittest.TestCase):
    def test_locked_file_refuses_other_reads_and_is_deleted_on_close(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp)/'source'
            path.write_bytes(b'hello')
            with ExclusiveSource(path) as locked:
                self.assertEqual(locked.sha256(), hashlib.sha256(b'hello').hexdigest())
                with self.assertRaises(OSError):
                    path.read_bytes()
                locked.mark_delete()
            self.assertFalse(path.exists())

    def test_existing_reader_prevents_exclusive_handle(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp)/'source'
            path.write_bytes(b'hello')
            with path.open('rb'):
                with self.assertRaises(OSError):
                    ExclusiveSource(path)
            self.assertEqual(path.read_bytes(), b'hello')


if __name__ == '__main__':
    unittest.main()
