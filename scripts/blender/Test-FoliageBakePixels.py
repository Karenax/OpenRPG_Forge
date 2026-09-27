"""Float emission normalization and PNG transport checks."""
import io
import unittest
import numpy as np
from PIL import Image
from foliage_bake_pixels import material_pixels, png_bytes


class BakePixels(unittest.TestCase):
    def test_constant_material_survives_different_transport_and_coverage(self):
        reference = np.array([[[.9375,.375,.1875,.90625],[1.0078125,.403125,.2015625,1]]],dtype=np.float32)
        result = material_pixels(reference,reference)
        np.testing.assert_allclose(result[:,:,:3],[[[1,.4,.2],[1,.4,.2]]],atol=1e-6)
        np.testing.assert_array_equal(result[:,:,3],reference[:,:,3])

    def test_other_material_channels_share_unit_reference(self):
        reference = np.array([[[.4,.2,0,.5],[0,0,0,0]]],dtype=np.float32)
        normal = np.array([[[.2,.2,.4,.5],[0,0,0,0]]],dtype=np.float32)
        np.testing.assert_allclose(material_pixels(normal,reference)[0,0],[.5,.5,1,.5])
        np.testing.assert_array_equal(material_pixels(normal,reference)[0,1],[0,0,0,0])

    def test_png_rows_and_srgb_not_applied_to_data(self):
        values = np.array([[[.5,.5,.5,1]],[[1,0,0,.5]]],dtype=np.float32)
        data = np.array(Image.open(io.BytesIO(png_bytes(values))))
        color = np.array(Image.open(io.BytesIO(png_bytes(values,srgb=True))))
        np.testing.assert_array_equal(data[0,0],[255,0,0,128])
        np.testing.assert_array_equal(data[1,0],[128,128,128,255])
        np.testing.assert_array_equal(color[1,0],[188,188,188,255])

    def test_missing_reference_mismatched_coverage_and_nonfinite_refused(self):
        reference = np.ones((1,1,4),dtype=np.float32)
        for invalid in (np.zeros((2,1,4)), np.full((1,1,4),np.nan), np.zeros((1,1,4))):
            with self.assertRaises(ValueError): material_pixels(invalid,reference)
        reference[0,0,0] = 0
        with self.assertRaises(ValueError): material_pixels(reference,reference)


if __name__ == '__main__':
    unittest.main()
