"""Retain and reuse the current oriented-shadow binary, with all input pins."""
from pathlib import Path
base=Path(__file__).with_name('capture_reused_source_scene_v4.py')
source=base.read_text('utf-8').replace('firepit-source-shadow-origin-native-v1','firepit-source-shadow-oriented-native-v1').replace('source-scene-capture-binary-v4','source-scene-capture-binary-v5')
exec(compile(source,str(base),'exec'),globals())
