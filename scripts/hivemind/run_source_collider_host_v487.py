"""Accept rustfmt's newline in the retained projection fixture initializer."""
from pathlib import Path
parent = Path(__file__).with_name('run_source_collider_host_v486.py')
code = parent.read_text('utf-8').replace('source-collider-host-cycle-v486', 'source-collider-host-cycle-v487').replace('source-collider-host-v486', 'source-collider-host-v487')
assert "r'    let projection = forge_engine" in code
code = code.replace("r'    let projection = forge_engine", "r'    let projection =\\s*forge_engine")
needle = "exec(compile(code, str(parent) + '#host-collision-v486', 'exec'), globals())"
assert needle in code
pins = 'pins={str(p):sha(p) for p in files}'
extra = 'files.append(Path(' + repr(str(parent)) + '))\n' + pins
code = code.replace(needle, 'code=code.replace(' + repr(pins) + ',' + repr(extra) + ')\n' + needle)
exec(compile(code, str(parent), 'exec'), globals())
