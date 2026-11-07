"""Compare the private fine-gradient material to the unchanged source reference."""
from pathlib import Path
import subprocess, sys
s=Path(__file__).parent
target=s/'compare_townsmith_fine_sampling_v29.py'
assert not target.exists()
q=(s/'compare_townsmith_normal_roughness_v4.py').read_text(encoding='utf-8')
q=q.replace('townsmith-paint-door-native-normal-roughness-draw-v4','townsmith-paint-door-native-fine-sampling-v29')
q=q.replace('townsmith-door-normal-roughness-comparison-v4','townsmith-fine-sampling-comparison-v29')
q=q.replace('townsmith-paint-door-normal-roughness-carrier-v4','townsmith-paint-door-fine-sampling-carrier-v29')
q=q.replace('townsmith-door-source-gbuffer-arrays-v21/door-nopost-linear','townsmith-door-source-fine-uv-v28/door-nopost-linear')
anchor="coverage_path = W / 'townsmith-paint-door-native-diffuse-material-draw-v3/FORGE-LINEAR.rgba16f'"
assert q.count(anchor)==1
q=q.replace(anchor,anchor+'''
source_work=W/'townsmith-door-source-fine-uv-v28'
source_run=read(source_work/'RUN-RESULT.v1.json')
assert source_run['actual_exit_code']==0 and source_run['source_inputs_unchanged'] and source_run['neutral_reference_byte_equal']
pins.update(read(source_work/'PREPARED.v1.json')['input_pins'])
for path in [source_work/'RUN-RESULT.v1.json',source_work/'NATIVE-RESULT.v1.json',W/'townsmith-fine-uv-comparison-v28/RESULT.v1.json']:
    pins[str(path)]=sha(path)
# Original ForceEnable GBuffer normal/roughness observation unchanged by UV instrumentation.
assert sha(source_path)==sha(W/'townsmith-door-source-anisotropy-controls-v23/door-nopost-linear._GBufferTexture1.rgba32f')
''')
q=q.replace("result = dict(status='native_source_normal_and_roughness_codes_compared_fidelity_open'", "result = dict(diagnostic_sampling='Explicit fine texture gradients; source reference retains original implicit sampling',status='fine_gradient_material_control_compared_fidelity_open'")
q=q.replace('MATERIAUX.NORMALES : comparaison vérifiée','MATERIAUX.SAMPLING.FIN : contrôle causal comparé')
q=q.replace('Échantillonnage implicite et profondeur native à isoler ; fidélité ouverte.', 'Contrôle de gradients fins uniquement ; fidélité originale encore ouverte.')
compile(q,str(target),'exec');target.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(target)],check=True)
