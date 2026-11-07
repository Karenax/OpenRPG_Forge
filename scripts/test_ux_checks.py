"""Contract tests for advisory UX checks, with independent bad/good fixtures."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('ux', Path(__file__).with_name('Check-UX.py'))
ux = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ux)


class UXChecks(unittest.TestCase):
    def check(self, name, text):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            file = root/name
            file.write_text(text, encoding='utf-8')
            return ux.inspect(file, root, 180)

    def test_html_visible_diagnostics_and_missing_names(self):
        rows = self.check('screen.html', '<p>HTTP 500 / SYNC_FAILED</p>\n<img src="x"><input>')
        self.assertEqual({r[2] for r in rows}, {'technical-copy', 'image-alt', 'field-name'})
        self.assertEqual(rows[0][1], 1)

    def test_accessible_fields_and_nonvisible_code(self):
        rows = self.check('screen.html', '<script>console.log("backend HTTP 500")</script><img alt=""><label for="n">Nom</label><input id="n"><label>Zone<select></select></label>')
        self.assertEqual(rows, [])

    def test_leptos_visible_literal_and_not_log_or_class(self):
        rows = self.check('screen.rs', 'fn x(){tracing::info!("backend HTTP 500"); view! { <div class="backend"><button>"HTTP 500"</button></div> }}')
        self.assertEqual(len(rows), 1)
        self.assertEqual(rows[0][2], 'technical-copy')

    def test_js_dom_text_but_not_logs(self):
        self.assertEqual(len(self.check('screen.js', 'console.log("HTTP 500"); label.textContent = "backend HTTP 500";')), 1)

    def test_long_help_is_only_warning_and_no_edit(self):
        rows = self.check('screen.html', '<p>' + 'Aide métier nécessaire. '*20 + '</p>')
        self.assertEqual(rows[0][2], 'long-copy')

    def test_reasoned_local_suppression(self):
        self.assertEqual(self.check('screen.html', '<!-- ux-ignore: technical-copy terme métier validé -->\n<p>backend</p>'), [])

    def test_skip_test_admin_and_generated_files(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for folder in ('tests', 'admin', 'gen', 'src'):
                (root/folder).mkdir()
                (root/folder/'screen.html').write_text('<input>', encoding='utf-8')
            self.assertEqual([str(p.relative_to(root)) for p in ux.source_files(root, ['.'])], [str(Path('src/screen.html'))])

    def test_duplicate_id(self):
        self.assertEqual(self.check('screen.html', '<div id="a"></div><div id="a"></div>')[0][2], 'duplicate-id')


if __name__ == '__main__':
    unittest.main()
