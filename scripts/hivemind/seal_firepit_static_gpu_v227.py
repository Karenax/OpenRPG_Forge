"""Retain the actual static LOD GPU milestone and its explicit remaining gates."""
from pathlib import Path
import datetime, hashlib, json
W=Path('F:/OpenRPG_ImportWork');O=W/'firepit-static-gpu-checkpoint-v227'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
gpu=W/'firepit-contract-lod-gpu-v224/RUN-RESULT.v1.json'
review=W/'firepit-contract-lod-gpu-reopen-v226/RESULT.v1.json'
assembly=W/'firepit-complete-static-glb-v4/RESULT.v1.json'
reader=W/'firepit-native-state-conversion-v222/CURRENT-PRODUCT-READER.json'
r=read(review);g=read(gpu);a=read(assembly)['rows'][0];p=read(reader)
assert g['cargo_exit_code']==g['capture_exit_code']==0
assert r['status']=='four_authored_lod_diagnostic_readbacks_independently_reopened'
assert len(r['rows'])==4 and all(x['stable_byte_equal'] and x['source_pipelines']>=2 for x in r['rows'])
assert a['sha256']==p['sha256']=='9caa979ee462cf89cecab7e18416c169098662deb776bd3d248710bf49d30e7b'
assert p['header_negative_control_passed'] and p['shader_integrity_negative_control_passed']
assert sha(Path(a['glb']))==a['sha256']
pins=dict(r['input_pins'])
for row in r['rows']:pins[row['preview']]=row['preview_sha256']
pins[r['contact']]=r['contact_sha256']
for path in [Path(__file__),gpu,review,assembly,reader,W/'firepit-native-state-conversion-v222/RESULT.v1.json',W/'firepit-complete-static-glb-v4/RECIPE-REOPEN.v1.json']:pins[str(path)]=sha(path)
for path,h in pins.items():assert sha(path)==h,path
O.mkdir()
result=dict(status='current_common_host_firepit_four_static_lod_draws_verified',input_pins=pins,static_lod_draws=4,gpu_readbacks=8,stable_pairs_byte_equal=4,agent_visual_review=True,human_visual_review=False,visual_observation='Four stone-ring and wood-pile variants visible with textures; no background-only, cropped, pink or magenta error frame. Source lighting fidelity not compared. Fixed previews clip exposed HDR above one; raw data retained.',source_binary_and_other_document_fields_preserved=True,native_renderer_flags=6,native_lod_groups=1,new_fully_qualified_assets=0,automatic_lod_selection_verified=False,effects_rendered=False,source_lighting_parity=False,shadow_parity=False,studio_verified=False,placement_verified=False,full_import=False,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
block='''## Foyer L05 — quatre variantes LOD rendues par le chargeur actuel (v227)

**Rendu statique acquis : 4 LOD / 8 readbacks GPU RGBA16F**, deux captures identiques octet pour octet par niveau. Pipelines forward/prepass prêts, images finies et non vides, textures et silhouettes inspectées par l'agent. Aucun verdict humain ou Studio ajouté.

v220 conserve le refus réel du champ source absent. v222 ajoute les six états renderer et l'identité/activation du groupe LOD à partir des observations Unity conservées ; binaire géométrie/textures/shaders inchangé. Lecteur produit actuel : admission et deux corruptions refusées. v223 conserve le refus du banc BRDF inadapté. v224 compile le banc avec les sources actuelles et respecte les dépendances déclarées par le matériau ; v226 réouvre les huit captures. v225 assembleur v4 reproduit exactement le GLB enrichi, sans refaire les acquisitions source.

**0 nouvel asset complet qualifié.** Le rendu statique ne qualifie pas la sélection automatique des LOD, les deux renderers d'effets (flammes/fumée), les ombres, la fidélité d'éclairage Unity/Forge, le placement ou la bibliothèque dans la GUI réelle. Toutes les preuves restent sur F:, catalogue Vault inchangé par ce lot. Prochaine tranche : comportements LOD et effets/source lumière, puis qualification dans le produit ; poursuivre les images restantes sans refaire les lots livrés.

[Checkpoint](../firepit-static-gpu-checkpoint-v227/RESULT.v1.json) · [Captures réouvertes](../firepit-contract-lod-gpu-reopen-v226/RESULT.v1.json) · [Planche](../firepit-contract-lod-gpu-reopen-v226/CONTACT.png) · [Recette v4](../firepit-complete-static-glb-v4/RECIPE-REOPEN.v1.json).

'''
for name in ['POINT_DE_REPRISE.md','AVANCEMENT_ACTUEL.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;old=p.read_text('utf-8-sig');(O/('BEFORE-'+name)).write_text(old,'utf-8');p.write_text(block+old,'utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
