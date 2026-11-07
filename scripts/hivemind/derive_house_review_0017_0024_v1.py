"""Prepare the next sixteen captured views for individual visual inspection."""
from pathlib import Path
import subprocess

S = Path(__file__).parent
target = S / 'prepare_house_thumbnail_review_0017_0024_v1.py'
assert not target.exists()
text = (S / 'prepare_house_thumbnail_review_0009_0016_v1.py').read_text(encoding='utf-8')
text = text.replace('house-source-thumbnail-batch-0009-0016', 'house-source-thumbnail-batch-0017-0024')
target.write_text(text, encoding='utf-8')
subprocess.run(['C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe', '-B', str(target)], check=True)
