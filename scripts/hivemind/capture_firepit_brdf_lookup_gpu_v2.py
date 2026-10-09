"""Retain v1 refusal; use inferred SHA type instead of an undeclared crate."""
from pathlib import Path
BASE = Path(__file__).with_name('capture_firepit_brdf_lookup_gpu_v1.py')
source = BASE.read_text('utf-8').replace('firepit-brdf-lookup-gpu-v1', 'firepit-brdf-lookup-gpu-v2')
old = 'let brdf_sha=forge_core::ContentHash::parse(case["brdf_lookup_protocol"]["inputs"]["sha256"].as_str().unwrap()).unwrap();'
new = 'let brdf_sha=case["brdf_lookup_protocol"]["inputs"]["sha256"].as_str().unwrap().parse().unwrap();'
assert source.count(old) == 1
source = source.replace(old, new)
exec(compile(source, str(BASE), 'exec'), globals())
