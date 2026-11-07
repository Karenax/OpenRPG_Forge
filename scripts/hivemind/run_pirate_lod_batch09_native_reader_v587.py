"""Verify the sixteen installed images through the current native product reader."""
from pathlib import Path
parent=Path(__file__).with_name('run_pirate_lod_thumbnail_batch03_native_reader_v373.py')
code=parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch03-native-reader-v373','pirate-lod-thumbnail-batch09-native-reader-v587').replace('pirate-lod-thumbnail-batch03-index-v372','pirate-lod-thumbnail-batch09-index-v586')
code=code.replace('inputs=[Path(__file__).resolve(),INDEX','inputs=[parent,Path(__file__).resolve(),INDEX')
exec(compile(code,str(parent)+'#lot09-current-native-reader','exec'),globals())
