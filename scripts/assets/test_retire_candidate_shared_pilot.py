import unittest

from retire_candidate_shared_pilot import NAME, PIN, SIZE, selected_rows


def plan():
    root = 'candidates/hivemind/'
    keep = root + 'keep/dependencies/' + NAME
    return {'schema': 'openrpg.candidate-dependency-sharing-plan.v1',
            'rows': [{'old': root + 'lot%d/dependencies/' % n + NAME,
                      'retained': keep, 'sha256': PIN, 'bytes': SIZE} for n in range(10)]}


class SelectionTests(unittest.TestCase):
    def test_expected_selection(self):
        self.assertEqual(len(selected_rows(plan())), 10)

    def test_refuses_changed_content(self):
        data = plan()
        data['rows'][0]['sha256'] = '0' * 64
        with self.assertRaises(ValueError):
            selected_rows(data)

    def test_refuses_delete_of_retained(self):
        data = plan()
        for row in data['rows']:
            row['retained'] = data['rows'][0]['old']
        with self.assertRaises(ValueError):
            selected_rows(data)

    def test_refuses_traversal(self):
        data = plan()
        data['rows'][0]['old'] = data['rows'][0]['old'].replace('/lot0/', '/../')
        with self.assertRaises(ValueError):
            selected_rows(data)


if __name__ == '__main__':
    unittest.main()
