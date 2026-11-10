import unittest
from audit_variant_source_dependencies import base_id, binding_signature


class BindingTests(unittest.TestCase):
    def test_variant_identity_and_shader_difference(self):
        self.assertEqual(base_id('pack.variant-abcd.model-lod1'), 'pack.model-lod1')
        base={'material_guid':'m','material_sha256':'x','shader_guid':'hdrp',
              'textures':[{'role':'albedo','guid':'t','offset':[0,0],'scale':[1,1],'status':'source_preserved'}]}
        other=dict(base,shader_guid='urp')
        self.assertNotEqual(binding_signature([base]),binding_signature([other]))
        self.assertEqual(binding_signature([base]),binding_signature([dict(base)]))


if __name__=='__main__':
    unittest.main()
