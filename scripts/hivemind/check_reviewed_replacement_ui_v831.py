"""Keep the failed fixture and retest actual helper using the canonical optional list."""
from pathlib import Path
original = Path(__file__).with_name('check_reviewed_replacement_ui_v827.py')
code = original.read_text('utf-8').replace('ReviewedReplacementUiV827.rs', 'ReviewedReplacementUiV831.rs').replace('reviewed-replacement-ui-tests-v827', 'reviewed-replacement-ui-tests-v831')
code = code.replace('files = [parent, tests, Path(__file__),', 'files = [parent, original, tests, Path(__file__),')
exec(compile(code, str(original)+'#canonical-optional-placement-fixture', 'exec'), globals())
