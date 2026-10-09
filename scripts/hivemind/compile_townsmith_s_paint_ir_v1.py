"""Reuse the source topology compiler on the exact purchased S_Paint closure."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,re,subprocess,sys,collections
W=Path("F:/OpenRPG_ImportWork");ROOT=W/"townsmith-s-paint-source-program-v1"
SOURCE=W/"hivemind-run/townsmith";Q=W/"queue/townsmith/prefabs.json"
GUID="abc00000000009486088228754689894"
PARENT=W/"native-hdrp-nine-source-programs-v1/compile_material_family_ir_v1.py"
def read(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def sha(p):
    with Path(p).open("rb") as f:return hashlib.file_digest(f,"sha256").hexdigest()
def documents(p):
    text=p.read_text(encoding="utf-8-sig");decoder=json.JSONDecoder();offset=0;values=[]
    while offset<len(text):
        while offset<len(text) and text[offset].isspace():offset+=1
        if offset==len(text):break
        value,offset=decoder.raw_decode(text,offset);assert isinstance(value,dict);values.append(value)
    return values
def main():
    assert not ROOT.exists()
    manifest_path=SOURCE/"source.v1.json";manifest=read(manifest_path);queue=read(Q);material=queue["materials"][GUID]
    files={r["stored_path"]:r for r in manifest["files"]}
    pins={str(p):sha(p) for p in [manifest_path,Q,PARENT,Path(__file__)]}
    def source(rel):
        p=SOURCE/"source"/rel
        assert p.resolve().is_relative_to((SOURCE/"source").resolve())
        expected=files["source/"+rel]["sha256"]
        assert p.is_file() and sha(p)==expected,p
        pins[str(p)]=expected
        return p
    mp=source(material["source"]);meta=source(material["source"]+".meta")
    assert sha(mp)==material["sha256"] and re.search(r"^guid: "+GUID+r"\s*$",meta.read_text(),re.M)
    root_guid=material["shader_guid"];assert root_guid=="2118bbc334c1aae4aa194cc048d10ff4"
    graphs=[];pending=[root_guid];seen=set()
    while pending:
        guid=pending.pop(0)
        if guid in seen:continue
        seen.add(guid);rel=manifest["guid_paths"][guid]
        assert rel.endswith((".shadergraph",".shadersubgraph"))
        p=source(rel);meta=source(rel+".meta")
        assert re.search(r"^guid: "+guid+r"\s*$",meta.read_text(),re.M)
        values=documents(p)
        assert len([o for o in values if o.get("m_Type")=="UnityEditor.ShaderGraph.GraphData"])==1
        graphs.append({"guid":guid,"path":str(p),"sha256":sha(p),"meta_sha256":sha(meta),"objects":values,"object_count":len(values),"node_types":dict(collections.Counter(o.get("m_Type","unknown").split(".")[-1] for o in values))})
        for node in values:
            if node.get("m_Type","").endswith(".SubGraphNode"):
                pending.append(json.loads(node["m_SerializedSubGraph"])["subGraph"]["guid"])
    assert graphs[0]["guid"]==root_guid
    # Keep all authored parameter resources, including legacy serialized values.
    textures=[]
    for name,texture in material["textures"].items():
        guid=texture.get("guid")
        if not guid or set(guid)=={"0"}:continue
        rel=manifest["guid_paths"][guid];p=source(rel);meta=source(rel+".meta")
        assert re.search(r"^guid: "+guid+r"\s*$",meta.read_text(),re.M)
        textures.append({"property":name,"guid":guid,"path":str(p),"sha256":sha(p),"meta_sha256":sha(meta),"scale":texture["scale"],"offset":texture["offset"],"native_compiler_liveness_qualified":False})
    ROOT.mkdir();(ROOT/"material-family-active-ir-v1").mkdir()
    closure=ROOT/"MASTERPROPS-SUBGRAPH-CLOSURE.v1.json"
    closure.write_text(json.dumps({"graphs":graphs,"source_pins":pins,"full_import":False},indent=2),encoding="utf-8")
    q=ROOT/"material-source-queue.v1.json"
    q.write_text(json.dumps({"source_root":str(SOURCE/"source"),"materials":{GUID:material}},indent=2),encoding="utf-8")
    text=PARENT.read_text(encoding="utf-8")
    first=text.index("B=Path(");last=text.index("\n",first)
    text=text[:first]+f"B=Path({str(ROOT)!r});Q=Path({str(q)!r})"+text[last:]
    assert text.count("material['shader_guid']=='3dc72df83e8fccf408898970630314e5'")==1
    text=text.replace("material['shader_guid']=='3dc72df83e8fccf408898970630314e5'",f"material['shader_guid']=={root_guid!r}")
    root_objects=graphs[0]["objects"]
    terminal_count=sum(o.get("m_Name","").startswith(("SurfaceDescription.","VertexDescription.")) for o in root_objects)
    assert terminal_count>0
    text=text.replace("assert len(terminals)==12",f"assert len(terminals)=={terminal_count}")
    compile(text,"derived-compiler","exec")
    compiler=ROOT/"compile_material_family_ir.v1.py";compiler.write_text(text,encoding="utf-8")
    for p in [closure,q,compiler]:pins[str(p)]=sha(p)
    child=subprocess.run([sys.executable,"-B",str(compiler),GUID],capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT/"stdout.log").write_bytes(child.stdout);(ROOT/"stderr.log").write_bytes(child.stderr)
    report={"status":"townsmith_s_paint_topology_compilation_refusal_retained","finished_utc":datetime.now(timezone.utc).isoformat(),"actual_exit_code":child.returncode,"graphs":len(graphs),"source_graph_objects":sum(g["object_count"] for g in graphs),"material_resources_preserved":len(textures),"texture_resources":textures,"material_guid":GUID,"root_graph_guid":root_guid,"material_source_sha256":sha(mp),"expected_terminals":terminal_count,"compiler_parent_preserved":True,"native_shader_semantics_qualified":False,"forge_material_render_qualified":False,"full_import":False,"complete_assets_delivered_increment":0,"input_pins":pins}
    if child.returncode==0:
        p=ROOT/"material-family-active-ir-v1"/(GUID+".json");ir=read(p)
        assert len(ir["terminals"])==terminal_count and ir["source_material_sha256"]==sha(mp)
        report.update(status="townsmith_s_paint_source_topology_and_material_branches_compiled",ir=str(p),ir_sha256=sha(p),active_nodes=len(ir["nodes"]),selected_branches=len(ir["selected_branches"]),subgraph_instances=len(ir["subgraph_bindings"]),opcode_counts=ir["opcode_counts"])
        pins[str(p)]=sha(p)
    for p,h in pins.items():assert sha(p)==h,p
    (ROOT/"RESULT.v1.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
    print(json.dumps({k:v for k,v in report.items() if k not in ["input_pins","texture_resources"]}))
    if child.returncode:print(child.stderr.decode("utf-8",errors="replace"))
if __name__=="__main__":main()
