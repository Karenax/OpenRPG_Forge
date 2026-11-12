import json
import tempfile
import unittest
from pathlib import Path

import vault_storage as storage
from prepare_variant_metadata_transition import encoded
from verify_model_preview_stage import verify


class StageVerificationTests(unittest.TestCase):
    def test_accepts_exact_relink_and_rejects_unrelated_metadata_change(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp).resolve()
            vault, stage = base/'vault', base/'stage'
            (vault/'metadata/rev').mkdir(parents=True)
            (vault/'pack').mkdir()
            (stage/'p').mkdir(parents=True)
            catalog = vault/'pack/catalog.json'
            original_catalog = {'assets':[{'id':'p.a','preview':'images/b.png','preview_sha256':'a'*64}]}
            catalog.write_bytes(encoded(original_catalog))
            derived = {'assets':[{'id':'p.a','preview':'images/a.png','preview_sha256':'a'*64}]}
            derived_raw = encoded(derived)
            new_catalog = catalog.parent/('openrpg-preview-'+storage.sha(derived_raw)+'.json')
            (stage/'catalog-0001.json').write_bytes(derived_raw)
            old_details = {'assets':[{'id':'p.a','source_catalog':str(catalog),'license':'license'}]}
            profiles = {'profiles':[]}
            details_raw, profiles_raw = encoded(old_details), encoded(profiles)
            (vault/'metadata/rev/details.v1.json').write_bytes(details_raw)
            (vault/'metadata/rev/profiles.v1.json').write_bytes(profiles_raw)
            registry = {'packs':{'p':{'path':'metadata/rev','files':{'details.v1.json':storage.sha(details_raw),
                                                                      'profiles.v1.json':storage.sha(profiles_raw)}}}}
            reg_raw = encoded(registry)
            (vault/'metadata/asset-import-registry.v1.json').write_bytes(reg_raw)
            (vault/'metadata/asset-source-support-registry.v1.json').write_bytes(b'{}')
            new_details = {'assets':[{'id':'p.a','source_catalog':str(new_catalog),'license':'license',
                           'source_catalog_preview_origin':{'path':str(catalog),'sha256':storage.sha(catalog.read_bytes())}}]}
            def save_stage(detail):
                dr, pr = encoded(detail), encoded(profiles)
                (stage/'p/details.v1.json').write_bytes(dr)
                (stage/'p/profiles.v1.json').write_bytes(pr)
                plan = {'schema':'openrpg.model-preview-relink-stage.v1','state':'prepared_only',
                        'registry_sha256':storage.sha(reg_raw),'source_registry_sha256':storage.sha(b'{}'),
                        'changed_ids':['p.a'], 'candidate_paths':[{'ids':['p.a'],'sha256':'a'*64,
                          'old':str(vault/'pack/images/b.png'),'retained':str(vault/'pack/images/a.png')}],
                        'catalogs':[{'old':str(catalog),'old_sha256':storage.sha(catalog.read_bytes()),
                           'new':str(new_catalog),'new_sha256':storage.sha(derived_raw),'stage_file':'catalog-0001.json'}],
                        'packs':{'p':{'details.v1.json':storage.sha(dr),'profiles.v1.json':storage.sha(pr),
                           'revision':storage.sha(encoded({'profiles':profiles,'details':detail}))}}}
                (stage/'plan.json').write_bytes(encoded(plan))
            save_stage(new_details)
            self.assertEqual(verify(vault, stage)['models'],1)
            new_details['assets'][0]['license'] = 'changed'
            save_stage(new_details)
            with self.assertRaisesRegex(ValueError, 'additional data'):
                verify(vault, stage)


if __name__ == '__main__':
    unittest.main()
