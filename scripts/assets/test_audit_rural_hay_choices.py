import copy
import hashlib
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from audit_rural_hay_choices import check,GUID


class RuralHayTests(unittest.TestCase):
    def test_source_review_and_triangle_guards(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);a=root/'a.prefab';b=root/'b.prefab';a.write_bytes(b'a');b.write_bytes(b'b')
            h=lambda data:hashlib.sha256(data).hexdigest()
            cards=[{'id':'a','model_sha256':'a'*64,'license':'private','missing':[],
                    'readiness':{'metadata_ready':True},'measurement':{'dimensions':[1,2,3],'triangles':20}},
                   {'id':'b','model_sha256':'b'*64,'license':'private','missing':[],
                    'readiness':{'metadata_ready':True},'measurement':{'dimensions':[1,2,3],'triangles':10}}]
            entries=[{'id':'a','sha256':'a'*64,'source_guid':GUID,'source_prefab':{'sha256':h(b'a')},
                      'material_review_pending':False,'visual_review':'agent_reviewed','geometry_roundtrip_passed':True},
                     {'id':'b','sha256':'b'*64,'source_guid':GUID,'source_sha256':h(b'b'),'geometry_roundtrip_passed':True}]
            with patch('audit_rural_hay_choices.ROOT',root),patch('audit_rural_hay_choices.SOURCES',(a,b)):
                self.assertEqual(check(cards,entries)['keep_side'],'main')
                changed=copy.deepcopy(cards);changed[1]['measurement']['triangles']=11
                with self.assertRaises(ValueError):check(changed,entries)
                changed=copy.deepcopy(entries);changed[0]['material_review_pending']=True
                with self.assertRaises(ValueError):check(cards,changed)


if __name__=='__main__':unittest.main()
