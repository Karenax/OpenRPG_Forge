from pathlib import Path
import subprocess,sys
S=Path(__file__).parent
candidate=S/'candidates/SourceTownsmithExplicitShadowsV1.cs'
runner=S/'queue_source_townsmith_explicit_shadows_v1.py'
assert not candidate.exists() and not runner.exists()
cs=(S/'candidates/SourceTownsmithDoorPassesV1.cs').read_text(encoding='utf-8').replace('OpenRPGSourceTownsmithDoorPassesV1','OpenRPGSourceTownsmithExplicitShadowsV1')
cs=cs.replace('request.cases.Length!=9','request.cases.Length!=6').replace('Nine door pass controls required','Six explicit shadow controls required')
cs=cs.replace('item.control_mode!="full_clone"&&item.control_mode!="original_child"&&item.control_mode!="visible_source_shadow"','item.control_mode!="explicit_full_shadow"&&item.control_mode!="explicit_visible_shadow"')
cs=cs.replace('var part=item.control_mode=="original_child"?mesh:UnityEngine.Object.Instantiate(mesh);','var part=UnityEngine.Object.Instantiate(mesh);')
cs=cs.replace('if(item.control_mode=="visible_source_shadow") {','if(true) {')
cs=cs.replace('renderer.enabled=true;renderer.shadowCastingMode=ShadowCastingMode.ShadowsOnly;','renderer.enabled=false;')
cs=cs.replace('if(item.control_mode!="visible_source_shadow"||sub==0)','if(sub==0)')
cs=cs.replace('if(item.control_mode=="visible_source_shadow")childRenderer.shadowCastingMode=ShadowCastingMode.Off;','''childRenderer.shadowCastingMode=ShadowCastingMode.Off;
    int shadows=item.control_mode=="explicit_full_shadow"?mesh.subMeshCount:1;
    for(int draw=0;draw<shadows;draw++){
     var shadowMesh=UnityEngine.Object.Instantiate(mesh);shadowMesh.hideFlags=HideFlags.HideAndDontSave;ownedMeshes.Add(shadowMesh);
     shadowMesh.SetSubMesh(0,mesh.GetSubMesh(draw),MeshUpdateFlags.DontRecalculateBounds);
     for(int unused=1;unused<shadowMesh.subMeshCount;unused++)shadowMesh.SetSubMesh(unused,new SubMeshDescriptor(0,0,mesh.GetTopology(unused)),MeshUpdateFlags.DontRecalculateBounds);
     if(!shadowMesh.GetIndices(0,true).SequenceEqual(mesh.GetIndices(draw,true)))throw new Exception("Shadow indices changed");
     var shadowChild=new GameObject("explicit-source-shadow-"+draw);shadowChild.hideFlags=HideFlags.HideAndDontSave;UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(shadowChild,scene);shadowChild.transform.SetParent(fixture.transform,false);
     shadowChild.AddComponent<MeshFilter>().sharedMesh=shadowMesh;var shadowRenderer=shadowChild.AddComponent<MeshRenderer>();shadowRenderer.sharedMaterials=new[]{materials[0]};shadowRenderer.shadowCastingMode=ShadowCastingMode.ShadowsOnly;
    }''')
cs=cs.replace('"; parent/child transform identity"','"; explicit shadow groups="+shadows+"; parent/child transform identity"')
candidate.write_text(cs,encoding='utf-8')
text=(S/'queue_source_townsmith_door_passes_v1.py').read_text(encoding='utf-8').replace('townsmith-door-pass-controls-v1','townsmith-explicit-shadow-controls-v1').replace('SourceTownsmithDoorPassesV1','SourceTownsmithExplicitShadowsV1')
text=text.replace("['full_clone','original_child','visible_source_shadow']","['explicit_full_shadow','explicit_visible_shadow']")
text=text.replace('len(cases)==9','len(cases)==6').replace("'cases':9","'cases':6").replace("len(report['rows'])==9","len(report['rows'])==6").replace('cases_captured=9','cases_captured=6')
text=text.replace("'source_townsmith_door_pass_controls_captured_comparison_pending'","'source_townsmith_explicit_shadow_controls_captured_comparison_pending'")
compile(text,str(runner),'exec')
runner.write_text(text,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(runner),'prepare'],check=True)
