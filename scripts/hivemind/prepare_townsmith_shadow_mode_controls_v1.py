from pathlib import Path
import subprocess,sys
S=Path(__file__).parent
candidate=S/"candidates/SourceTownsmithShadowModesV1.cs"
runner=S/"queue_source_townsmith_shadow_modes_v1.py"
assert not candidate.exists() and not runner.exists()
cs=(S/"candidates/SourceTownsmithExplicitShadowsV1.cs").read_text(encoding="utf-8").replace("OpenRPGSourceTownsmithExplicitShadowsV1","OpenRPGSourceTownsmithShadowModesV1")
cs=cs.replace("request.cases.Length!=6","request.cases.Length!=15").replace("Six explicit shadow controls required","Fifteen shadow-mode controls required")
start=cs.index("    // Original renderer defines framing")
end=cs.index('    co=new GameObject("source-camera");',start)
cs=cs[:start]+'''    bool sourceRenderer=item.control_mode.StartsWith("source_");
    if(!new[]{"source_default","visible_default","visible_cast_off","source_shadowed","visible_shadowed"}.Contains(item.control_mode))throw new Exception("Unknown shadow-mode control");
    MeshRenderer actualRenderer=renderer;
    if(!sourceRenderer) {
     renderer.enabled=false;
     var part=UnityEngine.Object.Instantiate(mesh);part.hideFlags=HideFlags.HideAndDontSave;ownedMeshes.Add(part);
     for(int sub=1;sub<part.subMeshCount;sub++)part.SetSubMesh(sub,new SubMeshDescriptor(0,0,mesh.GetTopology(sub)),MeshUpdateFlags.DontRecalculateBounds);
     part.bounds=mesh.bounds;
     if(!part.GetIndices(0,true).SequenceEqual(mesh.GetIndices(0,true))||!part.vertices.SequenceEqual(mesh.vertices)||!part.normals.SequenceEqual(mesh.normals)||!part.tangents.SequenceEqual(mesh.tangents)||!part.uv.SequenceEqual(mesh.uv)||!part.GetVertexAttributes().SequenceEqual(mesh.GetVertexAttributes()))throw new Exception("Original selected geometry changed");
     var child=new GameObject("visible-original-submesh");child.hideFlags=HideFlags.HideAndDontSave;UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(child,scene);child.transform.SetParent(fixture.transform,false);
     child.AddComponent<MeshFilter>().sharedMesh=part;actualRenderer=child.AddComponent<MeshRenderer>();actualRenderer.sharedMaterials=materials;
    }
    actualRenderer.shadowCastingMode=item.control_mode=="visible_cast_off"?ShadowCastingMode.Off:ShadowCastingMode.On;
'''+cs[end:]
anchor='lo.AddComponent<HDAdditionalLightData>();'
assert cs.count(anchor)==1
cs=cs.replace(anchor,'''var hdLight=lo.AddComponent<HDAdditionalLightData>();
    if(item.control_mode.EndsWith("_shadowed"))light.shadows=LightShadows.Soft;
    File.WriteAllText(Path.Combine(root,item.id+".light.json"),EditorJsonUtility.ToJson(light,true));
    File.WriteAllText(Path.Combine(root,item.id+".hdlight.json"),EditorJsonUtility.ToJson(hdLight,true));
    File.WriteAllText(Path.Combine(root,item.id+".control.txt"),"mode="+item.control_mode+"; light shadows="+light.shadows+"; renderer shadow casting="+actualRenderer.shadowCastingMode+"; source renderer="+sourceRenderer+"; original selected geometry exact");''')
candidate.write_text(cs,encoding="utf-8")
text=(S/"queue_source_townsmith_explicit_shadows_v1.py").read_text(encoding="utf-8").replace("townsmith-explicit-shadow-controls-v1","townsmith-shadow-mode-controls-v1").replace("SourceTownsmithExplicitShadowsV1","SourceTownsmithShadowModesV1")
text=text.replace("['explicit_full_shadow','explicit_visible_shadow']","['source_default','visible_default','visible_cast_off','source_shadowed','visible_shadowed']")
text=text.replace("len(cases)==6","len(cases)==15").replace("'cases':6","'cases':15").replace("len(report['rows'])==6","len(report['rows'])==15").replace("cases_captured=6","cases_captured=15")
text=text.replace("source_townsmith_explicit_shadow_controls_captured_comparison_pending","source_townsmith_shadow_mode_controls_captured_comparison_pending")
compile(text,str(runner),"exec")
runner.write_text(text,encoding="utf-8")
subprocess.run([sys.executable,"-B",str(runner),"prepare"],check=True)
