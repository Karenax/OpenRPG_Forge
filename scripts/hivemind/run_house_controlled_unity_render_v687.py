"""Capture the original material in a measured Unity fixture; parity remains pending."""
from pathlib import Path
import json

S = Path(__file__).resolve().parent
W = Path('F:/OpenRPG_ImportWork')
base = S / 'candidates/SourceLibraryThumbnailHouse06RocksEv0.cs'
CODE = base.read_text('utf-8-sig')
prep_path = W / 'house-controlled-gpu-fixture-v679/PREPARED.v1.json'
context_path = prep_path.with_name('CONTROLLED-CONTEXT.v1.json')
prep = json.loads(prep_path.read_text('utf-8-sig'))
context = json.loads(context_path.read_text('utf-8-sig'))
plan = json.loads((W/'house-collider-binding-plan-v678/RESULT.v1.json').read_text('utf-8-sig'))
row = next(r for r in plan['rows'] if r['prefab_guid'] == prep['prefab_guid'])

def scalar(v): return format(float(v), '.17g') + ('f' if '.' in format(float(v), '.17g') or 'e' in format(float(v), '.17g') else '.0f')
def source_vector(v): return 'new Vector3('+','.join(scalar(v[k]*( -1 if k=='z' else 1)) for k in 'xyz')+')'

CODE = CODE.replace('OpenRPGSourceLibraryThumbnailHouse06RocksEv0', 'OpenRPGHouseControlledRenderV687')
start = CODE.index(' public static void Export() {')
end = CODE.index(' static void Capture(Request request)', start)
CODE = CODE[:start] + ''' public static void Export() {
 string output=Environment.GetEnvironmentVariable("OPENRPG_COMPONENT_STATE_OUTPUT");if(string.IsNullOrEmpty(output))throw new Exception("Output required");
 Capture(new Request{id="controlled-source-render",prefab_path=SOURCE_PATH,prefab_guid=SOURCE_GUID,output=Path.Combine(output,"NATIVE-RESULT.v1.json")});
 }
''' .replace('SOURCE_PATH',json.dumps(row['source_component']['path'])).replace('SOURCE_GUID',json.dumps(prep['prefab_guid'])) + CODE[end:]
edits = {
 'string sourceBefore=EditorJsonUtility.ToJson(source,true);':
 'var components=source.GetComponentsInChildren<Component>(true);if(components.Any(c=>c==null||c is MonoBehaviour))throw new Exception("Purchased or missing scripts refused");var componentBefore=components.Select(c=>EditorJsonUtility.ToJson(c,true)).ToArray();string sourceBefore=EditorJsonUtility.ToJson(source,true);',
 'camera.fieldOfView=30;': 'camera.fieldOfView='+scalar(context['fov'])+';',
 'camera.nearClipPlane=.01f;camera.farClipPlane=Math.Max(10,radius*12);':
 'camera.nearClipPlane='+scalar(context['near'])+';camera.farClipPlane='+scalar(context['far'])+';',
 'new Color(.025f,.035f,.05f,1)': 'new Color(.025f,.025f,.025f,1)',
 'tone.mode.Override(TonemappingMode.Neutral)': 'tone.mode.Override(TonemappingMode.None)',
 'light.intensity=1;lightObject.AddComponent<HDAdditionalLightData>();light.intensity=1;':
 'lightObject.AddComponent<HDAdditionalLightData>();light.lightUnit=LightUnit.Lux;light.intensity='+scalar(context['light_intensity'])+';light.shadows=LightShadows.None;',
 'view<directions.Length': 'view<1',
 'camera.transform.position=bounds.center+directions[view]*radius*4;camera.transform.LookAt(bounds.center);':
 'camera.transform.position='+source_vector(context['camera_position'])+';camera.transform.LookAt('+source_vector(context['bounds_center'])+');',
 'byte[] bytes=png.EncodeToPNG();':
 'byte[] linear=raw.GetRawTextureData<byte>().ToArray();string linearPath=Path.Combine(Path.GetDirectoryName(request.output),"UNITY-LINEAR.rgba16f");if(File.Exists(linearPath))throw new Exception("Preserve previous linear capture");File.WriteAllBytes(linearPath,linear);byte[] bytes=png.EncodeToPNG();',
 'if(sourceBefore!=EditorJsonUtility.ToJson(source,true))throw new Exception("Source prefab changed");':
 'if(sourceBefore!=EditorJsonUtility.ToJson(source,true))throw new Exception("Source prefab changed");for(int i=0;i<components.Length;i++)if(componentBefore[i]!=EditorJsonUtility.ToJson(components[i],true))throw new Exception("Source component changed");',
 'presentation_tonemapping="Neutral"': 'presentation_tonemapping="None"',
 'camera_field_of_view=30': 'camera_field_of_view='+scalar(context['fov']),
}
for old, new in edits.items():
    assert CODE.count(old) == 1, (old, CODE.count(old))
    CODE = CODE.replace(old, new)
# This remains a controlled fixture. Native pass transport, camera response,
# lighting and background must be compared before source fidelity is accepted.
parent = S / 'run_house_component_state_v672.py'
recipe = parent.read_text('utf-8-sig').replace('v672', 'v687').replace('V672','V687')
recipe = recipe.replace('house-wire-native-storage-v668/HELPER-OWNERSHIP.v1.json',
                        'house-native-component-state-v684/HELPER-OWNERSHIP.v1.json').replace("len(audit['helpers'])==37","len(audit['helpers'])==41")
anchor = "exec(compile(code,str(BASE)+'#house-owned-component-capture','exec'),globals())"
addition = '''
needle="helper=PROJECT/'Assets/Editor/HouseComponentStateV687.cs'"
assert code.count(needle)==1
code=code.replace(needle,"code=OWNED_RENDER_CODE\\n"+needle)
code=code.replace('OpenRPGHouseComponentStateV687.Capture.Export','OpenRPGHouseControlledRenderV687.Export')
code=code.replace("'-nographics'","'-force-d3d12'")
old="assert len(native['prefabs'])==24 and sum(len(n['renderers']) for p in native['prefabs'] for n in p['nodes'])==24\\n    assert sum(len(n['colliders']) for p in native['prefabs'] for n in p['nodes'])==24"
assert code.count(old)==1
code=code.replace(old,"assert native['source_object_unchanged'] and len(native['images'])==1 and (O/'UNITY-LINEAR.rgba16f').stat().st_size==512*512*8")
code=code.replace('prefabs_instantiated=False','prefabs_instantiated=True')
code=code.replace('twentyfour_house_prefab_component_states_captured_review_pending','controlled_house_unity_render_captured_parity_pending')
code=code.replace("O.mkdir();(O/'temp').mkdir();", "O.mkdir();(O/'temp').mkdir();(O/'CONTROLLED-CONTEXT.v1.json').write_bytes(context_path.read_bytes());")
code=code.replace('pins={str(p):sha(p) for p in [Path(__file__),parent,bridge_path,closure_path]}','pins={str(p):sha(p) for p in [Path(__file__),parent,bridge_path,closure_path,base,prep_path,context_path]}')
'''
assert recipe.count(anchor) == 1
recipe = recipe.replace(anchor, addition + anchor)
OWNED_RENDER_CODE = CODE
exec(compile(recipe, str(parent)+'#house-controlled-original-render-v687', 'exec'), globals())
