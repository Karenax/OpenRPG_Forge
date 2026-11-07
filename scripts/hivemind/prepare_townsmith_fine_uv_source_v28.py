"""Causal source probe: default versus explicitly fine derivatives."""
from pathlib import Path
import subprocess, sys
s=Path(__file__).parent
target=s/'queue_townsmith_fine_uv_source_v28.py'
assert not target.exists()
q=(s/'queue_townsmith_depth_bound_uv_v27.py').read_text(encoding='utf-8')
q=q.replace('v27','v28').replace('V27','V28')
q=q.replace('townsmith-door-source-depth-bound-uv-v28','townsmith-door-source-fine-uv-v28')
before='if(_UvProbeMode<0.5)return float4(input.uv.xy,0,1);return float4(ddx(input.uv.xy),ddy(input.uv.xy));'
assert q.count(before)==1
q=q.replace(before,'if(_UvProbeMode<0.5)return float4(input.uv.xy,0,1);if(_UvProbeMode<1.5)return float4(ddx(input.uv.xy),ddy(input.uv.xy));return float4(ddx_fine(input.uv.xy),ddy_fine(input.uv.xy));')
assert q.count('for(int mode=0;mode<2;mode++)')==1
q=q.replace('for(int mode=0;mode<2;mode++)','for(int mode=0;mode<3;mode++)')
compile(q,str(target),'exec');target.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(target)],check=True)
