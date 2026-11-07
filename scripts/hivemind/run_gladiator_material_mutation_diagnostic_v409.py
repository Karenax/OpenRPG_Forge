"""Retain the strict material guard and capture its exact before/after failure."""
from pathlib import Path
import sys

S = Path(__file__).resolve().parent
base = S / 'run_gladiator_native_shader_defaults_v408.py'
code = base.read_text('utf-8').replace('v408', 'v409').replace('V408', 'V409')
needle = "    helper=PROJECT/'Assets/Editor/GladiatorMaterialDefaultsV409.cs';"
assert code.count(needle) == 1
injection = '''    guard='if(before!=EditorJsonUtility.ToJson(m,true))throw new Exception("Material mutated");'
    assert code.count(guard)==1
    replacement='if(before!=EditorJsonUtility.ToJson(m,true)){var diagnostic=Path.Combine(Path.GetDirectoryName(r.output),"material-mutation-"+guid);File.WriteAllText(diagnostic+".before.json",before);File.WriteAllText(diagnostic+".after.json",EditorJsonUtility.ToJson(m,true));throw new Exception("Material mutated; exact snapshots retained for "+guid);}'
    code=code.replace(guard,replacement)
'''
code = code.replace(needle, injection + needle)
namespace = dict(__file__=str(Path(__file__).resolve()), __name__='__main__')
exec(compile(code, str(base)+'#diagnostic-v409', 'exec'), namespace)
