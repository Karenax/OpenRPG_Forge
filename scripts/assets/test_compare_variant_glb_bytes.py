import json
import struct
import unittest

from compare_variant_glb_bytes import signature


def glb(vertex=b'abc', image=b'png', material='wood'):
    binary = vertex + image
    binary += b'\0' * (-len(binary) % 4)
    doc = {'asset': {'version': '2.0'}, 'buffers': [{'byteLength': len(binary)}],
           'bufferViews': [{'buffer': 0, 'byteOffset': 0, 'byteLength': len(vertex)},
                           {'buffer': 0, 'byteOffset': len(vertex), 'byteLength': len(image)}],
           'accessors': [{'bufferView': 0, 'componentType': 5126, 'count': 1, 'type': 'VEC3'}],
           'meshes': [{'primitives': [{'attributes': {'POSITION': 0}, 'material': 0}]}],
           'materials': [{'name': material}], 'images': [{'bufferView': 1, 'mimeType': 'image/png'}],
           'nodes': [{'mesh': 0}], 'scenes': [{'nodes': [0]}], 'scene': 0}
    payload = json.dumps(doc).encode(); payload += b' ' * (-len(payload) % 4)
    return struct.pack('<4sIII4s', b'glTF', 2, 28 + len(payload) + len(binary), len(payload), b'JSON') + payload + struct.pack('<I4s', len(binary), b'BIN\0') + binary


class ComparisonTest(unittest.TestCase):
    def test_detects_independent_geometry_material_and_image_changes(self):
        baseline = signature(glb())
        self.assertNotEqual(baseline['geometry'], signature(glb(vertex=b'xyz'))['geometry'])
        self.assertNotEqual(baseline['materials'], signature(glb(material='stone'))['materials'])
        self.assertNotEqual(baseline['images'], signature(glb(image=b'jpg'))['images'])
        self.assertEqual(baseline, signature(glb()))

    def test_invalid_container_refused(self):
        with self.assertRaises(ValueError):
            signature(b'not a real glb')


if __name__ == '__main__':
    unittest.main()
