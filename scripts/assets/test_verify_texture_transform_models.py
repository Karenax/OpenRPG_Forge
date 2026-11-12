import unittest
from verify_texture_transform_models import failure_ids


class TextureTransformFailureListTests(unittest.TestCase):
    def test_only_selects_transform_failures_and_deduplicates(self):
        log = ('Pre-existing export failure for pack.a: Glb: KHR_texture_transform unsupported\n'
               'Pre-existing export failure for pack.a: Glb: KHR_texture_transform unsupported\n'
               'Pre-existing export failure for pack.b: Png: pixel budget\n')
        self.assertEqual(failure_ids(log), {'pack.a'})


if __name__ == '__main__':
    unittest.main()
