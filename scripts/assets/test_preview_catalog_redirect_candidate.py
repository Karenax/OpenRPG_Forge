import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0,r'E:\CodexData\skills\openrpg-import-assets\scripts')
source = Path(r'E:\OpenRPG_Forge\.build\asset-conversion\asset-import-preview-redirect-candidate-20261112.py')
spec = importlib.util.spec_from_file_location('preview_redirect_candidate',source)
api = importlib.util.module_from_spec(spec)
spec.loader.exec_module(api)


class PreviewCatalogRedirectTests(unittest.TestCase):
    def test_resolves_pinned_derived_catalog_and_rejects_changed_bytes(self):
        with tempfile.TemporaryDirectory() as temp:
            vault=Path(temp).resolve()
            old=vault/'original.json'
            old.write_bytes(b'old')
            new=vault/('openrpg-preview-'+api.hashlib.sha256(b'new').hexdigest()+'.json')
            new.write_bytes(b'new')
            details=vault/'metadata/revision/details.v1.json'
            api.atomic(details,{'assets':[{'id':'p.a','source_catalog':str(new),
                 'source_catalog_preview_origin':{'path':str(old),'sha256':api.digest(old)}}]})
            api.atomic(api.index_path(vault),{'schema_version':1,'packs':{'p':{'path':'metadata/revision',
                 'files':{'details.v1.json':api.digest(details)}}}})
            pin=api.file_hash(api.index_path(vault))
            self.assertEqual(api.redirected_preview_catalog(vault,old,pin),new)
            new.write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError,'Derived preview catalog changed'):
                api.redirected_preview_catalog(vault,old,pin)

    def test_unrelated_catalog_is_unchanged(self):
        with tempfile.TemporaryDirectory() as temp:
            vault=Path(temp).resolve()
            old=vault/'original.json'
            old.write_bytes(b'old')
            self.assertEqual(api.redirected_preview_catalog(vault,old,api.EMPTY_HASH),old)


if __name__=='__main__':
    unittest.main()
