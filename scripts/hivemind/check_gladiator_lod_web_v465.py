"""Recheck after the two existing WASM derive imports were repaired."""
from pathlib import Path
parent=Path(__file__).with_name('check_gladiator_lod_web_v463.py')
code=parent.read_text('utf-8').replace('gladiator-lod-studio-web-check-v463','gladiator-lod-studio-web-check-v465')
code=code.replace("'source_graph_state.rs']", "'source_graph_state.rs','source_graph_resources.rs','fish_shadow_scene.rs']")
exec(compile(code,str(parent)+'#derive-imports-v465','exec'),globals())
