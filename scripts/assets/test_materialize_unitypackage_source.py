import hashlib
import io
import json
import tarfile
import tempfile
import unittest
from pathlib import Path
from materialize_unitypackage_source import materialize


class MaterializeUnityPackageTests(unittest.TestCase):
    def test_pinned_asset_and_folder_then_refuse_tampered_archive(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp).resolve();guid='a'*32;folder='b'*32;archive=root/'pack.unitypackage'
            entries={f'{guid}/asset':b'data',f'{guid}/asset.meta':b'guid: '+guid.encode(),
                     f'{guid}/pathname':b'Assets/X/a.txt\n00',
                     f'{folder}/asset.meta':b'guid: '+folder.encode(),f'{folder}/pathname':b'Assets/X\n00'}
            with tarfile.open(archive,'w:gz') as tar:
                for name,data in entries.items():
                    info=tarfile.TarInfo(name);info.size=len(data);tar.addfile(info,io.BytesIO(data))
            sha=lambda b:hashlib.sha256(b).hexdigest()
            rows=[]
            for g,source,kind in [(guid,'Assets/X/a.txt','asset'),(guid,'Assets/X/a.txt','asset.meta'),(folder,'Assets/X','asset.meta')]:
                data=entries[f'{g}/{kind}'];rows.append({'guid':g,'source_path':source,'kind':kind,'bytes':len(data),'sha256':sha(data)})
            doc={'archive_sha256':sha(archive.read_bytes()),'guid_paths':{guid:'Assets/X/a.txt',folder:'Assets/X'},'files':rows}
            manifest=root/'manifest.json';manifest.write_text(json.dumps(doc),encoding='utf8')
            result=materialize(archive,manifest,root/'source',root)
            self.assertEqual(result['files'],3);self.assertEqual((root/'source/Assets/X/a.txt').read_bytes(),b'data')
            self.assertTrue((root/'source/Assets/X.meta').exists())
            doc['archive_sha256']='0'*64;manifest.write_text(json.dumps(doc),encoding='utf8')
            with self.assertRaisesRegex(ValueError,'archive SHA differs'):
                materialize(archive,manifest,root/'bad',root)
            self.assertFalse((root/'bad').exists())


if __name__=='__main__':unittest.main()
