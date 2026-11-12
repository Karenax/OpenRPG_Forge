import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0,r'E:\CodexData\skills\openrpg-import-assets\scripts')
candidate=Path(r'E:\OpenRPG_Forge\.build\asset-conversion\asset-import-stale-preview-guard-20261112.py')
spec=importlib.util.spec_from_file_location('stale_preview_candidate',candidate)
api=importlib.util.module_from_spec(spec)
spec.loader.exec_module(api)


class StalePreviewGuardTests(unittest.TestCase):
    def test_stale_derived_catalog_is_refused_but_origin_and_active_resolve(self):
        with tempfile.TemporaryDirectory() as temp:
            vault=Path(temp).resolve()
            old=vault/'original.json';old.write_bytes(b'original')
            stale=vault/('openrpg-preview-'+api.hashlib.sha256(b'stale').hexdigest()+'.json');stale.write_bytes(b'stale')
            active=vault/('openrpg-preview-'+api.hashlib.sha256(b'active').hexdigest()+'.json');active.write_bytes(b'active')
            details=vault/'metadata/revision/details.v1.json'
            api.atomic(details,{'assets':[{'id':'p.a','source_catalog':str(active),
                'source_catalog_preview_origin':{'path':str(old),'sha256':api.digest(old)}}]})
            api.atomic(api.index_path(vault),{'schema_version':1,'packs':{'p':{'path':'metadata/revision',
                'files':{'details.v1.json':api.digest(details)}}}})
            pin=api.file_hash(api.index_path(vault))
            self.assertEqual(api.redirected_preview_catalog(vault,old,pin),active)
            self.assertEqual(api.redirected_preview_catalog(vault,active,pin),active)
            with self.assertRaisesRegex(ValueError,'Stale derived preview catalog'):
                api.redirected_preview_catalog(vault,stale,pin)


if __name__=='__main__':
    unittest.main()
