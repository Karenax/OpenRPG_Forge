import unittest
from audit_haunted_collision_pairs import parse_colliders


class ColliderAuditTests(unittest.TestCase):
    def test_distinguishes_different_capsule_shapes(self):
        first=b'--- !u!136 &1\nCapsuleCollider:\n  m_Radius: 0.1\n  m_Height: 2\n  m_Enabled: 1\n'
        second=first.replace(b'm_Radius: 0.1',b'm_Radius: 0.2')
        self.assertNotEqual(parse_colliders(first)['colliders'],parse_colliders(second)['colliders'])

    def test_component_ids_do_not_change_shape(self):
        first=b'--- !u!65 &1\nBoxCollider:\n  m_GameObject: {fileID: 10}\n  m_Size: {x: 1, y: 2, z: 3}\n'
        self.assertEqual(parse_colliders(first)['colliders'],parse_colliders(first.replace(b'&1',b'&2'))['colliders'])


if __name__=='__main__':unittest.main()
