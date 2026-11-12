import io
import unittest

from PIL import Image
from audit_viking_alpha_opaque import classify, classify_double_sided, classify_mask_preference, permitted_difference
from audit_variant_exact_candidates import diffs


def fixture(alpha=255, vertex_color=False):
    image = Image.new('RGBA', (2, 2), (40, 50, 60, alpha))
    stream = io.BytesIO()
    image.save(stream, format='PNG')
    binary = stream.getvalue()
    doc = {'meshes': [{'primitives': [{'attributes': {'COLOR_0': 0} if vertex_color else {}}]}],
           'materials': [{'alphaMode': 'OPAQUE', 'pbrMetallicRoughness': {'baseColorTexture': {'index': 0}}}],
           'textures': [{'source': 0}], 'images': [{'mimeType': 'image/png', 'bufferView': 0}],
           'bufferViews': [{'buffer': 0, 'byteLength': len(binary)}]}
    other = {**doc, 'materials': [{'alphaMode': 'MASK', 'pbrMetallicRoughness': {'baseColorTexture': {'index': 0}}}]}
    return doc, other, memoryview(binary)


class AlphaAuditTests(unittest.TestCase):
    def test_accepts_only_fully_opaque_pixels(self):
        a, b, binary = fixture()
        self.assertEqual(classify(a, b, binary, binary, ['$/materials/0/alphaMode'], {}), 'opaque_alpha_candidate')
        a, b, binary = fixture(254)
        self.assertEqual(classify(a, b, binary, binary, ['$/materials/0/alphaMode'], {}), 'texture_alpha_requires_review')

    def test_rejects_vertex_alpha_and_other_render_controls(self):
        a, b, binary = fixture(vertex_color=True)
        self.assertEqual(classify(a, b, binary, binary, ['$/materials/0/alphaMode'], {}), 'vertex_color_requires_review')
        self.assertFalse(permitted_difference('$/materials/0/doubleSided'))
        self.assertFalse(permitted_difference('$/materials/0/extensions/OPENRPG_source_fragment/normal_strength'))

    def test_rejects_custom_base_png_and_cutoff_at_one(self):
        a, b, binary = fixture()
        for material in (a['materials'][0], b['materials'][0]):
            material['extensions'] = {'OPENRPG_source_fragment': {'base_png': {'buffer_view': 0}}}
        self.assertEqual(classify(a, b, binary, binary, ['$/materials/0/alphaMode'], {}), 'texture_alpha_requires_review')
        a, b, binary = fixture()
        a['materials'][0]['alphaCutoff'] = 1
        self.assertEqual(classify(a, b, binary, binary,
                                  ['$/materials/0/alphaCutoff (missing)', '$/materials/0/alphaMode'], {}), 'unsupported_cutoff')

    def test_double_sided_choice_is_distinct_from_equivalence(self):
        a, b, binary = fixture()
        a['materials'][0].update(doubleSided=True, extensions={'OPENRPG_source_fragment': {'double_sided_normal_mode': 'flip'}})
        b['materials'][0].update(doubleSided=False, extensions={'OPENRPG_source_fragment': {'double_sided_normal_mode': 'single_sided'}})
        changes = list(diffs(a, b))
        self.assertEqual(classify_double_sided(a, b, binary, binary, changes, {}), 'double_sided_choice_candidate')
        a['materials'][0]['doubleSided'] = False
        self.assertEqual(classify_double_sided(a, b, binary, binary, list(diffs(a, b)), {}), 'other_rendering_difference')

    def test_prefers_mask_only_when_texture_contains_true_transparency(self):
        image = Image.new('RGBA', (2, 2), (40, 50, 60, 255))
        image.putpixel((0, 0), (40, 50, 60, 0))
        stream = io.BytesIO(); image.save(stream, format='PNG'); binary = memoryview(stream.getvalue())
        a, b, _ = fixture()
        a['bufferViews'][0]['byteLength'] = len(binary)
        a['materials'][0].update(alphaMode='MASK', doubleSided=True, extensions={'OPENRPG_source_fragment': {'double_sided_normal_mode': 'flip'}})
        b['materials'][0].update(alphaMode='OPAQUE', doubleSided=False, extensions={'OPENRPG_source_fragment': {'double_sided_normal_mode': 'single_sided'}})
        self.assertEqual(classify_mask_preference(a, b, binary, binary, list(diffs(a, b)), {}), 'double_sided_mask_choice_candidate')
        a['materials'][0]['alphaMode'], b['materials'][0]['alphaMode'] = 'OPAQUE', 'MASK'
        self.assertEqual(classify_mask_preference(a, b, binary, binary, list(diffs(a, b)), {}), 'other_rendering_difference')


if __name__ == '__main__':
    unittest.main()
