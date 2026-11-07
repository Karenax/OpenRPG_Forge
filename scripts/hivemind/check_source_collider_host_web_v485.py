"""Compile the current Studio host and exported source collision projection."""
from pathlib import Path
parent = Path(__file__).with_name('check_gladiator_lod_web_v463.py')
code = parent.read_text('utf-8').replace('gladiator-lod-studio-web-check-v463', 'source-collider-host-studio-web-check-v485')
code = code.replace("files=[R/'Cargo.toml'", "files=[parent,R/'crates/forge_engine/src/source_colliders.rs',R/'crates/forge_engine/src/source_collider_projection.rs',R/'crates/forge_engine/src/lib.rs',R/'Cargo.toml'")
exec(compile(code, str(parent) + '#source-collider-host-v485', 'exec'), globals())
