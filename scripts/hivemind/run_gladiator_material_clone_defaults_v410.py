"""Query a disposable material clone; strictly seal the original native objects."""
from pathlib import Path

S = Path(__file__).resolve().parent
base = S / 'run_gladiator_native_shader_defaults_v408.py'
code = base.read_text('utf-8').replace('v408', 'v410').replace('V408', 'V410')
needle = "    helper=PROJECT/'Assets/Editor/GladiatorMaterialDefaultsV410.cs';"
assert code.count(needle) == 1
injection = '''    edits={
      'public string guid,file_id,name,serialized_json;':'public string guid,file_id,name,serialized_json,read_probe_before_json,read_probe_after_json;',
      'sources.Add(m);originals.Add(before);var shader=m.shader;':'sources.Add(m);originals.Add(before);var sourceMaterial=m;m=new Material(sourceMaterial);m.name=sourceMaterial.name;string probeBefore=EditorJsonUtility.ToJson(m,true);var shader=m.shader;',
      'if(before!=EditorJsonUtility.ToJson(m,true))throw new Exception("Material mutated");':'if(before!=EditorJsonUtility.ToJson(sourceMaterial,true))throw new Exception("Original material mutated");',
      'serialized_json=before,render_queue=':'serialized_json=before,read_probe_before_json=probeBefore,read_probe_after_json=EditorJsonUtility.ToJson(m,true),render_queue=',
      'source_material_unchanged=true});}':'source_material_unchanged=true});UnityEngine.Object.DestroyImmediate(m);}'
    }
    for old,new in edits.items():
      assert code.count(old)==1,old
      code=code.replace(old,new)
'''
code = code.replace(needle, injection + needle)
namespace = dict(__file__=str(Path(__file__).resolve()), __name__='__main__')
exec(compile(code, str(base)+'#clone-defaults-v410', 'exec'), namespace)
