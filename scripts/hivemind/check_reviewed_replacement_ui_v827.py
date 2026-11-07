"""Exercise the actual UI request helper through the real domain and undo journal."""
from pathlib import Path
parent = Path(__file__).with_name('check_placement_reuse_v787.py')
tests = Path(__file__).with_name('candidates')/'ReviewedReplacementUiV827.rs'
code = parent.read_text('utf-8').replace('placement-physics-reuse-tests-v787', 'reviewed-replacement-ui-tests-v827')
code = code.replace("R/'apps/forge_studio/src/authoring_placement_reuse.rs'", "R/'apps/forge_studio/src/authoring_assets/placement_controls/replacement/request.rs'")
code = code.replace("#[allow(dead_code)]\\n", "")
code = code.replace('mod authoring_placement_reuse;', 'mod request;')
code = code.replace("+'\"]\\nmod request;\\nfn main() {}\\n', 'utf-8')", "+'\"]\\nmod request;\\nfn main() {}\\n'+tests.read_text('utf-8'), 'utf-8')")
assert "tests.read_text('utf-8')" in code
code = code.replace('files = [Path(__file__),', 'files = [parent, tests, Path(__file__),')
code = code.replace('actual_studio_placement_reuse_tests_verified', 'actual_ui_package_replacement_and_domain_undo_verified')
code = code.replace('Actual Studio root reconciliation decisions; no scene mount, GUI, or runtime simulation', 'Actual Studio request helper, explicit physical removal, package-specific batch, real domain retouches and undo; no scene mount or GUI qualification')
exec(compile(code, str(parent)+'#actual-ui-request-and-domain', 'exec'), globals())
