import unittest
from stage_source_support_relinks import replace


class RelinkTests(unittest.TestCase):
    def test_relinks_alias_without_changing_its_identity_or_unrelated_metadata(self):
        alias={'id':'old','source_prefab_file':'hdr/p.prefab','source_prefab_sha256':'pin','license':'private'}
        doc={'assets':[{'id':'keep','source_prefab_file':'urp/p.prefab','source_prefab_sha256':'pin',
                        'consolidated_sources':[{'retired_id':'old','original_detail':alias}]}]}
        mapping={'hdr/p.prefab':{'retained':'urp/p.prefab','sha256':'pin'}}
        with self.assertRaisesRegex(ValueError,'no supported'):replace(doc,mapping)
        out,counts,ids=replace(doc,mapping,include_aliases=True)
        self.assertEqual(out['assets'][0]['consolidated_sources'][0]['original_detail']['source_prefab_file'],'urp/p.prefab')
        self.assertEqual(out['assets'][0]['consolidated_sources'][0]['original_detail']['license'],'private')
        self.assertEqual((counts['hdr/p.prefab'],ids),(1,['keep']))
        self.assertEqual(alias['source_prefab_file'],'hdr/p.prefab')
        with self.assertRaisesRegex(ValueError,'SHA'):
            replace(doc,{'hdr/p.prefab':{'retained':'urp/p.prefab','sha256':'wrong'}},include_aliases=True)

    def test_archive_relink_preserves_provenance_and_rejects_mismatched_hash(self):
        doc={'assets':[{'id':'a','source_archive':'old/pack.unitypackage',
                        'source_archive_sha256':'digest','source_archive_bytes':17,'license':'private'}]}
        mapping={'old/pack.unitypackage':{'retained':'new/pack.unitypackage','sha256':'digest'}}
        updated,counts,ids=replace(doc,mapping)
        self.assertEqual(updated['assets'][0]['source_archive'],'new/pack.unitypackage')
        self.assertEqual(updated['assets'][0]['source_archive_bytes'],17)
        self.assertEqual(updated['assets'][0]['license'],'private')
        self.assertEqual((counts['old/pack.unitypackage'],ids),(1,['a']))
        with self.assertRaisesRegex(ValueError,'SHA'):
            replace(doc,{'old/pack.unitypackage':{'retained':'bad/pack.unitypackage','sha256':'wrong'}})

    def test_mesh_dependency_array_is_relinked_without_changing_other_fields(self):
        doc={'assets':[{'id':'a','source_mesh_files':['old/a.fbx','other/b.fbx'], 'license':'x'}]}
        out,counts,ids=replace(doc,{'old/a.fbx':{'retained':'new/a.fbx','sha256':'pin'}})
        self.assertEqual(out['assets'][0]['source_mesh_files'],['new/a.fbx','other/b.fbx'])
        self.assertEqual(doc['assets'][0]['source_mesh_files'][0],'old/a.fbx')
        self.assertEqual((counts['old/a.fbx'],ids),(1,['a']))

    def test_single_mesh_dependency_is_relinked(self):
        doc={'assets':[{'id':'a','source_mesh_file':'old/a.fbx'}]}
        out,counts,ids=replace(doc,{'old/a.fbx':{'retained':'new/a.fbx','sha256':'pin'}})
        self.assertEqual(out['assets'][0]['source_mesh_file'],'new/a.fbx')
        self.assertEqual((counts['old/a.fbx'],ids),(1,['a']))

    def test_relinks_only_matching_fields_and_preserves_other_metadata(self):
        doc = {'assets': [{'id': 'a', 'source_file': 'u/a', 'source_sha256': 'hash',
                           'source_asset_path': 'Assets/URP/a', 'license': 'x'}]}
        mapping = {'u/a': {'retained': 'h/a', 'sha256': 'hash'}}
        out, counts, ids = replace(doc, mapping)
        self.assertEqual(out['assets'][0]['source_file'], 'h/a')
        self.assertEqual(out['assets'][0]['source_asset_path'], 'Assets/URP/a')
        self.assertEqual(doc['assets'][0]['source_file'], 'u/a')
        self.assertEqual((counts['u/a'], ids), (1, ['a']))

    def test_refuses_changed_hash_and_unreferenced_candidate(self):
        doc = {'assets': [{'id': 'a', 'source_file': 'u/a', 'source_sha256': 'wrong'}]}
        with self.assertRaisesRegex(ValueError, 'SHA'):
            replace(doc, {'u/a': {'retained': 'h/a', 'sha256': 'hash'}})
        with self.assertRaisesRegex(ValueError, 'no supported'):
            replace(doc, {'u/b': {'retained': 'h/b', 'sha256': 'hash'}})

    def test_meta_relink_requires_its_own_hash(self):
        doc = {'assets': [{'id': 'a', 'source_meta_file': 'u/a.meta', 'source_meta_sha256': 'meta',
                           'source_sha256': 'different'}]}
        out, counts, _ = replace(doc, {'u/a.meta': {'retained': 'h/a.meta', 'sha256': 'meta'}})
        self.assertEqual(out['assets'][0]['source_meta_file'], 'h/a.meta')
        self.assertEqual(counts['u/a.meta'], 1)
        with self.assertRaisesRegex(ValueError, 'SHA'):
            replace(doc, {'u/a.meta': {'retained': 'h/a.meta', 'sha256': 'different'}})

    def test_preview_relink_checks_preview_hash(self):
        doc = {'assets': [{'id': 'a', 'preview': 'u/a.png', 'preview_sha256': 'image'}]}
        out, _, _ = replace(doc, {'u/a.png': {'retained': 'h/a.png', 'sha256': 'image'}})
        self.assertEqual(out['assets'][0]['preview'], 'h/a.png')
        with self.assertRaisesRegex(ValueError, 'SHA'):
            replace(doc, {'u/a.png': {'retained': 'h/a.png', 'sha256': 'other'}})

    def test_diagnostic_reference_is_relinked_only_with_matching_hash(self):
        doc = {'assets': [{'id': 'a', 'evidence': {'diagnostic_preview_reference': 'u/a.png',
                    'diagnostic_preview_reference_sha256': 'image', 'verdict': 'unchanged'}}]}
        out, counts, _ = replace(doc, {'u/a.png': {'retained': 'h/a.png', 'sha256': 'image'}})
        self.assertEqual(out['assets'][0]['evidence']['diagnostic_preview_reference'], 'h/a.png')
        self.assertEqual(out['assets'][0]['evidence']['verdict'], 'unchanged')
        self.assertEqual(counts['u/a.png'], 1)
        with self.assertRaisesRegex(ValueError, 'diagnostic preview SHA'):
            replace(doc, {'u/a.png': {'retained': 'h/a.png', 'sha256': 'different'}})


if __name__ == '__main__':
    unittest.main()
