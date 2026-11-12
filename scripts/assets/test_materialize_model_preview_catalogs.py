import contextlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import vault_storage as storage
import materialize_model_preview_catalogs as module


class FakeImporter:
    @staticmethod
    @contextlib.contextmanager
    def shared_import_lock(_vault):
        yield


class MaterializeTests(unittest.TestCase):
    def test_creates_immutable_catalog_and_rejects_conflict(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            vault, stage = root/'vault', root/'stage'
            (vault/'catalogs').mkdir(parents=True)
            stage.mkdir()
            old = vault/'catalogs/original.json'
            old.write_bytes(b'old')
            raw = b'new'
            new = old.parent/('openrpg-preview-'+storage.sha(raw)+'.json')
            (stage/'c.json').write_bytes(raw)
            plan = {'catalogs':[{'old':str(old),'old_sha256':storage.sha(b'old'),
                    'new':str(new),'new_sha256':storage.sha(raw),'stage_file':'c.json'}]}
            (stage/'plan.json').write_text(json.dumps(plan),encoding='utf8')
            with patch.object(module,'verify',return_value={'state':'stage_verified'}):
                result = module.materialize(vault,stage,FakeImporter())
                self.assertEqual(result['created'],[str(new)])
                self.assertEqual(module.materialize(vault,stage,FakeImporter())['reused'],[str(new)])
                new.write_bytes(b'changed')
                with self.assertRaisesRegex(ValueError,'conflicts'):
                    module.materialize(vault,stage,FakeImporter())
            self.assertEqual(old.read_bytes(),b'old')


if __name__ == '__main__':
    unittest.main()
