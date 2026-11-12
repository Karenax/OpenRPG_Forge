import unittest
from compose_variant_transitions import compose


def sample(keep, retired, pin='a' * 64):
    row = {'retained_id': keep, 'retired_id': retired, 'retired_sha256': 'b' * 64,
           'retained_uuid': 'uuid-' + keep, 'retained_sha256': 'c' * 64,
           'retained_path': keep + '.glb'}
    return ({'schema': 'openrpg.variant-consolidation-plan.v1', 'registry_sha256': pin,
             'candidate_count': 1, 'candidates': [row]},
            {'state': 'prepared_only', 'registry_sha256': pin,
             'recipes': {retired: {'logical_sha256': 'b' * 64}}})


class ComposeTests(unittest.TestCase):
    def test_one_retained_model_can_have_two_legacy_variants(self):
        plan, recipes = compose([sample('a', 'b'), sample('a', 'c')])
        self.assertEqual(plan['candidate_count'], 2)
        self.assertEqual(set(recipes['recipes']), {'b', 'c'})
        changed = sample('a', 'c')
        changed[0]['candidates'][0]['retained_sha256'] = 'd' * 64
        with self.assertRaises(ValueError):
            compose([sample('a', 'b'), changed])

    def test_merges_disjoint_plans(self):
        plan, recipes = compose([sample('a', 'b'), sample('c', 'd')])
        self.assertEqual(plan['candidate_count'], 2)
        self.assertEqual(set(recipes['recipes']), {'b', 'd'})

    def test_rejects_overlaps_and_stale_pins(self):
        with self.assertRaises(ValueError):
            compose([sample('a', 'b'), sample('b', 'c')])
        with self.assertRaises(ValueError):
            compose([sample('a', 'b'), sample('c', 'd', 'f' * 64)])

    def test_rejects_recipe_sha_mismatch(self):
        plan, recipes = sample('a', 'b')
        recipes['recipes']['b']['logical_sha256'] = '0' * 64
        with self.assertRaises(ValueError):
            compose([(plan, recipes)])


if __name__ == '__main__':
    unittest.main()
