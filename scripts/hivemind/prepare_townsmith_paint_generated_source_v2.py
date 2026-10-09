from pathlib import Path
import subprocess,sys
S=Path(__file__).parent
candidate=S/"candidates/SourceTownsmithPaintGeneratedV2.cs"
runner=S/"queue_townsmith_paint_generated_source_v2.py"
assert not candidate.exists() and not runner.exists()
cs=(S/"candidates/SourceTownsmithPaintGeneratedV1.cs").read_text(encoding="utf-8").replace("OpenRPGSourceTownsmithPaintGeneratedV1","OpenRPGSourceTownsmithPaintGeneratedV2")
old='m.GetParameters().Length==2&&m.GetParameters()[0].ParameterType==typeof(string)'
assert cs.count(old)==1
cs=cs.replace(old,'m.GetParameters().Length==4&&m.GetParameters()[0].ParameterType==typeof(string)&&m.GetParameters()[3].IsOut')
cs=cs.replace('object[] args={path,null};','object[] args={path,null,null,null};')
candidate.write_text(cs,encoding="utf-8")
text=(S/"queue_townsmith_paint_generated_source_v1.py").read_text(encoding="utf-8").replace("townsmith-s-paint-sdk-generated-source-v1","townsmith-s-paint-sdk-generated-source-v2").replace("SourceTownsmithPaintGeneratedV1","SourceTownsmithPaintGeneratedV2")
compile(text,str(runner),"exec")
runner.write_text(text,encoding="utf-8")
subprocess.run([sys.executable,"-B",str(runner),"prepare"],check=True)
