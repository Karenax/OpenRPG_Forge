import unittest
from audit_revision_chain import trace


class RevisionChainTests(unittest.TestCase):
    def test_walks_previous_without_treating_history_as_parent(self):
        a = {'transaction': 'a' * 32, 'revision': 'a', 'path': 'a'}
        b = {'transaction': 'b' * 32, 'revision': 'b', 'path': 'b', 'history': [{'revision': 'x'}]}
        events = {
            'a' * 32: {'pack_id': 'p', 'state': 'committed', 'operation': 'apply', 'next': a, 'previous': None},
            'b' * 32: {'pack_id': 'p', 'state': 'committed', 'operation': 'apply', 'next': b, 'previous': a},
        }
        revisions, transactions, issues = trace('p', b, events.__getitem__)
        self.assertEqual(revisions, {('a', 'a'), ('b', 'b')})
        self.assertEqual(len(transactions), 2)
        self.assertEqual(issues, [])

    def test_rejects_mismatch_and_bad_transaction(self):
        node = {'transaction': 'a' * 32}
        _, _, issues = trace('p', node, lambda _: {'pack_id': 'q', 'next': node, 'state': 'committed'})
        self.assertTrue(issues)

    def test_grouped_transaction_uses_correct_pack(self):
        previous = {'transaction': 'a' * 32, 'revision': 'a', 'path': 'a'}
        current = {'transaction': 'b' * 32, 'revision': 'b', 'path': 'b'}
        events = {
            'a' * 32: {'pack_id': 'p', 'state': 'committed', 'operation': 'apply', 'next': previous, 'previous': None},
            'b' * 32: {'transaction': 'b' * 32, 'state': 'committed', 'operation': 'apply-atomic-pack-pair',
                       'next_registry': {'packs': {'p': current}}, 'previous_registry': {'packs': {'p': previous}}},
        }
        revisions, _, issues = trace('p', current, events.__getitem__)
        self.assertEqual(revisions, {('a', 'a'), ('b', 'b')})
        self.assertFalse(issues)
        _, _, issues = trace('p', {'transaction': '../escape'}, lambda _: None)
        self.assertTrue(issues)


if __name__ == '__main__':
    unittest.main()
