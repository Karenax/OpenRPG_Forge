import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from prepare_variant_metadata_transition import encoded, prepare_transition, verify_stage


def fixture():
    def docs(pack, ident, sha, path):
        return {'details': {'pack_id': pack, 'assets': [{'id': ident, 'model_sha256': sha,
                 'model_path': path, 'license': 'LicenseRef-private', 'tags': [pack]}],
                 'duplicate_groups': []}, 'profiles': {'profiles': [{'external_id': ident,
                 'model_content_sha256': sha, 'geometry': {'pivot': 'source'}}]}}
    packs = {'main': docs('main', 'main.asset', 'a' * 64, 'main.glb'),
             'variant': docs('variant', 'variant.asset', 'b' * 64, 'variant.glb')}
    candidate = {'retained_id': 'main.asset', 'retired_id': 'variant.asset',
                 'retained_sha256': 'a' * 64, 'retired_sha256': 'b' * 64,
                 'retained_path': 'main.glb', 'retired_path': 'variant.glb',
                 'retained_uuid': 'uuid-a', 'retired_uuid': 'uuid-b'}
    return packs, candidate


class MetadataTransitionTests(unittest.TestCase):
    def test_multiple_variants_retain_one_card_and_both_histories(self):
        packs, candidate = fixture()
        extra = copy.deepcopy(packs['variant'])
        extra['details']['pack_id'] = 'variant2'
        extra['details']['assets'][0].update(id='variant2.asset', model_sha256='c' * 64, model_path='variant2.glb')
        extra['profiles']['profiles'][0].update(external_id='variant2.asset', model_content_sha256='c' * 64)
        packs['variant2'] = extra
        second = {**candidate, 'retired_id': 'variant2.asset', 'retired_sha256': 'c' * 64,
                  'retired_path': 'variant2.glb', 'retired_uuid': 'uuid-c'}
        result = prepare_transition(packs, [candidate, second])
        retained = result['packs']['main']['details']['assets'][0]
        self.assertEqual([v['retired_id'] for v in retained['consolidated_variants']],
                         ['variant.asset', 'variant2.asset'])
        self.assertEqual(result['packs']['variant']['details']['assets'], [])
        self.assertEqual(result['packs']['variant2']['details']['assets'], [])
        with self.assertRaisesRegex(ValueError, 'overlapping'):
            prepare_transition(packs, [candidate, {**second, 'retained_id': 'variant.asset'}])

    def test_preserves_metadata_and_original_snapshots(self):
        packs, candidate = fixture()
        original = copy.deepcopy(packs)
        out = prepare_transition(packs, [candidate])
        self.assertEqual(packs, original)
        self.assertEqual(out['state'], 'prepared_only')
        retained = out['packs']['main']['details']['assets'][0]
        preserved = retained['consolidated_variants'][0]
        self.assertEqual(preserved['original_detail'], original['variant']['details']['assets'][0])
        self.assertEqual(preserved['original_profile'], original['variant']['profiles']['profiles'][0])
        self.assertEqual(out['packs']['variant']['details']['assets'], [])
        self.assertEqual(out['packs']['variant']['profiles']['profiles'], [])
        self.assertEqual(out['aliases_requiring_project_migration'][0]['retired_sha256'], 'b' * 64)

    def test_refuses_changed_pin_and_overlapping_candidates(self):
        packs, candidate = fixture()
        with self.assertRaisesRegex(ValueError, 'SHA changed'):
            prepare_transition(packs, [{**candidate, 'retired_sha256': 'c' * 64}])
        with self.assertRaisesRegex(ValueError, 'overlapping'):
            prepare_transition(packs, [candidate, candidate])

    def test_merge_keeps_existing_alias_and_refuses_retiring_nested_aliases(self):
        packs, candidate = fixture()
        old = {'retired_id': 'older.asset', 'legacy_recipe': {'logical_sha256': 'c' * 64}}
        packs['main']['details']['assets'][0]['consolidated_variants'] = [old]
        result = prepare_transition(packs, [candidate])
        variants = result['packs']['main']['details']['assets'][0]['consolidated_variants']
        self.assertEqual(variants[0], old)
        self.assertEqual(variants[1]['retired_id'], 'variant.asset')
        self.assertEqual(packs['main']['details']['assets'][0]['consolidated_variants'], [old])
        packs['variant']['details']['assets'][0]['consolidated_variants'] = [old]
        with self.assertRaisesRegex(ValueError, 'retiring an asset'):
            prepare_transition(packs, [candidate])

    def test_requires_matching_recipe_for_every_legacy_identity(self):
        packs, candidate = fixture()
        recipe = {'schema': 'openrpg.legacy-glb-recipe.v1', 'logical_sha256': 'b' * 64}
        result = prepare_transition(packs, [candidate], {'variant.asset': recipe})
        saved = result['packs']['main']['details']['assets'][0]['consolidated_variants'][0]
        self.assertEqual(saved['legacy_recipe'], recipe)
        with self.assertRaisesRegex(ValueError, 'missing or mismatched'):
            prepare_transition(packs, [candidate], {})
        with self.assertRaisesRegex(ValueError, 'does not match candidates'):
            prepare_transition(packs, [candidate], {'variant.asset': recipe, 'extra': recipe})

    def test_only_explicitly_pinned_independent_aliases_can_move(self):
        packs, candidate = fixture()
        old = {'retired_id': 'older.asset', 'retired_model_sha256': 'c' * 64,
               'legacy_recipe': {'schema': 'openrpg.legacy-vault-descriptor.v1', 'logical_sha256': 'c' * 64}}
        packs['variant']['details']['assets'][0]['consolidated_variants'] = [old]
        pin = {'retired_id': old['retired_id'], 'metadata_sha256': hashlib.sha256(encoded(old)).hexdigest()}
        candidate['independent_aliases_to_migrate'] = [pin]
        result = prepare_transition(packs, [candidate])
        aliases = result['packs']['main']['details']['assets'][0]['consolidated_variants']
        self.assertEqual(aliases[0], old)
        self.assertEqual(aliases[1]['retired_id'], 'variant.asset')
        old['legacy_recipe']['schema'] = 'openrpg.legacy-glb-recipe.v1'
        candidate['independent_aliases_to_migrate'][0]['metadata_sha256'] = hashlib.sha256(encoded(old)).hexdigest()
        with self.assertRaisesRegex(ValueError, 'independent alias migration'):
            prepare_transition(packs, [candidate])

    def test_reopens_stage_and_rejects_modified_metadata(self):
        packs, candidate = fixture()
        plan = {'registry_sha256': 'f' * 64, 'candidates': [candidate]}
        proposed = prepare_transition(packs, plan['candidates'])
        with tempfile.TemporaryDirectory() as temp:
            stage = Path(temp)
            manifest = {'state': 'prepared_only', 'registry_sha256': plan['registry_sha256'],
                        'aliases_requiring_project_migration': proposed['aliases_requiring_project_migration'],
                        'packs': {}}
            for pack in proposed['changed_packs']:
                (stage / pack).mkdir()
                docs, info = proposed['packs'][pack], {}
                for key, filename in (('details', 'details.v1.json'), ('profiles', 'profiles.v1.json')):
                    raw = encoded(docs[key])
                    (stage / pack / filename).write_bytes(raw)
                    info[filename] = hashlib.sha256(raw).hexdigest()
                info['revision'] = hashlib.sha256(encoded({'details': docs['details'], 'profiles': docs['profiles']})).hexdigest()
                manifest['packs'][pack] = info
            (stage / 'transition.json').write_text(json.dumps(manifest), encoding='utf8')
            self.assertEqual(verify_stage(stage, packs, plan)['aliases'], 1)
            (stage / 'main/details.v1.json').write_text('{}', encoding='utf8')
            with self.assertRaisesRegex(ValueError, 'SHA changed'):
                verify_stage(stage, packs, plan)


if __name__ == '__main__':
    unittest.main()
