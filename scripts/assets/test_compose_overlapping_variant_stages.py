import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from compose_overlapping_variant_stages import build
from prepare_variant_metadata_transition import encoded, prepare_transition


class OverlapStageTests(unittest.TestCase):
    def test_merges_same_pack_without_reading_models_and_refuses_tampering(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            vault = root / 'vault'
            (vault / 'metadata').mkdir(parents=True)
            registry = vault / 'metadata/asset-import-registry.v1.json'
            registry.write_bytes(b'current registry')
            pin = hashlib.sha256(registry.read_bytes()).hexdigest()
            assets = [{'id': x, 'model_sha256': str(i) * 64, 'model_path': x + '.glb'}
                      for i, x in enumerate(('a', 'b', 'c', 'd'), 1)]
            packs = {'pack': {'details': {'pack_id': 'pack', 'assets': assets}, 'profiles':
                      {'profiles': [{'external_id': x['id'], 'model_content_sha256': x['model_sha256']} for x in assets]}}}
            sources = []
            for number, (keep, retired) in enumerate(((assets[0], assets[1]), (assets[2], assets[3]))):
                row = {'retained_id': keep['id'], 'retired_id': retired['id'],
                       'retained_uuid': 'u-' + keep['id'], 'retired_uuid': 'u-' + retired['id'],
                       'retained_sha256': keep['model_sha256'], 'retired_sha256': retired['model_sha256'],
                       'retained_path': keep['model_path'], 'retired_path': retired['model_path']}
                plan = {'schema': 'openrpg.variant-consolidation-plan.v1', 'registry_sha256': pin,
                        'candidate_count': 1, 'candidates': [row]}
                recipes = {'state': 'prepared_only', 'registry_sha256': pin, 'recipes': {
                    retired['id']: {'schema': 'openrpg.legacy-glb-recipe.v1', 'logical_sha256': retired['model_sha256']}}}
                proposal = prepare_transition(packs, [row], recipes['recipes'])
                stage = root / ('stage%d' % number)
                (stage / 'pack').mkdir(parents=True)
                docs, pins = proposal['packs']['pack'], {}
                for key, name in [('details', 'details.v1.json'), ('profiles', 'profiles.v1.json')]:
                    raw = encoded(docs[key]); (stage / 'pack' / name).write_bytes(raw)
                    pins[name] = hashlib.sha256(raw).hexdigest()
                pins['revision'] = hashlib.sha256(encoded({'details': docs['details'], 'profiles': docs['profiles']})).hexdigest()
                manifest = {'state': 'prepared_only', 'registry_sha256': pin, 'includes_legacy_recipes': True,
                            'aliases_requiring_project_migration': proposal['aliases_requiring_project_migration'],
                            'packs': {'pack': pins}}
                (stage / 'transition.json').write_bytes(encoded(manifest))
                sources.append((plan, recipes, stage))
            with patch('vault_storage.confined', side_effect=lambda vault, path: path), \
                 patch('compose_overlapping_variant_stages.load_packs', return_value=packs):
                plan, _, manifest = build(vault, sources, root / 'merged', root)
                self.assertEqual(plan['candidate_count'], 2)
                self.assertEqual(len(manifest['packs']), 1)
                (sources[0][2] / 'pack/details.v1.json').write_bytes(b'tampered')
                with self.assertRaises(ValueError):
                    build(vault, sources, root / 'refused', root)


if __name__ == '__main__':
    unittest.main()
