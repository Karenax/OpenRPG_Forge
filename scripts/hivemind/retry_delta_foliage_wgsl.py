"""Retry the six foliage kernels refused for missing NormalizeNode support.

Retain all earlier successes/refusals. Bind vector widths to original graph
property types; absolute-world position conversion requires an explicit host
matrix, never a guessed identity or generated tangent.
"""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-delta-foliage-wgsl-retry-v1"
EMITTER = W / "dungeon-original-material-wgsl-v1/adapted-node-emitter.py.txt"
READER = Path(__file__).resolve().with_name("generate_nested_material_wgsl.py")
UNITY = Path(r"F:\Unity\6000.5.8f1\Editor\Data\Resources\PackageManager\BuiltInPackages\com.unity.shadergraph")


def sha(path):
    h = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def read(path):
    return json.loads(path.read_text("utf-8-sig"))


WIDTH_HELPER = '''
def original_vector_width(v):
 if 'ref' in v: return dims[v['ref']]
 provenance=v.get('provenance','')
 if provenance.startswith('material:'):
  name=provenance.split(':',1)[1]
  if name not in source_property_widths: raise ValueError('Source vector property width unavailable: '+name)
  return source_property_widths[name]
 slot=v.get('source_slot',{}).get('m_Type','').split('.')[-1]
 widths={'Vector1MaterialSlot':1,'Vector2MaterialSlot':2,'Vector3MaterialSlot':3,'Vector4MaterialSlot':4,'PositionMaterialSlot':3,'NormalMaterialSlot':3,'TangentMaterialSlot':3,'UVMaterialSlot':4}
 if slot in widths:return widths[slot]
 raise ValueError('Original vector width unavailable')
'''
NODE_SUPPORT = '''
 elif op=='NormalizeNode':
  dim=original_vector_width(ins['In']);assert 1<=dim<=4
  selected='('+get('In')+').'+('xyzw'[:dim])
  normalized=selected+'/abs('+selected+')' if dim==1 else 'normalize('+selected+')'
  expr='vec4<f32>('+normalized+(','+','.join('0.0' for _ in range(4-dim)) if 1<dim<4 else '')+')'
 elif op=='LengthNode':
  width=original_vector_width(ins['In']);assert 1<=width<=4
  selected='('+get('In')+').'+('xyzw'[:width])
  expr='vec4<f32>('+('abs('+selected+')' if width==1 else 'length('+selected+')')+')';dim=1
 elif op=='TransformNode':
  assert p['m_SGVersion']==1 and p['m_Conversion']=={'from':4,'to':0} and p['m_ConversionType']==0, 'Unqualified source transform variant'
  field='source_object_from_absolute_world';input_requirements.append({'source_opcode':'TransformNode','from':4,'to':0,'conversion_type':0,'version':1,'field':field,'explicit_host_matrix_required':True})
  expr='vec4<f32>((i.'+field+'*vec4<f32>(('+get('In')+').xyz,1.0)).xyz,0.0)';dim=3
'''


def main():
    if ROOT.exists():
        raise RuntimeError("Previous foliage retry preserved")
    prior_path = W / "nested-prefab-delta-material-wgsl-v1/RESULT.v1.json"
    batch_path = W / "nested-prefab-delta-material-active-ir-v1/RESULT.v1.json"
    properties_path = W / "nested-prefab-delta-graph-property-bindings-v1/RESULT.v1.json"
    prior, batch, properties = [read(p) for p in [prior_path, batch_path, properties_path]]
    targets = {(r["pack_id"], r["material_guid"]) for r in prior["failures"] if r["error"] == "Unsupported source opcode NormalizeNode"}
    selected = [r for r in batch["rows"] if (r["pack_id"], r["material_guid"]) in targets]
    if len(selected) != 6 or not prior["original_sources_and_helpers_unchanged"] or not batch["all_input_pins_unchanged"]:
        raise RuntimeError("Exactly six proven foliage refusal inputs required")
    pins = {str(p): sha(p) for p in [prior_path, batch_path, properties_path, EMITTER, READER, Path(__file__).resolve()]}
    original_emitter = EMITTER.read_text("utf-8-sig")
    if sha(EMITTER) != prior["source_pins"][str(EMITTER)] or sha(READER) != "1c2cd685c978ed2b922e0b84a5187f5cfc4216e4909dd921fad2377e847b49c9":
        raise RuntimeError("Original emitter/generator differs from prior proof")
    # Pin installed upstream definitions. This does not qualify the original
    # bought project's Unity version or assert GPU equivalence.
    for name in ["Editor/Data/Nodes/Math/Advanced/NormalizeNode.cs", "Editor/Data/Nodes/Math/Advanced/LengthNode.cs", "Editor/Data/Util/SpaceTransformUtil.cs", "package.json", "LICENSE.md"]:
        p = UNITY / name
        pins[str(p)] = sha(p)
    widths = {}
    types = {"Vector1ShaderProperty":1,"Vector2ShaderProperty":2,"Vector3ShaderProperty":3,"Vector4ShaderProperty":4,"ColorShaderProperty":4,"BooleanShaderProperty":1}
    for row in properties["rows"]:
        key = (row["pack_id"], row["material_identity"]["guid"])
        if key in targets:
            widths[row["material_identity"]["guid"]] = {
                b["reference_name"]: types[b["source_property_object"]["m_Type"].split('.')[-1]]
                for b in row["bindings"] if b["source_property_object"]["m_Type"].split('.')[-1] in types}
    if len(widths) != 6 or any(v.get("_WindDirection") != 3 for v in widths.values()):
        raise RuntimeError("Explicit original Vector3 wind property required")
    anchor = " elif op=='NormalVectorNode':"
    if original_emitter.count(anchor) != 1:
        raise RuntimeError("Emitter node insertion point changed")
    adapted = WIDTH_HELPER + original_emitter.replace(anchor, NODE_SUPPORT + anchor)
    fields_anchor = "fields=[('view_direction_world',3)"
    if adapted.count(fields_anchor) != 1:
        raise RuntimeError("Explicit matrix input insertion point changed")
    adapted = adapted.replace(fields_anchor,"fields=[('source_object_from_absolute_world',16),('view_direction_world',3)")
    old_type = "typename=lambda n:'f32' if n==1 else f'vec{n}<f32>'"
    if adapted.count(old_type) != 1:
        raise RuntimeError("Input ABI declaration changed")
    adapted = adapted.replace(old_type,"typename=lambda n:'mat4x4<f32>' if n==16 else ('f32' if n==1 else f'vec{n}<f32>')")
    compile(adapted, "foliage-emitter", "exec")
    ROOT.mkdir()
    emitter_path = ROOT / "source-node-emitter.v2.py.txt"
    emitter_path.write_text(adapted, "utf-8")
    pins[str(emitter_path)] = sha(emitter_path)
    selection_path = ROOT / "SELECTED-TOPOLOGIES.v1.json"
    chosen = {**batch, "rows": selected, "materials": 6, "compiled_materials": 6}
    selection_path.write_text(json.dumps(chosen, indent=2), "utf-8")
    pins[str(selection_path)] = sha(selection_path)
    generator = READER.read_text("utf-8")
    configurations = [
        ('ROOT = W / "nested-prefab-material-wgsl-v1"', 'ROOT = W / "nested-prefab-delta-foliage-wgsl-v2"'),
        ('EMITTER = W / "dungeon-original-material-wgsl-v1/adapted-node-emitter.py.txt"', 'EMITTER = Path('+repr(str(emitter_path))+')'),
        ('batch_path = W / "nested-prefab-material-active-ir-v1/RESULT.v1.json"', 'batch_path = Path('+repr(str(selection_path))+')'),
        ('batch["compiled_materials"] != 16', 'batch["compiled_materials"] != 6'),
        ('"total": 16', '"total": 6'), ('"requested_materials": 16', '"requested_materials": 6'),
        ('"existing_node_emitter_unchanged": True', '"existing_node_emitter_unchanged": False'),
        ('exec(compile(emitter, str(EMITTER), "exec"), context)',
         'context["source_property_widths"]='+repr(widths)+'[row["material_guid"]]\n            exec(compile(emitter, str(EMITTER), "exec"), context)'),
    ]
    for before, after in configurations:
        if generator.count(before) != 1:
            raise RuntimeError("Generator configuration point changed")
        generator = generator.replace(before, after)
    compile(generator, "foliage-generator", "exec")
    child = ROOT / "generate_foliage_wgsl.py"
    child.write_text(generator, "utf-8")
    pins[str(child)] = sha(child)
    with (ROOT / "stdout.log").open("w",encoding="utf-8") as out, (ROOT / "stderr.log").open("w",encoding="utf-8") as err:
        process = subprocess.run([sys.executable,"-u",str(child)],stdout=out,stderr=err)
    result_path = W / "nested-prefab-delta-foliage-wgsl-v2/RESULT.v1.json"
    result = read(result_path) if result_path.exists() else {}
    unchanged = all(sha(p)==expected for p,expected in pins.items())
    proof = {"status":"foliage_retry_closed" if process.returncode==0 and unchanged and result.get("original_sources_and_helpers_unchanged") else "refused_or_partial",
             "actual_generator_exit_code":process.returncode,"source_pins":pins,
             "naga_validated_materials":result.get("naga_validated_materials"),"failures":result.get("failures"),
             "source_property_widths":widths,"all_input_pins_unchanged":unchanged,
             "prior_successes_reprocessed":False,"original_refusals_preserved":True,
             "explicit_host_matrix_required":True,"gpu_rendered":False,"studio_verified":False,"full_import":False}
    (ROOT / "RUN-RESULT.v1.json").write_text(json.dumps(proof,indent=2),"utf-8")
    print(json.dumps({k:v for k,v in proof.items() if k not in {"source_pins","source_property_widths"}}))
    if proof["status"] != "foliage_retry_closed":
        raise RuntimeError("Foliage retry incomplete; diagnostics preserved")


if __name__ == "__main__":
    main()
