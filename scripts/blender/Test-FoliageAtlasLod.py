"""Data-level derivative checks; use bundled Python with Pillow/NumPy."""
import importlib.util
import io
from pathlib import Path
import unittest

import numpy as np
from PIL import Image

spec = importlib.util.spec_from_file_location('atlas_lod', Path(__file__).with_name('Build-FoliageAtlasLod.py'))
lod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(lod)


def png(pixels):
    data = io.BytesIO()
    Image.fromarray(pixels).save(data, format='PNG')
    return data.getvalue()


class AtlasLod(unittest.TestCase):
    def test_baked_normal_round_trip_through_exported_tbn(self):
        # Nonzero Y is essential: a flat normal cannot detect the handedness bug.
        local = np.array([.3,.4,np.sqrt(.75)])
        encoded = np.rint((local*.5+.5)*255).astype(np.uint8)
        pixel = np.array([[list(encoded)+[173]]], dtype=np.uint8)
        corrected = np.array(Image.open(io.BytesIO(lod.normal_png_to_card_basis(png(pixel),
            'linear_0.5N+0.5_card_right_up_front_facing_hemisphere'))))
        np.testing.assert_array_equal(corrected[:,:, [0,2,3]], pixel[:,:, [0,2,3]])
        for basis in (np.eye(3), np.array([[0,1,0],[0,0,1],[1,0,0]])):
            tile = {'center_blender_m':[0,0,0], 'basis_blender':basis.tolist(),
                    'card_size_m':2, 'content_xy':[4,4], 'content_size':56}
            _, normals, tangents, _, _ = lod.card_geometry({'layout':{'size':64},'tiles':[tile]})
            n, t = np.array(normals[0]), np.array(tangents[0])
            tbn = np.column_stack((t[:3],np.cross(n,t[:3])*t[3],n))
            expected = lod.gltf_vector(local @ basis)
            decoded = corrected[0,0,:3].astype(float)/255*2-1
            np.testing.assert_allclose(tbn @ decoded, expected, atol=2/255)
            old = tbn @ (encoded.astype(float)/255*2-1)
            self.assertGreater(np.linalg.norm(old-np.array(expected)), .79)

    def test_normal_basis_conversion_refuses_unknown_or_already_converted_input(self):
        for encoding in (None, 'tangent_space', 'view_right_minus_up_front'):
            with self.assertRaises(ValueError):
                lod.normal_png_to_card_basis(png(np.zeros((1,1,4),dtype=np.uint8)), encoding)

    def test_padding_preserves_coverage_and_source_rgb(self):
        before = np.zeros((64,64,4),dtype=np.uint8)
        before[32,32] = [22,120,50,255]
        before[32,33] = [90,40,30,100]
        result = lod.padded_png(png(before), [{'tile_xy':[0,0]}], 64)
        after = np.array(Image.open(io.BytesIO(result)))
        np.testing.assert_array_equal(before[:,:,3], after[:,:,3])
        np.testing.assert_array_equal(before[before[:,:,3]>0], after[before[:,:,3]>0])
        self.assertGreater(after[32,31,:3].sum(), 0)
        self.assertEqual(result, lod.padded_png(png(before), [{'tile_xy':[0,0]}], 64))

    def test_padding_cannot_cross_tiles(self):
        before = np.zeros((128,128,4),dtype=np.uint8)
        before[96,63] = [255,0,0,255]
        before[96,64] = [0,0,255,255]
        after = np.array(Image.open(io.BytesIO(lod.padded_png(png(before),
            [{'tile_xy':[0,0]},{'tile_xy':[64,0]}],64))))
        self.assertEqual(after[:,64:,0].sum(), 0)
        self.assertEqual(after[:,:64,2].sum(), 0)

    def test_axes_uv_orientation_winding_and_tangent(self):
        tile = {'center_blender_m':[1,2,3], 'basis_blender':[[1,0,0],[0,1,0],[0,0,1]],
                'card_size_m':2, 'content_xy':[4,4], 'content_size':56}
        report = {'layout':{'size':64},'tiles':[tile]}
        p,n,t,uv,i = lod.card_geometry(report)
        np.testing.assert_allclose(np.mean(p,axis=0), (1,3,-2))
        np.testing.assert_allclose(np.cross(np.subtract(p[1],p[0]),np.subtract(p[2],p[0])),
                                   np.asarray(n[0])*4)
        self.assertEqual(uv[0], (4/64,60/64))
        self.assertEqual(uv[2], (60/64,4/64))
        self.assertEqual(t[0], (1,0,0,-1))
        self.assertEqual(i,[0,1,2,0,2,3])

    def test_invalid_basis_refused(self):
        with self.assertRaises(ValueError):
            lod.card_geometry({'layout':{'size':64}, 'tiles':[{'center_blender_m':[0,0,0],
                'basis_blender':[[1,0,0],[0,1,0],[0,0,-1]]}]})

    def test_crop_keeps_faint_pixels_and_linear_filter_margin(self):
        pixels = np.zeros((64,64,4),dtype=np.uint8)
        pixels[34:38,20:26,3] = 1  # Do not discard faint edge pixels with a cutoff.
        tile = {'content_xy':[4,4],'content_size':56,'card_size_m':2}
        report = {'layout':{'size':64},'tiles':[tile]}
        stats = lod.crop_tiles(report,png(pixels))
        self.assertLess(stats['area_ratio'],.1)
        np.testing.assert_allclose(tile['uv_rect'],[15/56,21/56,23/56,27/56])

    def test_crop_preserves_world_to_uv_mapping(self):
        tile = {'center_blender_m':[1,2,3], 'basis_blender':[[1,0,0],[0,1,0],[0,0,1]],
                'card_size_m':2, 'content_xy':[4,4], 'content_size':56,'uv_rect':[.25,.125,.75,.875]}
        p,n,t,uv,i = lod.card_geometry({'layout':{'size':64},'tiles':[tile]})
        for position,coord in zip(p,uv):
            u = (coord[0]*64-4)/56
            v = ((1-coord[1])*64-4)/56
            np.testing.assert_allclose(position,lod.gltf_vector((1+2*(u-.5),2+2*(v-.5),3)))


if __name__ == '__main__':
    unittest.main()
