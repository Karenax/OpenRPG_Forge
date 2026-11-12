import importlib.util
from pathlib import Path
import sys
import tempfile
from unittest.mock import patch
import unittest

sys.path.insert(0, str(Path(r'E:\CodexData\skills\openrpg-import-assets\scripts')))
ROOT = Path(r'E:\OpenRPG_Forge\.build\asset-conversion')


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT/filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


source = load('source_support_alias_candidate', 'source-support-import-alias-candidate-20261112.py')
models = load('asset_import_source_alias_candidate', 'asset-import-source-alias-candidate-20261112.py')


def catalog():
    old = {'id':'p.old','availability':'source_support','source_sha256':'a',
           'source_meta_sha256':'b','preview_sha256':'c'}
    keep = {'id':'p.keep','availability':'source_support','source_sha256':'a',
            'source_meta_sha256':'b','preview_sha256':'c',
            'consolidated_sources':[{'retired_id':'p.old','original_detail':old}]}
    return {'assets':[keep]}


class AliasGuardTests(unittest.TestCase):
    def test_both_importers_protect_old_identity(self):
        doc = catalog()
        self.assertEqual(source.source_alias_ids(doc, set()), {'p.old'})
        self.assertEqual(models.source_support_identity_set(doc), {'p.old','p.keep'})
        with self.assertRaisesRegex(ValueError, 'conflicting'):
            source.source_alias_ids(doc, {'p.old'})

    def test_rejects_duplicate_and_forged_alias(self):
        doc = catalog()
        doc['assets'][0]['consolidated_sources'].append(doc['assets'][0]['consolidated_sources'][0])
        with self.assertRaises(ValueError):
            source.source_alias_ids(doc, set())
        with self.assertRaises(ValueError):
            models.source_support_identity_set(doc)
        doc = catalog()
        doc['assets'][0]['consolidated_sources'][0]['original_detail']['source_sha256'] = 'changed'
        with self.assertRaises(ValueError):
            source.source_alias_ids(doc, set())

    def test_model_prepare_rejects_retired_source_identity(self):
        from test_asset_import import glb
        with tempfile.TemporaryDirectory() as temp:
            vault = Path(temp).resolve()
            mesh = vault/'model.glb'
            glb(mesh)
            models.atomic(vault/'catalog.json', {'id':'p', 'license':'x', 'assets':[
                {'id':'p.old','name':'x','model':'model.glb','sha256':models.digest(mesh)}]})
            models.atomic(vault/'annotations.json', {'schema_version':1,'pack_id':'p','assets':{'p.old':{}}})
            support = vault/'metadata/support/source-support.v1.json'
            models.atomic(support, {'schema_version':1, **catalog()})
            pin = models.digest(support)
            models.atomic(vault/'metadata/asset-source-support-registry.v1.json',
                          {'schema_version':1,'path':'metadata/support/source-support.v1.json','sha256':pin,'revision':pin})
            with self.assertRaisesRegex(ValueError, 'Source-support ID collision'):
                models.prepare(vault, vault/'catalog.json', vault/'annotations.json', vault/'stage')
            self.assertFalse((vault/'stage').exists())

    def test_source_prepare_rejects_retired_source_identity(self):
        import json
        from types import SimpleNamespace
        with tempfile.TemporaryDirectory() as temp:
            vault = Path(temp).resolve()
            incoming = vault/'incoming.json'
            incoming.write_text(json.dumps({'assets':[{'id':'p.old'}]}), encoding='utf8')
            args = SimpleNamespace(vault=str(vault), candidates=str(incoming), stage=str(vault/'stage'))
            with patch.object(source, 'load_state', return_value={'catalog':catalog(), 'model_ids':set()}):
                with self.assertRaisesRegex(ValueError, 'candidate IDs already active'):
                    source.cmd_prepare(args)
            self.assertFalse((vault/'stage').exists())


if __name__ == '__main__':
    unittest.main()
