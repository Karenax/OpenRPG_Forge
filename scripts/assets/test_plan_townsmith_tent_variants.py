import copy
import unittest
from plan_townsmith_tent_variants import validate_docs


class TentChoiceTests(unittest.TestCase):
    def test_preserves_source_and_rejects_changed_material(self):
        extras={'source_prefab_guid':'p','source_mesh_guid':'m','source_gameobject':'g',
                'source_sha256':'s','license':'l'}
        a={'nodes':[{'extras':extras}], 'materials':[{'name':'M_Tent_Inst','alphaMode':'MASK'},
                                                     {'name':'wood'}]}
        b=copy.deepcopy(a);b['materials'][0].pop('alphaMode');b['materials'][0]['doubleSided']=True
        validate_docs(a,b)
        changed=copy.deepcopy(b);changed['nodes'][0]['extras']['source_mesh_guid']='other'
        with self.assertRaises(ValueError):validate_docs(a,changed)
        changed=copy.deepcopy(b);changed['materials'][0]['alphaMode']='BLEND'
        with self.assertRaises(ValueError):validate_docs(a,changed)


if __name__=='__main__':unittest.main()
