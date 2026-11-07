"""Check the real inspector on the native Studio target."""
from pathlib import Path
parent = Path(__file__).with_name('check_placement_physics_native_clippy_v784.py')
code = parent.read_text('utf-8').replace('placement-physics-native-clippy-v784', 'saved-physics-inspector-native-v824')
code = code.replace('files=[Path(__file__),', 'files=[parent,Path(__file__),')
code = code.replace("'physics_controls_commands_verified'", "'saved_reference_inspector_native_clippy_verified'")
exec(compile(code, str(parent)+'#saved-reference-inspector', 'exec'), globals())
