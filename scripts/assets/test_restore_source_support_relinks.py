import tempfile
import unittest
from pathlib import Path

import vault_storage as storage
from restore_source_support_relinks import restore_one


class RestoreTests(unittest.TestCase):
    def test_restore_verify_and_never_overwrite(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            (root/'old').mkdir()
            (root/'keep').write_bytes(b'source')
            item = {'old': 'old/a', 'retained': 'keep', 'sha256': storage.sha(b'source')}
            self.assertEqual(restore_one(root, item), 'restored')
            self.assertEqual(restore_one(root, item), 'already_present')
            (root/'old/a').write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'existing recovery target differs'):
                restore_one(root, item)
            self.assertEqual((root/'old/a').read_bytes(), b'changed')

    def test_refuses_changed_retained_source(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            (root/'keep').write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'retained source SHA differs'):
                restore_one(root, {'old': 'missing', 'retained': 'keep', 'sha256': storage.sha(b'original')})
            self.assertFalse((root/'missing').exists())


if __name__ == '__main__':
    unittest.main()
