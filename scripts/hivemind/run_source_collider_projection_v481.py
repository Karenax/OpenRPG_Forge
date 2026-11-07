"""Retain v480; adapt its SystemState read to the pinned Bevy 0.19 API."""
from pathlib import Path

parent = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/scripts/hivemind/run_source_collider_projection_v480.py')
code = parent.read_text('utf-8').replace('source-collider-projection-query-v480', 'source-collider-projection-query-v481')
needle = "exec(compile(code, str(parent), 'exec'), {'__file__': __file__, '__name__': '__main__'})"
assert code.count(needle) == 1
repair = "code=code.replace(\"(O/'src/main.rs').write_text(source,'utf-8')\",\"source=source.replace('state.get(app.world());','state.get(app.world()).unwrap();')\\n(O/'src/main.rs').write_text(source,'utf-8')\")\n"
repair += "code=code.replace('pins={str(p):sha(p) for p in files}', 'files.append(Path(" + repr(str(parent)) + "))\\npins={str(p):sha(p) for p in files}')\n"
code = code.replace(needle, repair + needle)
exec(compile(code, str(parent), 'exec'), {'__file__': __file__, '__name__': '__main__'})
