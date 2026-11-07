"""Measure private fine-UV1 filtering against original in-pass source normals."""
from pathlib import Path
import subprocess
import sys

S=Path(__file__).parent
out=S/'compare_townsmith_fine_uv1_v39.py'
assert not out.exists()
q=(S/'compare_townsmith_source_uv1_v36.py').read_text(encoding='utf-8')
for old,new in [
 ('townsmith-native-source-uv1-v36','townsmith-native-fine-uv1-v39'),
 ('townsmith-source-uv1-comparison-v36','townsmith-fine-uv1-comparison-v39'),
 ('townsmith-source-uv1-carrier-v36','townsmith-fine-uv1-carrier-v39'),
 ('Original implicit sampling with preserved authored UV1 for RGBMask','Private explicit fine gradients for UV1 RGBMask only; source implicit sampling unchanged'),
 ('authored_uv1_material_control_compared_fidelity_open','private_fine_uv1_filtering_material_control_compared_fidelity_open'),
 ('Correction du canal UV1 du masque uniquement','Contrôle de filtrage fin UV1 uniquement ; la référence Unity originale reste inchangée'),
 ('MATERIAUX.UV1','MATERIAUX.UV1.FILTRAGE'),
]:
 assert old in q,old
 q=q.replace(old,new)
compile(q,str(out),'exec')
out.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(out)],check=True)
