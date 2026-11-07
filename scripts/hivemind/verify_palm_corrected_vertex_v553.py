"""Apply measured helper arithmetic to the complete source vertex dependency slice."""
from pathlib import Path
parent=Path(__file__).with_name('run_palm_wind_gpu_comparison_v545.py')
code=parent.read_text('utf-8').replace('v545','v553')
marker='input_struct=re.search'
injection='''
diagnosticpath=W/'pirate-palm-wind-gpu-comparison-v552/RESULT.v1.json'
diagnostic=read(diagnosticpath)
assert diagnostic['status']=='gpu_numeric_comparison_pass' and diagnostic['failed_components']==0
for p,h in diagnostic['pins'].items():assert sha(p)==h,p
correctedpath=W/'pirate-palm-wind-gpu-comparison-v552/src/wind.wgsl'
corrected=correctedpath.read_text('utf-8-sig')
def corrected_function(name):
    start=corrected.index('fn '+name+'(');brace=corrected.index('{',start);depth=1;end=brace+1
    while depth:
        depth+=(corrected[end]=='{')-(corrected[end]=='}');end+=1
    return corrected[start:end]
helpers='\\n'.join(corrected_function(n) for n in ['source_hlsl_remainder2','source_hlsl_remainder','source_gradient_noise_dir_v0','source_gradient_noise_v0','source_hlsl_remainder_add'])
for p in [diagnosticpath,correctedpath,parent]:pins[str(p)]=sha(p)
# The source X terminal is object X plus node13 wind displacement. FXC cancels
# that addition in the diagnostic Position.x-objectX output. Capture node13
# directly to avoid introducing an extra subtraction rounding in the probe.
assert nodes['14']['opcode']=='AddNode'
assert {v['value']['ref'] for v in nodes['14']['inputs']}=={'1','13'}
'''+marker
assert code.count(marker)==1;code=code.replace(marker,injection)
old='results[id.x*3u]=vec4<f32>(position,position.x-v.object_position.x);'
new='results[id.x*3u]=vec4<f32>(position,n13.x);'
assert code.count(old)==1;code=code.replace(old,new)
code=code.replace("for p in [Path(__file__),proofpath", "for p in [parent,Path(__file__),proofpath")
exec(compile(code,str(parent)+'#measured-fxc-arithmetic-vertex','exec'),globals())
