"""Record sealed fourth-lot delivery and the bounded real Studio observation."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault')
O = W / 'pirate-lot4-delivery-checkpoint-v1005'

def read(p):
    return json.loads(p.read_text('utf-8-sig'))

def sha(p):
    with p.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def main():
    delivery_path = W / 'pirate-upper-lod-images-delivery-v1003/RESULT.v1.json'
    delivery = read(delivery_path)
    assert delivery['cards_delivered'] == 19 and delivery['current_native_reader_verified']
    pointer_path = V / 'metadata/asset-source-support-registry.v1.json'
    pointer = read(pointer_path)
    assert pointer['revision'] == delivery['current_catalog_revision']
    catalog_path = V / pointer['path']
    assert sha(catalog_path) == pointer['sha256']
    catalog = read(catalog_path)['assets']
    missing = [c for c in catalog if not c.get('preview')]
    assert len(missing) == 426
    groups = {}
    for c in missing:
        groups[c['pack_id']] = groups.get(c['pack_id'], 0) + 1
    gui = W / 'installed-library-lot4-gui-v1004'
    observed = read(gui / 'observed.json')
    card = next(c for c in catalog if c['id'] == observed['selected_id'])
    assert card['id'].endswith('-lod5') and card['preview']
    exe = Path(observed['window']['app'].removeprefix('process:'))
    assert exe.is_file()
    gui_result = dict(status='SOURCE_LOD5_OBSERVED_IN_INSTALLED_STUDIO_PROBE',
                      observation=observed, executable_sha256=sha(exe),
                      input_pins={str(gui / name): sha(gui / name) for name in ('observed.json', 'accessibility.txt', 'library-lod5.png')},
                      full_import=False)
    assert not (gui / 'RESULT.v1.json').exists()
    (gui / 'RESULT.v1.json').write_text(json.dumps(gui_result, indent=2), 'utf-8')
    O.mkdir(exist_ok=False)
    text = f'''## Lot Pirate 4 livré et contrôlé — v1005

19 fiches source / 38 vues livrées, relecture native Rust v1002 PASS. Capture Unity v996 : 29 cas (19 candidats + 10 contrôles LOD0), exit 0, aucun refus, aucune entrée modifiée. Garde v998 : 29 admis ; réouverture v999, comparaison des maillages et inspection réelle des sept planches v1000. Transaction v1001 puis scellement v1003 : sources, licences, métadonnées, état non plaçable et registre modèles conservés. Révision support {pointer['revision']}.

Inventaire courant relu : 12 517 fiches source, 12 091 avec image principale et 426 sans image, dont {groups.get('hivemind.pirate-island', 0)} Pirate. Quatre derniers lots Pirate : 89 fiches / 178 vues. Aucun nouvel asset entièrement converti ou qualifié runtime ; les images ne ferment pas la conversion fidèle.

GUI v1004 : dans la fenêtre Studio de contrôle déjà ouverte, recherche SM_ShackSideShed3x3_01 avec filtres Pirate et Source conservée, six résultats, image et fiche LOD5 visibles ; licence, source sur F: et placement désactivé observés. Capture PNG et arbre d'accessibilité conservés avec SHA. Portée : cette fiche dans le workspace de contrôle, pas les 19 fiches ni le lancement normal du Hub. Le parcours normal du Hub reste à observer après expiration de son approbation technique ; la réparation installée v992/v993 reste vérifiée indépendamment.

Suite : ouvrir la prochaine tranche de conversions fidèles et de validations des effets/assemblages, sans confondre sources indexées et objets utilisables ; compléter les 426 images restantes avec leurs contraintes. Avant un nouveau lot Pirate, recalculer le routage sur cette révision et conserver les refus de matériaux et de groupes LOD mixtes. Aucun job v996 ou v1002 à relancer : résultats terminaux disponibles.

Preuves sous F:/OpenRPG_ImportWork : pirate-upper-lod-images-delivery-v1003/RESULT.v1.json ; installed-library-lot4-gui-v1004/RESULT.v1.json ; pirate-lot4-delivery-checkpoint-v1005/RESULT.v1.json.

'''
    for name in ('TABLEAU_DE_BORD_LOTS.md', 'SUIVI_LISIBLE.md', 'POINT_DE_REPRISE.md'):
        p = W / 'SUIVI_LOTS_HIVEMIND' / name
        before = p.read_bytes()
        (O / (name + '.before')).write_bytes(before)
        pending = p.with_name(name + '.v1005.pending')
        with pending.open('xb') as stream:
            stream.write(text.encode('utf-8') + before)
        assert p.read_bytes() == before, 'Concurrent tracking edit'
        pending.replace(p)
    result = dict(status='LOT4_DELIVERY_DOCUMENTED', finished_utc=datetime.now(timezone.utc).isoformat(),
                  cards_delivered=19, images_delivered=38, missing_source_images=426, missing_by_pack=groups,
                  catalog_revision=pointer['revision'], gui_scope='one source LOD5 card in existing installed Studio probe',
                  input_pins={str(delivery_path): sha(delivery_path), str(catalog_path): sha(catalog_path),
                              str(gui / 'RESULT.v1.json'): sha(gui / 'RESULT.v1.json')},
                  new_fully_converted_assets=0, full_import=False)
    (O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps(result))

if __name__ == '__main__':
    main()
