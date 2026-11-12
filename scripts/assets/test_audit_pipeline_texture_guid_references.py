import unittest
from audit_pipeline_texture_guid_references import GUID_REF,meta_guid


class UnityGuidTests(unittest.TestCase):
    def test_meta_requires_one_root_guid_and_extracts_yaml_reference(self):
        guid=b'0123456789abcdef0123456789abcdef'
        self.assertEqual(meta_guid(b'fileFormatVersion: 2\nguid: '+guid+b'\n'),guid.decode())
        self.assertEqual(GUID_REF.findall(b'm_Texture: {fileID: 2800000, guid: '+guid+b', type: 3}'),[guid])
        self.assertEqual(GUID_REF.findall(b'{"m_GuidSerialized":"'+guid+b'"}'),[guid])
        with self.assertRaises(ValueError):meta_guid(b'fileFormatVersion: 2\n')
        with self.assertRaises(ValueError):meta_guid(b'guid: '+guid+b'\nguid: '+guid+b'\n')


if __name__=='__main__':unittest.main()
