"""Reuse the pinned point-shadow diagnostic binary for shader-only controls."""
from pathlib import Path
base=Path(__file__).with_name('capture_reused_source_scene_v4.py')
source=base.read_text('utf-8').replace('firepit-source-shadow-origin-native-v1','firepit-point-shadow-diagnostics-native-v1').replace('source-scene-capture-binary-v4','source-scene-capture-binary-v6')
source=source.replace("'depth-probe.wgsl','caster-view-probe.wgsl']","'depth-probe.wgsl','caster-view-probe.wgsl','point-record-probe.wgsl']")
exec(compile(source,str(base),'exec'),globals())
