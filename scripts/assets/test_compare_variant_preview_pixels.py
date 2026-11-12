import unittest

from PIL import Image
from compare_variant_preview_pixels import difference


class PreviewPixelsTests(unittest.TestCase):
    def test_detects_rgb_with_unchanged_alpha(self):
        a = Image.new('RGBA', (2, 2), (10, 20, 30, 255))
        b = a.copy()
        b.putpixel((0, 0), (11, 20, 30, 255))
        self.assertEqual(difference(a, b)['state'], 'different_pixels')

    def test_detects_alpha_and_identical_images(self):
        a = Image.new('RGBA', (2, 2), (10, 20, 30, 255))
        b = a.copy()
        self.assertEqual(difference(a, b)['state'], 'equal_pixels')
        b.putpixel((0, 0), (10, 20, 30, 254))
        self.assertEqual(difference(a, b)['state'], 'different_pixels')

    def test_reports_different_sizes(self):
        self.assertEqual(difference(Image.new('RGBA', (1, 1)), Image.new('RGBA', (2, 1)))['state'], 'different_dimensions')


if __name__ == '__main__':
    unittest.main()
