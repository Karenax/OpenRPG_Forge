import hashlib
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

import verified_payload_store as payloads


class PayloadRetentionTests(unittest.TestCase):
    def setUp(self):
        self.work=tempfile.TemporaryDirectory();self.root=Path(self.work.name);self.data=b'embedded shader and texture data'*100
        self.sha=hashlib.sha256(self.data).hexdigest()
    def tearDown(self):payloads.close_all_stores();self.work.cleanup()
    def test_one_copy_during_use_no_copy_after_use(self):
        cache=self.root/'cache'
        with payloads.VerifiedPayloadStore(cache,self.root) as store:
            first=store.materialize(self.data,self.sha)
            self.assertEqual(first,store.materialize(self.data,self.sha))
            self.assertEqual(store.stats['new_files'],1)
            self.assertEqual(first.read_bytes(),self.data)
        self.assertFalse(cache.exists())
    def test_failed_consumer_also_releases_regenerable_copies(self):
        cache=self.root/'cache'
        with self.assertRaises(RuntimeError):
            with payloads.VerifiedPayloadStore(cache,self.root) as store:
                store.materialize(self.data,self.sha);raise RuntimeError('render failed')
        self.assertFalse(cache.exists())
    def test_changed_and_unknown_files_are_not_purged(self):
        cache=self.root/'cache';store=payloads.VerifiedPayloadStore(cache,self.root)
        p=store.materialize(self.data,self.sha);p.write_bytes(b'changed unique data')
        unknown=cache/'unknown.txt';unknown.write_text('keep')
        store.close();self.assertTrue(p.exists());self.assertTrue(unknown.exists());self.assertTrue(store.cleanup_notes)
    def test_path_escape_existing_root_and_wrong_digest_rejected(self):
        with self.assertRaises(ValueError):payloads.VerifiedPayloadStore(self.root.parent/'outside',self.root)
        cache=self.root/'cache';cache.mkdir()
        with self.assertRaises(FileExistsError):payloads.VerifiedPayloadStore(cache,self.root)
        cache.rmdir()
        with payloads.VerifiedPayloadStore(cache,self.root) as store:
            with self.assertRaises(ValueError):store.materialize(self.data,'0'*64)
        self.assertFalse(cache.exists())
    def test_legacy_worker_without_context_manager_cleans_on_process_exit(self):
        cache=self.root/'cache'
        code='import sys,hashlib;sys.path.insert(0,'+repr(str(Path(payloads.__file__).parent))+');from verified_payload_store import VerifiedPayloadStore;s=VerifiedPayloadStore('+repr(str(cache))+','+repr(str(self.root))+');s.materialize(b"payload",hashlib.sha256(b"payload").hexdigest())'
        subprocess.run([sys.executable,'-B','-c',code],check=True)
        self.assertFalse(cache.exists())


if __name__=='__main__':unittest.main()
