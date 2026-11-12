import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, r'E:\CodexData\skills\openrpg-import-assets\scripts')
import asset_import as api


class PrefabRedirectTests(unittest.TestCase):
    def test_nested_variant_catalog_redirect(self):
        with tempfile.TemporaryDirectory() as tmp:
            vault=Path(tmp).resolve();old=vault/'original.json';old.write_bytes(b'old')
            derived=vault/('openrpg-prefab-'+api.hashlib.sha256(b'new').hexdigest()+'.json');derived.write_bytes(b'new')
            details=vault/'metadata/revision/details.v1.json'
            api.atomic(details,{'assets':[{'consolidated_variants':[{'original_detail':{
                'source_catalog':str(derived),'source_catalog_prefab_origin':{'path':str(old),'sha256':api.digest(old)}}}]}]})
            api.atomic(api.index_path(vault),{'schema_version':1,'packs':{'p':{'path':'metadata/revision',
                'files':{'details.v1.json':api.digest(details)}}}})
            self.assertEqual(api.redirected_prefab_catalog(vault,old,api.file_hash(api.index_path(vault))),derived)

    def test_redirect_and_tampering_refusal(self):
        with tempfile.TemporaryDirectory() as tmp:
            vault = Path(tmp).resolve()
            old = vault/'original.json'; old.write_bytes(b'old')
            derived = vault/('openrpg-prefab-'+api.hashlib.sha256(b'new').hexdigest()+'.json')
            derived.write_bytes(b'new')
            stale = vault/('openrpg-prefab-'+api.hashlib.sha256(b'stale').hexdigest()+'.json')
            stale.write_bytes(b'stale')
            details = vault/'metadata/revision/details.v1.json'
            api.atomic(details, {'assets':[{'source_catalog':str(derived),
                'source_catalog_prefab_origin':{'path':str(old),'sha256':api.digest(old)}}]})
            api.atomic(api.index_path(vault), {'schema_version':1,'packs':{'p':{'path':'metadata/revision',
                'files':{'details.v1.json':api.digest(details)}}}})
            pin = api.file_hash(api.index_path(vault))
            self.assertEqual(api.redirected_prefab_catalog(vault, old, pin), derived)
            self.assertEqual(api.redirected_prefab_catalog(vault, derived, pin), derived)
            with self.assertRaisesRegex(ValueError,'Stale derived prefab catalog'):
                api.redirected_prefab_catalog(vault, stale, pin)
            derived.write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError,'Derived prefab catalog changed'):
                api.redirected_prefab_catalog(vault, old, pin)


if __name__ == '__main__':
    unittest.main()
