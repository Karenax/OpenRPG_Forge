import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from rebase_variant_transition import verify_unchanged, write_rebased_stage
from prepare_variant_metadata_transition import encoded, prepare_transition, verify_stage
from test_prepare_variant_metadata_transition import fixture


def fixtures():
    keep = {'id': 'pack.keep', 'model_sha256': 'a' * 64, 'name': 'kept'}
    old = {'id': 'pack.old', 'model_sha256': 'b' * 64, 'name': 'old'}
    kept_profile = {'external_id': 'pack.keep', 'model_content_sha256': 'a' * 64}
    old_profile = {'external_id': 'pack.old', 'model_content_sha256': 'b' * 64}
    current = {'pack': {'details': {'pack_id': 'pack', 'assets': [keep, old]},
                        'profiles': {'profiles': [kept_profile, old_profile]}}}
    staged_keep = copy.deepcopy(keep)
    staged_keep['consolidated_variants'] = [{'retired_id': 'pack.old',
        'original_detail': copy.deepcopy(old), 'original_profile': copy.deepcopy(old_profile)}]
    staged = {'pack': {'details': {'pack_id': 'pack', 'assets': [staged_keep]},
                       'profiles': {'profiles': [copy.deepcopy(kept_profile)]}}}
    plan = {'candidates': [{'retained_id': 'pack.keep', 'retired_id': 'pack.old'}]}
    return plan, staged, current


class RebaseTests(unittest.TestCase):
    def test_rebuilds_stage_metadata_without_reading_model_files(self):
        packs, candidate = fixture()
        recipe = {'schema': 'openrpg.legacy-glb-recipe.v1', 'logical_sha256': 'b' * 64}
        recipes = {'variant.asset': recipe}
        original = prepare_transition(packs, [candidate], recipes)
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            stage = root / 'old'
            stage.mkdir()
            manifest = {'state': 'prepared_only', 'registry_sha256': 'old',
                        'includes_legacy_recipes': True,
                        'aliases_requiring_project_migration': original['aliases_requiring_project_migration'],
                        'packs': {}}
            for pack in original['changed_packs']:
                folder = stage / pack
                folder.mkdir()
                docs, pins = original['packs'][pack], {}
                for key, filename in [('details', 'details.v1.json'), ('profiles', 'profiles.v1.json')]:
                    raw = encoded(docs[key]); (folder / filename).write_bytes(raw)
                    pins[filename] = hashlib.sha256(raw).hexdigest()
                pins['revision'] = hashlib.sha256(encoded({'details': docs['details'], 'profiles': docs['profiles']})).hexdigest()
                manifest['packs'][pack] = pins
            (stage / 'transition.json').write_bytes(encoded(manifest))
            plan = {'registry_sha256': 'new', 'candidates': [candidate]}
            output = root / 'new'
            result = write_rebased_stage(stage, output, packs, plan, recipes)
            self.assertEqual(result['registry_sha256'], 'new')
            self.assertEqual(verify_stage(output, packs, plan, recipes)['aliases'], 1)
            (stage / 'main/details.v1.json').write_bytes(b'tampered')
            with self.assertRaisesRegex(ValueError, 'source stage metadata changed'):
                write_rebased_stage(stage, root / 'refused', packs, plan, recipes)

    def test_multiple_new_and_existing_aliases_are_preserved(self):
        plan, staged, current = fixtures()
        previous = {'retired_id': 'older', 'original_detail': {}, 'original_profile': {}}
        current['pack']['details']['assets'][0]['consolidated_variants'] = [copy.deepcopy(previous)]
        staged_keep = staged['pack']['details']['assets'][0]
        staged_keep['consolidated_variants'].insert(0, copy.deepcopy(previous))
        extra = {'id': 'pack.old2', 'model_sha256': 'c' * 64, 'name': 'second'}
        extra_profile = {'external_id': 'pack.old2', 'model_content_sha256': 'c' * 64}
        current['pack']['details']['assets'].append(extra)
        current['pack']['profiles']['profiles'].append(extra_profile)
        staged_keep['consolidated_variants'].append({'retired_id': 'pack.old2',
             'original_detail': copy.deepcopy(extra), 'original_profile': copy.deepcopy(extra_profile)})
        plan['candidates'].append({'retained_id': 'pack.keep', 'retired_id': 'pack.old2'})
        verify_unchanged(plan, staged, current)
        current['pack']['details']['assets'][-1]['name'] = 'changed'
        with self.assertRaises(ValueError):
            verify_unchanged(plan, staged, current)

    def test_accepts_unchanged_originals(self):
        verify_unchanged(*fixtures())

    def test_refuses_metadata_and_profile_changes(self):
        plan, staged, current = fixtures()
        current['pack']['details']['assets'][1]['name'] = 'changed'
        with self.assertRaises(ValueError):
            verify_unchanged(plan, staged, current)
        plan, staged, current = fixtures()
        current['pack']['profiles']['profiles'][0]['extra'] = 'changed'
        with self.assertRaises(ValueError):
            verify_unchanged(plan, staged, current)

    def test_refuses_missing_original(self):
        plan, staged, current = fixtures()
        current['pack']['details']['assets'].pop()
        current['pack']['profiles']['profiles'].pop()
        with self.assertRaises(ValueError):
            verify_unchanged(plan, staged, current)


if __name__ == '__main__':
    unittest.main()
