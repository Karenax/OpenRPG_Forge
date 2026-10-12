"""Exercise rendering diagnostics with loss, duplication and coherent corruption."""
import copy
import json
from pathlib import Path
import runpy
import tempfile
import unittest

import numpy as np
from render_verification import (coverage_diagnostics, reconcile_source_ids,
                                 standard_depth_diagnostics, verify_position_roundtrip,
                                 verify_texture_mips)


class SourceCoverageTests(unittest.TestCase):
    def test_exact_identity_and_reasoned_exclusion(self):
        result = reconcile_source_ids(['a', 'b', 'helper'], ['b', 'a'],
                                      {'helper': 'source collision-only helper'})
        self.assertEqual(result['imported_ids'], 2)
        self.assertEqual(result['excluded_ids'], 1)

    def test_same_count_cannot_hide_replaced_source(self):
        with self.assertRaisesRegex(ValueError, "missing=.*b.*unexpected=.*c"):
            reconcile_source_ids(['a', 'b'], ['a', 'c'])

    def test_duplicates_invalid_ids_and_unaudited_omissions_rejected(self):
        for source, imported, excluded in [(['a', 'a'], ['a'], None),
                (['a'], ['a', 'a'], None), (['a', ''], ['a'], None),
                (['a'], [], {'a': ''}), (['a'], ['a'], {'a': 'helper'}),
                (['a'], [], {'b': 'helper'}), (['a'], [], None)]:
            with self.subTest(source=source, imported=imported, excluded=excluded):
                with self.assertRaises(ValueError):
                    reconcile_source_ids(source, imported, excluded)


class MipTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.mips = []
        for level, (width, height) in enumerate([(4, 2), (2, 1), (1, 1)]):
            name = f'mip-{level}.bin'
            (self.root / name).write_bytes(bytes(width * height * 4))
            self.mips.append(dict(level=level, width=width, height=height, path=name))
        self.paths = {m['path'] for m in self.mips}

    def test_complete_rectangular_chain(self):
        self.assertEqual(verify_texture_mips(self.root, self.mips, self.paths),
                         dict(width=4, height=2, mips=3, bytes=44))

    def test_missing_reordered_reused_or_truncated_mip_refused(self):
        for change in ('missing', 'order', 'reuse', 'dimension', 'manifest', 'bool'):
            mips = copy.deepcopy(self.mips)
            paths = self.paths.copy()
            if change == 'missing': mips.pop()
            if change == 'order': mips[1]['level'] = 2
            if change == 'reuse': mips[2]['path'] = mips[1]['path']
            if change == 'dimension': mips[1]['width'] = 4
            if change == 'manifest': paths.remove('mip-1.bin')
            if change == 'bool': mips[0]['level'] = False
            with self.subTest(change=change), self.assertRaises(ValueError):
                verify_texture_mips(self.root, mips, paths)
        (self.root / 'mip-2.bin').write_bytes(b'123')
        with self.assertRaisesRegex(ValueError, 'truncated'):
            verify_texture_mips(self.root, self.mips, self.paths)

    def test_path_escape_and_alias_refused(self):
        for name in ('../outside.bin', '/absolute.bin', 'C:/outside.bin',
                     'dir\\file.bin', './mip-0.bin', 'dir/../mip-0.bin'):
            mips = copy.deepcopy(self.mips)
            mips[0]['path'] = name
            with self.subTest(name=name), self.assertRaisesRegex(ValueError, 'unsafe'):
                verify_texture_mips(self.root, mips, self.paths | {name})

    def test_existing_village_verifier_uses_mip_reference_check(self):
        verify = runpy.run_path(str(Path(__file__).parent / 'tidewater/verify-village.py'))['validate_village']
        village = self.root / 'village'
        village.mkdir()
        attributes = {}
        paths = self.paths.copy()
        for name, arity in [('position', 3), ('normal', 3), ('uv', 2), ('tint', 3), ('vdata', 4)]:
            path = f'{name}.bin'
            (self.root / path).write_bytes(np.zeros((3, arity), dtype='<f4').tobytes())
            paths.add(path)
            attributes[name] = dict(path=path, count=3, item_size=arity)
        (self.root / 'indices.bin').write_bytes(np.array([0, 1, 2], dtype='<u4').tobytes())
        paths.add('indices.bin')
        (self.root / 'code.wgsl').write_text('source reference only', encoding='utf-8')
        paths.add('code.wgsl')
        (self.root / 'mip-0.bin').write_bytes(bytes(range(32)))
        matrix = np.eye(4).reshape(16).tolist()
        scene = dict(schema='forge.tidewater.village-render', version=1,
            nodes=[dict(source_id=f'tidewater:1438b1ab:village:{i}',
                        matrix_world=matrix, geometry='g', material='m0') for i in range(31)],
            geometries={'g': dict(attributes=attributes, indices=dict(path='indices.bin'),
                                 triangles=1, source_draw_range=dict(count=3))},
            materials=[dict(id=f'm{i}', side='front', bindings={'colour':dict(texture='t')},
                            modules=['s']) for i in range(7)],
            modules={'s': dict(code='code.wgsl')}, omitted=[dict(name='village_fish')])
        (village / 'render.json').write_text(json.dumps(scene), encoding='utf-8')
        textures = {'textures': {'t': dict(format='rgba8unorm', mips=self.mips)}}
        path = village / 'textures.json'
        path.write_text(json.dumps(textures), encoding='utf-8')
        verify(self.root, paths)
        textures['textures']['t']['mips'][0]['path'] = '../outside.bin'
        path.write_text(json.dumps(textures), encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'unsafe'):
            verify(self.root, paths | {'../outside.bin'})


class PositionTests(unittest.TestCase):
    def test_rotation_nonuniform_scale_and_mirror_preserved(self):
        local = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]
        matrix = [[0, -3, 0, 10], [2, 0, 0, 20], [0, 0, -4, 30], [0, 0, 0, 1]]
        result = verify_position_roundtrip(local, [[10, 22, 30], [7, 20, 30], [10, 20, 26]],
                                           matrix, 0)
        self.assertEqual(result['max_distance_m'], 0)

    def test_diagonal_displacement_uses_metres_not_component_tolerance(self):
        with self.assertRaisesRegex(ValueError, 'vertex 0 moved'):
            verify_position_roundtrip([[0, 0, 0]], [[.00009, .00009, .00009]], np.eye(4), .0001)

    def test_invalid_or_overflowing_geometry_refused(self):
        for local, source, matrix, tolerance in [([], [], np.eye(4), .1),
                ([[0, 0, 0]], [[float('nan'), 0, 0]], np.eye(4), .1),
                ([[0, 0, 0]], [[0, 0, 0]], np.zeros((4, 4)), .1),
                ([[0, 0, 0]], [[0, 0, 0]], np.eye(4), -1),
                ([[0, 0, 0]], [[0, 0, 0]], np.eye(4), float('inf')),
                ([[1e308, 0, 0]], [[-1e308, 0, 0]], np.eye(4), .1)]:
            with self.subTest(tolerance=tolerance), self.assertRaises(ValueError):
                verify_position_roundtrip(local, source, matrix, tolerance)


class PixelTests(unittest.TestCase):
    def test_directional_loss_and_addition_have_distinct_locations(self):
        reference = np.zeros((7, 7), dtype=bool)
        candidate = reference.copy()
        reference[1, 1] = True
        candidate[5, 5] = True
        result = coverage_diagnostics(reference, candidate)
        self.assertEqual(result['missing']['bbox_xyxy_inclusive'], [1, 1, 1, 1])
        self.assertEqual(result['additional']['bbox_xyxy_inclusive'], [5, 5, 5, 5])
        self.assertEqual(result['missing_beyond_one_pixel']['pixels'], 1)
        self.assertEqual(result['additional_beyond_one_pixel']['pixels'], 1)
        self.assertEqual(result['intersection_over_union'], 0)
        self.assertFalse(result['render_quality_qualified'])

    def test_one_pixel_edge_difference_is_still_reported(self):
        a = np.array([[False, True, False]], dtype=bool)
        b = np.array([[False, False, True]], dtype=bool)
        result = coverage_diagnostics(a, b)
        self.assertEqual(result['missing']['pixels'], 1)
        self.assertEqual(result['additional']['pixels'], 1)
        self.assertEqual(result['missing_beyond_one_pixel']['pixels'], 0)
        self.assertEqual(result['additional_beyond_one_pixel']['pixels'], 0)

    def test_blank_pair_is_no_evidence_and_has_no_perfect_iou(self):
        result = standard_depth_diagnostics(np.ones((2, 2)), np.ones((2, 2)))
        self.assertIn('empty_reference', result['status'])
        self.assertIsNone(result['intersection_over_union'])
        self.assertIsNone(result['shared_depth']['max_absolute_error'])
        json.dumps(result, allow_nan=False)

    def test_shared_depth_is_separate_from_missing_casters(self):
        result = standard_depth_diagnostics([[.2, .3, 1]], [[.1, 1, .4]])
        self.assertEqual(result['missing']['pixels'], 1)
        self.assertEqual(result['additional']['pixels'], 1)
        self.assertAlmostEqual(result['shared_depth']['max_absolute_error'], .1)
        self.assertEqual(result['shared_depth']['candidate_nearer_pixels'], 1)
        self.assertEqual(result['shared_depth']['candidate_farther_pixels'], 0)
        self.assertIn('not_metres', result['shared_depth']['units'])

    def test_nonfinite_wrong_shape_and_non_boolean_masks_refused(self):
        for a, b in [([[float('nan')]], [[0]]), ([[1.1]], [[0]]), ([[0]], [[0, 1]])]:
            with self.subTest(a=a), self.assertRaises(ValueError):
                standard_depth_diagnostics(a, b)
        with self.assertRaises(ValueError):
            coverage_diagnostics([[0]], [[0]])


if __name__ == '__main__':
    unittest.main()
