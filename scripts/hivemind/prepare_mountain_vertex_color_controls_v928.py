"""Prepare diagnostic-only white-color clones; never admit altered thumbnails."""
from pathlib import Path
import copy,hashlib,json
W=Path('F:/OpenRPG_ImportWork');O=W/'mountain-vertex-color-controls-v928';P=W/'mountain-hdrp-capture-stage-v869/project'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    prior=W/'mountain-native-vertex-diagnostic-v925/RUN-RESULT.v1.json';run=read(prior);assert run['unity_exit_code']==0 and not run['input_changes']
    pins=dict(run['input_pins']);pins[str(prior)]=sha(prior)
    fbx=W/'mountain-original-fbx-color-review-v927/RESULT.v1.json';assert all(f['structure']['total_vertex_color_layers']==0 for f in read(fbx)['files']);pins[str(fbx)]=sha(fbx)
    for p,d in pins.items():assert sha(p)==d,p
    source_path=W/'mountain-exact-source-capture-v873/SourceLibraryExactThumbnailBatchV873.cs';source=source_path.read_text('utf-8-sig').replace('OpenRPGSourceLibraryExactThumbnailBatchV873','OpenRPGMountainVertexColorControlsV928')
    source=source.replace('public int requested_lod,expected_empty_lod_slots,expected_lod_groups;','public int requested_lod,expected_empty_lod_slots,expected_lod_groups; public bool diagnostic_white_vertex_colors;')
    source=source.replace(' public bool source_object_unchanged,',' public bool diagnostic_white_vertex_colors; public bool source_object_unchanged,')
    anchor=' static string Hash(byte[] bytes)'
    assert source.count(anchor)==1
    source=source.replace(anchor,' static System.Collections.Generic.Dictionary<Mesh,string> diagnosticIdentities=new System.Collections.Generic.Dictionary<Mesh,string>();\n'+anchor)
    anchor='static string Identity(UnityEngine.Object obj) { string guid;long id;'
    assert source.count(anchor)==1
    source=source.replace(anchor,'static string Identity(UnityEngine.Object obj) { if(obj is Mesh diagnostic&&diagnosticIdentities.ContainsKey(diagnostic))return diagnosticIdentities[diagnostic]; string guid;long id;')
    anchor='  var preview=EditorSceneManager.NewPreviewScene();'
    assert source.count(anchor)==1
    source=source.replace(anchor,'  var diagnosticClones=new System.Collections.Generic.List<Mesh>();var sourceMeshGuards=new System.Collections.Generic.Dictionary<Mesh,string>();\n'+anchor)
    anchor='   var visible=fixture.GetComponentsInChildren<Renderer>()'
    assert source.count(anchor)==1
    injection='''   if(request.diagnostic_white_vertex_colors) {
    var mapped=new System.Collections.Generic.Dictionary<Mesh,Mesh>();
    foreach(var renderer in selectedLodRenderers) {
     var filter=renderer.GetComponent<MeshFilter>();if(filter==null)throw new Exception("Diagnostic requires static source mesh");
     var original=filter.sharedMesh;
     if(!mapped.ContainsKey(original)) {
      var colors=new System.Collections.Generic.List<Color>();original.GetColors(colors);if(colors.Count!=0)throw new Exception("Diagnostic requires original absence of vertex colors");
      sourceMeshGuards[original]=EditorJsonUtility.ToJson(original,true);
      var clone=UnityEngine.Object.Instantiate(original);clone.hideFlags=HideFlags.HideAndDontSave;clone.name="DIAGNOSTIC_ONLY_WHITE_COLOR_CLONE";
      clone.colors=Enumerable.Repeat(Color.white,original.vertexCount).ToArray();
      if(clone.vertexCount!=original.vertexCount||clone.subMeshCount!=original.subMeshCount||clone.bounds!=original.bounds)throw new Exception("Diagnostic topology/bounds changed");
      for(int slot=0;slot<clone.subMeshCount;slot++)if(!clone.GetIndices(slot).SequenceEqual(original.GetIndices(slot)))throw new Exception("Diagnostic indices changed");
      mapped.Add(original,clone);diagnosticClones.Add(clone);diagnosticIdentities.Add(clone,"diagnostic-white-vertex-colors-clone-of:"+Identity(original));
     }
     filter.sharedMesh=mapped[original];
    }
   }
'''
    source=source.replace(anchor,injection+anchor)
    anchor='   if(sourceBefore!=EditorJsonUtility.ToJson(source,true))'
    assert source.count(anchor)==1
    source=source.replace(anchor,'   foreach(var guard in sourceMeshGuards)if(EditorJsonUtility.ToJson(guard.Key,true)!=guard.Value)throw new Exception("Authored source mesh changed");\n'+anchor)
    source=source.replace('new Report{id=request.id,','new Report{diagnostic_white_vertex_colors=request.diagnostic_white_vertex_colors,id=request.id,')
    anchor='EditorSceneManager.ClosePreviewScene(preview);}'
    assert source.count(anchor)==1
    source=source.replace(anchor,'EditorSceneManager.ClosePreviewScene(preview);foreach(var clone in diagnosticClones)UnityEngine.Object.DestroyImmediate(clone);diagnosticIdentities.Clear();}')
    O.mkdir(exist_ok=False);(O/'temp').mkdir();helper=O/'MountainVertexColorControlsV928.cs';helper.write_text(source,'utf-8');target=P/'Assets/Editor'/helper.name;assert not target.exists();target.write_bytes(helper.read_bytes())
    existing=read(W/'mountain-exact-source-capture-v873/PREPARED.v1.json');rows={c['source_card_id']:c for c in existing['cases'] if c['id'].endswith(':current')}
    visual=read(W/'mountain-capture-agent-review-v898/RESULT.v1.json');cases=[]
    for hold in visual['visual_holds']:
        for label,diagnostic in [('warmup',False),('current',False),('current-repeat',False),('diagnostic-white-colors',True),('diagnostic-repeat',True),('repeat',False)]:
            case=copy.deepcopy(rows[hold['id']]);case.update(id=hold['id']+':'+label,diagnostic_white_vertex_colors=diagnostic,output=str(O/f'case-{len(cases):03d}-{label}'/'NATIVE-RESULT.v1.json'));Path(case['output']).parent.mkdir();cases.append(case)
    manifest=O/'manifest.json';manifest.write_text(json.dumps(dict(cases=cases,output=str(O/'NATIVE-RESULT.v1.json')),indent=2),'utf-8')
    pins.update({str(p):sha(p) for p in [source_path,helper,target,manifest,Path(__file__).resolve()]})
    prep=dict(status='native_original_settings_comparison_prepared_not_launched',project=str(P),helper=str(helper),manifest=str(manifest),execute_method='OpenRPGMountainVertexColorControlsV928.Export',cases=cases,capture_cases=18,source_cards=3,input_pins=pins,scope='Diagnostic white vertex colors on explicitly identified mesh clones only. Altered renders are not admissible library thumbnails or conversion evidence.',images_admitted=0,full_import=False)
    (O/'PREPARED.v1.json').write_text(json.dumps(prep,indent=2),'utf-8');print(json.dumps(dict(status='diagnostic_only_color_controls_prepared',cards=3,cases=18)))
if __name__=='__main__':main()
