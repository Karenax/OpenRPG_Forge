import unittest
from audit_haunted_collider_affine import affine_shapes


def prefab(position,center,parent='0'):
    return (f'--- !u!4 &10\nTransform:\n  m_GameObject: {{fileID: 20}}\n'
            f'  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n'
            f'  m_LocalPosition: {{x: {position}, y: 0, z: 0}}\n'
            f'  m_LocalScale: {{x: 1, y: 1, z: 1}}\n  m_Father: {{fileID: {parent}}}\n'
            f'--- !u!136 &30\nCapsuleCollider:\n  m_GameObject: {{fileID: 20}}\n'
            f'  m_Enabled: 1\n  m_IsTrigger: 0\n  m_Radius: 1\n  m_Height: 4\n'
            f'  m_Direction: 1\n  m_Center: {{x: {center}, y: 0, z: 0}}\n').encode()


class ColliderAffineTests(unittest.TestCase):
    def test_local_translation_and_center_can_cancel(self):
        self.assertEqual(affine_shapes(prefab(1,-1)),affine_shapes(prefab(0,0)))

    def test_different_position_is_detected(self):
        self.assertNotEqual(affine_shapes(prefab(1,0)),affine_shapes(prefab(0,0)))

    def test_missing_parent_is_rejected(self):
        with self.assertRaisesRegex(ValueError,'hierarchy'):
            affine_shapes(prefab(0,0,'999'))


if __name__=='__main__':unittest.main()
