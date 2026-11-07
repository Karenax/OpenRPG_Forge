"""Repeat the identical source capture in a new immutable Unity job."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'queue_townsmith_sky_capture_v9.py'
assert not target.exists()
text = (S / 'queue_townsmith_sky_capture_v8.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-sky-capture-v8', 'townsmith-door-source-sky-capture-v9')
text = text.replace('SourceTownsmithSkyCaptureV8', 'SourceTownsmithSkyCaptureV9')
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
