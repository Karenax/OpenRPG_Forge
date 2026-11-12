import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import tarfile
import tempfile
import unittest


class InspectPackageTests(unittest.TestCase):
    def test_verified_found_missing_and_wrong_hash(self):
        with tempfile.TemporaryDirectory() as work:
            root=Path(work); package=root/'test.unitypackage'; guid='a'*32
            with tarfile.open(package,'w:gz') as tar:
                data=b'Assets/Test/example.prefab';info=tarfile.TarInfo(guid+'/pathname');info.size=len(data)
                tar.addfile(info,io.BytesIO(data))
            digest=hashlib.sha256(package.read_bytes()).hexdigest()
            script=Path(__file__).with_name('inspect_unitypackage_guid.py')
            def run(g,sha,name):
                return subprocess.run([sys.executable,'-B',str(script),'--archive',str(package),'--sha256',sha,
                                       '--guid',g,'--output',str(root/name)],capture_output=True)
            self.assertEqual(run(guid,digest,'found.json').returncode,0)
            found=json.loads((root/'found.json').read_bytes())
            self.assertEqual(found['status'],'found')
            self.assertEqual(found['members'][0]['text'],'Assets/Test/example.prefab')
            self.assertEqual(run('b'*32,digest,'missing.json').returncode,0)
            self.assertEqual(json.loads((root/'missing.json').read_bytes())['status'],'not_found_in_verified_archive')
            self.assertNotEqual(run(guid,'0'*64,'invalid.json').returncode,0)
            self.assertFalse((root/'invalid.json').exists())


if __name__=='__main__':unittest.main()
