"""Retain prior harness refusals; mutate Bevy's material guard explicitly."""
from pathlib import Path
BASE = Path(__file__).with_name('capture_firepit_brdf_lookup_gpu_v2.py')
source = BASE.read_text('utf-8').replace('firepit-brdf-lookup-gpu-v2', 'firepit-brdf-lookup-gpu-v3')
needle = "exec(compile(source, str(BASE), 'exec'), globals())"
assert source.count(needle) == 1
source = source.replace(needle,
    "source=source.replace('let material=assets.get_mut(h).unwrap();','let mut material=assets.get_mut(h).unwrap();')\n" + needle)
exec(compile(source, str(BASE), 'exec'), globals())
