import json
import tempfile
import unittest
from pathlib import Path

import vault_storage as storage
from inventory_model_preview_roots import inventory


class PreviewInventoryTests(unittest.TestCase):
    def test_finds_duplicate_bytes_and_rejects_wrong_pin(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();(root/'metadata/rev').mkdir(parents=True)
            pack=root/'pack';(pack/'images').mkdir(parents=True)
            (pack/'images/a.png').write_bytes(b'image');(pack/'images/b.png').write_bytes(b'image')
            cat={'assets':[{'id':'p.a','sha256':'ma','model':'models/a.glb','preview':'images/a.png','preview_sha256':storage.sha(b'image')},
                           {'id':'p.b','sha256':'mb','model':'models/b.glb','preview':'images/b.png','preview_sha256':storage.sha(b'image')}]}
            catalog=pack/'catalog.json';catalog.write_text(json.dumps(cat),encoding='utf8')
            details={'assets':[{'id':'p.a','source_catalog':str(catalog),'model_path':str(pack/'models/a.glb'),'model_sha256':'ma'},
                               {'id':'p.b','source_catalog':str(catalog),'model_path':str(pack/'models/b.glb'),'model_sha256':'mb'}]}
            raw=json.dumps(details).encode();(root/'metadata/rev/details.v1.json').write_bytes(raw)
            reg={'packs':{'p':{'path':'metadata/rev','files':{'details.v1.json':storage.sha(raw)}}}}
            (root/'metadata/asset-import-registry.v1.json').write_text(json.dumps(reg),encoding='utf8')
            result=inventory(root)
            self.assertEqual(len(result['groups']),1)
            self.assertEqual(len(result['groups'][0]['paths']),2)
            (pack/'images/b.png').write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError,'preview SHA changed'):
                inventory(root)

    def test_shared_preview_is_used_when_old_path_is_absent(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();(root/'metadata/rev').mkdir(parents=True)
            pack=root/'pack';pack.mkdir();shared=root/'shared.png';shared.write_bytes(b'image')
            cat={'assets':[{'id':'p.a','sha256':'ma','model':'a.glb','preview':'missing.png',
                            'preview_vault':'shared.png','preview_sha256':storage.sha(b'image')}]}
            catalog=pack/'catalog.json';catalog.write_text(json.dumps(cat),encoding='utf8')
            details={'assets':[{'id':'p.a','source_catalog':str(catalog),'model_path':str(pack/'a.glb'),'model_sha256':'ma'}]}
            raw=json.dumps(details).encode();(root/'metadata/rev/details.v1.json').write_bytes(raw)
            reg={'packs':{'p':{'path':'metadata/rev','files':{'details.v1.json':storage.sha(raw)}}}}
            (root/'metadata/asset-import-registry.v1.json').write_text(json.dumps(reg),encoding='utf8')
            result=inventory(root)
            self.assertEqual(result['counts']['with_preview'],1)
            self.assertEqual(result['groups'],[])


if __name__=='__main__':
    unittest.main()
