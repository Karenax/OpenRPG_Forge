import hashlib
import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest

from audit_variant_archive_reconstruction import audit


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


class ReconstructionTests(unittest.TestCase):
    def test_valid_archive_and_changed_extraction(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            (root/'metadata').mkdir()
            guid = 'a'*32
            asset, meta = b'model source', b'guid: '+guid.encode()
            (root/'model.vfx').write_bytes(asset)
            (root/'model.vfx.meta').write_bytes(meta)
            archive = root/'test.unitypackage'
            with tarfile.open(archive, 'w:gz') as tar:
                for name, data in [('asset', asset),('asset.meta',meta),('pathname',b'Assets/test.vfx\n00')]:
                    member = tarfile.TarInfo(guid+'/'+name)
                    member.size = len(data)
                    tar.addfile(member, io.BytesIO(data))
            card = {'id':'hdrp', 'source_archive':archive.name,
                    'source_archive_sha256':sha(archive.read_bytes()), 'source_direct_archive_is_nested':False,
                    'source_guid':guid, 'source_file':'model.vfx', 'source_sha256':sha(asset),
                    'source_meta_file':'model.vfx.meta','source_meta_sha256':sha(meta),
                    'source_asset_path':'Assets/test.vfx'}
            catalog = json.dumps({'assets':[card]}).encode()
            (root/'metadata/catalog.json').write_bytes(catalog)
            pointer = json.dumps({'path':'metadata/catalog.json','sha256':sha(catalog)}).encode()
            (root/'metadata/asset-source-support-registry.v1.json').write_bytes(pointer)
            report = root/'report.json'
            report.write_text(json.dumps({'source_registry_sha256':sha(pointer),'results':[
                {'state':'compared','blocks_by_class':{'114':{'modified':1}},'card_pairs':[['hdrp','urp']]}]}))
            self.assertEqual(audit(root, report, archive.name)['count'], 1)
            (root/'model.vfx').write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'extracted source changed'):
                audit(root, report, archive.name)


if __name__ == '__main__':
    unittest.main()
