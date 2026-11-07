"""Test the fractional quotient remainder hypothesis; no production admission."""
from pathlib import Path
parent=Path(__file__).with_name('run_palm_noise_wgsl_intermediates_v548.py')
source=parent.read_text('utf-8').replace('v548','v549')
needle="exec(compile(code,str(parent)+'#matched-noise-intermediates','exec'),globals())"
injection='''
old="input_struct=re.search"
new="helpers=helpers.replace('return p - trunc(p / q) * q;', 'return sign(p) * fract(abs(p) / q) * q;')\\ninput_struct=re.search"
assert code.count(old)==1
code=code.replace(old,new)
'''+needle
assert source.count(needle)==1
source=source.replace(needle,injection)
exec(compile(source,str(parent)+'#fractional-quotient-remainder-probe','exec'),globals())
