"""Provide the same fixed-tolerance helpers used by the sealed projection replay."""
from pathlib import Path
parent = Path(__file__).with_name('run_source_collider_host_v486.py')
code = parent.read_text('utf-8').replace('source-collider-host-cycle-v486', 'source-collider-host-cycle-v496').replace('source-collider-host-v486', 'source-collider-host-v496')
code = code.replace("r'    let projection = forge_engine", "r'    let projection =\\s*forge_engine")
helpers = '\nfn near(a:f32,b:f32)->bool{a.is_finite()&&b.is_finite()&&(a-b).abs()<=1e-6*b.abs().max(1.)}\nfn vector_near(a:Vec3,b:Vec3)->bool{a.to_array().into_iter().zip(b.to_array()).all(|(a,b)|near(a,b))}\n'
fixture_patch = "fixture = probe.read_text('utf-8').replace('use std::collections::BTreeMap;\\n','')\nfixture += " + repr(helpers)
code = code.replace("fixture = probe.read_text('utf-8')", fixture_patch)
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
