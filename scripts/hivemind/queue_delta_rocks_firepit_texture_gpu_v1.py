"""Reuse native texture/sampler GPU probe for ten new textures and 120 mips."""
from pathlib import Path

BASE=Path(__file__).with_name('queue_delta_foliage_texture_gpu_v1.py')
SCRIPT=BASE.read_text('utf-8-sig')
REUSE=Path('F:/OpenRPG_ImportWork/delta-foliage-texture-gpu-v2')

def replace(old,new):
    global SCRIPT
    assert SCRIPT.count(old)==1, old
    SCRIPT=SCRIPT.replace(old,new)

replace("ROOT = W / 'delta-foliage-texture-gpu-v1'", "ROOT = W / 'delta-rocks-firepit-texture-gpu-v1'")
replace("CAPTURE = W / 'unity-delta-foliage-texture-storage-v2'", "CAPTURE = W / 'unity-delta-rocks-firepit-texture-storage-v1'")
replace("check({row['path']: row['sha256'] for row in prior_pins})",
        "check({row['path']: row['sha256'] for row in prior_pins if Path(row['path']).is_relative_to(OLD)})")
replace("pins = dict(read(CAPTURE / 'PREPARED.v1.json')['input_pins'])",
        "pins = dict(reopened['input_pins'])\n    check(pins)")
replace("shutil.copyfile(OLD / 'src' / name, ROOT / 'src' / name)",
        "source = {'imported_texture.rs': REPO / 'crates/forge_engine/src/source_graph_imported_texture.rs', 'sampler.rs': REPO / 'crates/forge_engine/src/source_graph_sampler.rs'}.get(name, OLD / 'src' / name)\n        shutil.copyfile(source, ROOT / 'src' / name)\n        pins[str(source)] = sha(source)")
replace("cargo = (OLD / 'Cargo.toml').read_text('utf-8').replace('openrpg-imported-texture-gpu-probe-v2', 'openrpg-delta-foliage-texture-gpu-v1')",
        "cargo = (REUSE / 'Cargo.toml').read_text('utf-8')\n    pins[str(REUSE / 'Cargo.toml')] = sha(REUSE / 'Cargo.toml')")
replace("shutil.copyfile(OLD / 'Cargo.lock', ROOT / 'Cargo.lock')",
        "shutil.copyfile(REUSE / 'Cargo.lock', ROOT / 'Cargo.lock')\n    pins[str(REUSE / 'Cargo.lock')] = sha(REUSE / 'Cargo.lock')")
replace("len(expansions) == 3", "len(expansions) == 0")
# Only batch cardinalities change; algorithms, shader, layouts and samplers stay exact.
SCRIPT=SCRIPT.replace('18','10').replace('225','120').replace('eighteen','ten')
exec(compile(SCRIPT,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve()),'REUSE':REUSE})
