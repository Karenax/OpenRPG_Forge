"""Match the controlled FXC final MAD while keeping the original comparison tolerance."""
from pathlib import Path
parent=Path(__file__).with_name('verify_palm_corrected_vertex_v553.py')
source=parent.read_text('utf-8').replace('v553','v555')
needle="exec(compile(code,str(parent)+'#measured-fxc-arithmetic-vertex','exec'),globals())"
injection='''
marker="assert len(lines)==len(required)"
addition="""assert len(lines)==len(required)
assemblypath=W/'pirate-palm-fxc-assembly-v554/controlled-fxc.asm'
assert 'mad r0.x, r1.x, r1.y, r0.x' in assemblypath.read_text('utf-8-sig')
pins[str(assemblypath)]=sha(assemblypath)
old_line='let n14=vec4<f32>(((n1)+(n13)).x);'
assert old_line in lines
lines=[x.replace(old_line,'let n14=vec4<f32>(fma(n9.x,n12.x,n1.x));') for x in lines]
"""
assert code.count(marker)==1
code=code.replace(marker,addition)
'''+needle
assert source.count(needle)==1
exec(compile(source.replace(needle,injection),str(parent)+'#final-fxc-mad','exec'),globals())
