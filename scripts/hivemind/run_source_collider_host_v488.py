"""Use both production Cargo patches and retain dependency identity guards."""
from pathlib import Path
parent = Path(__file__).with_name('run_source_collider_host_v486.py')
code = parent.read_text('utf-8').replace('source-collider-host-cycle-v486', 'source-collider-host-cycle-v488').replace('source-collider-host-v486', 'source-collider-host-v488')
code = code.replace("r'    let projection = forge_engine", "r'    let projection =\\s*forge_engine")
needle = "exec(compile(code, str(parent) + '#host-collision-v486', 'exec'), globals())"
assert needle in code
write = "(O/'Cargo.toml').write_text(cargo+"
injection = "cargo += 'rerecast={path=\"'+(R/'third_party/rerecast-0.4.0').as_posix()+'\"}\\n'\n" + write
pins = 'pins={str(p):sha(p) for p in files}'
extra = 'files.append(Path(' + repr(str(parent)) + '))\n'
extra += "files.extend(p for p in (R/'third_party/rerecast-0.4.0').rglob('*') if p.is_file() and (p.suffix=='.rs' or p.name=='Cargo.toml'))\n" + pins
repair = 'code=code.replace(' + repr(write) + ',' + repr(injection) + ')\n'
repair += 'code=code.replace(' + repr(pins) + ',' + repr(extra) + ')\n'
code = code.replace(needle, repair + needle)
exec(compile(code, str(parent), 'exec'), globals())
