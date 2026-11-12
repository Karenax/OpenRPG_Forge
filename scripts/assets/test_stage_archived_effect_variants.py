import unittest
from stage_archived_effect_variants import consolidate, consolidate_existing


class ArchivedEffectTests(unittest.TestCase):
    def test_existing_alias_keeps_identity_and_archives_previous_source(self):
        old = {'id':'hdrp','source_guid':'a'*32,'source_sha256':'1'*64,'source_meta_sha256':'2'*64,
               'source_file':'old.prefab','source_meta_file':'old.prefab.meta','source_asset_path':'Assets/HDRP/old.prefab',
               'source_archive':'pack.unitypackage','source_archive_sha256':'3'*64,'source_direct_archive_is_nested':None}
        owner = {**old,'id':'urp','source_sha256':'4'*64,'source_file':'new.prefab',
                 'source_meta_file':'new.prefab.meta','source_asset_path':'Assets/URP/new.prefab'}
        owner['consolidated_sources']=[{'retired_id':'hdrp','original_detail':old,
                                        'source_variation':{'kind':'lod_thresholds_only'}}]
        proof={**old,'guid':'a'*32,'archive':'pack.unitypackage','archive_sha256':'3'*64}
        result=consolidate_existing({'assets':[owner]},[{'hdrp_id':'hdrp','urp_id':'urp'}],{'hdrp':proof},'5'*64)
        alias=result['assets'][0]['consolidated_sources'][0]
        self.assertEqual(alias['retired_id'],'hdrp')
        self.assertEqual(alias['original_detail']['source_file'],'new.prefab')
        self.assertEqual(alias['archived_original']['source_sha256'],'1'*64)
        self.assertEqual(owner['consolidated_sources'][0]['original_detail']['source_file'],'old.prefab')

    def test_selects_one_source_and_keeps_archive_provenance(self):
        old = {'id':'hdrp', 'source_guid':'a'*32, 'source_sha256':'1'*64,
               'source_meta_sha256':'2'*64, 'source_file':'original/old.vfx',
               'source_meta_file':'original/old.vfx.meta', 'source_asset_path':'Assets/HDRP/old.vfx',
               'source_archive':'original/pack.unitypackage', 'source_archive_sha256':'3'*64,
               'source_direct_archive_is_nested':False, 'descriptions':{'fr':'ancien'}}
        keep = {**old, 'id':'urp', 'source_sha256':'4'*64, 'source_file':'original/new.vfx',
                'source_meta_file':'original/new.vfx.meta', 'source_asset_path':'Assets/URP/new.vfx',
                'source_direct_archive_is_nested':True}
        proof = {**old, 'guid':old['source_guid'], 'archive':old['source_archive'],
                 'archive_sha256':old['source_archive_sha256']}
        pairs=[{'hdrp_id':'hdrp','urp_id':'urp'}]
        prior={'schema_version':1,'assets':[old,keep]}
        result=consolidate(prior,pairs,{'hdrp':proof},'5'*64)
        self.assertEqual(len(result['assets']),1)
        alias=result['assets'][0]['consolidated_sources'][0]
        self.assertEqual(alias['original_detail']['source_file'],keep['source_file'])
        self.assertEqual(alias['archived_original']['source_sha256'],old['source_sha256'])
        self.assertEqual(alias['archived_original']['prior_catalog_sha256'],'5'*64)
        self.assertEqual(alias['original_detail']['descriptions'],old['descriptions'])
        self.assertEqual(prior['assets'][0]['source_file'],'original/old.vfx')

    def test_rejects_archive_pin_and_guid_mismatch(self):
        old={'id':'a','source_guid':'a'*32,'source_file':'old.vfx','source_sha256':'1'*64,
             'source_meta_file':'old.meta','source_meta_sha256':'2'*64,'source_asset_path':'Assets/HDRP/old',
             'source_archive':'archive','source_archive_sha256':'3'*64,'source_direct_archive_is_nested':False}
        keep={**old,'id':'b','source_guid':'b'*32,'source_asset_path':'Assets/URP/new'}
        proof={**old,'guid':'a'*32,'archive':'archive','archive_sha256':'3'*64}
        with self.assertRaisesRegex(ValueError,'identity'):
            consolidate({'assets':[old,keep]},[{'hdrp_id':'a','urp_id':'b'}],{'a':proof},'5'*64)


if __name__ == '__main__':
    unittest.main()
