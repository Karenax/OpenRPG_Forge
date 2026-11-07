"""Preserve the FXC cancellation of noise +0.5 followed by -0.5 before wind scaling."""
from pathlib import Path
parent=Path(__file__).with_name('verify_palm_corrected_vertex_v553.py')
source=parent.read_text('utf-8').replace('v553','v556')
needle="exec(compile(code,str(parent)+'#measured-fxc-arithmetic-vertex','exec'),globals())"
injection='''
marker='input_struct=re.search'
addition="""centered=corrected_function('source_gradient_noise_v0').replace('fn source_gradient_noise_v0(','fn source_gradient_noise_centered_v0(')
assert centered.count(' + 0.5;')==1
helpers+='\\\\n'+centered.replace(' + 0.5;',';')
input_struct=re.search"""
assert code.count(marker)==1
code=code.replace(marker,addition)
marker='assert len(lines)==len(required)'
addition="""assert len(lines)==len(required)
lines=[x.replace('source_gradient_noise_v0(', 'source_gradient_noise_centered_v0(').replace('let n8=vec4<f32>(((n7)+(vec4<f32>(-0.5,0.0,0.0,0.0))).x);','let n8=n7;').replace('let n14=vec4<f32>(((n1)+(n13)).x);','let n14=vec4<f32>(fma(n9.x,n12.x,n1.x));') for x in lines]
"""
assert code.count(marker)==1
code=code.replace(marker,addition)
'''+needle
assert source.count(needle)==1
exec(compile(source.replace(needle,injection),str(parent)+'#fxc-cancelled-noise-offset','exec'),globals())
