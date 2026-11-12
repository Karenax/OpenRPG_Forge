import unittest

from audit_variant_material_choices import choose


class MaterialChoiceTests(unittest.TestCase):
    def doc(self, **material):
        return {'materials': [{'name': 'cloth', **material}]}

    def test_prefers_double_sided_and_mask_when_they_agree(self):
        main = self.doc(alphaMode='OPAQUE', doubleSided=False)
        alternate = self.doc(alphaMode='MASK', doubleSided=True)
        result = choose(main, alternate)
        self.assertEqual(result['keep_side'], 'variant')
        self.assertEqual(result['preferred_controls'], ['alpha', 'double_sided'])

    def test_conflict_requires_review(self):
        with self.assertRaisesRegex(ValueError, 'preferences conflict'):
            choose(self.doc(alphaMode='MASK', doubleSided=False),
                   self.doc(alphaMode='OPAQUE', doubleSided=True))

    def test_does_not_equate_blend_with_mask_or_reorder_slots(self):
        with self.assertRaisesRegex(ValueError, 'not interchangeable'):
            choose(self.doc(alphaMode='BLEND'), self.doc(alphaMode='MASK'))
        with self.assertRaisesRegex(ValueError, 'names or ordering'):
            choose(self.doc(), {'materials': [{'name': 'other'}]})

    def test_differing_emission_and_cutoff_require_review(self):
        with self.assertRaisesRegex(ValueError, 'emissive appearance differs'):
            choose(self.doc(emissiveFactor=[1, 0, 0]), self.doc())
        with self.assertRaisesRegex(ValueError, 'alpha cutoff differs'):
            choose(self.doc(alphaMode='MASK', alphaCutoff=0.2),
                   self.doc(alphaMode='MASK', alphaCutoff=0.8))


if __name__ == '__main__':
    unittest.main()
