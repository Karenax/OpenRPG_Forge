import copy
import hashlib
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from audit_native_candidate_choices import check_metadata, check_qualified_pair


class NativeChoicesTests(unittest.TestCase):
    def test_two_qualified_models_require_same_real_sources(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder); (root/'catalog.json').write_text('{}'); (root/'main.glb').write_bytes(b'model')
            for name in ('old.prefab','old.fbx'):(root/name).write_bytes(name.encode())
            prefab=hashlib.sha256(b'old.prefab').hexdigest();fbx=hashlib.sha256(b'old.fbx').hexdigest()
            old={'id':'old','model_sha256':'a'*64,'license':'private','readiness':{'metadata_ready':True},
                 'missing':[],'measurement':{'dimensions':[1,2,3],'triangles':4},'model_path':str(root/'main.glb'),
                 'source_catalog':str(root/'catalog.json'),
                 'source_prefab':[{'path':'old.prefab','original_source_path':'old.prefab','sha256':prefab}],
                 'source_fbx':[{'path':'old.fbx','original_source_path':'old.fbx','sha256':fbx}]}
            primary={'id':'new','model_sha256':'b'*64,'license':'private','readiness':{'metadata_ready':True},
                     'missing':[],'measurement':{'dimensions':[1,2,3],'triangles':4},
                     'source_prefab':str(root/'old.prefab'),'source_fbx':str(root/'old.fbx')}
            before={'id':'old','sha256':'a'*64,'geometry_roundtrip_passed':True}
            after={'id':'new','sha256':'b'*64,'geometry_roundtrip_passed':True,
                   'source_authored_frames_preserved':True,'material_review_pending':False}
            with patch('audit_native_candidate_choices.materials.glb_document',return_value={'nodes':[]}):
                self.assertEqual(check_qualified_pair(old,primary,before,after,root)['keep_side'],'variant')
                (root/'old.fbx').write_bytes(b'changed')
                with self.assertRaises(ValueError):check_qualified_pair(old,primary,before,after,root)
            (root/'old.fbx').write_bytes(b'old.fbx')
            with patch('audit_native_candidate_choices.materials.glb_document',return_value={'nodes':[], 'extensionsUsed':['OPENRPG_source_fragment']}):
                self.assertEqual(check_qualified_pair(old,primary,before,after,root)['keep_side'],'variant')
            with patch('audit_native_candidate_choices.materials.glb_document',return_value={'nodes':[], 'extensionsUsed':['OPENRPG_unknown']}):
                with self.assertRaises(ValueError):check_qualified_pair(old,primary,before,after,root)

    def test_matching_sources_and_safety_guards(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);(root/'source.prefab').write_bytes(b'prefab')
            sha=hashlib.sha256(b'prefab').hexdigest()
            old={'id':'old','model_sha256':'a'*64,'license':'private','readiness':{'metadata_ready':False},
                 'missing':['details'],'measurement':{'dimensions':[1,2,3],'triangles':4,
                   'collision_geometry':{'triangles':0,'mesh_instances':0}}}
            primary={'id':'new','model_sha256':'b'*64,'license':'private','readiness':{'metadata_ready':True},
                     'missing':[],'measurement':{'dimensions':[1,2,3],'triangles':4},
                     'source_prefab':{'sha256':sha}}
            before={'id':'old','sha256':'a'*64,'source_support_provenance':
                    {'source_guid':'g','source_sha256':sha,'source_file':'source.prefab'}}
            after={'id':'new','sha256':'b'*64,'geometry_roundtrip_passed':True,
                   'source_authored_frames_preserved':True,'material_review_pending':False}
            self.assertEqual(check_metadata(old,primary,before,after,root)['keep_side'],'variant')
            for field in ('source','collision','dimensions','model','license','frames'):
                c=copy.deepcopy(old);p=copy.deepcopy(primary);a=copy.deepcopy(after)
                if field=='source':p['source_prefab']['sha256']='0'*64
                if field=='collision':c['measurement']['collision_geometry']['triangles']=1
                if field=='dimensions':p['measurement']['dimensions'][0]=5
                if field=='model':a['sha256']='0'*64
                if field=='license':p['license']='other'
                if field=='frames':a['source_authored_frames_preserved']=False
                with self.subTest(field=field),self.assertRaises(ValueError):check_metadata(c,p,before,a,root)


if __name__=='__main__':unittest.main()
