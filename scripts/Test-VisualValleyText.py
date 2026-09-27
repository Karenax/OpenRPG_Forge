"""Synthetic inventory guards, no asset or legal qualification."""
import importlib.util
import json
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('audit', Path(__file__).with_name('Audit-VisualValleyText.py'))
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class InventoryTests(unittest.TestCase):
    def test_nested_strings_and_json_pointer_escaping(self):
        values = list(audit.strings({'a/b': [{'~key': 'hello'}, 42, False, None]}))
        self.assertEqual(values, [{'pointer': '/a~1b/0/~0key', 'text': 'hello'}])

    def test_duplicate_keys_cannot_hide_text(self):
        with self.assertRaises(ValueError):
            json.loads('{"name":"first","name":"second"}', object_pairs_hook=audit.unique_object)

    def test_unicode_and_case_mentions_are_detected(self):
        self.assertEqual(audit.reference_mentions('ＣＡＤＬＥ Humpalumps'), ['cadle', 'humpalumps'])

    def test_reference_mention_is_not_an_infringement_verdict(self):
        self.assertEqual(audit.reference_mentions('Do not copy Cadle assets'), ['cadle'])
        self.assertEqual(audit.reference_mentions('independent technical rock'), [])


if __name__ == '__main__':
    unittest.main()
