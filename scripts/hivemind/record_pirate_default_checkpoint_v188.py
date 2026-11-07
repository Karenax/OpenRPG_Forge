"""Retain the remaining stencil blocker and update the lot dashboard honestly."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, re

W=Path('F:/OpenRPG_ImportWork');O=W/'pirate-material-stencil-diagnostic-v188'
P=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
SDK=P/'Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
audit=read(W/'pirate-private-import-change-audit-v173/RESULT.v1.json')
verified=read(W/'pirate-material-native-defaults-reopen-v187/RESULT.v1.json')
assert all(r['equals_shader_default'] for r in verified['native_float_properties'])
for p,h in verified['input_pins'].items():assert sha(p)==h,p
change=audit['changes'][0];before=Path(change['original_path']);after=Path(change['path'])
assert sha(before)==change['prior_sha256'] and sha(after)==change['current_sha256']
rows=[]
for name in ['_StencilWriteMaskDepth','_StencilWriteMaskGBuffer','_StencilWriteMaskMV']:
    values=[int(re.search(r'^\s*- '+name+r': (\d+)\s*$',p.read_text('utf-8-sig'),re.M)[1]) for p in [before,after]]
    assert values[1]==values[0]|1 and values[0]&1==0
    rows.append(dict(name=name,before=values[0],after=values[1],added_bits=1))
enum=SDK/'Runtime/RenderPipeline/HDStencilUsage.cs'
assert 'IsUnlit = (1 << 0)' in enum.read_text('utf-8-sig')
files=[enum,SDK/'Runtime/Material/Lit/BaseLitAPI.cs',SDK/'Runtime/Material/ShaderGraphAPI.cs']
pins={str(p):sha(p) for p in files+[before,after,Path(__file__),W/'pirate-material-native-defaults-reopen-v187/RESULT.v1.json']}
O.mkdir();(O/'sdk-evidence').mkdir()
for p in files:
    (O/'sdk-evidence'/p.name).write_bytes(p.read_bytes())
result=dict(status='three_added_stencil_write_mask_bits_identified_render_equivalence_unproven',
    added_property_defaults_verified=10,stencil_changes=rows,current_sdk_bit_name='IsUnlit',
    stencil_reference_values_unchanged=True,clears_unlit_bit_in_lit_passes=True,
    source_material_migration_equivalent=False,images_admitted=0,full_import=False,
    next_action='Compare retained pre-migration material and current material in the same source HDRP renderer; also resolve the inactive mask branch.',
    input_pins=pins,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Pirate.07 — défauts matériau vérifiés, stencil restant à résoudre

Diagnostic Unity v186 terminé (exit 0), puis réouverture indépendante v187 : **10/10 valeurs ajoutées égales aux défauts natifs du shader**, dont quatre défauts également vérifiés dans le ShaderGraph acheté. Tous les fichiers épinglés sont inchangés après ce diagnostic.

v188 identifie exactement les trois changements de masques d'écriture stencil : depth 8→9, GBuffer 14→15, motion vectors 40→41. Les références stencil sont inchangées : depth 8, GBuffer 10, motion vectors 40. Le bit ajouté vaut 1, nommé `IsUnlit` par le SDK HDRP installé : son inclusion dans le masque d'écriture permet de l'effacer dans ces passes éclairées, elle ne marque pas le matériau comme non éclairé. La neutralité du changement sur ce rendu source reste à prouver. Le masque de texture ajouté nul et sa branche inactive restent aussi à vérifier.

**Aucune nouvelle fiche/image admise** : livraison Pirate conservée à 103 fiches / 206 images, deux refus de ce lot maintenus. Prochaine action : comparer les versions conservées du matériau avant/après dans le même rendu HDRP source et vérifier la branche du masque, puis admettre uniquement si les preuves passent. Aucun modèle créé, aucune qualification Studio/runtime ajoutée.

[Diagnostic Unity](../pirate-material-native-defaults-v186/RUN-RESULT.v1.json) · [Réouverture](../pirate-material-native-defaults-reopen-v187/RESULT.v1.json) · [Cause stencil](../pirate-material-stencil-diagnostic-v188/RESULT.v1.json).

'''
for name in ['POINT_DE_REPRISE.md','AVANCEMENT_ACTUEL.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name
    original=p.read_text('utf-8-sig');(O/(name+'.before')).write_text(original,'utf-8')
    p.write_text(note+original,'utf-8')
print(json.dumps(dict(status=result['status'],stencil_changes=rows,checkpoint=str(O),images_admitted=0)))
