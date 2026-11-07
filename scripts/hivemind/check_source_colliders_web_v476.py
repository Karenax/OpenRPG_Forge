"""Check the common collider adapter in actual Studio WebGPU sources."""
from pathlib import Path
parent=Path(__file__).with_name('check_gladiator_lod_web_v463.py')
code=parent.read_text('utf-8').replace('gladiator-lod-studio-web-check-v463','source-colliders-studio-web-check-v476')
code=code.replace("files=[R/'Cargo.toml'", "files=[R/'crates/forge_engine/src/source_colliders.rs',R/'crates/forge_engine/src/lib.rs',parent,R/'Cargo.toml'")
exec(compile(code,str(parent)+'#source-colliders-v476','exec'),globals())
