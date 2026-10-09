from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re
W=Path("F:/OpenRPG_ImportWork");OUT=W/"townsmith-s-paint-native-types-v2"
def sha(p):
    with Path(p).open("rb") as f:return hashlib.file_digest(f,"sha256").hexdigest()
def read(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def main():
    assert not OUT.exists()
    source=W/"townsmith-s-paint-source-program-reopen-v1/RESULT.v1.json";proof=read(source)
    for p,h in proof["input_pins"].items():assert sha(p)==h,p
    ip=W/"townsmith-s-paint-source-program-v1/material-family-active-ir-v1/abc00000000009486088228754689894.json"
    hp=Path(proof["sdk_generated_source"]);ir=read(ip);text=hp.read_text(encoding="utf-8-sig")
    lines=text.splitlines();rows=[];missing=[];ambiguous=[]
    for node in ir["nodes"]:
        pattern=re.compile(r"\b(float|half)([234]?)\s+([A-Za-z_]\w*_"+re.escape(node["node_id"])+r"_\w+_"+str(node["output_slot_id"])+r"_(?:Float|Vector[234]))\b")
        observations=[]
        for line_no,line in enumerate(lines,1):
            for m in pattern.finditer(line):
                observations.append({"line":line_no,"precision":m[1],"width":int(m[2] or 1),"variable":m[3],"declaration":line.strip()})
        if not observations and node["opcode"] in ["PositionNode","NormalVectorNode","ColorNode"]:
            if node["opcode"]=="ColorNode":
                direct=re.compile(r"\\b(float|half)([234]?)\\s+Color_"+re.escape(node["node_id"])+r"\\b")
            else:
                expected_space=4 if node["opcode"]=="PositionNode" else 2
                assert node["source_parameters"]["m_Space"]==expected_space
                name="AbsoluteWorldSpacePosition" if expected_space==4 else "WorldSpaceNormal"
                direct=re.compile(r"\\b(float|half)([234]?)\\s+"+name+r"\\s*;")
                assert "IN."+name in text
            for line_no,line in enumerate(lines,1):
                for m in direct.finditer(line):
                    observations.append({"line":line_no,"precision":m[1],"width":int(m[2] or 1),"variable":m[0],"declaration":line.strip(),"source_alias_elided_by_unity":True})
        types={(o["precision"],o["width"]) for o in observations}
        entry={"ir_id":node["id"],"scope":node["scope"],"node_id":node["node_id"],"opcode":node["opcode"],"output_slot_id":node["output_slot_id"],"observations":observations}
        if not observations:missing.append(entry)
        elif len(types)!=1:ambiguous.append(entry)
        else:
            precision,width=next(iter(types));entry.update(precision=precision,width=width);rows.append(entry)
    result={"status":"native_shader_output_types_reopened" if not missing and not ambiguous else "native_shader_output_type_admission_partial","sub_lot":"L03.TOWNSMITH.SHADER.S_PAINT.TYPES","finished_utc":datetime.now(timezone.utc).isoformat(),"nodes_expected":len(ir["nodes"]),"nodes_typed":len(rows),"missing":missing,"ambiguous":ambiguous,"rows":rows,"numeric_gpu_semantics_qualified":False,"forge_material_render_qualified":False,"full_import":False,"complete_assets_delivered_increment":0,"input_pins":{str(p):sha(p) for p in [source,ip,hp,Path(__file__)]}}
    OUT.mkdir();(OUT/"RESULT.v1.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
    print(json.dumps({"status":result["status"],"nodes_typed":len(rows),"missing":[(r["ir_id"],r["opcode"]) for r in missing],"ambiguous":[(r["ir_id"],r["opcode"]) for r in ambiguous]}))
if __name__=="__main__":main()
