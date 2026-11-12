import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0,r'E:\CodexData\skills\openrpg-import-assets\scripts')
source=Path(r'E:\OpenRPG_Forge\.build\asset-conversion\asset-import-rollback-candidate-v76.py')
spec=importlib.util.spec_from_file_location('rollback_candidate_v76',source)
api=importlib.util.module_from_spec(spec);spec.loader.exec_module(api)


class RollbackGuardTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup)
        self.v=Path(self.tmp.name).resolve();(self.v/'metadata/asset-import-history').mkdir(parents=True)
        self.current={'id':'keep','model_path':str(self.v/'keep.glb'),
                      'consolidated_variants':[{'retired_id':'old','retired_model_sha256':'a'*64}]}
        (self.v/'keep.glb').write_bytes(b'model')

    def snapshot(self,name,assets,revision):
        directory=self.v/name;directory.mkdir()
        api.atomic(directory/'details.v1.json',{'pack_id':'pack','assets':assets})
        api.atomic(directory/'profiles.v1.json',{'profiles':[]})
        return {'revision':revision*64,'path':name,'files':{x:api.digest(directory/x) for x in ('details.v1.json','profiles.v1.json')}}

    def install(self,previous_assets):
        previous=self.snapshot('previous',previous_assets,'a')
        current=self.snapshot('current',[self.current],'b');current['transaction']='c'*32
        api.atomic(self.v/'metadata/asset-import-history'/('c'*32+'.json'),{'next':current,'previous':previous,'pack_id':'pack'})
        api.atomic(api.index_path(self.v),{'schema_version':1,'packs':{'pack':current}})
        return api.index_path(self.v).read_bytes()

    def test_rollback_refuses_to_discard_consolidated_alias(self):
        old={'id':'keep','model_path':str(self.v/'keep.glb')}
        before=self.install([old,{'id':'old','model_path':str(self.v/'old.glb')}])
        with self.assertRaisesRegex(ValueError,'discard consolidated'):
            api.rollback(self.v,'pack','b'*64)
        self.assertEqual(api.index_path(self.v).read_bytes(),before)

    def test_rollback_refuses_missing_previous_model(self):
        previous={**self.current,'model_path':str(self.v/'missing.glb')}
        before=self.install([previous])
        with self.assertRaisesRegex(ValueError,'Rollback model missing'):
            api.rollback(self.v,'pack','b'*64)
        self.assertEqual(api.index_path(self.v).read_bytes(),before)


if __name__=='__main__':unittest.main()
