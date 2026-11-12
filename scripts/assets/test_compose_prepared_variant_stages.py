import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from compose_prepared_variant_stages import compose


def stage(root, name, pack, alias):
    path = root / name
    (path / pack).mkdir(parents=True)
    pins = {'revision': 'f' * 64}
    for filename in ('details.v1.json', 'profiles.v1.json'):
        raw = (pack + filename).encode()
        (path / pack / filename).write_bytes(raw)
        pins[filename] = hashlib.sha256(raw).hexdigest()
    (path / 'transition.json').write_text(json.dumps({'state': 'prepared_only',
        'includes_legacy_recipes': True, 'registry_sha256': 'a' * 64,
        'aliases_requiring_project_migration': [alias], 'packs': {pack: pins}}))
    return path


def alias(keep, retired):
    return {'retained_id': keep, 'retired_id': retired, 'retained_uuid': keep + '-uuid',
            'retired_uuid': retired + '-uuid', 'retained_sha256': 'b' * 64, 'retired_sha256': 'c' * 64}


class StageCompositionTests(unittest.TestCase):
    def test_composes_disjoint_packs_and_checks_content(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            a, b = alias('one', 'old-one'), alias('two', 'old-two')
            first, second = stage(root, 'first', 'pack-one', a), stage(root, 'second', 'pack-two', b)
            plan = {'registry_sha256': 'a' * 64, 'candidate_count': 2, 'candidates': [a, b]}
            result = compose([first, second], plan, root / 'combined', root)
            self.assertEqual(len(result['packs']), 2)
            self.assertEqual((root / 'combined/pack-one/details.v1.json').read_bytes(), b'pack-onedetails.v1.json')
            (second / 'pack-two/details.v1.json').write_bytes(b'tampered')
            with self.assertRaises(ValueError):
                compose([first, second], plan, root / 'bad', root)
            self.assertFalse((root / 'bad').exists())

    def test_rejects_overlapping_pack(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            a, b = alias('one', 'old-one'), alias('two', 'old-two')
            first, second = stage(root, 'first', 'same', a), stage(root, 'second', 'same', b)
            with self.assertRaises(ValueError):
                compose([first, second], {'registry_sha256': 'a' * 64, 'candidate_count': 2,
                                         'candidates': [a, b]}, root / 'combined', root)


if __name__ == '__main__':
    unittest.main()
