"""Type-aware scalar/vector author guards for the sole refused Mountain card."""
from pathlib import Path
import copy,hashlib,json,re

W=Path('F:/OpenRPG_ImportWork');O=W/'mountain-waterfall-typed-controls-v912'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def colors(text):
    result={}
    for name,r,g,b,a in re.findall(r'^    - (\S+): \{\s*r:\s*([^,\s]+),\s*g:\s*([^,\s]+),\s*b:\s*([^,\s]+),\s*a:\s*([^}\s]+)\s*\}$',text,re.M):
        assert name not in result
        result[name]=[float(v) for v in [r,g,b,a]]
    return result
def main():
    old_path=W/'mountain-native-parameter-comparison-v908/PREPARED.v1.json'
    prep=read(old_path)
    for p,d in prep['input_pins'].items():assert sha(p)==d,p
    proof_path=W/'mountain-exact-capture-review-v897/RESULT.v1.json'
    proof=read(proof_path);contracts=copy.deepcopy(prep['material_contracts'])
    pins=dict(prep['input_pins']);pins[str(old_path)]=sha(old_path);pins[str(proof_path)]=sha(proof_path)
    for row in proof['source_material_changes']:
        assert sha(row['source'])==row['original_sha256'] and sha(row['target'])==row['migrated_sha256']
        meta=Path(row['source']+'.meta').read_text('utf-8-sig')
        guid=re.search(r'^guid: (\w+)$',meta,re.M)[1]
        values=colors(Path(row['source']).read_text('utf-8-sig'))
        assert values==colors(Path(row['target']).read_text('utf-8-sig')),'Original vector/color changed'
        contracts[guid]['original_author_colors']=[dict(name=k,value=dict(x=v[0],y=v[1],z=v[2],w=v[3])) for k,v in values.items()]
    source=Path(prep['helper']).read_text('utf-8-sig').replace('OpenRPGMountainParameterComparisonV908','OpenRPGMountainParameterComparisonV912')
    source=source.replace('public Scalar[] changed_floats,original_author_floats;', 'public Scalar[] changed_floats,original_author_floats; public AuthorVector[] original_author_colors;')
    source=source.replace(' [Serializable] public class Scalar', ' [Serializable] public class AuthorVector { public string name; public Vector4 value; }\n [Serializable] public class Scalar')
    anchor='      foreach(var input in entry.original_author_floats)if(clone.HasProperty(input.name)) {'
    assert source.count(anchor)==1
    source=source.replace(anchor,'''      foreach(var input in entry.original_author_floats)if(IsActiveScalar(clone,input.name)) {''')
    source=source.replace('foreach(var input in entry.original_author_floats)if(clone.HasProperty(input.name)&&', 'foreach(var input in entry.original_author_floats)if(IsActiveScalar(clone,input.name)&&')
    reset='      if(!UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(clone))'
    assert source.count(reset)==1
    source=source.replace(reset,'      GuardAuthorVectors(clone,entry.original_author_colors);\n'+reset)
    source=source.replace('      sdkApplied=true;sdkState=', '      GuardAuthorVectors(clone,entry.original_author_colors);\n      sdkApplied=true;sdkState=')
    anchor=' public static void Export()'
    assert source.count(anchor)==1
    methods=''' static bool IsActiveScalar(Material material,string name) {
  int index=material.shader.FindPropertyIndex(name);if(index<0)return false;
  var type=material.shader.GetPropertyType(index);
  return type==UnityEngine.Rendering.ShaderPropertyType.Float||type==UnityEngine.Rendering.ShaderPropertyType.Range||type==UnityEngine.Rendering.ShaderPropertyType.Int;
 }
 static void GuardAuthorVectors(Material material,AuthorVector[] values) {
  foreach(var input in values) {
   int index=material.shader.FindPropertyIndex(input.name);if(index<0)continue;
   var type=material.shader.GetPropertyType(index);
   if(type!=UnityEngine.Rendering.ShaderPropertyType.Vector&&type!=UnityEngine.Rendering.ShaderPropertyType.Color)continue;
   Vector4 actual=type==UnityEngine.Rendering.ShaderPropertyType.Color?(Vector4)material.GetColor(input.name):material.GetVector(input.name);
   if(actual.x!=input.value.x||actual.y!=input.value.y||actual.z!=input.value.z||actual.w!=input.value.w)throw new Exception("Original author vector/color differs: "+input.name);
  }
 }
'''
    source=source.replace(anchor,methods+anchor)
    O.mkdir(exist_ok=False);helper=O/'MountainParameterComparisonV912.cs';helper.write_text(source,'utf-8')
    target=Path(prep['project'])/'Assets/Editor'/helper.name
    assert not target.exists();target.write_bytes(helper.read_bytes())
    cases=copy.deepcopy(prep['cases'][-6:]);assert len({c['source_card_id'] for c in cases})==1
    for index,c in enumerate(cases):
        c['output']=str(O/f'case-{index:03d}-{c["id"].split(":")[-1]}'/'NATIVE-RESULT.v1.json')
        c['comparisons']=[contracts[e['guid']] for e in c['comparisons']]
    manifest=O/'manifest.json';manifest.write_text(json.dumps(dict(cases=cases,output=str(O/'NATIVE-RESULT.v1.json')),indent=2),'utf-8')
    pins.update({str(helper):sha(helper),str(target):sha(target),str(manifest):sha(manifest),str(Path(__file__).resolve()):sha(__file__)})
    prep.update(helper=str(helper),manifest=str(manifest),execute_method='OpenRPGMountainParameterComparisonV912.Export',cases=cases,material_contracts=contracts,input_pins=pins,source_cards=1,capture_cases=6,scope='Original active typed scalar/vector/color author inputs guarded before and after official SDK reset; stale floats under vector property names excluded from scalar semantics, not from vector guards.')
    (O/'PREPARED.v1.json').write_text(json.dumps(prep,indent=2),'utf-8')
    print(json.dumps(dict(status='waterfall_typed_controls_prepared',source_cards=1,capture_cases=6,root=str(O))))
if __name__=='__main__':main()
