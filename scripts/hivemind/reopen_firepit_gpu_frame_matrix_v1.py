"""Decode original f32 bits from four byte channels at a fixed reference mask."""
from pathlib import Path
from datetime import datetime,timezone
from collections import Counter
import json,struct,hashlib,math
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-gpu-frame-matrix-v1';CASE=W/'firepit-gpu-frame-matrix-case-v1';ROOT=W/'firepit-gpu-frame-matrix-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json');protocol=read(CASE/'RESULT.v1.json');previous=read(W/'firepit-surface-native-reopen-v1/RESULT.v1.json')
    assert run['actual_exit_code']==0 and read(CAP/'GPU-PAIR.v1.json')['actual_gpu_frame_matrix_diagnostic']
    for pins in [prep['input_pins'],protocol['input_pins']]:
        for p,h in pins.items():assert sha(p)==h,p
    rows=[];files=[CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',CASE/'RESULT.v1.json']
    for view in range(2):
        mask_path=W/f'firepit-surface-native-v1/gpu-{view}.rgba16f';assert sha(mask_path)==previous['input_pins'][str(mask_path)]
        reference=list(struct.iter_unpack('<4e',mask_path.read_bytes()));p=CAP/f'gpu-{view}.rgba16f';raw=list(struct.iter_unpack('<4e',p.read_bytes()));assert len(raw)==len(reference)==512*512
        observations=[Counter() for _ in range(96)]
        for i,(pixel,mask) in enumerate(zip(raw,reference)):
            if mask[3]!=.5:continue
            assert all(math.isfinite(v) and 0<=v<=1 for v in pixel)
            encoded=bytes(round(v*255) for v in pixel);observations[i%96][encoded]+=1
        matrices=[]
        for group,label in enumerate(protocol['matrix_labels']):
            values=[];sample_counts=[]
            for component in range(16):
                counts=observations[group*16+component];assert len(counts)==1,(label,component,counts)
                data,count=next(iter(counts.items()));assert count>=protocol['minimum_samples_per_scalar'];value=struct.unpack('<f',data)[0];assert math.isfinite(value);values.append(value);sample_counts.append(count)
            matrices.append(dict(label=label,values_column_major=values,sample_counts=sample_counts))
        camera_path=CAP/f'CAMERA-STATE-{view}.json';camera=read(camera_path)
        cpu={'world_from_view':camera['camera_world'],'world_from_local':camera['draw_transforms'][0]['world_from_local'],**camera['source_uniform_rows'][0]}
        differences={m['label']:dict(maximum_absolute_difference=max(abs(a-b) for a,b in zip(m['values_column_major'],cpu[m['label']])),equal_numeric=m['values_column_major']==cpu[m['label']]) for m in matrices if m['label'] in cpu}
        rows.append(dict(view=view,matrices=matrices,cpu_field_differences=differences,model_samples=sum(sum(v.values()) for v in observations)))
        files.extend([p,mask_path,camera_path])
    result=dict(status='actual_gpu_six_frame_matrices_96_scalar_bits_reopened',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,scalar_cases=96,views_verified=2,source_pins_verified=len(prep['input_pins']),encoded_bytes_stable_across_all_samples=True,cpu_uniform_equality_measured=True,shader_vertex_position_not_yet_read_back=True,full_import=False,render_fidelity_qualified=False,editor_visual_verified=False,input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),*files]},next_action='Use actual GPU matrices to project preserved positions and test vertex output/mesh storage before changing materials or lighting.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','rows']}));print(json.dumps([r['cpu_field_differences'] for r in rows]))
if __name__=='__main__':main()
