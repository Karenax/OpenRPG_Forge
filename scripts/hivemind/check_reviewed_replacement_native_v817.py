"""Check actual native Studio after reviewed replacement dispatch changes."""
from pathlib import Path
parent = Path(__file__).with_name('check_placement_physics_native_clippy_v784.py')
code = parent.read_text('utf-8').replace('placement-physics-native-clippy-v784', 'reviewed-replacement-native-clippy-v817')
code = code.replace('files=[Path(__file__),', 'files=[parent,Path(__file__),')
code = code.replace("'physics_controls_commands_verified'", "'native_studio_lib_and_binary_clippy_verified'")
exec(compile(code, str(parent)+'#reviewed-replacement-native', 'exec'), globals())
