import unittest
from audit_source_prefab_differences import summarize_diff


class PrefabDifferenceTests(unittest.TestCase):
    def test_reports_light_and_script_changes_without_claiming_equivalence(self):
        hdr='--- !u!108 &1\nLight:\n  m_Intensity: 55\n  m_Script: {guid: '+('a'*32)+'}\n'
        urp='--- !u!108 &1\nLight:\n  m_Intensity: 3\n  m_Script: {guid: '+('b'*32)+'}\n'
        result=summarize_diff(hdr,urp)
        self.assertEqual(result['different_fields']['m_Intensity'],2)
        self.assertEqual(result['hdrp_only_guids'],['a'*32])
        self.assertEqual(result['urp_only_guids'],['b'*32])
        self.assertEqual(result['hdrp_classes'],{'108':1})
        self.assertFalse(result['normalized_lines_equal'])

    def test_crlf_alone_is_detected_as_equal_lines(self):
        self.assertTrue(summarize_diff('Light:\r\n  value: 1\r\n','Light:\n  value: 1\n')['normalized_lines_equal'])

    def test_lod_mask_does_not_hide_other_changes(self):
        a='m_LODs:\n  - screenRelativeHeight: 0.6\n    renderer: {fileID: 1}\n'
        b='m_LODs:\n  - screenRelativeHeight: 0.3\n    renderer: {fileID: 1}\n'
        self.assertTrue(summarize_diff(a,b)['equal_except_lod_thresholds'])
        self.assertFalse(summarize_diff(a,b.replace('fileID: 1','fileID: 2'))['equal_except_lod_thresholds'])


if __name__=='__main__':unittest.main()
