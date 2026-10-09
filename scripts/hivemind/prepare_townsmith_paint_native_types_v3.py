from pathlib import Path
import subprocess,sys
S=Path(__file__).parent;target=S/"audit_townsmith_paint_native_types_v3.py"
assert not target.exists()
text=(S/"audit_townsmith_paint_native_types_v2.py").read_text(encoding="utf-8")
text=text.replace("townsmith-s-paint-native-types-v2","townsmith-s-paint-native-types-v3")
for token in ["b","s"]:
    text=text.replace(chr(92)*2+token,chr(92)+token)
compile(text,str(target),"exec")
target.write_text(text,encoding="utf-8")
subprocess.run([sys.executable,"-B",str(target)],check=True)
