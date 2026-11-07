"""Version the failed private UV probe; preserve every v24 input and refusal."""
from pathlib import Path
import subprocess
import sys

s = Path(__file__).parent
target = s / 'queue_townsmith_uv_derivatives_v25.py'
assert not target.exists()
text = (s / 'queue_townsmith_uv_derivatives_v24.py').read_text(encoding='utf-8')
text = text.replace('v24', 'v25').replace('V24', 'V25')
needle = '#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"'
assert text.count(needle) == 1
text = text.replace(needle, '#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"\\n' + needle)
needle = 'throw new Exception("Owned UV shader unresolved")'
assert text.count(needle) == 1
text = text.replace(needle, 'throw new Exception("Owned UV shader unresolved: "+(sourceUvShader==null?"null asset":string.Join(";",System.Array.ConvertAll(ShaderUtil.GetShaderMessages(sourceUvShader),message=>message.message))))')
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
