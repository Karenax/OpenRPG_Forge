"""Document physical native derivatives; keep reader, render and runtime qualifications separate."""
from pathlib import Path
import json
from assemble_native_villa_delivery import read, sha, require, now

W = Path('F:/OpenRPG_ImportWork')
O = W / 'villa-repaired-native-checkpoint-v1018'


def main():
    p = W / 'villa-repaired-native-storage-and-frames-v1015/RESULT.v1.json'
    result = read(p)
    require(result['status'] == '19_NATIVE_STORAGE_AND_CAPTURED_FRAMES_REOPENED' and
            len(result['rows']) == result['verified_scenes'] == 19, 'Native lot incomplete')
    bindings = sum(r['native_storage_receipt']['texture_bindings_reopened'] for r in result['rows'])
    mips = sum(r['native_storage_receipt']['mip_associations_reopened'] for r in result['rows'])
    require(all(r['original_source_vectors_reopened_bitwise_equal'] and r['target_geometry_unchanged']
                for r in result['rows']), 'Native vector or geometry proof missing')
    reader_path = W / 'villa-repaired-current-canonical-reader-v1017/RESULT.v1.json'
    reader = read(reader_path)
    O.mkdir(exist_ok=False)
    text = f'''## Lot L04.VILLA.NATIVE.19 : stockage et repères natifs rouverts — v1018

Les 19 dérivés réparés ont maintenant leurs textures importées natives et leurs captures incorporées, puis leurs vecteurs source normals/tangents reliés aux octets exacts de capture Unity. Contrôles de réouverture : {bindings} associations de textures, {mips} associations de mips ; 19/19 scènes, géométrie cible conservée. Les convertisseurs existants de stockage et de repères ont été réutilisés, sans assouplir leurs contrôles. Le slot builtin 7 est traité séparément avec la réparation et les preuves v1010/v1014 ; les autres slots de textures restent soumis aux preuves d'identité GPU/importer et de décodage existantes.

Livraison physique : F:/OpenRPG_ImportWork/villa-repaired-native-storage-and-frames-v1015/frame-bound/jobs. Preuve : {p}. Aucun original, licence ou registre du Vault modifié. Ces nouveaux fichiers ne sont pas encore déclarés placables ou entièrement qualifiés. Les 207 admissions historiques Villa ne sont pas rejouées par ce lot.

Lecteur canonique courant v1017 : état observé {reader['status']}. Build depuis le checkout commun, 1214 entrées épinglées, inventaire Git des fichiers suivis/modifiés/non suivis conservé ; le résultat final de ce job fait autorité. Vérification attendue : exacts SHA des 19 GLB, géométrie/source-vecteurs, stockage et mips, contrôles négatifs de corruption et de capture invalide. Le rendu complet, placement/sauvegarde/réouverture, runtime et intégrité finale du Vault restent ouverts. Aucun nouvel asset entièrement qualifié dans ce checkpoint.

'''
    for name in ('TABLEAU_DE_BORD_LOTS.md', 'SUIVI_LISIBLE.md', 'POINT_DE_REPRISE.md'):
        target = W / 'SUIVI_LOTS_HIVEMIND' / name
        before = target.read_bytes()
        (O / (name + '.before')).write_bytes(before)
        pending = target.with_name(name + '.v1018.pending')
        with pending.open('xb') as stream:
            stream.write(text.encode('utf-8') + before)
        require(target.read_bytes() == before, 'Concurrent tracking edit')
        pending.replace(target)
    receipt = dict(status='19_NATIVE_STORAGE_AND_FRAME_DERIVATIVES_DOCUMENTED', finished_utc=now(),
                   native_receipt_sha256=sha(p), native_scene_count=19, texture_bindings=bindings,
                   mip_associations=mips, reader_state_at_observation=reader['status'],
                   new_fully_qualified_assets=0, registry_modified=False, full_import=False)
    (O / 'RESULT.v1.json').write_text(json.dumps(receipt, indent=2), 'utf-8')
    print(json.dumps(receipt))


if __name__ == '__main__':
    main()
