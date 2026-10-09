from pathlib import Path
BASE_INSTANCE=Path(__file__).with_name('reopen_firepit_gpu_frame_matrix_v1.py')
source=BASE_INSTANCE.read_text('utf-8').replace('firepit-gpu-frame-matrix-v1','firepit-gpu-frame-instance-fix-v1').replace('firepit-gpu-frame-matrix-case-v1','firepit-gpu-frame-instance-fix-case-v1').replace('firepit-gpu-frame-matrix-reopen-v1','firepit-gpu-frame-instance-fix-reopen-v1')
source=source.replace("if mask[3]!=.5:continue", "if pixel[3] in [.25,1.0]:continue")
source=source.replace('cpu_uniform_equality_measured=True,','cpu_uniform_equality_measured=True,current_model_mask_protocol_applied=True,explicit_entrypoint_instance_transport=True,')
exec(compile(source,str(BASE_INSTANCE),'exec'),globals())
