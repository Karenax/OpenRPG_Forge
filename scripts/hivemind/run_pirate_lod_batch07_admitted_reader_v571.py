"""Verify the ten lot07 previews through the current native library reader."""
from pathlib import Path
parent=Path(__file__).with_name('run_pirate_lod_batch06_admitted_reader_v521.py')
code=parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch06-admitted-reader-v521','pirate-lod-thumbnail-batch07-admitted-reader-v571').replace('pirate-lod-thumbnail-batch06-admitted-index-v520','pirate-lod-thumbnail-batch07-admitted-index-v570')
exec(compile(code,str(parent)+'#lot07-native-reader','exec'),globals())
