import copy
import unittest

from audit_variant_exact_candidates import diffs
from plan_double_sided_variants import choose


class DoubleSidedChoiceTests(unittest.TestCase):
    def test_selects_version_with_backface_visibility(self):
        left = {'materials': [{'doubleSided': True}], 'nodes': [{'extras': {'asset_id': 'a'}}]}
        right = {'materials': [{'doubleSided': False}], 'nodes': [{'extras': {'asset_id': 'b'}}]}
        self.assertEqual(choose(left, right, list(diffs(left, right))), 'main')
        self.assertEqual(choose(right, left, list(diffs(right, left))), 'variant')

    def test_rejects_mixed_choices_or_other_material_changes(self):
        left = {'materials': [{'doubleSided': True}, {'doubleSided': False}]}
        right = {'materials': [{'doubleSided': False}, {'doubleSided': True}]}
        with self.assertRaisesRegex(ValueError, 'neither version'):
            choose(left, right, list(diffs(left, right)))
        left = {'materials': [{'doubleSided': True, 'alphaMode': 'OPAQUE'}]}
        right = {'materials': [{'doubleSided': False, 'alphaMode': 'MASK'}]}
        with self.assertRaisesRegex(ValueError, 'other rendering'):
            choose(left, right, list(diffs(left, right)))

    def test_missing_default_false_and_input_unchanged(self):
        left = {'materials': [{'doubleSided': True}]}
        right = {'materials': [{}]}
        original = copy.deepcopy(right)
        self.assertEqual(choose(left, right, list(diffs(left, right))), 'main')
        self.assertEqual(right, original)


if __name__ == '__main__':
    unittest.main()
