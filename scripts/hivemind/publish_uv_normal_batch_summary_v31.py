"""Publish the completed diagnostic tranche without upgrading material readiness."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
w=Path('F:/OpenRPG_ImportWork');t=w/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
normal_proof=w/'townsmith-door-source-inpass-normal-reopen-v31/RESULT.v1.json'
uv_proof=w/'townsmith-fine-uv-comparison-v28/RESULT.v1.json'
material_proof=w/'townsmith-fine-sampling-comparison-v29/RESULT.v1.json'
normal=read(normal_proof);uv=read(uv_proof);material=read(material_proof)
assert normal['actual_camera_normal_binding_qualified'] and not material['full_material_fidelity_qualified']
prior=read(w/'townsmith-door-normal-roughness-comparison-v4/RESULT.v1.json')
now=datetime.now(timezone.utc).isoformat()
p=t/'SUIVI_LISIBLE.md';text=p.read_text(encoding='utf-8')
start=text.index('## Travail actif');end=text.index('## Découpage et ordre')
section=f'''## Travail actif

**L03 · Townsmith · matériau de la porte : échantillonnage des textures et repère tangent.**

La tranche UV/référence est terminée techniquement : six canaux natifs comparés ; 36 captures Unity pour les dérivées fines ; 12 lectures du vrai buffer de normales pendant le rendu. La couverture correspond sur 34 445 pixels. Les lectures de normales après rendu ont été corroborées par le vrai buffer caméra dans trois contrôles. Les sources et la référence originale sont inchangées.

Le contrôle de gradients fins réduit fortement l'écart des dérivées horizontales, mais presque pas celui du matériau : angle moyen des normales {prior['source_world_normal_mean_angle_degrees']:.6f}° → {material['source_world_normal_mean_angle_degrees']:.6f}°, maximum inchangé {material['source_world_normal_max_angle_degrees']:.6f}°. Il reste un diagnostic ; l'échantillonnage original est conservé.

Prochain sous-lot : comparer les valeurs filtrées des textures de normales et leur transformation dans le repère tangent, avec la référence caméra désormais qualifiée. Puis terminer le matériau, assembler les prefabs et vérifier leur usage dans Studio.

**Aucun nouvel asset complet qualifié par cette tranche. Matériau complet, prefabs et parcours Studio restent ouverts.**

[UV et dérivées](../townsmith-fine-uv-comparison-v28/RESULT.v1.json) · [Contrôle matériau](../townsmith-fine-sampling-comparison-v29/RESULT.v1.json) · [Vraies normales caméra](../townsmith-door-source-inpass-normal-reopen-v31/RESULT.v1.json).

'''
p.write_text(text[:start]+section+text[end:],encoding='utf-8')
receipt=dict(status='uv_and_normal_reference_diagnostic_tranche_delivered',finished_utc=now,
             input_pins={str(p):sha(p) for p in [Path(__file__),normal_proof,uv_proof,material_proof]},
             agent_visual_review=dict(image=str(w/'townsmith-fine-sampling-comparison-v29/normal-roughness-controls.png'),
                                      sha256=sha(w/'townsmith-fine-sampling-comparison-v29/normal-roughness-controls.png'),
                                      observation='Same isolated door coverage and overall normals/roughness appearance; measured normal differences remain. No human or Studio review.'),
             full_import=False,material_fidelity_qualified=False,editor_visual_verified=False,complete_assets_delivered_increment=0)
out=t/'TRANCHE_UV_NORMALES_V31.v1.json';assert not out.exists();out.write_text(json.dumps(receipt,indent=2),encoding='utf-8')
p=t/'SUIVI_SIMPLE.v1.json';data=read(p)
data.update(updated_utc=now,active_sub_lot='L03.TOWNSMITH.MATERIAUX.TEXTURES.NORMALES',
            current_work_state='Previous diagnostic controllers terminal. Next sub-lot: filtered normal textures and tangent frame.',
            current_work_receipt=str(out),current_source_work_receipt=str(normal_proof),
            state=receipt['status'],next_action='Capture source/native filtered normal texture values and tangent transforms using the qualified camera normal reference; preserve original implicit sampling.')
tmp=p.with_suffix('.v31.tmp');tmp.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8');tmp.replace(p)
print(json.dumps(dict(summary=str(out),dashboard=str(t/'SUIVI_LISIBLE.md'),full_import=False)))
