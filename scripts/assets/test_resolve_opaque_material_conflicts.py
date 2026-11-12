import io
import unittest

from PIL import Image
from resolve_opaque_material_conflicts import resolve


def model(mode, double, alpha=255, vertex_color=False):
    stream = io.BytesIO()
    Image.new('RGBA', (2, 2), (10, 20, 30, alpha)).save(stream, format='PNG')
    blob = stream.getvalue()
    attrs = {'POSITION': 0}
    if vertex_color:
        attrs['COLOR_0'] = 1
    doc = {'materials': [{'name': 'stone', 'alphaMode': mode, 'doubleSided': double,
                          'pbrMetallicRoughness': {'baseColorTexture': {'index': 0}}}],
           'textures': [{'source': 0}], 'images': [{'mimeType': 'image/png', 'bufferView': 0}],
           'bufferViews': [{'buffer': 0, 'byteLength': len(blob)}],
           'meshes': [{'primitives': [{'attributes': attrs}]}]}
    return doc, memoryview(blob)


class OpacityConflictTests(unittest.TestCase):
    def test_opaque_masks_allow_double_sided_choice(self):
        a, ab = model('OPAQUE', True)
        b, bb = model('MASK', False)
        result = resolve(a, b, ab, bb, {})
        self.assertEqual(result['keep_side'], 'main')
        self.assertEqual(result['opaque_alpha_slots'], [0])

    def test_transparent_pixels_and_vertex_color_require_review(self):
        a, ab = model('OPAQUE', True)
        b, bb = model('MASK', False, alpha=0)
        with self.assertRaisesRegex(ValueError, 'not fully opaque'):
            resolve(a, b, ab, bb, {})
        b, bb = model('MASK', False, vertex_color=True)
        with self.assertRaisesRegex(ValueError, 'vertex color'):
            resolve(a, b, ab, bb, {})


if __name__ == '__main__':
    unittest.main()
