"""Resume House host composition without shadowing the shared regex module."""
from pathlib import Path

BASE = Path(__file__).with_name('build_house_bevy_vertex_stages_v656.py')
code = BASE.read_text('utf-8-sig')
assert code.count('    import re\n') == 1
code = code.replace('    import re\n', '')
code = code.replace('house-bevy-vertex-stages-v656', 'house-bevy-vertex-stages-v657')
exec(compile(code, str(BASE) + '#regex-scope-repair', 'exec'), globals())
