import contextlib
import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
import retire_duplicate_inbox_archives as target


class DryRunTests(unittest.TestCase):
    def test_dry_run_never_marks_deletion(self):
        with tempfile.TemporaryDirectory() as temp:
            vault=Path(temp).resolve();old=vault/'inbox/manual/a.zip';keep=vault/'originals/pack/a.zip'
            old.parent.mkdir(parents=True);keep.parent.mkdir(parents=True);old.write_bytes(b'archive');keep.write_bytes(b'archive')
            (vault/'metadata').mkdir();(vault/'metadata/asset-source-support-registry.v1.json').write_bytes(b'source')
            (vault/'metadata/asset-import-registry.v1.json').write_bytes(b'model')
            sha=hashlib.sha256(b'archive').hexdigest();inventory=vault/'inventory.json'
            inventory.write_text(json.dumps({'groups':[{'same_sha':True,'files':[
                {'path':str(old),'sha256':sha,'bytes':7},{'path':str(keep),'sha256':sha,'bytes':7}]}]}))
            class FakeExclusive:
                def __init__(self,path):self.path=path
                def __enter__(self):return self
                def __exit__(self,*args):pass
                def sha256(self):return hashlib.sha256(self.path.read_bytes()).hexdigest()
                def mark_delete(self):raise AssertionError('dry run attempted deletion')
            @contextlib.contextmanager
            def source(path,share_delete=False):
                with path.open('rb') as f:yield f
            importer=SimpleNamespace(shared_import_lock=lambda _:contextlib.nullcontext())
            with patch.object(target,'ExclusiveSource',FakeExclusive),patch.object(target.storage,'locked_source',source),\
                 patch.object(target,'exclusive_storage_lock',lambda _:contextlib.nullcontext()),\
                 patch.object(target,'active_references',lambda *_:(b'source',b'model')),\
                 patch.object(target.subprocess,'run',return_value=SimpleNamespace(returncode=1,stdout='')):
                result=target.run(vault,inventory,importer,vault/'journal',False)
            self.assertEqual(result['state'],'dry_run_passed');self.assertTrue(old.exists());self.assertTrue(keep.exists())


if __name__=='__main__':unittest.main()
