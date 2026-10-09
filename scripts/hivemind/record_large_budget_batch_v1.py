from pathlib import Path
import json,re,datetime,hashlib
W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def main():
 proof=W/'source-graph-large-desktop-budget-v1/RESULT.v1.json'
 if not proof.exists():return
 result=read(proof);assert result['status']=='actual_rocks_large_carrier_and_static_geometry_admitted' and result['product_source_pins_unchanged'] and result['nodes']==86 and result['primitive_instances']==92 and result['unique_primitives_decoded']==47
 tests=result['tests'][0]['passed_tests'];assert len(tests)==12
 path=W/'studio-large-scene-intake-native-v4/RUN-RESULT.v1.json';studio=read(path) if path.exists() else {'status':'not_started'}
 studio_ok=studio['status']=='actual_studio_intake_and_hidden_scene_hierarchies_verified'
 now=datetime.datetime.now(datetime.timezone.utc).isoformat()
 studio_text=('Admission par le vrai module Studio et hiérarchies ECS cachées vérifiées pour Rocks et FirePit.' if studio_ok else "Contrôle réel de l'admission Studio en cours : "+studio["status"]+".")
 block=f"""<!-- LARGE-GLB-BUDGET-BEGIN -->
## L01 — Grand GLB Rocks accepté par le chargeur

Actualisé : {now}.

**Étape vérifiée** : lecture réelle de 709 944 920 octets, 8 contrats matériau, 86 nœuds, 47 primitives partagées décodées et 92 instances de dessin. 396 559 occurrences de sommets et 476 943 indices décodés dans ces primitives partagées.

**12 tests réussis** sur les modules produit du lecteur et des attributs. Budget standard conservé à 512 Mio ; opt-in desktop borné à 1 Gio. Le plafond JSON de 16 Mio et les limites de géométrie/images décodées restent inchangés. Web garde le budget standard.

**Studio** : {studio_text} Cela ne prouve pas le raccordement de l'aperçu visible, le rendu GPU, les LOD/états actifs, les particules ou la sauvegarde/réouverture.

[Reçu du vrai chargeur](../source-graph-large-desktop-budget-v1/RESULT.v1.json) · [État du contrôle Studio](../studio-large-scene-intake-native-v4/RUN-RESULT.v1.json).

**Suite** : LOD et états actifs, passes de rendu et raccordement de l'aperçu/placement. Le lot Effets conserve la simulation autonome FirePit.
<!-- LARGE-GLB-BUDGET-END -->
"""
 for name in ['AVANCEMENT_PAR_LOTS.md','LOTS_ACTIFS.md']:
  target=T/name;t=target.read_text('utf-8-sig');t=re.sub(r'<!-- LARGE-GLB-BUDGET-BEGIN -->.*?<!-- LARGE-GLB-BUDGET-END -->\s*','',t,flags=re.S)
  head,sep,tail=t.partition('\n');t=head+'\n\n'+block+'\n'+tail.lstrip()
  t=t.replace('Limite actuelle du chargeur : 512 Mio. | Admission bornée des grands fichiers, puis rendu.', 'Budget desktop de 1 Gio vérifié par le vrai chargeur ; budget standard de 512 Mio conservé. | Hôte Studio puis rendu.')
  lines=[]
  for line in t.splitlines():
   if line.startswith('| L04 — Matériaux et végétation |'):line='| L04 — Matériaux et végétation | Textures, normales, transparence et déformations | Rocks et FirePit : GLB statiques réouverts ; contrats admis par le chargeur. Admission Studio, rendu et états LOD/actifs restent distincts. |'
   if line.startswith('| L04.Rocks.01 |'):line='| L04.Rocks.01 | Huit matériaux et 47 primitives partagées admis par le vrai chargeur ; 86 nœuds et 92 instances conservés. | Hôte Studio, LOD/états actifs, rendu et recette éditeur/runtime. |'
   lines.append(line)
  text='\n'.join(lines)+'\n'
  while re.search(r'(?m)^(\|[^\n]*\|)\n\n+(?=\|)',text):text=re.sub(r'(?m)^(\|[^\n]*\|)\n\n+(?=\|)',r'\1\n',text)
  temp=target.with_suffix('.md.tmp');temp.write_text(text,'utf-8');temp.replace(target)
 checkpoint=dict(updated_utc=now,active_batch='L01',active_sub_batch='L04.ROCKS_FIREPIT.LOD_VISIBILITE' if studio_ok else 'L01.STUDIO.INTAKE.ROCKS_FIREPIT',rocks_actual_reader_admitted=True,reader_attribute_tests_passed=12,studio_intake_status=studio['status'],studio_intake_and_hidden_hierarchy_verified=studio_ok,visible_studio_and_gpu_verified=False,full_import=False,evidence={str(proof):hashlib.file_digest(proof.open('rb'),'sha256').hexdigest()})
 state=read(T/'ETAT.v1.json');state.update(updated_utc=now,active_batch='L01',active_sub_batch=checkpoint['active_sub_batch'],latest_large_budget_checkpoint=checkpoint,goal_complete=False)
 (T/'ETAT.v1.json').write_text(json.dumps(state,indent=2),'utf-8');(T/'L01_ROCKS_GRAND_GLB.v1.json').write_text(json.dumps(checkpoint,indent=2),'utf-8')
 journal=T/'JOURNAL.md';marker='L01.ROCKS.GLB.CAPACITE — lecture réelle réussie'
 if marker not in journal.read_text('utf-8-sig'):
  with journal.open('a',encoding='utf-8') as f:f.write(f'\n\n## {marker}\n\n{result["finished_utc"]} : 709 944 920 octets lus par forge_assets, 8 contrats et 47 primitives partagées décodées pour 92 instances. 12 tests des modules produit réussis ; limites de géométrie/images et JSON inchangées. Préimages conservées, sources produit épinglées pendant la recette. Opt-in desktop 1 Gio ; standard et Web 512 Mio. Contrôle Studio séparé, rendu/GPU/LOD/particules/recette globale restant. [Reçu](../source-graph-large-desktop-budget-v1/RESULT.v1.json).\n')
 if studio_ok:
  marker='L01.STUDIO.INTAKE.02 — Rocks et FirePit admis en hiérarchies cachées'
  if marker not in journal.read_text('utf-8-sig'):
   with journal.open('a',encoding='utf-8') as f:f.write(f'\n\n## {marker}\n\n{studio["finished_utc"]} : vrais modules Studio et moteur compilés ; Rocks 86 nœuds/92 instances/47 géométries et FirePit 9 nœuds/4 instances/4 géométries. Nœuds, parents et matrices locales contrôlés dans ECS ; racines cachées, indices de scène invalides refusés. Quatre tests des modules source hérités de leur reçu, sources recontrôlées. Entité interne DefaultQueryFilters de Bevy comptée séparément. Refus v1/v2/v3 conservés. Aucun GPU, affichage visible, LOD/particules ou build complet Studio qualifié. [Reçu](../studio-large-scene-intake-native-v4/RESULT.v1.json).\n')
 print(json.dumps(checkpoint))
 import record_lod_metric_checkpoint_v1
 record_lod_metric_checkpoint_v1.main()
if __name__=='__main__':main()
