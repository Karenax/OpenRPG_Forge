"""Derive isolated original-triangle slot controls; do not alter purchased assets."""
from pathlib import Path
import json, subprocess, hashlib

S = Path(__file__).parent
W = Path("F:/OpenRPG_ImportWork")

def main():
    candidate = S / "candidates/SourceTownsmithExplicitSlotsV1.cs"
    runner = S / "queue_source_townsmith_explicit_slots_v1.py"
    assert not candidate.exists() and not runner.exists()
    cs = (S / "candidates/SourceTownsmithDrawSlotsV3.cs").read_text(encoding="utf-8")
    cs = cs.replace("OpenRPGSourceTownsmithDrawSlotsV3", "OpenRPGSourceTownsmithExplicitSlotsV1")
    cs = cs.replace("public string id;public Ref mesh;", "public string id,control_mode;public Ref mesh;")
    cs = cs.replace("request.cases.Length!=8", "request.cases.Length!=16")
    cs = cs.replace("Eight exact cases required", "Sixteen explicit controls required")
    cs = cs.replace("var ownedMaterials=new List<Material>();", "var ownedMaterials=new List<Material>();var ownedMeshes=new List<Mesh>();")
    anchor = "    co=new GameObject(\"source-camera\");"
    assert cs.count(anchor) == 1
    block = """    // Keep camera framing from the original renderer. Only triangle grouping changes.
    renderer.enabled=false;
    var mappings=new List<string>();
    int count=item.control_mode=="submesh_clamp"?mesh.subMeshCount:materials.Length;
    if(item.control_mode!="submesh_clamp"&&item.control_mode!="slot_repeat_last")throw new Exception("Unknown explicit control");
    for(int draw=0;draw<count;draw++) {
     int submesh=item.control_mode=="submesh_clamp"?draw:Math.Min(draw,mesh.subMeshCount-1);
     int slot=item.control_mode=="submesh_clamp"?Math.Min(draw,materials.Length-1):draw;
     var part=UnityEngine.Object.Instantiate(mesh);part.hideFlags=HideFlags.HideAndDontSave;
     part.subMeshCount=1;part.SetIndices(mesh.GetIndices(submesh,true),mesh.GetTopology(submesh),0,false,0);part.bounds=mesh.bounds;ownedMeshes.Add(part);
     var child=new GameObject("explicit-original-triangles-"+draw);child.hideFlags=HideFlags.HideAndDontSave;
     UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(child,scene);child.transform.SetParent(fixture.transform,false);
     child.AddComponent<MeshFilter>().sharedMesh=part;child.AddComponent<MeshRenderer>().sharedMaterials=new[]{materials[slot]};
     mappings.Add("submesh:"+submesh+";slot:"+slot+";indices:"+mesh.GetIndexCount(submesh));
    }
    File.WriteAllText(Path.Combine(root,item.id+".explicit-mapping.txt"),string.Join("\\n",mappings));
"""
    cs = cs.replace(anchor, block + anchor)
    cs = cs.replace("foreach(var owned in ownedMaterials)", "foreach(var owned in ownedMeshes)UnityEngine.Object.DestroyImmediate(owned);foreach(var owned in ownedMaterials)")
    candidate.write_text(cs, encoding="utf-8")
    text = (S / "queue_source_townsmith_draw_slots_v3.py").read_text(encoding="utf-8")
    text = text.replace("townsmith-native-draw-slots-v3", "townsmith-explicit-slot-controls-v1")
    text = text.replace("SourceTownsmithDrawSlotsV3", "SourceTownsmithExplicitSlotsV1")
    text = text.replace("OpenRPGSourceTownsmithExplicitSlotsV1", "OpenRPGSourceTownsmithExplicitSlotsV1")
    anchor = " scope=read(SCOPE);"
    assert text.count(anchor) == 1
    text = text.replace(anchor, """ context_path=W/'townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json'
 context=read(context_path)
 assert context['cases_reopened']==8 and context['source_files_unchanged']
 derived={r['private']:r['private_serialized_sha256'] for r in context['private_material_serializations']}
""" + anchor)
    text = text.replace("if target.exists():assert sha(target)==info['sha256']", "if target.exists():assert sha(target)==derived.get(str(target),info['sha256'])")
    text = text.replace("assert sha(target)==h;private[str(target)]=h", "assert sha(target)==derived.get(str(target),h);private[str(target)]=sha(target)")
    anchor = " HELPER.write_bytes(CS.read_bytes());"
    assert text.count(anchor) == 1
    text = text.replace(anchor, """ pins[str(context_path)]=sha(context_path)
 cases=[dict(c,id=c['id']+'-'+mode,control_mode=mode) for c in cases for mode in ['submesh_clamp','slot_repeat_last']]
 assert len(cases)==16
""" + anchor)
    text = text.replace("'cases':8", "'cases':16")
    text = text.replace("len(report['rows'])==8", "len(report['rows'])==16")
    text = text.replace("cases_captured=8", "cases_captured=16")
    text = text.replace("'source_townsmith_original_mesh_slots_hdrp_captured_draw_review_pending'", "'source_townsmith_explicit_triangle_slot_controls_captured_comparison_pending'")
    text = text.replace("'source_material_assets_replaced':False", "'source_material_assets_replaced':False,'scoped_private_serialization_receipt':str(context_path),'triangle_geometry_modified':False,'isolated_triangle_grouping_controls':True")
    compile(text, str(runner), "exec")
    runner.write_text(text, encoding="utf-8")
    subprocess.run([__import__("sys").executable, "-B", str(runner), "prepare"], check=True)

if __name__ == "__main__":
    main()
