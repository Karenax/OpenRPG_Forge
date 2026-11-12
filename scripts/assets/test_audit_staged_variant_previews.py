import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from audit_staged_variant_previews import preview


class PreviewAuditTests(unittest.TestCase):
    def test_declared_but_missing_preview_is_not_verified(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            catalog = root / 'catalog.json'
            catalog.write_text(json.dumps({'assets': [{'id': 'model', 'sha256': 'pin',
                'preview': 'missing/image.png', 'preview_sha256': 'a' * 64}]}), encoding='utf8')
            with patch('vault_storage.confined', side_effect=lambda vault, path: path):
                result = preview(root, {'source_catalog': str(catalog), 'model_sha256': 'pin'}, 'model', {})
            self.assertEqual(result['state'], 'missing_declared')
            self.assertEqual(result['expected_sha256'], 'a' * 64)

    def test_checks_preview_bytes_and_model_pin(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            image = root / 'preview.png'
            image.write_bytes(b'not interpreted as rendering')
            pin = hashlib.sha256(image.read_bytes()).hexdigest()
            catalog = root / 'catalog.json'
            detail = {'source_catalog': str(catalog), 'model_sha256': 'model-pin'}
            catalog.write_text(json.dumps({'assets': [{'id': 'model', 'sha256': 'model-pin',
                'preview': image.name, 'preview_sha256': pin}]}), encoding='utf8')
            with patch('vault_storage.confined', side_effect=lambda vault, path: path):
                self.assertEqual(preview(root, detail, 'model', {})['sha256'], pin)
                image.write_bytes(b'changed')
                with self.assertRaises(ValueError):
                    preview(root, detail, 'model', {})
                detail['model_sha256'] = 'wrong'
                with self.assertRaises(ValueError):
                    preview(root, detail, 'model', {})

    def test_refuses_parent_path(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            catalog = root / 'catalog.json'
            catalog.write_text(json.dumps({'assets': [{'id': 'model', 'sha256': 'pin',
                'preview': '../outside.png', 'preview_sha256': 'hash'}]}), encoding='utf8')
            with patch('vault_storage.confined', side_effect=lambda vault, path: path):
                with self.assertRaises(ValueError):
                    preview(root, {'source_catalog': str(catalog), 'model_sha256': 'pin'}, 'model', {})


if __name__ == '__main__':
    unittest.main()
