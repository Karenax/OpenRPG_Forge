import copy
import unittest
from plan_haunted_matching_foliage import same_except_tangents


class TangentScopeTests(unittest.TestCase):
    def test_only_tangent_can_differ(self):
        a={'mesh_payloads':[[{'attributes':{'POSITION':{'sha':'p'},'TANGENT':{'sha':'a'}},'indices':{'sha':'i'}}]]}
        b=copy.deepcopy(a);b['mesh_payloads'][0][0]['attributes']['TANGENT']['sha']='b'
        self.assertTrue(same_except_tangents(a,b))
        b['mesh_payloads'][0][0]['attributes']['POSITION']['sha']='other'
        self.assertFalse(same_except_tangents(a,b))

    def test_missing_tangent_is_rejected(self):
        a={'mesh_payloads':[[{'attributes':{'POSITION':1},'indices':2}]]}
        self.assertFalse(same_except_tangents(a,copy.deepcopy(a)))


if __name__=='__main__': unittest.main()
