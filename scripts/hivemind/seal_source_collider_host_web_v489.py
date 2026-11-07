"""Seal the actual Studio compile of the exported collider projection and host."""
from pathlib import Path
parent = Path(__file__).with_name('seal_source_colliders_web_v478.py')
code = parent.read_text('utf-8').replace('source-colliders-web-delivery-v478', 'source-collider-host-web-delivery-v489').replace('source-colliders-studio-web-check-v476', 'source-collider-host-studio-web-check-v485')
code = code.replace('actual_studio_webgpu_source_box_adapter_compile_sealed', 'actual_studio_webgpu_exported_collider_projection_and_host_compile_sealed')
code = code.replace('Le nouveau module commun source_colliders et son export sont épinglés', "Les modules source_colliders, source_collider_projection, leur export moteur et le host Studio raccordé sont épinglés")
code = code.replace('La comparaison native Rapier reste suivie dans gladiator-rapier-box-query-v475/RUN-RESULT.v1.json ; reprendre son exécution existante, sans la redémarrer sur timeout.', "Le contrôle de projection précédent est scellé en v484. Le replay du host réel et des 207 cycles LOD reste suivi dans gladiator-source-collider-host-cycle-v488/RUN-RESULT.v1.json ; reprendre ce même job.")
exec(compile(code, str(parent) + '#host-web-v489', 'exec'), globals())
