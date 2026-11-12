import io
import unittest
from audit_large_source_variants import signatures,MAX_LINE


class StreamingVariantTests(unittest.TestCase):
    def test_lod_only_changes_are_classified_but_geometry_changes_are_not(self):
        hdr=b'm_LODs:\r\n  - screenRelativeHeight: 0.6\r\n    renderer: {fileID: 1}\r\n'
        urp=b'm_LODs:\n  - screenRelativeHeight: 0.3\n    renderer: {fileID: 1}\n'
        a,b=signatures(io.BytesIO(hdr)),signatures(io.BytesIO(urp))
        self.assertNotEqual(a['sha256'],b['sha256'])
        self.assertEqual(a['lod_normalized_sha256'],b['lod_normalized_sha256'])
        c=signatures(io.BytesIO(urp.replace(b'fileID: 1',b'fileID: 2')))
        self.assertNotEqual(a['lod_normalized_sha256'],c['lod_normalized_sha256'])

    def test_binary_and_unbounded_lines_are_refused(self):
        with self.assertRaisesRegex(ValueError,'binary'):signatures(io.BytesIO(b'ab\0cd'))
        with self.assertRaisesRegex(ValueError,'line exceeds'):signatures(io.BytesIO(b'x'*(MAX_LINE+1)))


if __name__=='__main__':unittest.main()
