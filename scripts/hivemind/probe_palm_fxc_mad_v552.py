"""Probe the fractional remainder plus fused operations observed in controlled FXC assembly."""
from pathlib import Path
parent=Path(__file__).with_name('run_palm_noise_wgsl_intermediates_v548.py')
source=parent.read_text('utf-8').replace('v548','v552')
needle="exec(compile(code,str(parent)+'#matched-noise-intermediates','exec'),globals())"
injection=r'''
changes=[
 ('return p - trunc(p / q) * q;', 'return sign(p) * fract(abs(p) * (1.0 / q)) * q;'),
 ('source_hlsl_remainder((34.0 * p.x + 1.0) * p.x, 289.0) + p.y', 'source_hlsl_remainder_add(fma(34.0,p.x,1.0)*p.x,289.0,p.y)'),
 ('source_hlsl_remainder((34.0 * x + 1.0) * x, 289.0)', 'source_hlsl_remainder(fma(34.0,x,1.0)*x,289.0)'),
 ('fract(x / 41.0) * 2.0 - 1.0', 'fma(fract(x * (1.0 / 41.0)),2.0,-1.0)'),
]
prefix="helpers='\\n'.join(function(n) for n in ['source_hlsl_remainder2','source_hlsl_remainder','source_gradient_noise_dir_v0','source_gradient_noise_v0'])"
# Insert changes after the helper string is extracted from the preserved v543 source.
marker='input_struct=re.search'
addition='for old,new in '+repr(changes)+':\n    assert old in helpers,old\n    helpers=helpers.replace(old,new)\n'
addition+="helpers+='\\nfn source_hlsl_remainder_add(p:f32,q:f32,a:f32)->f32 { return fma(sign(p)*q,fract(abs(p)*(1.0/q)),a); }'\n"
assert code.count(marker)==1
code=code.replace(marker,addition+marker)
code=code.replace('source_hlsl_remainder((34.0*reduced.x+1.0)*reduced.x,289.0)+reduced.y','source_hlsl_remainder_add(fma(34.0,reduced.x,1.0)*reduced.x,289.0,reduced.y)')
code=code.replace('source_hlsl_remainder((34.0*h0+1.0)*h0,289.0)','source_hlsl_remainder(fma(34.0,h0,1.0)*h0,289.0)')
'''+needle
assert source.count(needle)==1
exec(compile(source.replace(needle,injection),str(parent)+'#controlled-fxc-mad-probe','exec'),globals())
