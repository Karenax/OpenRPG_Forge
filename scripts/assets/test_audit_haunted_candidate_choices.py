import copy
import hashlib
from pathlib import Path
import tempfile
import unittest
from audit_haunted_candidate_choices import check_metadata


class HauntedChoicesTests(unittest.TestCase):
    def test_matching_sources_and_safety_guards(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);(root/'source.prefab').write_bytes(b'prefab')
            sha=hashlib.sha256(b'prefab').hexdigest()
            main={'id':'main','model_sha256':'a'*64,'license':'private','readiness':{'metadata_ready':True},
                  'missing':[],'measurement':{'dimensions':[1,2,3],'triangles':4}}
            old={'id':'old','model_sha256':'b'*64,'license':'private','readiness':{'metadata_ready':False},
                 'missing':['details'],'measurement':{'dimensions':[1,2,3],'triangles':4,
                   'collision_geometry':{'triangles':0,'mesh_instances':0}}}
            primary={'id':'main','sha256':'a'*64,'source_guid':'g','source_sha256':sha,
                     'geometry_roundtrip_passed':True,'visual_review':'agent_reviewed'}
            alternate={'id':'old','sha256':'b'*64,'source_support_provenance':
                       {'source_guid':'g','source_sha256':sha,'source_file':'source.prefab'}}
            self.assertEqual(check_metadata(main,old,primary,alternate,root)['keep_side'],'main')
            pending=copy.deepcopy(primary);pending.update(visual_review='agent_review',material_review_pending=True)
            with self.assertRaises(ValueError):check_metadata(main,old,pending,alternate,root)
            result=check_metadata(main,old,pending,alternate,root,allow_cave_pending=True)
            self.assertIn('primary_material_review_pending',result['preferred_controls'])
            self.assertIn('review pending',result['limitations'])
            for field in ('source','collision','dimensions','model','license'):
                c=copy.deepcopy(old);p=copy.deepcopy(primary);v=copy.deepcopy(alternate)
                if field=='source':v['source_support_provenance']['source_sha256']='0'*64
                if field=='collision':c['measurement']['collision_geometry']['triangles']=1
                if field=='dimensions':c['measurement']['dimensions'][0]=5
                if field=='model':p['sha256']='0'*64
                if field=='license':c['license']='other'
                with self.subTest(field=field),self.assertRaises(ValueError):check_metadata(main,c,p,v,root)


if __name__=='__main__':unittest.main()
