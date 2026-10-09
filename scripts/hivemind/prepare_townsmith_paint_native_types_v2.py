from pathlib import Path
import subprocess,sys
S=Path(__file__).parent
target=S/"audit_townsmith_paint_native_types_v2.py"
assert not target.exists()
text=(S/"audit_townsmith_paint_native_types_v1.py").read_text(encoding="utf-8")
text=text.replace("townsmith-s-paint-native-types-v1","townsmith-s-paint-native-types-v2")
anchor='        types={(o["precision"],o["width"]) for o in observations}'
assert text.count(anchor)==1
block='''        if not observations and node["opcode"] in ["PositionNode","NormalVectorNode","ColorNode"]:
            if node["opcode"]=="ColorNode":
                direct=re.compile(r"\\\\b(float|half)([234]?)\\\\s+Color_"+re.escape(node["node_id"])+r"\\\\b")
            else:
                expected_space=4 if node["opcode"]=="PositionNode" else 2
                assert node["source_parameters"]["m_Space"]==expected_space
                name="AbsoluteWorldSpacePosition" if expected_space==4 else "WorldSpaceNormal"
                direct=re.compile(r"\\\\b(float|half)([234]?)\\\\s+"+name+r"\\\\s*;")
                assert "IN."+name in text
            for line_no,line in enumerate(lines,1):
                for m in direct.finditer(line):
                    observations.append({"line":line_no,"precision":m[1],"width":int(m[2] or 1),"variable":m[0],"declaration":line.strip(),"source_alias_elided_by_unity":True})
'''
text=text.replace(anchor,block+anchor)
compile(text,str(target),"exec")
target.write_text(text,encoding="utf-8")
subprocess.run([sys.executable,"-B",str(target)],check=True)
