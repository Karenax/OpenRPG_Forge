"""Adapt the texture-sampling binding only; preserve the unrelated POM branch."""
from pathlib import Path
parent=Path(__file__).with_name('generate_pirate_palm_native_bound_wgsl_v537.py')
code=parent.read_text('utf-8').replace('v537','v538')
old='needle="index=indices[t[\'guid\']]";assert source_emitter.count(needle)==1'
new='needle="t=ins[\'Texture\'][\'literal\'];index=indices[t[\'guid\']]";assert source_emitter.count(needle)==1'
assert code.count(old)==1;code=code.replace(old,new)
old='source_emitter=source_emitter.replace(needle,"index=indices[t[\'guid\'] if \'guid\' in t else t[\'native_sdk_resource\'][\'key\']]")'
new='source_emitter=source_emitter.replace(needle,"t=ins[\'Texture\'][\'literal\'];index=indices[t[\'guid\'] if \'guid\' in t else t[\'native_sdk_resource\'][\'key\']]")'
assert code.count(old)==1;code=code.replace(old,new)
code=code.replace('[generator,emitter,bp,Path(__file__)','[parent,generator,emitter,bp,Path(__file__)')
exec(compile(code,str(parent)+'#exact-texture-binding-adaptation','exec'),globals())
