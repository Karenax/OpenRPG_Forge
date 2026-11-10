import csv
import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


class SourceAuditTest(unittest.TestCase):
    def test_lod_cards_share_verified_files_but_remain_referenced(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            vault = root / 'vault'
            (vault / 'metadata').mkdir(parents=True)
            assets = []
            for side in ('HDRP', 'URP'):
                source = f'sources/{side}/a.prefab'
                meta = source + '.meta'
                for name, content in ((source, b'same prefab'), (meta, b'same meta')):
                    path = vault / name
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_bytes(content)
                for lod in (0, 1):
                    assets.append({'id': side + str(lod), 'pack_id': 'pack',
                        'source_asset_path': f'Assets/{side}/a.prefab',
                        'source_file': source, 'source_sha256': hashlib.sha256(b'same prefab').hexdigest(),
                        'source_meta_file': meta, 'source_meta_sha256': hashlib.sha256(b'same meta').hexdigest()})
            catalog = json.dumps({'assets': assets}).encode()
            (vault / 'metadata/source.json').write_bytes(catalog)
            (vault / 'metadata/asset-source-support-registry.v1.json').write_text(json.dumps({
                'path': 'metadata/source.json', 'sha256': hashlib.sha256(catalog).hexdigest()}))
            out = root / 'groups.csv'
            script = Path(__file__).with_name('audit_variant_source_groups.py')
            subprocess.run([sys.executable, '-B', str(script), '--vault', str(vault), '--output', str(out)], check=True, capture_output=True)
            with out.open(encoding='utf-8-sig', newline='') as f:
                rows = list(csv.DictReader(f))
            self.assertEqual(len(rows), 1)
            self.assertEqual((rows[0]['hdrp_card_count'], rows[0]['urp_card_count']), ('2', '2'))
            self.assertEqual(rows[0]['all_source_contents_identical'], 'True')
            self.assertEqual(rows[0]['all_meta_hashes_verified'], 'True')
            self.assertEqual(rows[0]['deletable_verified'], 'False')
            (vault / 'sources/URP/a.prefab').write_bytes(b'changed')
            out2 = root / 'changed.csv'
            subprocess.run([sys.executable, '-B', str(script), '--vault', str(vault), '--output', str(out2)], check=True, capture_output=True)
            with out2.open(encoding='utf-8-sig', newline='') as f:
                changed = next(csv.DictReader(f))
            self.assertEqual(changed['all_source_hashes_verified'], 'False')
            self.assertEqual(changed['all_source_contents_identical'], 'False')


if __name__ == '__main__':
    unittest.main()
