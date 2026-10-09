"""Corrected harness using the exact ContentHash::parse API; retain v1 evidence."""
from pathlib import Path
BASE = Path(__file__).with_name('check_source_brdf_storage_v1.py')
source = BASE.read_text('utf-8').replace(
    "source = BASE.read_text('utf-8').replace('source-diffuse-probe-packet-check-v1', 'source-brdf-storage-check-v1')",
    "source = BASE.read_text('utf-8').replace('source-diffuse-probe-packet-check-v1', 'source-brdf-storage-check-v2')")
exec(compile(source, str(BASE), 'exec'), globals())
