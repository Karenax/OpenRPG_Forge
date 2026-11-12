import unittest

from pathlib import Path
from audit_shared_binary_variants import audit, material_summary, select
from test_variant_legacy_recipe import glb


class SelectSharedBinaryTests(unittest.TestCase):
    def setUp(self):
        self.row = {'id': 'pack.item', 'main_id': 'pack.item', 'variant_id': 'pack.variant.item',
                    'main_path': 'a.glb', 'variant_path': 'b.glb',
                    'main_sha': 'a' * 64, 'variant_sha': 'b' * 64}
        self.evidence = {'id': self.row['id'], 'main_path': 'a.glb', 'variant_path': 'b.glb',
                         'status': 'compared', 'binary_chunk_exact': True,
                         'json_difference_paths': ['$/materials/0/alphaMode'],
                         'difference_paths_truncated': False}

    def test_differences_are_retained_and_never_marked_removable(self):
        result = select([self.evidence], {self.row['id']: self.row}, 'f' * 64)
        candidate = result['candidates'][0]
        self.assertFalse(candidate['ready_for_removal'])
        self.assertEqual(candidate['differences'], ['$/materials/0/alphaMode'])
        self.assertEqual(result['state'], 'audit_only')

    def test_rejects_wrong_paths_and_overlapping_pairs(self):
        with self.assertRaises(ValueError):
            select([dict(self.evidence, main_path='wrong')], {self.row['id']: self.row}, 'f' * 64)
        with self.assertRaises(ValueError):
            select([self.evidence, self.evidence], {self.row['id']: self.row}, 'f' * 64)

    def test_compares_effective_material_defaults(self):
        raw = glb(b'{"materials":[{"name":"leaf"},{"alphaMode":"MASK","alphaCutoff":0.3,"doubleSided":true}]}')
        materials = material_summary(raw)
        self.assertEqual(materials[0]['alpha_mode'], 'OPAQUE')
        self.assertEqual(materials[0]['alpha_cutoff'], 0.5)
        self.assertEqual(materials[1]['alpha_mode'], 'MASK')
        self.assertEqual(materials[1]['alpha_cutoff'], 0.3)
        self.assertTrue(materials[1]['double_sided'])

    def test_requires_both_optional_outputs(self):
        with self.assertRaises(ValueError):
            audit(Path('.'), Path('.'), Path('output'), Path('plan'), None)


if __name__ == '__main__':
    unittest.main()
