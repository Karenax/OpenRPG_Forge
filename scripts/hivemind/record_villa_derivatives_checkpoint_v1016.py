"""Record the completed decoder derivative lot without claiming native or runtime admission."""
import json
from pathlib import Path
from assemble_native_villa_delivery import read, sha, require, now

W = Path('F:/OpenRPG_ImportWork')
O = W / 'villa-derivatives-checkpoint-v1016'


def main():
    path = W / 'villa-builtin-decoder-derivatives-v1014/RESULT.v1.json'
    result = read(path)
    require(result['status'] == '19_MEASURED_DECODER_DERIVATIVES_REOPENED' and
            len(result['rows']) == result['verified_derivatives'] == 19, 'Derivative lot incomplete')
    native_path = W / 'villa-repaired-native-storage-and-frames-v1015/RESULT.v1.json'
    native = read(native_path) if native_path.exists() else {}
    O.mkdir(exist_ok=False)
    text = f'''## Lot L04.VILLA.DECODER.19 : 19 dérivés créés et rouverts — v1016

Les 19 scènes différées ont désormais un dérivé avec le décodeur RG-or-AG mesuré sur GPU et le slot builtin 7 explicitement lié au défaut blanc observé par l'API Unity. Les originaux n'ont pas été modifiés. La réouverture vérifie le préfixe BIN d'origine octet pour octet et restaure exactement le JSON source après annulation des seuls descripteurs shader et du mode concerné. Géométrie, pivots, textures, métadonnées et anciens octets shader sont conservés. Les autres matériaux restent inchangés et ne reçoivent aucune qualification implicite. Le premier dérivé réussi de v1013 est réutilisé et revérifié ; les 18 suivants sont sous v1014. Les essais refusés v1012/v1013 sont conservés.

Preuve du lot : {path}. Assemblage textures natives et repères capturés v1015 : état {native.get('status', 'non observé')}, {len(native.get('rows', []))}/19 scènes rouverts au moment de ce relevé. Ses résultats ultérieurs font autorité. Le lecteur canonique courant, le rendu complet des matériaux, placement, sauvegarde/réouverture et runtime restent à qualifier ; aucune nouvelle admission au Vault ni aucun asset entièrement qualifié n'est déclaré ici. Les 207 admissions Villa historiques restent inchangées.

Fichiers : F:/OpenRPG_ImportWork/villa-builtin-decoder-derivatives-v1013/jobs et villa-builtin-decoder-derivatives-v1014/jobs. Prochaine livraison technique : villa-repaired-native-storage-and-frames-v1015, puis vérificateur réutilisable du checkout commun crates/forge_engine/examples/verify_native_source_scene_manifest.rs. Les compteurs d'images ne sont pas modifiés par ce lot.

'''
    for name in ('TABLEAU_DE_BORD_LOTS.md', 'SUIVI_LISIBLE.md', 'POINT_DE_REPRISE.md'):
        p = W / 'SUIVI_LOTS_HIVEMIND' / name
        before = p.read_bytes()
        (O / (name + '.before')).write_bytes(before)
        pending = p.with_name(name + '.v1016.pending')
        with pending.open('xb') as stream:
            stream.write(text.encode('utf-8') + before)
        require(p.read_bytes() == before, 'Concurrent tracking edit')
        pending.replace(p)
    receipt = dict(status='19_DERIVATIVES_DOCUMENTED', finished_utc=now(),
                   derivative_receipt=str(path), derivative_receipt_sha256=sha(path),
                   decoder_derivatives_reopened=19, native_state_at_observation=native.get('status'),
                   native_rows_at_observation=len(native.get('rows', [])),
                   new_fully_qualified_assets=0, registry_modified=False, full_import=False)
    (O / 'RESULT.v1.json').write_text(json.dumps(receipt, indent=2), 'utf-8')
    print(json.dumps(receipt))


if __name__ == '__main__':
    main()
