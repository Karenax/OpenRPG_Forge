import json
import tempfile
import unittest
from pathlib import Path

from audit_candidate_local_references import find_references


class CandidateReferenceTests(unittest.TestCase):
    def test_finds_relative_catalog_and_card_references(self):
        with tempfile.TemporaryDirectory() as tmp:
            vault = Path(tmp).resolve()
            pack = vault/'candidates/hivemind/example'
            (pack/'models').mkdir(parents=True)
            (pack/'cards').mkdir()
            model = pack/'models/asset-08.glb'
            model.write_bytes(b'model')
            (pack/'catalog.promote.v1.json').write_text(json.dumps({'assets':[{'model':'models/asset-08.glb'}]}))
            (pack/'cards/asset-08.json').write_text(json.dumps({'model':'models/asset-08.glb'}))
            (pack/'cards/unexpected.json').write_text(json.dumps({'source':'asset-08.glb'}))
            (pack/'note.txt').write_text('reference models/asset-08.glb')
            result = find_references(vault, [{'retired_id':'asset', 'retired_path':str(model)}])
            self.assertEqual(len(result), 1)
            self.assertEqual(len(result[0]['references']), 4)
            self.assertEqual(find_references(vault, [{'retired_id':'other', 'retired_path':str(vault/'normalized/a.glb')}]), [])


if __name__ == '__main__':
    unittest.main()
