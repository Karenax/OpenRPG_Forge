"""Nine source-GPU controls separating child context, full mesh and shadow geometry."""
from pathlib import Path
import subprocess,sys
S=Path(__file__).parent
candidate=S/'candidates/SourceTownsmithDoorPassesV1.cs'
runner=S/'queue_source_townsmith_door_passes_v1.py'
assert not candidate.exists() and not runner.exists()
cs=(S/'candidates/SourceTownsmithDescriptorSlotsV2.cs').read_text(encoding='utf-8').replace('OpenRPGSourceTownsmithDescriptorSlotsV2','OpenRPGSourceTownsmithDoorPassesV1')
cs=cs.replace('request.cases.Length!=16','request.cases.Length!=9').replace('Sixteen explicit controls required','Nine door pass controls required')
start=cs.index('    // Keep camera framing')
end=cs.index('    co=new GameObject("source-camera");',start)
cs=cs[:start]+'''    // Original renderer defines framing; isolated renderers retain source geometry and transforms.
    renderer.enabled=false;
    if(item.control_mode!="full_clone"&&item.control_mode!="original_child"&&item.control_mode!="visible_source_shadow")throw new Exception("Unknown pass control");
    var part=item.control_mode=="original_child"?mesh:UnityEngine.Object.Instantiate(mesh);
    if(part!=mesh){part.hideFlags=HideFlags.HideAndDontSave;ownedMeshes.Add(part);}
    if(item.control_mode=="visible_source_shadow") {
     for(int unused=1;unused<part.subMeshCount;unused++)part.SetSubMesh(unused,new SubMeshDescriptor(0,0,mesh.GetTopology(unused)),MeshUpdateFlags.DontRecalculateBounds);
     renderer.enabled=true;renderer.shadowCastingMode=ShadowCastingMode.ShadowsOnly;
    }
    if(!part.vertices.SequenceEqual(mesh.vertices)||!part.normals.SequenceEqual(mesh.normals)||!part.tangents.SequenceEqual(mesh.tangents)||!part.uv.SequenceEqual(mesh.uv)||!part.GetVertexAttributes().SequenceEqual(mesh.GetVertexAttributes()))throw new Exception("Original vertex attributes changed");
    for(int sub=0;sub<part.subMeshCount;sub++)if(item.control_mode!="visible_source_shadow"||sub==0)if(!part.GetIndices(sub,true).SequenceEqual(mesh.GetIndices(sub,true)))throw new Exception("Original selected indices changed");
    part.bounds=mesh.bounds;
    var child=new GameObject("door-pass-control");child.hideFlags=HideFlags.HideAndDontSave;
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(child,scene);child.transform.SetParent(fixture.transform,false);
    child.AddComponent<MeshFilter>().sharedMesh=part;var childRenderer=child.AddComponent<MeshRenderer>();childRenderer.sharedMaterials=materials;
    if(item.control_mode=="visible_source_shadow")childRenderer.shadowCastingMode=ShadowCastingMode.Off;
    File.WriteAllText(Path.Combine(root,item.id+".control.txt"),"mode="+item.control_mode+"; vertex attributes and selected indices exact; original shadow renderer="+renderer.enabled+"; child shadow="+childRenderer.shadowCastingMode+"; parent/child transform identity");
'''+cs[end:]
# Do not assign bounds to the original asset, even an identical value.
cs=cs.replace('    part.bounds=mesh.bounds;','    if(part!=mesh)part.bounds=mesh.bounds;')
candidate.write_text(cs,encoding='utf-8')
text=(S/'queue_source_townsmith_descriptor_slots_v2.py').read_text(encoding='utf-8').replace('townsmith-descriptor-slot-controls-v2','townsmith-door-pass-controls-v1').replace('SourceTownsmithDescriptorSlotsV2','SourceTownsmithDoorPassesV1')
old="cases=[dict(c,id=c['id']+'-'+mode,control_mode=mode) for c in cases for mode in ['submesh_clamp','slot_repeat_last']]"
assert text.count(old)==1
text=text.replace(old,"cases=[dict(c,id=c['id']+'-'+mode,control_mode=mode) for c in cases[:3] for mode in ['full_clone','original_child','visible_source_shadow']]")
text=text.replace('len(cases)==16','len(cases)==9').replace("'cases':16","'cases':9").replace("len(report['rows'])==16","len(report['rows'])==9").replace('cases_captured=16','cases_captured=9')
text=text.replace("'affected_bindings':37","'affected_bindings':15")
text=text.replace("'source_townsmith_explicit_triangle_slot_controls_captured_comparison_pending'","'source_townsmith_door_pass_controls_captured_comparison_pending'")
compile(text,str(runner),'exec')
runner.write_text(text,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(runner),'prepare'],check=True)
