"""CPU-only bounds and mapping checks; no rendering qualification implied."""
import unittest
from foliage_atlas_layout import atlas_layout


class Layout(unittest.TestCase):
    def test_real_tree_counts_and_memory_bound(self):
        for count in (271, 130):
            plan = atlas_layout(count)
            self.assertEqual(plan['size'], 2048)
            self.assertEqual(len(plan['tiles']), count * 3)
            self.assertEqual(plan['rgba8_full_mips_bytes_per_map'], 22369620)
            self.assertEqual(plan, atlas_layout(count))

    def test_no_overlap_and_source_mapping(self):
        plan = atlas_layout(271)
        occupied = set()
        for i, tile in enumerate(plan['tiles']):
            xy = tuple(tile['tile_xy'])
            self.assertNotIn(xy, occupied)
            occupied.add(xy)
            self.assertEqual((tile['group'], tile['axis']), (i // 3, 'xyz'[i % 3]))
            self.assertTrue(all(0 <= c <= plan['size'] - 64 for c in xy))
            self.assertEqual(tile['content_size'], 56)

    def test_budget_and_invalid_input_refusal(self):
        for count in (0, -1, True, 1.5, 342):
            with self.assertRaises(ValueError): atlas_layout(count)
        for kwargs in ({'tile_size': 63}, {'gutter': 0}, {'gutter': 32},
                       {'max_size': 1000}, {'max_size': 32}):
            with self.assertRaises(ValueError): atlas_layout(1, **kwargs)


if __name__ == '__main__':
    unittest.main()
