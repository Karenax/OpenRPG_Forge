import copy
import unittest
from unittest.mock import patch
from audit_rural_plants_choices import check


class RuralPlantChoicesTests(unittest.TestCase):
    def test_source_emission_and_geometry_guards(self):
        source={'source_prefab_guid':'p','source_mesh_guid':'m','source_sha256':'s','source_gameobject':'g'}
        common={'alphaMode':'MASK','alphaCutoff':0.25,'doubleSided':True}
        a={**common,'name':'M_WildCarrot_Atlas_01','extras':{'source_unity_guid':'u'}}
        b={**common,'name':'baked','extras':{'source_unity_guid':'u','source_material':a['name']},
           'emissiveFactor':[1,1,1],'emissiveTexture':{'index':0}}
        docs=[{'nodes':[{'mesh':0,'extras':source}],'materials':[a]},
              {'nodes':[{'mesh':0,'extras':copy.deepcopy(source)}],'materials':[b]}]
        cards=[{'license':'same','missing':[],'readiness':{'metadata_ready':True}}]*2
        with patch('audit_rural_plants_choices.triangle_positions',return_value=[1]):
            self.assertEqual(check(docs,[b'a',b'b'],cards)['keep_side'],'main')
            changed=copy.deepcopy(docs);changed[1]['nodes'][0]['extras']['source_mesh_guid']='other'
            with self.assertRaises(ValueError):check(changed,[b'a',b'b'],cards)
            changed=copy.deepcopy(docs);changed[1]['materials'][0]['doubleSided']=False
            with self.assertRaises(ValueError):check(changed,[b'a',b'b'],cards)
        with patch('audit_rural_plants_choices.triangle_positions',side_effect=[[1],[2]]):
            with self.assertRaises(ValueError):check(docs,[b'a',b'b'],cards)


if __name__=='__main__':unittest.main()
