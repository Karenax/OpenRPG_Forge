"""Match the native MAD combining world X and time before gradient noise."""
from pathlib import Path
parent=Path(__file__).with_name('verify_palm_centered_noise_v556.py')
code=parent.read_text('utf-8').replace('v556','v561')
old=".replace('let n14=vec4<f32>(((n1)+(n13)).x);','let n14=vec4<f32>(fma(n9.x,n12.x,n1.x));')"
new=old+".replace('let n6=vec4<f32>(((n4)+(n5)).x);','let n6=vec4<f32>(fma(n3.x,3.0,n5.x));')"
assert code.count(old)==1;code=code.replace(old,new)
exec(compile(code,str(parent)+'#native-world-time-mad','exec'),globals())
