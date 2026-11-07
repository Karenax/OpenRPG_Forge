"""Derive comparison against corroborated in-pass original camera normals."""
from pathlib import Path
import sys
import subprocess

S=Path(__file__).parent
out=S/'compare_townsmith_source_uv1_v36.py'
assert not out.exists()
q=(S/'compare_townsmith_fine_sampling_v29.py').read_text(encoding='utf-8')
for old,new in [
 ('townsmith-paint-door-native-fine-sampling-v29','townsmith-native-source-uv1-v36'),
 ('townsmith-fine-sampling-comparison-v29','townsmith-source-uv1-comparison-v36'),
 ('townsmith-paint-door-fine-sampling-carrier-v29','townsmith-source-uv1-carrier-v36'),
 ('Explicit fine texture gradients; source reference retains original implicit sampling','Original implicit sampling with preserved authored UV1 for RGBMask'),
 ('fine_gradient_material_control_compared_fidelity_open','authored_uv1_material_control_compared_fidelity_open'),
 ('Contrôle de gradients fins uniquement','Correction du canal UV1 du masque uniquement'),
 ('MATERIAUX.SAMPLING.FIN','MATERIAUX.UV1'),
 ('L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.MATERIAUX.SAMPLING','L03.TOWNSMITH.MATERIAUX.UV1'),
 ('Compare source/native implicit texture sampling and derivatives; capture native depth buffer values, preserving the exact coverage proof.','Recompare corrected full material lighting and graph intermediates before prefab and Studio qualification.'),
 ('source_global_buffer_lifetime_qualified=False','source_normal_buffer_inpass_binding_qualified=True, source_global_buffer_lifetime_qualified=False'),
]:
 assert old in q,old
 q=q.replace(old,new)
anchor='for path, expected in pins.items():'
assert q.count(anchor)==1
q=q.replace(anchor,'''camera_proof=W/'townsmith-door-source-inpass-normal-reopen-v31/RESULT.v1.json'
camera_pixels=W/'townsmith-door-source-inpass-normal-v31/door-nopost-linear.inpass-normal-3.rgba32f'
assert sha(source_path)==sha(camera_pixels)
pins.update(read(camera_proof)['input_pins'])
pins[str(camera_proof)]=sha(camera_proof)
pins[str(camera_pixels)]=sha(camera_pixels)
'''+anchor)
compile(q,str(out),'exec')
out.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(out)],check=True)
