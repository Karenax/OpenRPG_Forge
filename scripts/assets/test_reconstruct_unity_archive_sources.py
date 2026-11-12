import hashlib
import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest

from reconstruct_unity_archive_sources import reconstruct


def sha(data):
    return hashlib.sha256(data).hexdigest()


class ReconstructionTests(unittest.TestCase):
    def test_extracts_exact_bytes_and_rejects_tampered_archive(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();vault=root/'vault';vault.mkdir();work=root/'work';work.mkdir()
            data=b'unity prefab';guid='a'*32;archive=vault/'source.unitypackage'
            with tarfile.open(archive,'w:gz') as tar:
                for name,body in [('asset',data),('pathname',b'Assets/HDRP/test.prefab\n00')]:
                    info=tarfile.TarInfo(guid+'/'+name);info.size=len(body);tar.addfile(info,io.BytesIO(body))
            row={'old':'originals/test.prefab','archive':archive.name,'archive_sha256':sha(archive.read_bytes()),
                 'guid':guid,'archive_member':'asset','sha256':sha(data),'bytes':len(data),
                 'source_asset_path':'Assets/HDRP/test.prefab'}
            manifest=root/'recovery.json';manifest.write_text(json.dumps({'rows':[row]}))
            result=reconstruct(vault,manifest,work/'first',work)
            self.assertEqual(result['files'],1)
            self.assertEqual((work/'first/originals/test.prefab').read_bytes(),data)
            archive.write_bytes(b'tampered')
            with self.assertRaisesRegex(ValueError,'archive SHA'):
                reconstruct(vault,manifest,work/'second',work)

    def test_rejects_escaping_paths(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();vault=root/'vault';vault.mkdir();work=root/'work';work.mkdir()
            manifest=root/'recovery.json';manifest.write_text(json.dumps({'rows':[{'old':'../escape','archive':'x'}]}))
            with self.assertRaises(ValueError):reconstruct(vault,manifest,work/'test',work)


if __name__=='__main__':
    unittest.main()
