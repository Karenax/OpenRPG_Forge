import unittest
from compare_unity_lod_collider_variants import compare


class LodColliderTests(unittest.TestCase):
    def test_only_expected_lod_values_and_known_collider_names_are_ignored(self):
        a='--- !u!205 &1\nLODGroup:\n  m_Size: 1\n  - screenRelativeHeight: 0.6\n--- !u!1 &2\nGameObject:\n  m_Name: Collider\n--- !u!65 &3\nBoxCollider:\n  m_Size: {x: 1, y: 1, z: 1}\n'
        b=a.replace('m_Size: 1\n','m_Size: 2\n').replace('0.6','0.3').replace('m_Name: Collider','m_Name: BoxColliderObject (1)')
        self.assertTrue(compare(a,b))
        self.assertFalse(compare(a,b.replace('x: 1','x: 2')))
        self.assertFalse(compare(a,b.replace('BoxColliderObject (1)','DifferentObject')))
        self.assertFalse(compare(a,a))


if __name__=='__main__':unittest.main()
