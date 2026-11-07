"""Document verified tranche receipts without promoting them to full import."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json

W=Path('F:/OpenRPG_ImportWork'); V=Path('F:/OpenRPG_AssetVault'); T=W/'SUIVI_LOTS_HIVEMIND'
def read(p): return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()

def main():
    out=W/'lot-checkpoint-v147'; assert not out.exists()
    paths={
        'pirate':W/'pirate-source-thumbnail-batch-0105-0112-delivery-reopen-v1/RESULT.v1.json',
        'reader':W/'torch-and-pirate-current-native-reader-v146/RESULT.v1.json',
        'tint':W/'medieval-torch-native-tint-variant-v129/RUN-RESULT.v1.json',
        'hierarchy':W/'medieval-torch-full-hierarchy-contract-v139/RESULT.v1.json',
        'particles':W/'medieval-torch-native-particle-behavior-v140/RUN-RESULT.v1.json',
    }
    r={k:read(p) for k,p in paths.items()}
    assert r['pirate']['cards_updated']==8 and r['pirate']['source_images_installed']==16
    assert r['reader']['actual_exit_code']==0 and r['reader']['torch_source_verified']
    assert r['particles']['native_behavior_verified']
    ptr=read(V/'metadata/asset-source-support-registry.v1.json'); cp=V/ptr['path']; assert sha(cp)==ptr['sha256']
    cards=read(cp)['assets']; missing=sum(not a.get('preview') for a in cards)
    assert len(cards)==12517 and missing==805
    stamp=datetime.now(timezone.utc).isoformat()
    block=f'''## Point vérifié {stamp} — Pirate.06 livré, torche indexée et contrôles natifs terminés

**L08.IMAGE.PIRATE.06 : 8 examinés, 8 admis, 0 refus ; 16 images livrées sur F.** Recherche, filtres de pack/disponibilité, détails, licence et image principale contrôlés avec le lecteur actuel du produit. Contrôle v146 rejoué après ajout de la torche : les huit images restent accessibles avec leur SHA attendu. Pirate.01–06 cumule **97 fiches / 194 images**, avec 15 refus historiques conservés.

**L00 / L06 Torche : 1 nouvelle fiche source non plaçable**, 25 dépendances / 50 fichiers originaux conservés et rehashés sur F. Recherche « torche », filtre Medieval Kingdom/source_support, détails, licence et SHA du prefab validés par le lecteur natif. Image encore absente, refus de placement et de demande de modèle vérifié. Catalogue courant : **12 517 fiches source ; 805 sans image principale**, dont cette nouvelle torche. Les 14 219 fiches modèle déclarées converties ne constituent pas un compteur de qualification complète.

**L06.MEDIEVAL.TORCH.001 :** les 16 fixtures de teintes, l'import du parent complet à 8 nœuds et la comparaison des transforms ont terminé. **112 états temporels natifs** feu/fumée contrôlés, avec répétitions et contrôles de corruption. Simulation de chaque système isolé ; rendu complet, systèmes simultanés, préchauffage autonome et parité de version source restent non qualifiés. Trois migrations privées de matériaux restent à classifier. Géométrie native complète et conversion de l'effet dans Forge restent à livrer.

**L01 GLB Web v141 :** compilation encore observée active au dernier contrôle de processus de ce tour, après compilation de la bibliothèque moteur. Aucun résultat de dessin navigateur n'est déduit de cet état. Conserver les sources épinglées jusqu'à son reçu terminal.

**Prochaine tranche :** résultat GPU v141 ; classifier les trois matériaux privés de la torche et obtenir la géométrie native ; poursuivre conversion temporelle et recette visuelle Studio. **0 nouvel asset complet qualifié** par ces seules preuves. Aucun contrôle global du Vault revendiqué à partir de ces deltas.

[Pirate.06](../pirate-source-thumbnail-batch-0105-0112-delivery-reopen-v1/RESULT.v1.json) · [Lecteur courant : torche et images](../torch-and-pirate-current-native-reader-v146/RESULT.v1.json) · [Hiérarchie](../medieval-torch-full-hierarchy-contract-v139/RESULT.v1.json) · [Particules natives](../medieval-torch-native-particle-behavior-v140/RUN-RESULT.v1.json) · [GLB Web](../browser-source-glb-v141/RUN-RESULT.v1.json)

'''
    out.mkdir()
    for name in ['POINT_DE_REPRISE.md','AVANCEMENT_ACTUEL.md']:
        p=T/name; before=p.read_bytes(); (out/(name+'.before')).write_bytes(before)
        p.write_text(block+before.decode('utf-8-sig'),'utf-8')
    receipt=dict(status='verified_lot_checkpoint_documented',finished_utc=stamp,source_cards=len(cards),missing_primary_images=missing,full_import=False,actual_editor_visual_verified=False,input_pins={str(p):sha(p) for p in paths.values()},catalog_sha256=ptr['sha256'],document_pins={str(T/n):sha(T/n) for n in ['POINT_DE_REPRISE.md','AVANCEMENT_ACTUEL.md']})
    (out/'RESULT.v1.json').write_text(json.dumps(receipt,indent=2),'utf-8')
    print(json.dumps(receipt))

if __name__=='__main__': main()
