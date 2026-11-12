import copy
import hashlib
from pathlib import Path
import tempfile
import unittest

from audit_military_candidate_choices import check_metadata


class MilitaryChoicesTests(unittest.TestCase):
    def test_candidate_requires_source_evidence_and_no_collision(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);source=root/'source.prefab';source.write_bytes(b'prefab')
            sha=hashlib.sha256(b'prefab').hexdigest()
            main={'id':'main','model_sha256':'a'*64,'source_prefab':'hdrp.prefab','license':'private',
                  'readiness':{'metadata_ready':True},'missing':[],
                  'measurement':{'dimensions':[1,2,3],'triangles':4}}
            old={'source_prefab':{'path':str(source),'guid':'abc','sha256':sha},'license':'private',
                 'readiness':{'metadata_ready':False},'missing':['details'],
                 'measurement':{'dimensions':[1,2,3],'triangles':4,
                                'collision_geometry':{'triangles':0,'mesh_instances':0}}}
            evidence={'id':'main','sha256':'a'*64,'source_prefab':'hdrp.prefab','source_guid':'abc',
                      'source_prefab_sha256':'b'*64,'geometry_roundtrip_passed':True,'visual_review':'agent_review'}
            self.assertEqual(check_metadata(main,old,evidence,root)['keep_side'],'main')
            for field in ('source','collision','dimensions','metadata','license','guid'):
                candidate=copy.deepcopy(old);proof=copy.deepcopy(evidence)
                if field=='source':candidate['source_prefab']['sha256']='0'*64
                if field=='collision':candidate['measurement']['collision_geometry']['triangles']=1
                if field=='dimensions':candidate['measurement']['dimensions'][0]=5
                if field=='metadata':candidate['readiness']['metadata_ready']=True
                if field=='license':candidate['license']='other'
                if field=='guid':proof['source_guid']='other'
                with self.subTest(field=field),self.assertRaises(ValueError):
                    check_metadata(main,candidate,proof,root)


if __name__=='__main__':unittest.main()
