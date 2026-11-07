"""Prepare current/original-settings/current-repeat native comparisons on clones.

This compares original serialized settings under the installed SDK. It does not
claim equivalence to the original Unity/HDRP SDK or alter any purchased material.
"""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import math
import re
import shutil

W = Path('F:/OpenRPG_ImportWork')
O = W / 'mountain-native-parameter-comparison-v901'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def floats(text):
    block = re.search(r'(?ms)^    m_Floats:\s*\n(.*?)(?=^    m_Colors:)', text)
    if block is None:
        raise ValueError('Saved float property section absent')
    values = {}
    for line in block[1].splitlines():
        match = re.fullmatch(r'    - (\S+): (\S+)', line)
        assert match and match[1] not in values, line
        value = float(match[2])
        assert math.isfinite(value)
        values[match[1]] = value
    return values

def keywords_block(text):
    block = re.search(r'(?ms)^  m_ValidKeywords:(.*?)(?=^  m_InvalidKeywords:)', text)
    assert block
    body = block[1].strip()
    if body == '[]':
        return block[0], []
    lines = body.splitlines()
    assert all(line.strip().startswith('- ') for line in lines)
    return block[0], [line.strip()[2:] for line in lines]

def normalized(text):
    return re.sub(r'\{([^}]+)\}', lambda match: '{'+' '.join(match[1].split())+'}', text)

def main():
    proof_path = W / 'mountain-exact-capture-review-v897/RESULT.v1.json'
    proof = read(proof_path)
    prepared_path = W / 'mountain-exact-source-capture-v873/PREPARED.v1.json'
    prepared = read(prepared_path)
    source_helper = Path(prepared['helper'])
    project = Path(prepared['project'])
    comparisons, pins = {}, {str(proof_path):sha(proof_path), str(prepared_path):sha(prepared_path)}
    for material in proof['source_material_changes']:
        assert sha(material['source']) == material['original_sha256']
        assert sha(material['target']) == material['migrated_sha256']
        old = Path(material['source']).read_text('utf-8-sig')
        new = Path(material['target']).read_text('utf-8-sig')
        before, after = floats(old), floats(new)
        assert before.keys() <= after.keys(), 'Removed original saved property'
        added = sorted(after.keys() - before.keys())
        changed = sorted(key for key in before if before[key] != after[key])
        old_block, old_keywords = keywords_block(old)
        new_block, _ = keywords_block(new)
        restored = new.replace(new_block, old_block)
        for key in added:
            restored, count = re.subn(r'(?m)^    - '+re.escape(key)+r': \S+\n', '', restored)
            assert count == 1
        for key in changed:
            original_line = re.search(r'(?m)^    - '+re.escape(key)+r': \S+\n', old)[0]
            restored, count = re.subn(r'(?m)^    - '+re.escape(key)+r': \S+\n', lambda _: original_line, restored)
            assert count == 1
        for line in ['  m_Parent: {fileID: 0}\n','  m_ModifiedSerializedProperties: 0\n',
                     '  m_LockedProperties: \n','  m_AllowLocking: 1\n']:
            if line not in old:
                assert restored.count(line) <= 1
                restored = restored.replace(line,'')
        if '  version: 12\n' in old and '  version: 13\n' in restored:
            restored = restored.replace('  version: 13\n','  version: 12\n')
        assert normalized(restored) == normalized(old), 'Unexplained non-parameter material change: '+material['source']
        meta = Path(material['source']+'.meta')
        guid = re.search(r'(?m)^guid: ([0-9a-f]{32})$',meta.read_text('utf-8-sig'))[1]
        assert guid not in comparisons
        comparisons[guid] = dict(guid=guid, original_keywords=old_keywords,
                                 changed_floats=[dict(name=key,value=before[key]) for key in changed],
                                 added_float_names=added)
        pins.update({material['source']:material['original_sha256'], material['target']:material['migrated_sha256'],str(meta):sha(meta)})
    source = source_helper.read_text('utf-8-sig').replace('OpenRPGSourceLibraryExactThumbnailBatchV873','OpenRPGMountainParameterComparisonV901')
    source = source.replace('public int requested_lod,expected_empty_lod_slots,expected_lod_groups;',
                            'public int requested_lod,expected_empty_lod_slots,expected_lod_groups; public bool restore_original_values; public MaterialComparison[] comparisons;')
    source = source.replace(' [Serializable] public class TextureBinding', ''' [Serializable] public class Scalar { public string name; public float value; }
 [Serializable] public class MaterialComparison { public string guid; public string[] original_keywords,added_float_names; public Scalar[] changed_floats; }
 [Serializable] public class MigrationObservation { public string guid; public bool restored_original_values; public Scalar[] shader_defaults; }
 [Serializable] public class TextureBinding''')
    source = source.replace('public RendererInfo[] renderers; public Row[] images;', 'public RendererInfo[] renderers; public Row[] images; public MigrationObservation[] material_comparisons;')
    anchor = 'var preview=EditorSceneManager.NewPreviewScene();'
    assert source.count(anchor) == 1
    source = source.replace(anchor, '''var comparisonClones=new System.Collections.Generic.List<Material>();
  var borrowed=new System.Collections.Generic.Dictionary<Material,string>();
  var comparisonObservations=new System.Collections.Generic.List<MigrationObservation>();
  '''+anchor)
    anchor = '   if(request.requested_lod<0)throw new Exception("Negative source LOD");'
    assert source.count(anchor) == 1
    block = '''   foreach(var entry in request.comparisons) {
    var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(entry.guid));
    if(material==null)throw new Exception("Comparison material identity absent");
    borrowed[material]=EditorJsonUtility.ToJson(material,true);
    var defaults=new Material(material.shader);defaults.hideFlags=HideFlags.HideAndDontSave;
    var observed=new System.Collections.Generic.List<Scalar>();
    Material clone=null;
    try {
     if(request.restore_original_values){clone=new Material(material);clone.hideFlags=HideFlags.HideAndDontSave;comparisonClones.Add(clone);clone.shaderKeywords=entry.original_keywords;}
     foreach(var name in entry.added_float_names) {
      if(!material.HasProperty(name)||!defaults.HasProperty(name))throw new Exception("Added property missing from installed shader: "+name);
      float value=defaults.GetFloat(name);if(!float.IsFinite(value))throw new Exception("Nonfinite native default");
      observed.Add(new Scalar{name=name,value=value});if(clone!=null)clone.SetFloat(name,value);
     }
     foreach(var scalar in entry.changed_floats) {
      if(!material.HasProperty(scalar.name))throw new Exception("Original changed property missing: "+scalar.name);
      if(clone!=null){clone.SetFloat(scalar.name,scalar.value);if(clone.GetFloat(scalar.name)!=scalar.value)throw new Exception("Original value restore mismatch");}
     }
     if(clone!=null) {
      int replaced=0;foreach(var renderer in fixture.GetComponentsInChildren<Renderer>(true)) {
       var slots=renderer.sharedMaterials;for(int i=0;i<slots.Length;i++)if(slots[i]==material){slots[i]=clone;replaced++;}renderer.sharedMaterials=slots;
      }
      if(replaced==0)throw new Exception("Comparison material not used by exact prefab");
      for(int pass=0;pass<clone.passCount;pass++) {
       string name=clone.GetPassName(pass);if(!clone.GetShaderPassEnabled(name)||name.IndexOf("RayTracing",StringComparison.OrdinalIgnoreCase)>=0)continue;
       ShaderUtil.CompilePass(clone,pass,true);if(!ShaderUtil.IsPassCompiled(clone,pass)||ShaderUtil.ShaderHasError(clone.shader))throw new Exception("Restored clone pass not compiled");
      }
     }
     comparisonObservations.Add(new MigrationObservation{guid=entry.guid,restored_original_values=request.restore_original_values,shader_defaults=observed.ToArray()});
    }finally {UnityEngine.Object.DestroyImmediate(defaults);}
   }
'''
    source = source.replace(anchor, block+anchor)
    anchor = '   if(sourceBefore!=EditorJsonUtility.ToJson(source,true))'
    assert source.count(anchor) == 1
    source = source.replace(anchor, '   foreach(var pair in borrowed)if(pair.Value!=EditorJsonUtility.ToJson(pair.Key,true))throw new Exception("Borrowed material mutated");\n'+anchor)
    source = source.replace('new Report{id=request.id,', 'new Report{material_comparisons=comparisonObservations.ToArray(),id=request.id,')
    source = source.replace('EditorSceneManager.ClosePreviewScene(preview);', 'foreach(var clone in comparisonClones)UnityEngine.Object.DestroyImmediate(clone);EditorSceneManager.ClosePreviewScene(preview);')
    O.mkdir(exist_ok=False)
    (O/'temp').mkdir()
    helper = O/'MountainParameterComparisonV901.cs'
    helper.write_text(source,encoding='utf-8')
    target = project/'Assets/Editor'/helper.name
    assert not target.exists()
    shutil.copyfile(helper,target)
    cases = []
    for index in range(47):
        original_request = prepared['cases'][index*3+1]
        native = read(original_request['output'])
        used = {identity.split(':')[0] for row in native['renderers'] for identity in row['materials']}
        selected = [comparisons[guid] for guid in sorted(used & comparisons.keys())]
        assert selected, 'No migrated material for source prefab'
        for label, restore in [('warmup',False),('current',False),('original-settings',True),('repeat',False)]:
            request = dict(original_request)
            request.update(id=request['source_card_id']+':'+label,restore_original_values=restore,comparisons=selected,
                           output=str(O/f'case-{len(cases):03d}-{label}'/'NATIVE-RESULT.v1.json'))
            Path(request['output']).parent.mkdir()
            cases.append(request)
    manifest = O/'manifest.json'
    manifest.write_text(json.dumps(dict(cases=cases,output=str(O/'NATIVE-RESULT.v1.json')),indent=2),encoding='utf-8')
    pins.update({str(p):sha(p) for p in [Path(__file__),source_helper,helper,target,manifest]})
    result = dict(status='native_original_settings_comparison_prepared_not_launched',project=str(project),
                  manifest=str(manifest),helper=str(helper),execute_method='OpenRPGMountainParameterComparisonV901.Export',
                  cases=cases,material_contracts=comparisons,input_pins=pins,source_cards=47,capture_cases=188,
                  scope='Original serialized scalar values and keywords versus migrated settings under installed SDK; new properties use measured current shader defaults. No original SDK parity claim.',
                  images_admitted=0,full_import=False,utc=datetime.now(timezone.utc).isoformat())
    (O/'PREPARED.v1.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(status=result['status'],cards=47,cases=188,materials=len(comparisons))))

if __name__ == '__main__':
    main()
