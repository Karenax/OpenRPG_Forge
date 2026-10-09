"""Queue four native normal references after the exact ten-texture GPU controller."""
from pathlib import Path

BASE=Path(__file__).with_name('queue_delta_foliage_normal_reference_v1.py')
SCRIPT=BASE.read_text('utf-8-sig')

def replace(old,new):
    global SCRIPT
    assert SCRIPT.count(old)==1, old
    SCRIPT=SCRIPT.replace(old,new)

replace("ROOT = W / 'unity-delta-foliage-normal-reference-v1'", "ROOT = W / 'unity-delta-rocks-firepit-normal-reference-v1'")
replace("STORAGE = W / 'unity-delta-foliage-texture-storage-v2'", "STORAGE = W / 'unity-delta-rocks-firepit-texture-storage-v1'")
replace("GPU = W / 'delta-foliage-texture-gpu-v3'", "GPU = W / 'delta-rocks-firepit-texture-gpu-v1'")
replace("kernel_path = W / 'delta-vector-node-wgsl-v3/RESULT.v1.json'", "kernel_path = W / 'nested-prefab-delta-material-wgsl-v1/RESULT.v1.json'")
replace("if texture['requires_normal_decode']}",
        "if texture['requires_normal_decode'] and row['material_guid'] in {'abc00000000014678987794877247210','abc00000000009294800760372203426'}}")
replace("copies_by_path = {row['project_copy']: row for row in source_prep['copies']}", """copies_by_path = {}
    mapped={row['copied']:row for row in source_prep['copies']}
    for row in source_prep['copies']:
        if row['copied'].endswith('.meta'): continue
        meta=mapped[row['copied']+'.meta']
        copies_by_path[row['copied']]=dict(original=row['original'],original_sha256=row['sha256'],
            original_meta=meta['original'],original_meta_sha256=meta['sha256'],
            project_copy=row['copied'],project_copy_sha256=row['sha256'],
            project_meta_copy=meta['copied'],project_meta_copy_sha256=meta['sha256'])
""")
replace("assert read(STORAGE / 'TEXTURE-REOPEN.v1.json')['source_texture_storage_captures_verified'] == 18",
        "assert read(STORAGE / 'TEXTURE-REOPEN.v1.json')['source_texture_storage_captures_verified'] == 10")
SCRIPT=SCRIPT.replace('== 6','== 4').replace('cases=6','cases=4').replace('normal_textures=6','normal_textures=4')
SCRIPT=SCRIPT.replace('native_normal_textures_verified=6','native_normal_textures_verified=4')
SCRIPT=SCRIPT.replace("'==6'", "'==4'").replace('"\'verified_textures\':6"','"\'verified_textures\':4"')
SCRIPT=SCRIPT.replace('DeltaFoliage','DeltaRocksFirePit').replace('OpenRPGFoliageProbe','OpenRPGRocksFirePitProbe')
SCRIPT=SCRIPT.replace('six_exact_foliage','four_exact_rocks_firepit').replace('six_native_foliage','four_native_rocks_firepit')
SCRIPT=SCRIPT.replace('waiting_for_exact_foliage_gpu_controller','waiting_for_exact_rocks_firepit_gpu_controller')
exec(compile(SCRIPT,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve())})
