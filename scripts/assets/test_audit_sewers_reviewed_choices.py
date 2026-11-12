import copy
import unittest
from audit_sewers_reviewed_choices import check_metadata


class SewerChoicesTests(unittest.TestCase):
    def test_reviewed_source_and_rejection_guards(self):
        main={'id':'main','model_sha256':'a'*64,'license':'private','readiness':{'metadata_ready':True},
              'missing':[],'measurement':{'vertices':3,'triangles':1}}
        old={'id':'old','model_sha256':'b'*64,'license':'private','readiness':{'metadata_ready':True},
             'missing':[],'measurement':{'vertices':3,'triangles':1}}
        a={'id':'main','sha256':'a'*64,'source_guid':'g','source_sha256':'c'*64,
           'geometry_roundtrip_passed':True,'material_review_pending':False}
        b={'id':'old','sha256':'b'*64,'source_guid':'g','source_sha256':'c'*64,
           'geometry_roundtrip_passed':True,'material_review_pending':True}
        self.assertEqual(check_metadata(main,old,a,b)['keep_side'],'main')
        both=copy.deepcopy(b);both['material_review_pending']=False
        self.assertEqual(check_metadata(main,old,a,both,allow_both_reviewed=True)['keep_side'],'main')
        with self.assertRaises(ValueError):check_metadata(main,old,a,both)
        revision=copy.deepcopy(b);revision['source_sha256']='d'*64
        with self.assertRaises(ValueError):check_metadata(main,old,a,revision)
        self.assertEqual(check_metadata(main,old,a,revision,allow_source_revisions=True)['keep_side'],'main')
        split=copy.deepcopy(old);split['measurement']['vertices']=4
        with self.assertRaises(ValueError):check_metadata(main,split,a,b)
        self.assertEqual(check_metadata(main,split,a,b,allow_vertex_difference=True)['keep_side'],'main')
        for change in ('source','review','geometry','model','license'):
            v=copy.deepcopy(old);left=copy.deepcopy(a);right=copy.deepcopy(b)
            if change=='source':right['source_sha256']='0'*64
            if change=='review':right['material_review_pending']=False
            if change=='geometry':v['measurement']['triangles']=2
            if change=='model':left['sha256']='0'*64
            if change=='license':v['license']='other'
            with self.subTest(change=change),self.assertRaises(ValueError):check_metadata(main,v,left,right)


if __name__=='__main__':unittest.main()
