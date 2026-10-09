"""Native raw sampling for both images; normal decode applies to only one."""
from pathlib import Path
BASE=Path(__file__).with_name('queue_delta_foliage_normal_reference_v1.py')
SCRIPT=BASE.read_text('utf-8-sig')
def once(old,new):
    global SCRIPT
    assert SCRIPT.count(old)==1,old;SCRIPT=SCRIPT.replace(old,new)
once("ROOT = W / 'unity-delta-foliage-normal-reference-v1'","ROOT = W / 'detail-native-sampling-reference-v1'")
once("STORAGE = W / 'unity-delta-foliage-texture-storage-v2'","STORAGE = W / 'detail-native-texture-storage-v1'")
once("GPU = W / 'delta-foliage-texture-gpu-v3'","GPU = W / 'detail-texture-gpu-v1'")
once("kernel_path = W / 'delta-vector-node-wgsl-v3/RESULT.v1.json'","kernel_path = W / 'detail-native-default-kernel-v1/RESULT.v1.json'")
once("guids = {texture['guid'] for row in kernels['rows'] for texture in row['source_texture_bindings']\n             if texture['requires_normal_decode']}",
     "guids = {texture['guid'] for texture in kernels['source_texture_bindings']}\n    normal_guids = [texture['guid'] for texture in kernels['source_texture_bindings'] if texture['requires_normal_decode']]\n    assert len(normal_guids)==1")
once("copies_by_path = {row['project_copy']: row for row in source_prep['copies']}","""copies_by_path = {}
    by_copy = {row['copy']:row for row in source_prep['copies']}
    for row in source_prep['copies']:
        if row['is_meta']: continue
        meta = by_copy[row['copy']+'.meta']
        copies_by_path[row['copy']] = dict(original=row['source'],original_sha256=row['sha256'],
            original_meta=meta['source'],original_meta_sha256=meta['sha256'],project_copy=row['copy'],
            project_copy_sha256=row['sha256'],project_meta_copy=meta['copy'],project_meta_copy_sha256=sha(meta['copy']))
""")
once("assert read(STORAGE / 'TEXTURE-REOPEN.v1.json')['source_texture_storage_captures_verified'] == 18",
     "assert read(STORAGE / 'TEXTURE-REOPEN.v1.json')['source_texture_storage_captures_verified'] == 2")
SCRIPT=SCRIPT.replace('== 6','== 2').replace('cases=6','cases=2').replace('normal_textures=6','source_texture_references=2')
SCRIPT=SCRIPT.replace('native_normal_textures_verified=6','native_source_texture_references_verified=2')
SCRIPT=SCRIPT.replace("'==6'","'==2'").replace('"\'verified_textures\':6"','"\'verified_textures\':2"')
SCRIPT=SCRIPT.replace('DeltaFoliage','DetailSource').replace('OpenRPGFoliageProbe','OpenRPGDetailProbe')
SCRIPT=SCRIPT.replace('Assets/DetailSourceTextures/','Assets/DetailOriginalTextures/')
SCRIPT=SCRIPT.replace('six_exact_foliage','two_detail_source').replace('six_native_foliage','two_native_detail_source')
SCRIPT=SCRIPT.replace('waiting_for_exact_foliage_gpu_controller','waiting_for_exact_detail_gpu_controller')
once("copies=copies, input_proofs=pins, wait_pid=pid, wait_birth_ticks=birth,",
     "copies=copies, input_proofs=pins, wait_pid=pid, wait_birth_ticks=birth, normal_applicable_guids=normal_guids, raw_sampling_guids=sorted(guids), colour_normal_diagnostic_not_used_as_material=True,")
if __name__=='__main__':
    exec(compile(SCRIPT,str(BASE),'exec'),dict(__file__=str(Path(__file__).resolve()),__name__='__main__'))
