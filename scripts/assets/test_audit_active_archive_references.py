import unittest
from pathlib import Path
from audit_active_archive_references import matching_targets, catalog_references


class ReferencesTests(unittest.TestCase):
    def test_collects_catalogs_from_nested_original_details(self):
        doc={'assets':[{'source_catalog':'current.json','consolidated_variants':[
            {'original_detail':{'source_catalog':'historic.json'}}]}]}
        self.assertEqual(set(catalog_references(doc)),{'current.json','historic.json'})
    def test_matches_catalog_and_vault_relative_paths(self):
        vault=Path('F:/Vault');doc=vault/'normalized/pack/catalog.json'
        source=vault/'normalized/pack/sources/a.fbx'
        targets={source.as_posix().casefold():'found'}
        self.assertEqual(matching_targets('sources/a.fbx',doc,vault,targets),{'found'})
        self.assertEqual(matching_targets('normalized/pack/sources/a.fbx',doc,vault,targets),{'found'})
        self.assertEqual(matching_targets('../pack/sources/a.fbx',doc,vault,targets),{'found'})
        nested=vault/'normalized/pack/sources/source-closure.v1.json'
        self.assertEqual(matching_targets('sources/a.fbx',nested,vault,targets),{'found'})
        self.assertEqual(matching_targets('sources/a.fbx',nested,vault,targets,('.fbx',)),{'found'})
        self.assertEqual(matching_targets('not a file',nested,vault,targets,('.fbx',)),set())
        self.assertEqual(matching_targets('../../../outside/a.fbx',doc,vault,targets),set())


if __name__=='__main__':unittest.main()
