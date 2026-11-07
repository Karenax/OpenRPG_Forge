"""Replay against the exact SystemState and point-query APIs of the pinned backend."""
from pathlib import Path

parent = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/scripts/hivemind/run_source_collider_projection_v480.py')
code = parent.read_text('utf-8').replace('source-collider-projection-query-v480', 'source-collider-projection-query-v483')
needle = "exec(compile(code, str(parent), 'exec'), {'__file__': __file__, '__name__': '__main__'})"
assert code.count(needle) == 1
write = "(O/'src/main.rs').write_text(source,'utf-8')"
fixed = "source=source.replace('state.get(app.world());','state.get(app.world()).unwrap();')\n"
fixed += "source=source.replace('.project_point(query, true, QueryFilter::default())','.project_point(query, 1000., true, QueryFilter::default())')\n" + write
pins = 'pins={str(p):sha(p) for p in files}'
more_pins = 'files.append(Path(' + repr(str(parent)) + '))\n' + pins
repair = 'code=code.replace(' + repr(write) + ',' + repr(fixed) + ')\n'
repair += 'code=code.replace(' + repr(pins) + ',' + repr(more_pins) + ')\n'
code = code.replace(needle, repair + needle)
exec(compile(code, str(parent), 'exec'), {'__file__': __file__, '__name__': '__main__'})
