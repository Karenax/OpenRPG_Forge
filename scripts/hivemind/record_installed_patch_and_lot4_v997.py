"""Publish an evidence-linked checkpoint without changing qualification counts."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
O = W / 'installed-patch-and-lot4-checkpoint-v997'

def read(p):
    return json.loads(p.read_text('utf-8-sig'))

def sha(p):
    with p.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def main():
    installed_path = W / 'installed-vault-patch-verification-v993/RESULT.v1.json'
    installed = read(installed_path)
    assert installed['status'] == 'INSTALLED_PATCH_REOPENED_AND_VERIFIED'
    prepared_path = W / 'pirate-upper-lod-images-v996/PREPARED.v1.json'
    prepared = read(prepared_path)
    assert prepared['expected_cards'] == 19 and prepared['expected_images'] == 38
    assert len(prepared['cases']) == 29
    O.mkdir(exist_ok=False)
    text = '''## Installation corrigée et lot Pirate 4 — v997

Mise à jour locale v992 appliquée, installation révision 7. Revalidation v993 : 1 023 fichiers du nouveau paquet relus par SHA-256, dont 1 020 fichiers conservés à l'identique ; sauvegardes des deux exécutables remplacés et snapshot de rollback conservés. Les 28 fichiers d'auteur épinglés sont inchangés. Le CLI installé passe verify et prépare le lancement de Studio avec OPENRPG_ASSET_VAULT=F:/OpenRPG_AssetVault. Studio et jeu conservés, pas reconstruits ; pas de qualification de release ou de conversion complète.

Parcours normal du Hub encore à vérifier dans l'interface : la demande d'approbation technique Computer Use a expiré avant ouverture. Aucun verdict GUI ajouté. La preuve GUI v972 reste limitée au lancement contrôlé et aux fiches qui y ont été observées.

Hiérarchie v994 et routage v995 actualisés sur la révision source df61f87d00c8dd94ac9814eda64b71cfe34549fada0bce34d6b4cfe773e64bea : 111 fiches Pirate encore sans image, 48 prefabs contrôlés, 19 fiches LOD supérieures admissibles et 62 retenues avec leurs raisons. Lot v996 préparé : 19 candidats / 38 vues attendues, plus 10 contrôles LOD0, soit 29 captures natives. Son contrôleur a démarré ; consulter RUN-RESULT.v1.json et le processus avant toute reprise. Rien de ce lot n'est encore livré ni compté comme validé.

Compteurs livrés inchangés : 445 images source principales manquantes, dont 111 Pirate. Les trois précédents lots ont livré 70 fiches / 140 vues. Aucun nouvel asset entièrement converti ou qualifié runtime dans ce checkpoint. Poursuivre captures, contrôles des sources, revue visuelle, transaction d'indexation et lecteur Rust ; puis les conversions fidèles et validations runtime restant ouvertes.

Preuves : installed-vault-patch-verification-v993/RESULT.v1.json ; pirate-source-lod-hierarchy-v994/RESULT.v1.json ; pirate-upper-lod-frontier-v995/RESULT.v1.json ; pirate-upper-lod-images-v996/PREPARED.v1.json et RUN-RESULT.v1.json, sous F:/OpenRPG_ImportWork.

'''
    before = {}
    for name in ('TABLEAU_DE_BORD_LOTS.md', 'SUIVI_LISIBLE.md', 'POINT_DE_REPRISE.md'):
        p = W / 'SUIVI_LOTS_HIVEMIND' / name
        payload = p.read_bytes()
        before[name] = hashlib.sha256(payload).hexdigest()
        (O / (name + '.before')).write_bytes(payload)
        pending = p.with_name(name + '.v997.pending')
        with pending.open('xb') as stream:
            stream.write(text.encode('utf-8') + payload)
        assert sha(p) == before[name], 'Concurrent tracking edit; preserve both versions'
        pending.replace(p)
    result = dict(status='CHECKPOINT_DOCUMENTED', finished_utc=datetime.now(timezone.utc).isoformat(),
                  input_pins={str(installed_path): sha(installed_path), str(prepared_path): sha(prepared_path)},
                  tracking_before=before, lot4_delivered=False, new_fully_converted_assets=0, full_import=False)
    (O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps(result))

if __name__ == '__main__':
    main()
