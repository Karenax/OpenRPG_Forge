"""Test explicit reciprocal multiplication for the captured FXC remainder path."""
from pathlib import Path
parent=Path(__file__).with_name('probe_palm_legacy_remainder_v549.py')
code=parent.read_text('utf-8').replace('v549','v550')
old='return sign(p) * fract(abs(p) / q) * q;'
new='return sign(p) * fract(abs(p) * (1.0 / q)) * q;'
assert code.count(old)==1
code=code.replace(old,new)
exec(compile(code,str(parent)+'#explicit-reciprocal-test','exec'),globals())
