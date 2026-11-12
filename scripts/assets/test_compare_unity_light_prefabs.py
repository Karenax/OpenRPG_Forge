import unittest
from compare_unity_light_prefabs import compare


def prefab(script_id,script_guid,intensity,mesh='mesh'):
    return ('%YAML 1.1\n--- !u!1 &5\nGameObject:\n  m_Component:\n'
            '  - component: {fileID: 10}\n  - component: {fileID: '+script_id+'}\n'
            '  mesh: '+mesh+'\n--- !u!108 &10\nLight:\n  m_GameObject: {fileID: 5}\n'
            '  m_Enabled: 1\n  m_Intensity: '+intensity+'\n--- !u!114 &'+script_id+'\nMonoBehaviour:\n'
            '  m_Script: {fileID: 11500000, guid: '+script_guid+', type: 3}\n')


class LightPrefabTests(unittest.TestCase):
    def test_pipeline_light_difference_does_not_mask_mesh_change(self):
        a=prefab('11','7a68c43fe1f2a47cfa234b5eeaa98012','10')
        b=prefab('12','474bcb49853aa07438625e644c072ee6','1')
        self.assertTrue(compare(a,b)['equal_outside_pipeline_lights'])
        self.assertFalse(compare(a,b.replace('mesh: mesh','mesh: other'))['equal_outside_pipeline_lights'])

    def test_unknown_script_and_other_component_reference_are_refused(self):
        a=prefab('11','7a68c43fe1f2a47cfa234b5eeaa98012','10')
        b=prefab('12','474bcb49853aa07438625e644c072ee6','1')
        with self.assertRaisesRegex(ValueError,'no known'):
            compare(a,b.replace('474bcb49853aa07438625e644c072ee6','0'*32))
        with self.assertRaisesRegex(ValueError,'another reference'):
            compare(a,b.replace('mesh: mesh','link: {fileID: 12}'))


if __name__=='__main__':unittest.main()
