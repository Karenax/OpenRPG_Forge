import copy
import unittest
from audit_temple_graph_choices import check


class TempleChoicesTests(unittest.TestCase):
    def test_scene_name_and_asset_id_only_are_ignored(self):
        a={'mesh_payloads':[1],'images':[2],'private_extensions':['OPENRPG_source_graph'],
           'geometry':{'nodes':[{'mesh':0,'extras':{'asset_id':'a','source_lod':0}}],
                       'scenes':[{'name':'a','nodes':[0]}],'scene':0}}
        b=copy.deepcopy(a);b['geometry']['nodes'][0]['extras']['asset_id']='b';b['geometry']['scenes'][0]['name']='b'
        self.assertEqual(check(a,b)['keep_side'],'main')
        changed=copy.deepcopy(b);changed['geometry']['nodes'][0]['translation']=[1,0,0]
        with self.assertRaises(ValueError):check(a,changed)
        changed=copy.deepcopy(b);changed['images']=[3]
        with self.assertRaises(ValueError):check(a,changed)


if __name__=='__main__':unittest.main()
