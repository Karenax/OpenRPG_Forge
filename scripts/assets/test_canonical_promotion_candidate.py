import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, r'E:\CodexData\skills\openrpg-import-assets\scripts')
from test_asset_import import glb

path = Path(r'E:\OpenRPG_Forge\.build\asset-conversion\asset-import-promotion-candidate-v66.py')
spec = importlib.util.spec_from_file_location('promotion_candidate_v66', path)
api = importlib.util.module_from_spec(spec)
spec.loader.exec_module(api)


class PromotionTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.v = Path(self.tmp.name).resolve()
        self.keep = self.v/'keep.glb'
        self.old = self.v/'old.glb'
        glb(self.keep)
        glb(self.old)
        self.catalog = {'id':'test.pack', 'license':'LicenseRef-Test', 'assets':[
            {'id':'test.keep','model':'keep.glb','sha256':api.digest(self.keep)},
            {'id':'test.old','model':'old.glb','sha256':api.digest(self.old)}]}
        self.annotations={'schema_version':1,'pack_id':'test.pack','assets':{}}
        api.atomic(self.v/'catalog.json',self.catalog)
        api.atomic(self.v/'annotations.json',self.annotations)
        api.prepare(self.v,self.v/'catalog.json',self.v/'annotations.json',self.v/'initial')
        api.apply(self.v,self.v/'initial')
        reg=api.registry(self.v);entry=reg['packs']['test.pack'];root=self.v/entry['path']
        details=api.read(root/'details.v1.json');profiles=api.read(root/'profiles.v1.json')
        old=next(x for x in details['assets'] if x['id']=='test.old')
        keep=next(x for x in details['assets'] if x['id']=='test.keep')
        keep['consolidated_variants']=[{'retired_id':'test.old','retired_model_sha256':old['model_sha256']}]
        details['assets']=[keep];profiles['profiles']=[x for x in profiles['profiles'] if x['external_id']=='test.keep']
        api.atomic(root/'details.v1.json',details);api.atomic(root/'profiles.v1.json',profiles)
        entry['files']={name:api.digest(root/name) for name in ('details.v1.json','profiles.v1.json')}
        api.atomic(api.index_path(self.v),reg)
        self.old.unlink()

    def test_filter_allows_missing_retired_file_and_preserves_aliases(self):
        contract={'asset_ids':['test.keep','test.old'],'runtime_qualified':False}
        api.atomic(self.v/'geometry.json',contract)
        self.catalog['source_geometry_contract']={'path':'geometry.json','sha256':api.digest(self.v/'geometry.json')}
        api.atomic(self.v/'catalog.json',self.catalog)
        api.prepare(self.v,self.v/'catalog.json',self.v/'annotations.json',self.v/'stage',merge_existing=True)
        plan=api.read(self.v/'stage/plan.json');detail=api.read(self.v/'stage/details.v1.json')
        self.assertEqual(plan['superseded_asset_ids'],['test.old'])
        self.assertEqual([x['id'] for x in detail['assets']],['test.keep'])
        self.assertEqual(detail['assets'][0]['consolidated_variants'][0]['retired_id'],'test.old')
        self.assertEqual(api.apply(self.v,self.v/'stage')['status'],'applied')

    def test_consolidated_owner_changed_requires_explicit_migration(self):
        glb(self.keep,lambda d:d['nodes'][0].update(translation=[1,0,0]))
        self.catalog['assets'][0]['sha256']=api.digest(self.keep)
        api.atomic(self.v/'catalog.json',self.catalog)
        with self.assertRaisesRegex(ValueError,'owner model changed'):
            api.prepare(self.v,self.v/'catalog.json',self.v/'annotations.json',self.v/'stage',merge_existing=True)

    def test_all_superseded_does_not_create_stage(self):
        self.catalog['assets']=[self.catalog['assets'][1]]
        api.atomic(self.v/'catalog.json',self.catalog)
        with self.assertRaisesRegex(ValueError,'already consolidated'):
            api.prepare(self.v,self.v/'catalog.json',self.v/'annotations.json',self.v/'stage',merge_existing=True)
        self.assertFalse((self.v/'stage').exists())


if __name__=='__main__': unittest.main()
