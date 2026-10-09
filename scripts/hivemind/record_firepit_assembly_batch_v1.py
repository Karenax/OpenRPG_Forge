from pathlib import Path
import datetime,json,re,hashlib
W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def main():
 now=datetime.datetime.now(datetime.timezone.utc).isoformat()
 native=read(W/'firepit-static-material-native-reimport-v3/RUN-RESULT.v1.json');assert native['status']=='static_firepit_original_shader_and_native_passes_reopened'
 assembly=read(W/'firepit-complete-static-glb-v2/RESULT.v1.json');reopen=read(W/'firepit-complete-static-glb-v2/INDEPENDENT-REOPEN.v1.json')
 row=assembly['rows'][0];assert row['sha256']==reopen['glb_sha256'] and reopen['nodes_reopened']==9 and len(reopen['negative_controls'])==6
 product_path=W/'firepit-static-glb-product-check-v2/RUN-RESULT.v1.json';product=read(product_path) if product_path.exists() else {'status':'not_started'}
 accepted=product.get('status')=='actual_product_static_material_carrier_admitted' and product.get('actual_checker_exit_code')==0
 rocks_path=W/'rocks-firepit-complete-static-glb-v1/INDEPENDENT-REOPEN.v1.json'
 rocks=read(rocks_path) if rocks_path.exists() else {}
 rocks_ok=rocks.get('status')=='static_rocks_hierarchy_and_embedded_payload_independently_reopened'
 rocks_text='Réouverture indépendante réussie : 86 nœuds, 92 dessins, 149 blobs et 1 044 flux ; six corruptions refusées.' if rocks_ok else 'Réouverture indépendante restante.'
 evidence=[rocks_path,W/'firepit-static-material-native-reimport-v3/REOPEN.v1.json',W/'firepit-complete-static-glb-v2/RESULT.v1.json',W/'firepit-complete-static-glb-v2/INDEPENDENT-REOPEN.v1.json',W/'rocks-firepit-complete-static-glb-v1/ASSEMBLY-REFUSAL.v1.json',product_path]
 pins={str(p):hashlib.file_digest(p.open('rb'),'sha256').hexdigest() for p in evidence if p.exists()}
 product_text=('Chargeur Forge : admission réussie du matériau et de ses données embarquées.' if accepted else 'Chargeur Forge : '+product['status']+'. Le refus du cache Rust 1.99/1.98 reste conservé ; contrôle v2 isolé sur F.')
 block=f"""<!-- FIREPIT-ASSEMBLY-CHECKPOINT-BEGIN -->
## Lot actuel : L04 — Rocks / FirePit

Actualisé : {now}.

| Étape | Résultat vérifié | Suite |
|---|---|---|
| Matériau FirePit | Shader original et passes compilés dans Unity ; sources originales inchangées. Migration de la copie privée documentée. | Comparer le rendu complet. |
| GLB statique FirePit | 36 796 448 octets sur F ; 9 nœuds, 4 dessins, 1 matériau, 17 blobs et 40 flux réouverts ; 6 corruptions refusées. | Admission produit puis rendu, LOD et états actifs. |
| Admission du GLB FirePit | {product_text} | Recette visuelle, placement, sauvegarde et réouverture Studio. |
| GLB Rocks | Candidat de 709 944 920 octets conservé ; 86 nœuds, 57 renderers et 92 dessins prévus. {rocks_text} Limite actuelle du chargeur : 512 Mio. | Admission bornée des grands fichiers, puis rendu. |
| Effets FirePit — L06 | Les deux renderers de particules et leurs métadonnées originales sont conservés. | Simulation autonome et rendu complet des effets. |

[Capture native](../firepit-static-material-native-reimport-v3/REOPEN.v1.json) · [GLB assemblé](../firepit-complete-static-glb-v2/RESULT.v1.json) · [Vérification indépendante](../firepit-complete-static-glb-v2/INDEPENDENT-REOPEN.v1.json) · [Contrôle Forge](../firepit-static-glb-product-check-v2/RUN-RESULT.v1.json).

Cette étape ferme l'assemblage et la réouverture du **contenu statique** FirePit. Elle ne compte pas comme un asset livré dans Studio ou un effet complet.
<!-- FIREPIT-ASSEMBLY-CHECKPOINT-END -->
"""
 for name in ['AVANCEMENT_PAR_LOTS.md','LOTS_ACTIFS.md']:
  target=T/name;text=target.read_text('utf-8-sig');backup=T/'historique-focus'/('before-firepit-assembly-'+name)
  if not backup.exists():backup.parent.mkdir(exist_ok=True);backup.write_bytes(target.read_bytes())
  text=re.sub(r'<!-- FIREPIT-ASSEMBLY-CHECKPOINT-BEGIN -->.*?<!-- FIREPIT-ASSEMBLY-CHECKPOINT-END -->\s*','',text,flags=re.S)
  text=re.sub(r'\n{3,}','\n\n',text)
  head,sep,tail=text.partition('\n');text=head+'\n\n'+block+'\n'+tail.lstrip()
  lines=[]
  for line in text.splitlines():
   if line.startswith('| L04 — Matériaux et végétation |'):line='| L04 — Matériaux et végétation | Textures, normales, transparence et déformations | GLB statique FirePit assemblé et réouvert ; contrôle produit en cours. Rocks : candidat conservé, admission >512 Mio et rendu restant. |' if not accepted else '| L04 — Matériaux et végétation | Textures, normales, transparence et déformations | GLB statique FirePit admis par le chargeur ; rendu et Studio restant. Rocks : admission >512 Mio restant. |'
   if line.startswith('| L04.FirePit.02 |'):line='| L04.FirePit.02 | Neuf nœuds et quatre dessins assemblés et réouverts ; six corruptions refusées. | Rendu, LOD, lumière/collider et deux effets, puis recette Studio. |'
   if line.startswith('| L04.Rocks.01 |'):line='| L04.Rocks.01 | Huit matériaux et hiérarchie assemblés dans un candidat conservé de 709 944 920 octets ; défaut Detail mesuré. | Admission bornée du grand GLB, vérification indépendante, rendu et Studio. |'
   lines.append(line)
  normalized='\n'.join(lines)+'\n'
  while re.search(r'(?m)^(\|[^\n]*\|)\n\n+(?=\|)',normalized):normalized=re.sub(r'(?m)^(\|[^\n]*\|)\n\n+(?=\|)',r'\1\n',normalized)
  temp=target.with_suffix('.md.tmp');temp.write_text(normalized,'utf-8');temp.replace(target)
 checkpoint=dict(updated_utc=now,active_batch='L04',active_sub_batch='L01.ROCKS.GLB.CAPACITE' if accepted else 'L04.FIREPIT.GLB.ADMISSION',static_scene_assembled=True,independent_reopen_passed=True,rocks_independent_reopen_passed=rocks_ok,canonical_reader_accepted=accepted,product_check_status=product['status'],evidence_pins=pins,full_import=False,all_assets_delivered=False,next_steps=['admission bornée Rocks >512 Mio','rendu FirePit et Rocks','LOD/états actifs, effets et recette Studio'] if accepted else ['admission produit FirePit','admission bornée Rocks >512 Mio','rendu, effets et recette Studio'])
 (T/'L04_FIREPIT_ASSEMBLAGE.v1.json').write_text(json.dumps(checkpoint,indent=2),'utf-8')
 state=read(T/'ETAT.v1.json');state.update(updated_utc=now,active_batch='L04',active_sub_batch=checkpoint['active_sub_batch'],goal_complete=False,latest_scene_assembly_checkpoint=checkpoint)
 (T/'ETAT.v1.json').write_text(json.dumps(state,indent=2),'utf-8')
 journal=T/'JOURNAL.md';marker='L04.FIREPIT.GLB.02 — assemblage statique réouvert'
 if marker not in journal.read_text('utf-8-sig'):
  with journal.open('a',encoding='utf-8') as f:f.write(f'\n\n## {marker}\n\n{now} : 9 nœuds, 4 dessins, 1 matériau et 36 796 448 octets. Réouverture indépendante de 17 blobs/40 flux ; 6 corruptions refusées. Shader original réparé par réimport dépendances ; sources inchangées, copie privée migrée et conservée. Rendu/LOD/particules/Studio restant. [Reçu](../firepit-complete-static-glb-v2/INDEPENDENT-REOPEN.v1.json). Chargeur v1 refusé pour cache Rust incompatible ; v2 isolé sur F.\n')
 if accepted:
  marker='L01.FIREPIT.GLB.ADMISSION — chargeur produit réussi'
  if marker not in journal.read_text('utf-8-sig'):
   with journal.open('a',encoding='utf-8') as f:f.write(f'\n\n## {marker}\n\n{product["finished_utc"]} : build Rust 1.98 isolé et lecture par le vrai forge_assets réussis ; sources produit inchangées. GLB 36 796 448 octets, 9 nœuds et 1 contrat matériau admis ; corruption de header refusée. GPU, LOD/états actifs, effets et Studio restent non qualifiés. [Reçu](../firepit-static-glb-product-check-v2/RESULT.v1.json). Suite : admission bornée du GLB Rocks de 709 944 920 octets, puis rendu complet.\n')
 print(json.dumps(checkpoint))
 from record_large_budget_batch_v1 import main as record_large_budget
 record_large_budget()
if __name__=='__main__':main()
