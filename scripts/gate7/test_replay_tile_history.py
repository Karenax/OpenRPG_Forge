import copy
import unittest

from replay_tile_history import Cache, cache_hash, replay


def node(index):
    return [index, 0, 8, 0]


class TileCacheReplay(unittest.TestCase):
    def test_overflow_retains_active_and_reuses_old_tile_in_larger_cache(self):
        initial = [[node(i), 1 if i == 0 else 0, True] for i in range(256)]
        small = Cache(256, initial, 1, node(0))
        large = Cache(384, initial, 1, node(0))
        for cache in (small, large):
            cache.select([tuple(node(0)), tuple(node(256))], False)
            cache.select([tuple(node(0)), tuple(node(1))], False)
        self.assertEqual(small.misses, 2)
        self.assertEqual(large.misses, 1)
        self.assertEqual(len(small.entries), 256)
        self.assertIn(tuple(node(0)), small.entries)
        self.assertIn(tuple(node(1)), small.entries)
        self.assertEqual(small.evictions, 2)

    def test_replay_checks_actual_cache_before_trusting_comparison(self):
        event = {'material': 'surface', 'root': 'root', 'root_changed': False,
                 'frame': 10, 'prior_generation': 0, 'prior_root_key': node(0),
                 'initial_cache': [[node(0), 0, True]],
                 'prior_cache_sha256': cache_hash({tuple(node(0)): (0, True)}),
                 'nodes': [node(0), node(1)]}
        result = replay([event])
        self.assertEqual(result[0]['capacities']['256']['new_meshes'], 1)
        corrupt = copy.deepcopy(event)
        corrupt['prior_cache_sha256'] = '0' * 64
        with self.assertRaises(ValueError):
            replay([corrupt])
        duplicate = copy.deepcopy(event)
        duplicate['nodes'].append(node(1))
        with self.assertRaises(ValueError):
            replay([duplicate])


if __name__ == '__main__':
    unittest.main()
