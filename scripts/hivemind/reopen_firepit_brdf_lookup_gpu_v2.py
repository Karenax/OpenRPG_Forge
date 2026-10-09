"""Reopen corrected GPU harness using the original, unchanged 35-case protocol."""
from pathlib import Path
BASE = Path(__file__).with_name('reopen_firepit_brdf_lookup_gpu_v1.py')
source = BASE.read_text('utf-8')
needle = "source = source.replace('range(6)', 'range(35)')"
assert source.count(needle) == 1
source = source.replace(needle,
    "source = source.replace('firepit-brdf-lookup-gpu-v1','firepit-brdf-lookup-gpu-v2').replace('firepit-brdf-lookup-gpu-reopen-v1','firepit-brdf-lookup-gpu-reopen-v2')\n" + needle)
exec(compile(source, str(BASE), 'exec'), globals())
