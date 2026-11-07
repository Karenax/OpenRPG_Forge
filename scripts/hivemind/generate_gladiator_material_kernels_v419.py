"""Correct the retained kernel refusal by reusing the measured RG-or-AG helper."""
from pathlib import Path
S=Path(__file__).resolve().parent
recipe=S/'generate_gladiator_material_kernels_v414.py'
code=recipe.read_text('utf-8').replace('gladiator-material-kernels-v414','gladiator-material-kernels-v419')
needle='O.mkdir();rows=[];failures=[]'
assert code.count(needle)==1
injection='''def function(text,name):
    start=text.index('fn '+name+'(');opening=text.index('{',start);depth=1
    for i in range(opening+1,len(text)):
        depth+=(text[i]=='{')-(text[i]=='}')
        if depth==0:return text[start:i+1]
    raise ValueError('Unclosed helper')
measured=W/'delta-foliage-normal-decoder-gpu-v1/src/sample.wgsl'
pins[str(measured)]=sha(measured)
previous=function(prefix,'source_graph_normal_sample')
replacement=function(measured.read_text('utf-8-sig'),'source_graph_normal_sample')
assert 'if mode==2.0' in replacement and 'encoded.r*encoded.a' in replacement
assert prefix.count(previous)==1
prefix=prefix.replace(previous,replacement)
'''
code=code.replace(needle,injection+needle)
# Preserve the old emitter; only the decoder library function changes.
code=code.replace("source_inputs_unchanged=True,material_gpu_qualified=False","source_inputs_unchanged=True,normal_helper_mode2_added=True,material_gpu_qualified=False")
exec(compile(code,str(recipe)+'#mode2-library-fix','exec'),dict(__file__=str(Path(__file__).resolve()),__name__='__main__'))
