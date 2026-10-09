"""Measure the exact normal helper present in both newly composed full programs."""
from pathlib import Path

BASE=Path(__file__).with_name('queue_delta_foliage_normal_decoder_gpu_v1.py')
SCRIPT=BASE.read_text('utf-8-sig')

def replace(old,new):
    global SCRIPT
    assert SCRIPT.count(old)==1, old
    SCRIPT=SCRIPT.replace(old,new)

replace("ROOT = W / 'delta-foliage-normal-decoder-gpu-v1'", "ROOT = W / 'delta-rocks-firepit-normal-decoder-gpu-v1'")
replace("RAW = W / 'delta-foliage-texture-gpu-v3'", "RAW = W / 'delta-rocks-firepit-texture-gpu-v1'")
replace("BUILD = W / 'delta-foliage-texture-gpu-v2'", "BUILD = W / 'delta-rocks-firepit-texture-gpu-v1'")
replace("REFERENCE = W / 'unity-delta-foliage-normal-reference-v1'", "REFERENCE = W / 'unity-delta-rocks-firepit-normal-reference-v1'")
replace("PROGRAMS = W / 'delta-native-lit-fragments-rgorag-v2'", "PROGRAMS = W / 'delta-rocks-firepit-native-lit-v1'")
replace("raw_path = W / 'delta-foliage-normal-raw-gpu-parity-v1/RESULT.v1.json'", "raw_path = W / 'delta-rocks-firepit-normal-raw-gpu-parity-v1/RESULT.v1.json'")
replace("assert reference['verified_textures'] == programs['native_lit_programs_reopened'] == 6",
        "assert reference['verified_textures'] == 4 and programs['native_lit_programs_reopened'] == 2")
SCRIPT=SCRIPT.replace('len(rows) == 6','len(rows) == 4').replace('== 75','== 48')
SCRIPT=SCRIPT.replace('assert_eq!(results.len(), 18);','assert_eq!(results.len(), 10);')
SCRIPT=SCRIPT.replace('assert_eq!(results.len(), 6);','assert_eq!(results.len(), 4);')
SCRIPT=SCRIPT.replace('assert_eq!(total_mips, 225);','assert_eq!(total_mips, 120);')
SCRIPT=SCRIPT.replace('assert_eq!(total_mips, 75);','assert_eq!(total_mips, 48);')
SCRIPT=SCRIPT.replace('"gpu_sampled_textures":18','"gpu_sampled_textures":10')
SCRIPT=SCRIPT.replace('"gpu_sampled_textures":6','"gpu_sampled_textures":4')
SCRIPT=SCRIPT.replace("after = 'openrpg-delta-foliage-texture-gpu-v1'", "after = 'openrpg-delta-foliage-texture-gpu-v1'")
SCRIPT=SCRIPT.replace("'openrpg-delta-foliage-normal-decoder-gpu-v1'", "'openrpg-delta-rocks-firepit-normal-decoder-gpu-v1'")
SCRIPT=SCRIPT.replace('debug/openrpg-delta-foliage-normal-decoder-gpu-v1.exe','debug/openrpg-delta-rocks-firepit-normal-decoder-gpu-v1.exe')
SCRIPT=SCRIPT.replace("'unity-delta-foliage-normal-reference-v1'", "'unity-delta-rocks-firepit-normal-reference-v1'")
SCRIPT=SCRIPT.replace("'delta-foliage-normal-raw-gpu-parity-v1/RESULT.v1.json'", "'delta-rocks-firepit-normal-raw-gpu-parity-v1/RESULT.v1.json'")
SCRIPT=SCRIPT.replace("'==6'", "'==4'").replace("'==75'", "'==48'")
SCRIPT=SCRIPT.replace("exact_normal_textures_compared\\':6", "exact_normal_textures_compared\\':4")
SCRIPT=SCRIPT.replace('all_six_materials','both_materials').replace('expected_textures=6','expected_textures=4')
SCRIPT=SCRIPT.replace('expected_mips=75','expected_mips=48').replace('normal_textures=6, mips=75','normal_textures=4, mips=48')
SCRIPT=SCRIPT.replace('building_exact_six_material','building_exact_two_material').replace('sampling_exact_six_material','sampling_exact_two_material')
SCRIPT=SCRIPT.replace('== 6','== 4').replace('native_normal_textures_verified=6','native_normal_textures_verified=4')
SCRIPT=SCRIPT.replace('normal_mips_verified=75','normal_mips_verified=48').replace('six_exact_candidate_decoders','four_exact_candidate_decoders')
exec(compile(SCRIPT,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve())})
